using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Helpers;
using HospitalManagement.Api.Interfaces;
using System.Linq.Expressions;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>
/// Staff-side patient registration creates the medical record only (Patient.UserId stays null).
/// A portal login account is created exclusively through self-registration (AuthService.RegisterAsync),
/// which creates the User and the linked Patient atomically.
/// </summary>
public class PatientService(AppDbContext dbContext, ILogger<PatientService> logger) : IPatientService
{
    /// <summary>Single projection so every endpoint returns the same, complete set of patient details.</summary>
    private static readonly Expression<Func<Patient, PatientListItemDto>> Projection = patient => new PatientListItemDto(
        patient.Id,
        patient.PatientNumber,
        patient.FirstName,
        patient.LastName,
        patient.DateOfBirth,
        patient.Gender,
        patient.Phone,
        patient.Email,
        patient.City,
        patient.State,
        patient.Status,
        patient.RegisteredAt,
        patient.BloodGroup,
        patient.Address,
        patient.PostalCode,
        patient.EmergencyContactName,
        patient.EmergencyContactPhone,
        patient.InsuranceProvider,
        patient.InsuranceNumber,
        patient.UserId != null);

    private static readonly Func<Patient, PatientListItemDto> ToDto = Projection.Compile();

    public async Task<PatientPageDto> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Patients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Case-insensitive, and also matches e-mail so a self-registered patient can be found by login e-mail.
            var term = search.Trim().ToLower();
            query = query.Where(patient =>
                patient.FirstName.ToLower().Contains(term) ||
                patient.LastName.ToLower().Contains(term) ||
                (patient.FirstName + " " + patient.LastName).ToLower().Contains(term) ||
                patient.PatientNumber.ToLower().Contains(term) ||
                patient.Phone.Contains(term) ||
                (patient.Email != null && patient.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(patient => patient.RegisteredAt)
            .ThenBy(patient => patient.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PatientPageDto(items, totalCount, page, pageSize);
    }

    public async Task<PatientListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.Id == id)
            .Select(Projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>The patient record linked to the given login account, or null when the user has none.</summary>
    public async Task<PatientListItemDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.UserId == userId)
            .Select(Projection)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PatientListItemDto> CreateAsync(
        CreatePatientRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DateOfBirth is null)
            throw new RequestValidationException("Date of birth is required.");
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new RequestValidationException("Date of birth cannot be in the future.");

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var phone = request.Phone.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();

        if (firstName.Length == 0 || lastName.Length == 0 || phone.Length == 0)
            throw new RequestValidationException("First name, last name and phone cannot be blank.");

        if (email is not null)
        {
            // A login account with this e-mail already exists. Its Patient record was created at registration,
            // so registering again would create a duplicate record (or silently attach someone else's account).
            if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
                throw new ConflictException(
                    "A portal account with this email already exists. Search the patient list instead of registering again.");

            if (await dbContext.Patients.AnyAsync(patient => patient.Email != null && patient.Email.ToLower() == email, cancellationToken))
                throw new ConflictException("A patient with this email is already registered.");
        }

        var dateOfBirth = request.DateOfBirth.Value;
        if (await dbContext.Patients.AnyAsync(patient =>
                patient.FirstName.ToLower() == firstName.ToLower() &&
                patient.LastName.ToLower() == lastName.ToLower() &&
                patient.DateOfBirth == dateOfBirth &&
                patient.Phone == phone, cancellationToken))
            throw new ConflictException(
                "A patient with the same name, date of birth and phone number is already registered.");

        var patient = new Patient
        {
            PatientNumber = await PatientNumberGenerator.NextAsync(dbContext, cancellationToken),
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dateOfBirth,
            Gender = request.Gender,
            Phone = phone,
            Email = email,
            Address = request.Address.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            EmergencyContactName = request.EmergencyContactName.Trim(),
            EmergencyContactPhone = request.EmergencyContactPhone.Trim(),
            RegisteredAt = DateTime.UtcNow
        };

        dbContext.Patients.Add(patient);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            logger.LogWarning(ex, "Unique constraint violated while registering patient {PatientNumber}", patient.PatientNumber);
            throw new ConflictException("The patient could not be registered because it conflicts with an existing record.");
        }

        logger.LogInformation("Patient {PatientNumber} registered by staff", patient.PatientNumber);

        return ToDto(patient);
    }

    public async Task<PatientListItemDto?> GetOrCreateForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await GetByUserIdAsync(userId, cancellationToken);
        if (existing is not null)
            return existing;

        var user = await dbContext.Users
            .Include(candidate => candidate.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive, cancellationToken);
        if (user is null || user.Role.Name != "Patient")
            return null;

        var patient = new Patient
        {
            PatientNumber = await PatientNumberGenerator.NextAsync(dbContext, cancellationToken),
            UserId = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.PhoneNumber ?? string.Empty,
            Gender = Gender.Unknown,
            RegisteredAt = DateTime.UtcNow,
            Status = PatientStatus.Active
        };

        dbContext.Patients.Add(patient);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // A concurrent request created it first (UserId is unique): just return that one.
            dbContext.Entry(patient).State = EntityState.Detached;
            return await GetByUserIdAsync(userId, cancellationToken);
        }

        logger.LogWarning("Patient record {PatientNumber} was missing for portal account {UserId} and has been created", patient.PatientNumber, userId);
        return ToDto(patient);
    }

    public async Task<PatientListItemDto> UpdateMineAsync(
        Guid userId,
        UpdatePatientProfileRequest request,
        CancellationToken cancellationToken)
    {
        // Make sure the record exists, then resolve it from the account - never from a client-supplied id.
        var mine = await GetOrCreateForUserAsync(userId, cancellationToken)
            ?? throw new NotFoundException("No patient record is linked to this account.");
        return await UpdateAsync(mine.Id, request, cancellationToken);
    }

    public async Task<PatientListItemDto> UpdateAsync(
        Guid id,
        UpdatePatientProfileRequest request,
        CancellationToken cancellationToken)
    {
        var patient = await dbContext.Patients
            .Include(candidate => candidate.User)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("Patient was not found.");

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (firstName.Length == 0 || lastName.Length == 0)
            throw new RequestValidationException("First name and last name cannot be blank.");
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new RequestValidationException("Date of birth cannot be in the future.");
        if (!string.IsNullOrWhiteSpace(request.Phone) && !new System.ComponentModel.DataAnnotations.PhoneAttribute().IsValid(request.Phone))
            throw new RequestValidationException("Phone number is invalid.");

        patient.FirstName = firstName;
        patient.LastName = lastName;
        patient.DateOfBirth = request.DateOfBirth;
        patient.Gender = request.Gender;
        patient.BloodGroup = request.BloodGroup;
        patient.Phone = request.Phone?.Trim() ?? string.Empty;
        patient.Address = request.Address?.Trim() ?? string.Empty;
        patient.City = request.City?.Trim() ?? string.Empty;
        patient.State = request.State?.Trim() ?? string.Empty;
        patient.PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode.Trim();
        patient.EmergencyContactName = request.EmergencyContactName?.Trim() ?? string.Empty;
        patient.EmergencyContactPhone = request.EmergencyContactPhone?.Trim() ?? string.Empty;
        patient.InsuranceProvider = string.IsNullOrWhiteSpace(request.InsuranceProvider) ? null : request.InsuranceProvider.Trim();
        patient.InsuranceNumber = string.IsNullOrWhiteSpace(request.InsuranceNumber) ? null : request.InsuranceNumber.Trim();

        // Keep the login account's display data in step with the medical record.
        if (patient.User is not null)
        {
            patient.User.FirstName = firstName;
            patient.User.LastName = lastName;
            patient.User.PhoneNumber = patient.Phone.Length == 0 ? null : patient.Phone;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(patient);
    }
}
