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
[Route("api/consultations")]
public class ConsultationsController(IConsultationService consultationService) : ControllerBase
{
    [HttpGet]
    [HasPermission("consultations.read")]
    [ProducesResponseType(typeof(ApiResponse<ConsultationPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await consultationService.GetPageAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<ConsultationPageDto>.Ok(result));
    }

    [HttpGet("lookup")]
    [HasPermission("consultations.read")]
    [ProducesResponseType(typeof(ApiResponse<ConsultationLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(CancellationToken cancellationToken)
    {
        var lookup = await consultationService.GetLookupAsync(cancellationToken);
        return Ok(ApiResponse<ConsultationLookupDto>.Ok(lookup));
    }

    [HttpGet("{id:guid}")]
    [HasPermission("consultations.read")]
    [ProducesResponseType(typeof(ApiResponse<ConsultationDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await consultationService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ConsultationDetailDto>.Ok(result));
    }

    [HttpPost]
    [HasPermission("consultations.manage")]
    [ProducesResponseType(typeof(ApiResponse<ConsultationDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateConsultationRequest request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAppException();

        var consultation = await consultationService.CreateAsync(request, userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ConsultationDetailDto>.Ok(consultation, "Consultation recorded."));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("consultations.manage")]
    [ProducesResponseType(typeof(ApiResponse<ConsultationDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, UpdateConsultationRequest request, CancellationToken cancellationToken)
    {
        var consultation = await consultationService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<ConsultationDetailDto>.Ok(consultation, "Consultation updated."));
    }
}
