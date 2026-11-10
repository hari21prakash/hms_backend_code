using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admissions")]
public class AdmissionsController(IAdmissionService admissionService) : ControllerBase
{
    [HttpGet]
    [HasPermission("inpatient.read")]
    [ProducesResponseType(typeof(ApiResponse<AdmissionPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await admissionService.GetPageAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<AdmissionPageDto>.Ok(result));
    }

    [HttpGet("lookup")]
    [HasPermission("inpatient.read")]
    [ProducesResponseType(typeof(ApiResponse<AdmissionLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(CancellationToken cancellationToken)
    {
        var lookup = await admissionService.GetLookupAsync(cancellationToken);
        return Ok(ApiResponse<AdmissionLookupDto>.Ok(lookup));
    }

    [HttpPost]
    [HasPermission("inpatient.manage")]
    [ProducesResponseType(typeof(ApiResponse<AdmissionDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateAdmissionRequest request, CancellationToken cancellationToken)
    {
        var result = await admissionService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AdmissionDetailDto>.Ok(result, "Admission created successfully."));
    }

    [HttpPost("{id:guid}/discharge")]
    [HasPermission("inpatient.manage")]
    public async Task<IActionResult> Discharge(Guid id, DischargeAdmissionRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<DischargeResultDto>.Ok(await admissionService.DischargeAsync(id, request, cancellationToken), "Patient discharged."));
}
