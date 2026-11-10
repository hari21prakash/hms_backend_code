using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pharmacy")]
public class PharmacyController(
    IPharmacyService pharmacyService,
    IPharmacyStockService pharmacyStockService,
    IDispensingService dispensingService) : ControllerBase
{
    // -------------------------------------------------------------------------
    // Lookup
    // -------------------------------------------------------------------------

    [HttpGet("lookup")]
    [HasPermission("pharmacy.read")]
    [ProducesResponseType(typeof(ApiResponse<PharmacyLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(CancellationToken cancellationToken)
    {
        var result = await pharmacyService.GetLookupAsync(cancellationToken);
        return Ok(ApiResponse<PharmacyLookupDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // Stock
    // -------------------------------------------------------------------------

    [HttpGet("stock")]
    [HasPermission("pharmacy.read")]
    [ProducesResponseType(typeof(ApiResponse<PharmacyStockPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStock(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await pharmacyService.GetStockAsync(
            search,
            page,
            pageSize,
            cancellationToken);

        return Ok(ApiResponse<PharmacyStockPageDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // Medicines
    // -------------------------------------------------------------------------

    [HttpGet("medicines")]
    [HasPermission("pharmacy.read")]
    [ProducesResponseType(typeof(ApiResponse<MedicinePageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedicines(
        [FromQuery] string? search,
        [FromQuery] MedicineStatus? status,
        [FromQuery] string? category,
        [FromQuery(Name = "stock")] string? stockFilter,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await pharmacyService.GetMedicinesAsync(
            search,
            status,
            category,
            stockFilter,
            page,
            pageSize,
            cancellationToken);

        return Ok(ApiResponse<MedicinePageDto>.Ok(result));
    }

    [HttpGet("medicines/{id:guid}")]
    [HasPermission("pharmacy.read")]
    [ProducesResponseType(typeof(ApiResponse<MedicineDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedicine(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await pharmacyService.GetMedicineAsync(id, cancellationToken);
        return Ok(ApiResponse<MedicineDetailDto>.Ok(result));
    }

    [HttpPost("medicines")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<MedicineDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMedicine(
        MedicineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pharmacyService.CreateMedicineAsync(
            request,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<MedicineDetailDto>.Ok(result, "Medicine created."));
    }

    [HttpPut("medicines/{id:guid}")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<MedicineDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMedicine(
        Guid id,
        MedicineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pharmacyService.UpdateMedicineAsync(
            id,
            request,
            cancellationToken);

        return Ok(ApiResponse<MedicineDetailDto>.Ok(result, "Medicine updated."));
    }

    [HttpPatch("medicines/{id:guid}/status")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<MedicineDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetMedicineStatus(
        Guid id,
        [FromBody] UpdateMedicineStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pharmacyService.SetMedicineStatusAsync(
            id,
             request.Status!.Value,
            cancellationToken);

        return Ok(ApiResponse<MedicineDetailDto>.Ok(result, "Medicine status updated."));
    }

    // -------------------------------------------------------------------------
    // Suppliers
    // -------------------------------------------------------------------------

    [HttpGet("suppliers")]
    [HasPermission("pharmacy.read")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await pharmacyService.GetSuppliersAsync(
            search,
            isActive,
            page,
            pageSize,
            cancellationToken);

        return Ok(ApiResponse<SupplierPageDto>.Ok(result));
    }

    [HttpPost("suppliers")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSupplier(
        SupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pharmacyService.CreateSupplierAsync(
            request,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<SupplierDto>.Ok(result, "Supplier created."));
    }

    [HttpPut("suppliers/{id:guid}")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSupplier(
        Guid id,
        SupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pharmacyService.UpdateSupplierAsync(
            id,
            request,
            cancellationToken);

        return Ok(ApiResponse<SupplierDto>.Ok(result, "Supplier updated."));
    }

    [HttpPatch("suppliers/{id:guid}/status")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetSupplierStatus(
        Guid id,
        [FromBody] UpdateSupplierStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pharmacyService.SetSupplierStatusAsync(
            id,
              request.IsActive!.Value,
            cancellationToken);

        return Ok(ApiResponse<SupplierDto>.Ok(result, "Supplier status updated."));
    }

    [HttpDelete("suppliers/{id:guid}")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSupplier(
        Guid id,
        CancellationToken cancellationToken)
    {
        await pharmacyService.DeleteSupplierAsync(id, cancellationToken);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    // Batches
    // -------------------------------------------------------------------------

    [HttpGet("batches")]
    [HasPermission("pharmacy.read")]
    [ProducesResponseType(typeof(ApiResponse<BatchPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatches(
        [FromQuery] string? search,
        [FromQuery] Guid? medicineId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await pharmacyService.GetBatchesAsync(
            search,
            medicineId,
            status,
            page,
            pageSize,
            cancellationToken);

        return Ok(ApiResponse<BatchPageDto>.Ok(result));
    }

    [HttpPost("batches")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<BatchListItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> ReceiveStock(
        ReceiveStockRequest request,
        CancellationToken cancellationToken)
    {
       /*var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

if (!Guid.TryParse(actorUserId, out var actorId))
    return Unauthorized();

var result = await pharmacyStockService.ReceiveStockAsync(
    request,
    actorId,
    cancellationToken);
*/
var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
              User.FindFirstValue(ClaimTypes.NameIdentifier);

if (!Guid.TryParse(subject, out var actorUserId))
    return Unauthorized();

var result = await pharmacyStockService.ReceiveStockAsync(
    request,
    actorUserId,
    cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<BatchListItemDto>.Ok(result, "Stock received."));
    }

    [HttpPut("batches/{id:guid}")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<BatchListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBatch(
        Guid id,
        UpdateBatchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await pharmacyStockService.UpdateBatchAsync(
    id,
    request,
    cancellationToken);

        return Ok(ApiResponse<BatchListItemDto>.Ok(result, "Batch updated."));
    }

    [HttpPost("batches/{id:guid}/adjustments")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<BatchListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdjustStock(
        Guid id,
        AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
       /*var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

if (!Guid.TryParse(actorUserId, out var actorId))
    return Unauthorized();

var result = await pharmacyStockService.AdjustStockAsync(
    id,
    request,
    actorId,
    cancellationToken);*/
    var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
              User.FindFirstValue(ClaimTypes.NameIdentifier);

if (!Guid.TryParse(subject, out var actorUserId))
    return Unauthorized();

var result = await pharmacyStockService.AdjustStockAsync(
    id,
    request,
    actorUserId,
    cancellationToken);
        return Ok(ApiResponse<BatchListItemDto>.Ok(result, "Stock adjusted."));
    }

    // -------------------------------------------------------------------------
    // Transactions
    // -------------------------------------------------------------------------

    [HttpGet("transactions")]
    [HasPermission("pharmacy.read")]
    [ProducesResponseType(typeof(ApiResponse<PharmacyTransactionPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] string? search,
        [FromQuery] Guid? medicineId,
        [FromQuery] Guid? batchId,
        [FromQuery] PharmacyTransactionType? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await pharmacyService.GetTransactionsAsync(
            search,
            medicineId,
            batchId,
            type,
            page,
            pageSize,
            cancellationToken);

        return Ok(ApiResponse<PharmacyTransactionPageDto>.Ok(result));
    }

    // -------------------------------------------------------------------------
    // Dispensing
    // -------------------------------------------------------------------------

    [HttpGet("dispensing/{prescriptionId:guid}")]
    [HasPermission("pharmacy.read")]
    [ProducesResponseType(typeof(ApiResponse<DispenseWorksheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDispenseWorksheet(
        Guid prescriptionId,
        CancellationToken cancellationToken)
    {
        var result = await dispensingService.GetWorksheetAsync(
    prescriptionId,
    cancellationToken);

        return Ok(ApiResponse<DispenseWorksheetDto>.Ok(result));
    }

    [HttpPost("dispensing")]
    [HasPermission("pharmacy.manage")]
    [ProducesResponseType(typeof(ApiResponse<DispenseWorksheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dispense(
        DispenseRequest request,
        CancellationToken cancellationToken)
    {
      /*var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

if (!Guid.TryParse(actorUserId, out var actorId))
    return Unauthorized();

var result = await dispensingService.DispenseAsync(
    request,
    actorId,
    cancellationToken);*/
    var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
              User.FindFirstValue(ClaimTypes.NameIdentifier);

if (!Guid.TryParse(subject, out var actorUserId))
    return Unauthorized();

var result = await dispensingService.DispenseAsync(
    request,
    actorUserId,
    cancellationToken);

        return Ok(ApiResponse<DispenseWorksheetDto>.Ok(result, "Medicine dispensed."));
    }
}