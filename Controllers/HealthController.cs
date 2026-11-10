using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(IHealthService healthService) : ControllerBase
{
    /// <summary>Reports API and database status. Returns 503 when the database is unreachable.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<HealthStatusDto>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var status = await healthService.CheckAsync(cancellationToken);

        if (status.Database.Connected)
            return Ok(ApiResponse<HealthStatusDto>.Ok(status, "Healthy"));

        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            ApiResponse<HealthStatusDto>.Fail("Database is unreachable.", status));
    }
}
