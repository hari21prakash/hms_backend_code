using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/laboratory")]
public class LaboratoryController(ILaboratoryService laboratoryService) : ControllerBase
{
    [HttpGet]
    [HasPermission("laboratory.read")]
    [ProducesResponseType(typeof(ApiResponse<LaboratoryPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await laboratoryService.GetPageAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<LaboratoryPageDto>.Ok(result));
    }

    [HttpGet("lookup")]
    [HasPermission("laboratory.read")]
    [ProducesResponseType(typeof(ApiResponse<LaboratoryLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(CancellationToken cancellationToken)
    {
        var lookup = await laboratoryService.GetLookupAsync(cancellationToken);
        return Ok(ApiResponse<LaboratoryLookupDto>.Ok(lookup));
    }

    [HttpPost]
    [HasPermission("laboratory.order")]
    [ProducesResponseType(typeof(ApiResponse<LabOrderDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateLabOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await laboratoryService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<LabOrderDetailDto>.Ok(result, "Lab order created."));
    }

    /// <summary>Records the result of a lab order and completes it. Notifies the patient and the ordering doctor.</summary>
    [HttpPost("{id:guid}/result")]
    [HasPermission("laboratory.manage")]
    [ProducesResponseType(typeof(ApiResponse<LabOrderDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordResult(Guid id, RecordLabResultRequest request, CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAppException();

        var result = await laboratoryService.RecordResultAsync(id, request, userId, cancellationToken);
        return Ok(ApiResponse<LabOrderDetailDto>.Ok(result, "Lab result recorded."));
    }
}
