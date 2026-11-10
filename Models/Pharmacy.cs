using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.Models;

public class Prescription : BaseEntity
{
    public string PrescriptionNumber { get; set; } = string.Empty;

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public Guid? ConsultationId { get; set; }
    public Consultation? Consultation { get; set; }

    public DateTime PrescribedAt { get; set; }
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Active;
    public string? Instructions { get; set; }

    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}

public class PrescriptionItem : BaseEntity
{
    public Guid PrescriptionId { get; set; }
    public Prescription Prescription { get; set; } = null!;

    public Guid MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    /// <summary>Free text such as "500mg".</summary>
    public string Dosage { get; set; } = string.Empty;
    /// <summary>Free text such as "2 times/day".</summary>
    public string Frequency { get; set; } = string.Empty;
    public string Route { get; set; } = "Oral";
    public int DurationDays { get; set; }
    public int Quantity { get; set; }
    public int QuantityDispensed { get; set; }
    public string? Instructions { get; set; }
}

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Medicine catalogue entry. Stock, prices and expiry live on <see cref="MedicineBatch"/>.</summary>
public class Medicine : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string Strength { get; set; } = string.Empty;
    public MedicineForm Form { get; set; } = MedicineForm.Tablet;
    public int ReorderLevel { get; set; }
    public MedicineStatus Status { get; set; } = MedicineStatus.Active;

    public ICollection<MedicineBatch> Batches { get; set; } = new List<MedicineBatch>();
}

public class MedicineBatch : BaseEntity
{
    public Guid MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int QuantityReceived { get; set; }
    public int QuantityOnHand { get; set; }
}

/// <summary>Immutable stock ledger. QuantityChange is signed (+ in, - out).</summary>
public class PharmacyTransaction : BaseEntity
{
    public Guid MedicineBatchId { get; set; }
    public MedicineBatch MedicineBatch { get; set; } = null!;

    public PharmacyTransactionType Type { get; set; }
    public int QuantityChange { get; set; }
    public DateTime TransactionDate { get; set; }

    public Guid? PrescriptionItemId { get; set; }
    public PrescriptionItem? PrescriptionItem { get; set; }

    public Guid PerformedByUserId { get; set; }
    public User PerformedByUser { get; set; } = null!;

    public string? Notes { get; set; }
}
