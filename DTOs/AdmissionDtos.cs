using System.ComponentModel.DataAnnotations;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.DTOs;

public class CreateAdmissionRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public Guid DepartmentId { get; set; }

    [Required]
    public Guid BedId { get; set; }

    public DateTime? AdmittedAt { get; set; }

    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public record AdmissionListItemDto(
    Guid Id,
    string AdmissionNumber,
    string PatientName,
    string DoctorName,
    string DepartmentName,
    string WardName,
    string RoomNumber,
    string BedNumber,
    AdmissionStatus Status,
    DateTime AdmittedAt,
    string Reason);

public record AdmissionDetailDto(
    Guid Id,
    string AdmissionNumber,
    string PatientName,
    string DoctorName,
    string DepartmentName,
    string WardName,
    string RoomNumber,
    string BedNumber,
    AdmissionStatus Status,
    DateTime AdmittedAt,
    string Reason,
    decimal DailyRateSnapshot);

public record AdmissionPageDto(
    IReadOnlyList<AdmissionListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record DepartmentOptionDto(
    Guid Id,
    string Name,
    string Code);

public record WardOptionDto(
    Guid Id,
    string Name,
    string Code,
    WardType Type,
    string? Description);

public record BedOptionDto(
    Guid Id,
    string WardName,
    string RoomNumber,
    string BedNumber,
    decimal DailyRate,
    BedStatus Status);

public record AdmissionLookupDto(
    IReadOnlyList<PatientOptionDto> Patients,
    IReadOnlyList<DoctorOptionDto> Doctors,
    IReadOnlyList<DepartmentOptionDto> Departments,
    IReadOnlyList<WardOptionDto> Wards,
    IReadOnlyList<BedOptionDto> Beds);
