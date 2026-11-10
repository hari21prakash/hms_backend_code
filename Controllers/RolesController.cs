using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Route("api/roles")]
public class RolesController(IRoleManagementService roleService) : ControllerBase
{
    [HttpGet]
    [HasPermission("roles.read")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await roleService.GetRolesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoleDto>>.Ok(roles));
    }

    [HttpGet("permissions")]
    [HasPermission("roles.read")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PermissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissions(CancellationToken cancellationToken)
    {
        var permissions = await roleService.GetPermissionsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PermissionDto>>.Ok(permissions));
    }

    [HttpPut("{roleId:guid}/permissions")]
    [HasPermission("roles.manage")]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPermissions(
        Guid roleId,
        SetRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var role = await roleService.SetPermissionsAsync(roleId, request.PermissionCodes, cancellationToken);
        return Ok(ApiResponse<RoleDto>.Ok(role, "Role permissions updated."));
    }
}