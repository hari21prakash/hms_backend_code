using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/facilities")]
public class FacilitiesController(IFacilityService facilityService) : ControllerBase
{
    [HttpGet("wards")]
    [HasPermission("inpatient.read")]
    public async Task<IActionResult> GetWards(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<WardOverviewDto>>.Ok(await facilityService.GetWardsAsync(cancellationToken)));

    [HttpPost("wards")]
    [HasPermission("inpatient.manage")]
    public async Task<IActionResult> CreateWard(CreateWardRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created,
            ApiResponse<WardOverviewDto>.Ok(await facilityService.CreateWardAsync(request, cancellationToken), "Ward created."));

    [HttpPost("rooms")]
    [HasPermission("inpatient.manage")]
    public async Task<IActionResult> CreateRoom(CreateRoomRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created,
            ApiResponse<RoomOverviewDto>.Ok(await facilityService.CreateRoomAsync(request, cancellationToken), "Room created."));

    [HttpPost("beds")]
    [HasPermission("inpatient.manage")]
    public async Task<IActionResult> CreateBeds(CreateBedsRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created,
            ApiResponse<IReadOnlyList<BedOverviewDto>>.Ok(await facilityService.CreateBedsAsync(request, cancellationToken), "Bed(s) created."));

    [HttpPut("beds/{bedId:guid}/status")]
    [HasPermission("inpatient.manage")]
    public async Task<IActionResult> UpdateBedStatus(Guid bedId, UpdateBedStatusRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<BedOverviewDto>.Ok(await facilityService.UpdateBedStatusAsync(bedId, request, cancellationToken), "Bed updated."));
}
