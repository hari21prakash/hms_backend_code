using System.ComponentModel.DataAnnotations;

namespace HospitalManagement.Api.DTOs;

public class CreateVitalSignRequest
{
    [Required]
    public Guid PatientId { get; set; }

    public Guid? AdmissionId { get; set; }

    public decimal? TemperatureCelsius { get; set; }

    [Range(0, 250)]
    public int? SystolicBp { get; set; }

    [Range(0, 250)]
    public int? DiastolicBp { get; set; }

    [Range(0, 300)]
    public int? HeartRate { get; set; }

    [Range(0, 120)]
    public int? RespiratoryRate { get; set; }

    [Range(0, 100)]
    public int? OxygenSaturation { get; set; }

    [Range(0, 500)]
    public decimal? WeightKg { get; set; }

    [Range(0, 300)]
    public decimal? HeightCm { get; set; }
}

public class CreateNursingNoteRequest
{
    [Required]
    public Guid PatientId { get; set; }

    public Guid? AdmissionId { get; set; }

    [Required]
    [StringLength(4000)]
    public string Note { get; set; } = string.Empty;
}

public record VitalSignListItemDto(
    Guid Id,
    string PatientName,
    string? AdmissionNumber,
    string RecordedBy,
    DateTime RecordedAt,
    decimal? TemperatureCelsius,
    int? SystolicBp,
    int? DiastolicBp,
    int? HeartRate,
    int? RespiratoryRate,
    int? OxygenSaturation,
    decimal? WeightKg,
    decimal? HeightCm);

public record NursingNoteListItemDto(
    Guid Id,
    string PatientName,
    string? AdmissionNumber,
    string NurseName,
    DateTime CreatedAt,
    string Note);

public record AdmissionOptionDto(
    Guid Id,
    string AdmissionNumber,
    string PatientName);

public record NursingPageDto(
    IReadOnlyList<VitalSignListItemDto> VitalSigns,
    IReadOnlyList<NursingNoteListItemDto> Notes,
    int TotalVitalSigns,
    int TotalNotes,
    int Page,
    int PageSize);

public record NursingLookupDto(
    IReadOnlyList<PatientOptionDto> Patients,
    IReadOnlyList<AdmissionOptionDto> Admissions);
