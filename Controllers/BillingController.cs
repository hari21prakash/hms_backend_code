using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Helpers;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/billing")]
public class BillingController(IBillingService billingService) : ControllerBase
{
    [HttpGet]
    [HasPermission("billing.read")]
    [ProducesResponseType(typeof(ApiResponse<InvoicePageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await billingService.GetPageAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<InvoicePageDto>.Ok(result));
    }

    [HttpGet("lookup")]
    [HasPermission("billing.read")]
    [ProducesResponseType(typeof(ApiResponse<BillingLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(CancellationToken cancellationToken)
    {
        var result = await billingService.GetLookupAsync(cancellationToken);
        return Ok(ApiResponse<BillingLookupDto>.Ok(result));
    }

    [HttpPost("invoices")]
    [HasPermission("billing.manage")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvoice(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var result = await billingService.CreateInvoiceAsync(request, User.GetRequiredUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<InvoiceDetailDto>.Ok(result, "Invoice created."));
    }

    [HttpPost("payments")]
    [HasPermission("billing.manage")]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordPayment(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await billingService.RecordPaymentAsync(request, User.GetRequiredUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PaymentDto>.Ok(result, "Payment recorded."));
    }
}
