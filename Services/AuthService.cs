using System.IdentityModel.Tokens.Jwt;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HospitalManagement.Api.Authentication;
using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Helpers;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HospitalManagement.Api.Services;

public class AuthService(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions, ILogger<AuthService>? logger = null) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthSession> RegisterAsync(
        RegisterRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (Encoding.UTF8.GetByteCount(request.Password) > 72)
            throw new RequestValidationException("Password must be no more than 72 UTF-8 bytes.");
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !new PhoneAttribute().IsValid(request.PhoneNumber))
            throw new RequestValidationException("Phone number is invalid.");

        if (request.DateOfBirth is { } dob && dob > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new RequestValidationException("Date of birth cannot be in the future.");

        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
            throw new ConflictException("An account with this email already exists.");

        var role = await dbContext.Roles
            .Include(candidate => candidate.RolePermissions)
            .ThenInclude(link => link.Permission)
            .SingleOrDefaultAsync(candidate => candidate.Name == "Patient", cancellationToken);
        if (role is null)
            throw new InvalidOperationException("The patient registration role has not been initialized.");

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (firstName.Length == 0 || lastName.Length == 0)
            throw new RequestValidationException("First name and last name cannot be blank.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };

        // A patient portal account IS a patient: the medical record is created together with the login.
        // Fields the sign-up form does not collect are left empty / Unknown and completed by staff or the patient later.
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = await PatientNumberGenerator.NextAsync(dbContext, cancellationToken),
            UserId = user.Id,
            User = user,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.PhoneNumber ?? string.Empty,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender ?? Gender.Unknown,
            RegisteredAt = DateTime.UtcNow,
            Status = PatientStatus.Active
        };

        var session = CreateSession(user, patient.Id, ipAddress);

        // User + Patient + RefreshToken are persisted by ONE SaveChanges call, which EF Core/Npgsql runs in a single
        // database transaction: either all three rows exist or none do.
        dbContext.Users.Add(user);
        dbContext.Patients.Add(patient);
        dbContext.RefreshTokens.Add(session.RefreshTokenEntity);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // Lost a race against a concurrent registration with the same e-mail.
            logger?.LogWarning(ex, "Unique constraint violated during patient registration");
            throw new ConflictException("An account with this email already exists.");
        }

        logger?.LogInformation("Patient account {UserId} registered with patient record {PatientId}", user.Id, patient.Id);
        return session;
    }

    private const string PatientRoleName = "Patient";

    public async Task<AuthSession> LoginPatientAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        return await LoginAsync(request, ipAddress, user =>
        {
            // Checked only AFTER the password is verified, so this message cannot be used to probe which e-mails exist.
            if (!string.Equals(user.Role.Name, PatientRoleName, StringComparison.Ordinal))
                throw new ForbiddenException("This is a staff account. Please use the staff login page.");
        }, cancellationToken);
    }

    public async Task<AuthSession> LoginStaffAsync(
        StaffLoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var selectedRole = request.Role.Trim();
        return await LoginAsync(request, ipAddress, user =>
        {
            if (string.Equals(user.Role.Name, PatientRoleName, StringComparison.Ordinal))
                throw new ForbiddenException("Patients cannot sign in here. Please use the patient login page.");

            if (!string.Equals(user.Role.Name, selectedRole, StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("The selected role does not match this account. Choose the role assigned to you.");
        }, cancellationToken);
    }

    private async Task<AuthSession> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        Action<User> verifyPortal,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .Include(candidate => candidate.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(link => link.Permission)
            .SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        if (user is null || !user.IsActive || Encoding.UTF8.GetByteCount(request.Password) > 72 ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAppException("Email or password is incorrect.");

        verifyPortal(user);

        user.LastLoginAt = DateTime.UtcNow;
        var session = CreateSession(user, await GetPatientIdAsync(user.Id, cancellationToken), ipAddress);
        dbContext.RefreshTokens.Add(session.RefreshTokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<AuthSession> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var oldHash = HashRefreshToken(refreshToken);
        var now = DateTime.UtcNow;
        var oldToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .Include(token => token.User)
            .ThenInclude(user => user.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(link => link.Permission)
            .SingleOrDefaultAsync(token => token.TokenHash == oldHash, cancellationToken);

        if (oldToken is null || oldToken.RevokedAt is not null || oldToken.ExpiresAt <= now || !oldToken.User.IsActive)
            throw new UnauthorizedAppException("Refresh token is invalid or expired.");

        var session = CreateSession(oldToken.User, await GetPatientIdAsync(oldToken.User.Id, cancellationToken), ipAddress);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var revoked = await dbContext.RefreshTokens
            .Where(token => token.TokenHash == oldHash && token.RevokedAt == null && token.ExpiresAt > now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(token => token.RevokedAt, now)
                .SetProperty(token => token.ReplacedByTokenHash, session.RefreshTokenEntity.TokenHash), cancellationToken);

        if (revoked != 1)
            throw new UnauthorizedAppException("Refresh token is invalid or has already been used.");

        dbContext.RefreshTokens.Add(session.RefreshTokenEntity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task RevokeAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var hash = HashRefreshToken(refreshToken);
        var now = DateTime.UtcNow;
        await dbContext.RefreshTokens
            .Where(token => token.TokenHash == hash && token.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }

    public async Task<AuthUserDto> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(candidate => candidate.Role)
            .ThenInclude(role => role.RolePermissions)
            .ThenInclude(link => link.Permission)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive, cancellationToken);

        if (user is null)
            throw new UnauthorizedAppException();

        return ToDto(user, await GetPatientIdAsync(user.Id, cancellationToken));
    }

    private Task<Guid?> GetPatientIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.UserId == userId)
            .Select(patient => (Guid?)patient.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private AuthSession CreateSession(User user, Guid? patientId, string? ipAddress)
    {
        var now = DateTime.UtcNow;
        var accessExpiresAt = now.AddMinutes(_jwt.AccessTokenMinutes);
        var refreshExpiresAt = now.AddDays(_jwt.RefreshTokenDays);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(ClaimTypes.Role, user.Role.Name)
        };
        claims.AddRange(user.Role.RolePermissions.Select(link =>
            new Claim(PermissionAuthorizationHandler.ClaimType, link.Permission.Code)));
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, now, accessExpiresAt, signingCredentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        var rawRefreshToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(rawRefreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedByIp = ipAddress
        };

        return new AuthSession(
            new AuthResponseDto(accessToken, accessExpiresAt, ToDto(user, patientId)),
            rawRefreshToken,
            refreshExpiresAt,
            refreshToken);
    }

    private static AuthUserDto ToDto(User user, Guid? patientId) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.Role.Name,
            user.Role.RolePermissions.Select(link => link.Permission.Code).Distinct().OrderBy(code => code).ToArray(),
            patientId);

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}