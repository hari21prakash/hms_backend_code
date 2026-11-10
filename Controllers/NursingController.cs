using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Helpers;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/nursing")]
public class NursingController(INursingService nursingService) : ControllerBase
{
    [HttpGet]
    [HasPermission("nursing.read")]
    [ProducesResponseType(typeof(ApiResponse<NursingPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await nursingService.GetPageAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<NursingPageDto>.Ok(result));
    }

    [HttpGet("lookup")]
    [HasPermission("nursing.read")]
    [ProducesResponseType(typeof(ApiResponse<NursingLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(CancellationToken cancellationToken)
    {
        var result = await nursingService.GetLookupAsync(cancellationToken);
        return Ok(ApiResponse<NursingLookupDto>.Ok(result));
    }

    [HttpPost("vital-signs")]
    [HasPermission("nursing.manage")]
    [ProducesResponseType(typeof(ApiResponse<VitalSignListItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVitalSign(CreateVitalSignRequest request, CancellationToken cancellationToken)
    {
        var result = await nursingService.CreateVitalSignAsync(request, User.GetRequiredUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<VitalSignListItemDto>.Ok(result, "Vital signs recorded."));
    }

    [HttpPost("notes")]
    [HasPermission("nursing.manage")]
    [ProducesResponseType(typeof(ApiResponse<NursingNoteListItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateNursingNote(CreateNursingNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await nursingService.CreateNursingNoteAsync(request, User.GetRequiredUserId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<NursingNoteListItemDto>.Ok(result, "Nursing note saved."));
    }
}
