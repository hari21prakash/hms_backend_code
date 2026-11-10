using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IBillingService
{
    Task<InvoicePageDto> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<BillingLookupDto> GetLookupAsync(CancellationToken cancellationToken);
    Task<InvoiceDetailDto> CreateInvoiceAsync(CreateInvoiceRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<PaymentDto> RecordPaymentAsync(CreatePaymentRequest request, Guid actorUserId, CancellationToken cancellationToken);
}
