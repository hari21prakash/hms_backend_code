using System.ComponentModel.DataAnnotations;

namespace HospitalManagement.Api.DTOs;

public class CreateConsultationRequest
{
    [Required]
    public Guid AppointmentId { get; set; }

    [StringLength(200)]
    public string? ChiefComplaint { get; set; }

    [StringLength(2000)]
    public string? Symptoms { get; set; }

    [StringLength(2000)]
    public string? Examination { get; set; }

    [StringLength(2000)]
    public string? Diagnosis { get; set; }

    [StringLength(2000)]
    public string? TreatmentPlan { get; set; }

    [StringLength(2000)]
    public string? DoctorNotes { get; set; }

    public DateOnly? FollowUpDate { get; set; }
}

public class UpdateConsultationRequest
{
    [StringLength(200)]
    public string? ChiefComplaint { get; set; }

    [StringLength(2000)]
    public string? Symptoms { get; set; }

    [StringLength(2000)]
    public string? Examination { get; set; }

    [StringLength(2000)]
    public string? Diagnosis { get; set; }

    [StringLength(2000)]
    public string? TreatmentPlan { get; set; }

    [StringLength(2000)]
    public string? DoctorNotes { get; set; }

    public DateOnly? FollowUpDate { get; set; }
}

public record ConsultationListItemDto(
    Guid Id,
    string AppointmentNumber,
    string PatientName,
    string DoctorName,
    string DepartmentName,
    DateOnly AppointmentDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? ChiefComplaint,
    string? Diagnosis,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateOnly? FollowUpDate);

public record ConsultationDetailDto(
    Guid Id,
    Guid AppointmentId,
    string AppointmentNumber,
    string PatientName,
    string DoctorName,
    string DepartmentName,
    DateOnly AppointmentDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? ChiefComplaint,
    string? Symptoms,
    string? Examination,
    string? Diagnosis,
    string? TreatmentPlan,
    string? DoctorNotes,
    DateOnly? FollowUpDate,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? Reason,
    string? Notes);

public record ConsultationAppointmentOptionDto(
    Guid Id,
    string AppointmentNumber,
    string PatientName,
    string DoctorName,
    DateOnly AppointmentDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string? Reason);

public record ConsultationPageDto(
    IReadOnlyList<ConsultationListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record ConsultationLookupDto(
    IReadOnlyList<ConsultationAppointmentOptionDto> Appointments);
