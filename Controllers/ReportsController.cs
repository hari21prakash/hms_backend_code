using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("summary")]
    [HasPermission("reports.read")]
    [ProducesResponseType(typeof(ApiResponse<ReportsSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var result = await reportService.GetSummaryAsync(cancellationToken);
        return Ok(ApiResponse<ReportsSummaryDto>.Ok(result));
    }
}
