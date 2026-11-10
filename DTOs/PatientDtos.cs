using System.ComponentModel.DataAnnotations;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.DTOs;

public class CreatePatientRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public DateOnly? DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    [Required, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string State { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string EmergencyContactName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string EmergencyContactPhone { get; set; } = string.Empty;
}

/// <summary>Full patient details. Used by the list, the staff detail view and the patient's own profile.</summary>
public record PatientListItemDto(
    Guid Id,
    string PatientNumber,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    Gender Gender,
    string Phone,
    string? Email,
    string City,
    string State,
    PatientStatus Status,
    DateTime RegisteredAt,
    BloodGroup BloodGroup = BloodGroup.Unknown,
    string Address = "",
    string? PostalCode = null,
    string EmergencyContactName = "",
    string EmergencyContactPhone = "",
    string? InsuranceProvider = null,
    string? InsuranceNumber = null,
    bool HasPortalAccount = false);

/// <summary>
/// Profile update used by a patient (PUT /patients/me) and by staff (PUT /patients/{id}).
/// Everything except the name is optional so a self-registered patient can complete the profile step by step.
/// The e-mail is the login identity and is deliberately not editable here.
/// </summary>
public class UpdatePatientProfileRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public Gender Gender { get; set; } = Gender.Unknown;

    public BloodGroup BloodGroup { get; set; } = BloodGroup.Unknown;

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(150)]
    public string? EmergencyContactName { get; set; }

    [StringLength(30)]
    public string? EmergencyContactPhone { get; set; }

    [StringLength(150)]
    public string? InsuranceProvider { get; set; }

    [StringLength(100)]
    public string? InsuranceNumber { get; set; }
}

public record PatientPageDto(
    IReadOnlyList<PatientListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);