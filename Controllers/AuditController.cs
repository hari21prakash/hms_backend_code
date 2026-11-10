using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/audit")]
public class AuditController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [HasPermission("audit.read")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] string? entityName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await notificationService.GetAuditLogsAsync(search, entityName, page, pageSize, cancellationToken);
        return Ok(ApiResponse<AuditLogPageDto>.Ok(result));
    }
}
