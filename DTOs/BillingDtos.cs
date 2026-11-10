using System.ComponentModel.DataAnnotations;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.DTOs;

public class CreateInvoiceItemRequest
{
    [Required]
    public InvoiceItemType ItemType { get; set; } = InvoiceItemType.Other;

    [Required]
    [StringLength(300)]
    public string Description { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int Quantity { get; set; } = 1;

    [Range(0, 1000000)]
    public decimal UnitPrice { get; set; }

    public Guid? LabOrderId { get; set; }
    public Guid? PrescriptionId { get; set; }
}

public class CreateInvoiceRequest
{
    [Required]
    public Guid PatientId { get; set; }

    public Guid? AppointmentId { get; set; }
    public Guid? AdmissionId { get; set; }

    [Range(0, 1000000)]
    public decimal DiscountAmount { get; set; }

    [Range(0, 1000000)]
    public decimal TaxAmount { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [MinLength(1)]
    public List<CreateInvoiceItemRequest> Items { get; set; } = [];
}

public class CreatePaymentRequest
{
    [Required]
    public Guid InvoiceId { get; set; }

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    [Required]
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [StringLength(100)]
    public string? TransactionReference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public record InvoiceItemDto(
    Guid Id,
    Guid InvoiceId,
    InvoiceItemType ItemType,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    Guid? LabOrderId,
    Guid? PrescriptionId);

public record PaymentDto(
    Guid Id,
    Guid InvoiceId,
    string PaymentNumber,
    decimal Amount,
    PaymentMethod Method,
    PaymentStatus Status,
    string? TransactionReference,
    DateTime PaymentDate,
    string? Notes);

public record InvoiceListItemDto(
    Guid Id,
    string InvoiceNumber,
    string PatientName,
    DateTime InvoiceDate,
    InvoiceStatus Status,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount);

public record InvoiceDetailDto(
    Guid Id,
    string InvoiceNumber,
    string PatientName,
    DateTime InvoiceDate,
    InvoiceStatus Status,
    decimal SubtotalAmount,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string? Notes,
    IReadOnlyList<InvoiceItemDto> Items,
    IReadOnlyList<PaymentDto> Payments);

public record InvoicePageDto(
    IReadOnlyList<InvoiceListItemDto> Invoices,
    int TotalCount,
    int Page,
    int PageSize);

public record BillingLookupDto(
    IReadOnlyList<PatientOptionDto> Patients,
    IReadOnlyList<AdmissionOptionDto> Admissions);
