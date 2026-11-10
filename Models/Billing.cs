using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.Models;

public class Invoice : BaseEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public Guid? AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public DateTime InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    // Amounts are calculated by the billing service; database check constraints guarantee consistency.
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public InvoiceItemType ItemType { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Guid? LabOrderId { get; set; }
    public LabOrder? LabOrder { get; set; }

    public Guid? PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }
}

public class Payment : BaseEntity
{
    public string PaymentNumber { get; set; } = string.Empty;

    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
    public string? TransactionReference { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }

    public Guid ReceivedByUserId { get; set; }
    public User ReceivedByUser { get; set; } = null!;
}
