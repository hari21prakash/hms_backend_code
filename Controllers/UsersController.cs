using System.Security.Claims;
using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(IStaffAccountService staffAccountService) : ControllerBase
{
    [HttpGet("assignable-roles")]
    [HasPermission("users.manage")]
    public async Task<IActionResult> GetAssignableRoles(CancellationToken cancellationToken)
    {
        var roles = await staffAccountService.GetAssignableRolesAsync(GetActorRole(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StaffRoleOptionDto>>.Ok(roles));
    }

    [HttpPost]
    [HasPermission("users.manage")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccountDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        CreateStaffAccountRequest request,
        CancellationToken cancellationToken)
    {
        var account = await staffAccountService.CreateAsync(request, GetActorRole(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<StaffAccountDto>.Ok(account, "Staff account created."));
    }

    private string GetActorRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}