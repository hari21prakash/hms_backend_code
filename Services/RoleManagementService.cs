using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class RoleManagementService(AppDbContext dbContext) : IRoleManagementService
{
    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new RoleDto(
                role.Id,
                role.Name,
                role.Description,
                role.IsSystemRole,
                role.RolePermissions.Select(link => link.Permission.Code).OrderBy(code => code).ToArray()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Module)
            .ThenBy(permission => permission.Code)
            .Select(permission => new PermissionDto(permission.Id, permission.Code, permission.Module, permission.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleDto> SetPermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Include(candidate => candidate.RolePermissions)
            .SingleOrDefaultAsync(candidate => candidate.Id == roleId, cancellationToken);
        if (role is null)
            throw new NotFoundException("Role", roleId);

        var normalizedCodes = permissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (role.Name == "Super Admin" &&
            (!normalizedCodes.Contains("roles.read") || !normalizedCodes.Contains("roles.manage")))
        {
            throw new RequestValidationException("Super Admin must retain roles.read and roles.manage permissions.");
        }

        var permissions = await dbContext.Permissions
            .Where(permission => normalizedCodes.Contains(permission.Code))
            .ToListAsync(cancellationToken);
        var foundCodes = permissions.Select(permission => permission.Code).ToHashSet(StringComparer.Ordinal);
        var unknownCodes = normalizedCodes.Where(code => !foundCodes.Contains(code)).ToArray();
        if (unknownCodes.Length > 0)
            throw new RequestValidationException("One or more permission codes are unknown.", unknownCodes);

        dbContext.RolePermissions.RemoveRange(role.RolePermissions);
        foreach (var permission in permissions)
            dbContext.RolePermissions.Add(new Models.RolePermission { RoleId = role.Id, PermissionId = permission.Id });

        await dbContext.SaveChangesAsync(cancellationToken);
        return new RoleDto(role.Id, role.Name, role.Description, role.IsSystemRole,
            permissions.Select(permission => permission.Code).OrderBy(code => code).ToArray());
    }
}