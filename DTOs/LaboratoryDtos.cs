using System.ComponentModel.DataAnnotations;

namespace HospitalManagement.Api.DTOs;

public class CreateLabOrderRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    public Guid? ConsultationId { get; set; }

    [Required]
    public Guid LabTestId { get; set; }

    public string Priority { get; set; } = "Routine";

    [StringLength(1000)]
    public string? ClinicalNotes { get; set; }
}

public class RecordLabResultRequest
{
    [Required, StringLength(500, MinimumLength = 1)]
    public string ResultValue { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Unit { get; set; }

    public bool IsAbnormal { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }
}

public record LabTestOptionDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    decimal Price,
    string? NormalRange,
    string? Unit);

public record LabOrderListItemDto(
    Guid Id,
    string OrderNumber,
    string PatientName,
    string DoctorName,
    string TestName,
    string Priority,
    string Status,
    DateTime OrderedAt);

public record LabOrderDetailDto(
    Guid Id,
    string OrderNumber,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid LabTestId,
    string TestName,
    string? Priority,
    string? Status,
    DateTime OrderedAt,
    string? ClinicalNotes,
    string? ResultValue,
    string? Unit,
    string? NormalRange,
    bool IsAbnormal,
    string? Remarks);

public record LaboratoryLookupDto(
    IReadOnlyList<PatientOptionDto> Patients,
    IReadOnlyList<DoctorOptionDto> Doctors,
    IReadOnlyList<LabTestOptionDto> Tests);

public record LaboratoryPageDto(
    IReadOnlyList<LabOrderListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
