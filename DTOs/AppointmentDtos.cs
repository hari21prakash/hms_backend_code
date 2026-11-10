using System.ComponentModel.DataAnnotations;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.DTOs;

public class CreateAppointmentRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    [Required]
    public DateOnly AppointmentDate { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    [Required]
    public TimeOnly EndTime { get; set; }

    public AppointmentType Type { get; set; } = AppointmentType.Consultation;

    [StringLength(500)]
    public string? Reason { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class CancelAppointmentRequest
{
    [StringLength(500)]
    public string? Reason { get; set; }
}

public record AppointmentListItemDto(
    Guid Id,
    string AppointmentNumber,
    string PatientName,
    string DoctorName,
    string DepartmentName,
    DateOnly AppointmentDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    AppointmentType Type,
    AppointmentStatus Status,
    string? Reason,
    DateTime CreatedAt);

public record AppointmentPageDto(
    IReadOnlyList<AppointmentListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record DoctorOptionDto(
    Guid Id,
    string FullName,
    string Specialization,
    string EmployeeId,
    string DepartmentName);

public record PatientOptionDto(
    Guid Id,
    string FullName,
    string PatientNumber);

public record AppointmentLookupDto(
    IReadOnlyList<DoctorOptionDto> Doctors,
    IReadOnlyList<PatientOptionDto> Patients);
