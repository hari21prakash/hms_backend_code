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
[Route("api/appointments")]
public class AppointmentsController(IAppointmentService appointmentService) : ControllerBase
{
    [HttpGet]
    [HasPermission("appointments.read")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await appointmentService.GetPageAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<AppointmentPageDto>.Ok(result));
    }

    [HttpGet("lookup")]
    [HasPermission("appointments.read")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentLookupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookup(CancellationToken cancellationToken)
    {
        var lookup = await appointmentService.GetLookupAsync(cancellationToken);
        return Ok(ApiResponse<AppointmentLookupDto>.Ok(lookup));
    }

    [HttpPost]
    [HasPermission("appointments.manage")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentListItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAppException();

        var appointment = await appointmentService.CreateAsync(request, userId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AppointmentListItemDto>.Ok(appointment, "Appointment booked."));
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission("appointments.manage")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        CancelAppointmentRequest? request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
            User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(subject, out var userId))
            throw new UnauthorizedAppException();

        var appointment = await appointmentService.CancelAsync(id, request ?? new CancelAppointmentRequest(), userId, cancellationToken);
        return Ok(ApiResponse<AppointmentListItemDto>.Ok(appointment, "Appointment cancelled."));
    }

    [HttpPost("{id:guid}/status")]
    [HasPermission("appointments.manage")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateAppointmentStatusRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AppointmentListItemDto>.Ok(await appointmentService.UpdateStatusAsync(id, request, cancellationToken), "Appointment updated."));
}
