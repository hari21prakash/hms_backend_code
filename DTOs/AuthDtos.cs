using System.ComponentModel.DataAnnotations;

namespace HospitalManagement.Api.DTOs;

public class RegisterRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    /// <summary>Optional at sign-up; stored on the patient record so it does not have to be re-entered later.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Optional at sign-up; defaults to Unknown.</summary>
    public Models.Enums.Gender? Gender { get; set; }

    [Required, MinLength(12), StringLength(72)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class CreateStaffAccountRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? PhoneNumber { get; set; }

    [Required, MinLength(12), StringLength(72)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Role { get; set; } = string.Empty;
}

public record StaffRoleOptionDto(string Name, string Description);
public record StaffAccountDto(Guid Id, string Email, string FirstName, string LastName, string Role, bool IsActive);

/// <summary>Patient portal sign-in.</summary>
public class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Staff portal sign-in. The selected role must match the role assigned to the account.</summary>
public class StaffLoginRequest : LoginRequest
{
    [Required, StringLength(100)]
    public string Role { get; set; } = string.Empty;
}

/// <summary>PatientId is derived server-side from the account; it is null for staff accounts.</summary>
public record AuthUserDto(Guid Id, string Email, string FirstName, string LastName, string Role, IReadOnlyList<string> Permissions, Guid? PatientId = null);

public record AuthResponseDto(string AccessToken, DateTime AccessTokenExpiresAt, AuthUserDto User);

public sealed class AuthSession(
    AuthResponseDto response,
    string refreshToken,
    DateTime refreshTokenExpiresAt,
    Models.RefreshToken refreshTokenEntity)
{
    public AuthResponseDto Response { get; } = response;
    public string RefreshToken { get; } = refreshToken;
    public DateTime RefreshTokenExpiresAt { get; } = refreshTokenExpiresAt;
    internal Models.RefreshToken RefreshTokenEntity { get; } = refreshTokenEntity;
}