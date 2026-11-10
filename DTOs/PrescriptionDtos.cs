using System.ComponentModel.DataAnnotations;

namespace HospitalManagement.Api.DTOs;

public class CreatePrescriptionItemRequest
{
    [Required]
    public Guid MedicineId { get; set; }

    [Required, StringLength(100)]
    public string Dosage { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Frequency { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Route { get; set; } = "Oral";

    [Range(1, int.MaxValue)]
    public int DurationDays { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [StringLength(500)]
    public string? Instructions { get; set; }
}

public class CreatePrescriptionRequest
{
    [Required]
    public Guid PatientId { get; set; }

    [Required]
    public Guid DoctorId { get; set; }

    public Guid? ConsultationId { get; set; }

    [StringLength(1000)]
    public string? Instructions { get; set; }

    [MinLength(1)]
    public List<CreatePrescriptionItemRequest> Items { get; set; } = new();
}

public record PrescriptionItemDto(
    Guid Id,
    Guid MedicineId,
    string MedicineName,
    string Dosage,
    string Frequency,
    string Route,
    int DurationDays,
    int Quantity,
    int QuantityDispensed,
    string? Instructions);

public record PrescriptionListItemDto(
    Guid Id,
    string PrescriptionNumber,
    string PatientName,
    string DoctorName,
    DateTime PrescribedAt,
    string Status,
    int ItemCount);

public record PrescriptionDetailDto(
    Guid Id,
    string PrescriptionNumber,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    Guid? ConsultationId,
    DateTime PrescribedAt,
    string Status,
    string? Instructions,
    IReadOnlyList<PrescriptionItemDto> Items);

public record PrescriptionPageDto(
    IReadOnlyList<PrescriptionListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record PrescriptionLookupDto(
    IReadOnlyList<PatientOptionDto> Patients,
    IReadOnlyList<DoctorOptionDto> Doctors,
    IReadOnlyList<MedicineOptionDto> Medicines);

public record MedicineOptionDto(
    Guid Id,
    string Name,
    string Strength,
    string Form,
    string Category,
    int QuantityOnHand);

public record MedicineStockItemDto(
    Guid MedicineId,
    string MedicineName,
    string BatchNumber,
    string Strength,
    DateOnly ExpiryDate,
    int QuantityOnHand,
    decimal SellingPrice);

public record PharmacyStockPageDto(
    IReadOnlyList<MedicineStockItemDto> Items,
    int TotalCount);
