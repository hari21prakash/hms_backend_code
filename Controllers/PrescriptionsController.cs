using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/prescriptions")]
public class PrescriptionsController(IPrescriptionService prescriptionService) : ControllerBase
{
    [HttpGet]
    [HasPermission("prescriptions.read")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await prescriptionService.GetPageAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PrescriptionPageDto>.Ok(result));
    }

    [HttpGet("lookup")]
    [HasPermission("prescriptions.read")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(CancellationToken cancellationToken)
    {
        var lookup = await prescriptionService.GetLookupAsync(cancellationToken);
        return Ok(ApiResponse<PrescriptionLookupDto>.Ok(lookup));
    }

    [HttpGet("{id:guid}")]
    [HasPermission("prescriptions.read")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await prescriptionService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<PrescriptionDetailDto>.Ok(result));
    }

    [HttpPost]
    [HasPermission("prescriptions.manage")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreatePrescriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await prescriptionService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PrescriptionDetailDto>.Ok(result, "Prescription created."));
    }
}
