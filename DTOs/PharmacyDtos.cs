using System.ComponentModel.DataAnnotations;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.DTOs;

// ---------- Medicines ----------

public class MedicineRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? GenericName { get; set; }

    [Required, StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Manufacturer { get; set; }

    [Required, StringLength(50)]
    public string Strength { get; set; } = string.Empty;

    [Required]
    public MedicineForm? Form { get; set; }

    [Range(0, 1_000_000)]
    public int ReorderLevel { get; set; }
}

public class UpdateMedicineStatusRequest
{
    [Required]
    public MedicineStatus? Status { get; set; }
}

public record MedicineListItemDto(
    Guid Id,
    string Name,
    string? GenericName,
    string Category,
    string? Manufacturer,
    string Strength,
    string Form,
    int ReorderLevel,
    string Status,
    int UsableStock,
    int ExpiredStock,
    bool IsLowStock);

public record MedicinePageDto(IReadOnlyList<MedicineListItemDto> Items, int TotalCount, int Page, int PageSize);

public record MedicineDetailDto(
    Guid Id,
    string Name,
    string? GenericName,
    string Category,
    string? Manufacturer,
    string Strength,
    string Form,
    int ReorderLevel,
    string Status,
    int UsableStock,
    int ExpiredStock,
    bool IsLowStock,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<BatchListItemDto> Batches);

// ---------- Suppliers ----------

public class SupplierRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? ContactPerson { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}

public class UpdateSupplierStatusRequest
{
    [Required]
    public bool? IsActive { get; set; }
}

public record SupplierDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive,
    int BatchCount,
    DateTime CreatedAt);

public record SupplierPageDto(IReadOnlyList<SupplierDto> Items, int TotalCount, int Page, int PageSize);

// ---------- Batches / stock ----------

public class ReceiveStockRequest
{
    [Required]
    public Guid MedicineId { get; set; }

    public Guid? SupplierId { get; set; }

    [Required, StringLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [Required]
    public DateOnly? ExpiryDate { get; set; }

    public DateOnly? ReceivedDate { get; set; }

    [Range(0, 10_000_000)]
    public decimal PurchasePrice { get; set; }

    [Range(0, 10_000_000)]
    public decimal SellingPrice { get; set; }

    [Range(1, 1_000_000)]
    public int Quantity { get; set; }

    [StringLength(400)]
    public string? Notes { get; set; }
}

/// <summary>Corrects batch details. Quantity is never edited here: use stock adjustments so the ledger stays complete.</summary>
public class UpdateBatchRequest
{
    public Guid? SupplierId { get; set; }

    [Required, StringLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [Required]
    public DateOnly? ExpiryDate { get; set; }

    [Range(0, 10_000_000)]
    public decimal PurchasePrice { get; set; }

    [Range(0, 10_000_000)]
    public decimal SellingPrice { get; set; }
}

public enum StockAdjustmentDirection { Increase, Decrease }

public class AdjustStockRequest
{
    /// <summary>Adjustment or WriteOff only. Purchases and dispenses have their own workflows.</summary>
    [Required]
    public PharmacyTransactionType? Type { get; set; }

    /// <summary>Required for Adjustment. WriteOff always removes stock.</summary>
    public StockAdjustmentDirection? Direction { get; set; }

    [Range(1, 1_000_000)]
    public int Quantity { get; set; }

    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public record BatchListItemDto(
    Guid Id,
    Guid MedicineId,
    string MedicineName,
    string Strength,
    string Form,
    Guid? SupplierId,
    string? SupplierName,
    string BatchNumber,
    DateOnly ExpiryDate,
    DateOnly ReceivedDate,
    decimal PurchasePrice,
    decimal SellingPrice,
    int QuantityReceived,
    int QuantityOnHand,
    bool IsExpired,
    string Status);

public record BatchPageDto(IReadOnlyList<BatchListItemDto> Items, int TotalCount, int Page, int PageSize);

public record PharmacyTransactionDto(
    Guid Id,
    string Type,
    int QuantityChange,
    DateTime TransactionDate,
    Guid MedicineId,
    string MedicineName,
    string BatchNumber,
    string PerformedBy,
    string? Notes,
    string? PrescriptionNumber);

public record PharmacyTransactionPageDto(IReadOnlyList<PharmacyTransactionDto> Items, int TotalCount, int Page, int PageSize);

// ---------- Lookup ----------

public record PharmacyMedicineOptionDto(Guid Id, string Name, string Strength, string Form);
public record SupplierOptionDto(Guid Id, string Name);

public record PharmacyLookupDto(
    IReadOnlyList<PharmacyMedicineOptionDto> Medicines,
    IReadOnlyList<SupplierOptionDto> Suppliers,
    IReadOnlyList<string> Categories);

// ---------- Dispensing ----------

public record DispenseBatchOptionDto(Guid BatchId, string BatchNumber, DateOnly ExpiryDate, int QuantityOnHand, decimal SellingPrice);

public record DispenseItemDto(
    Guid ItemId,
    Guid MedicineId,
    string MedicineName,
    string Strength,
    string Form,
    string MedicineStatus,
    string Dosage,
    string Frequency,
    int Quantity,
    int QuantityDispensed,
    int Remaining,
    int UsableStock,
    IReadOnlyList<DispenseBatchOptionDto> Batches);

public record DispenseWorksheetDto(
    Guid PrescriptionId,
    string PrescriptionNumber,
    string Status,
    Guid PatientId,
    string PatientName,
    string DoctorName,
    DateTime PrescribedAt,
    string? Instructions,
    IReadOnlyList<DispenseItemDto> Items);

public class DispenseAllocationRequest
{
    [Required]
    public Guid BatchId { get; set; }

    [Range(1, 1_000_000)]
    public int Quantity { get; set; }
}

public class DispenseItemRequest
{
    [Required]
    public Guid PrescriptionItemId { get; set; }

    [Required, MinLength(1)]
    public List<DispenseAllocationRequest> Allocations { get; set; } = [];
}

public class DispenseRequest
{
    [Required]
    public Guid PrescriptionId { get; set; }

    [Required, MinLength(1)]
    public List<DispenseItemRequest> Items { get; set; } = [];

    [StringLength(400)]
    public string? Notes { get; set; }
}
