using System.ComponentModel.DataAnnotations;

namespace HospitalManagement.Api.DTOs;

public record RoleDto(Guid Id, string Name, string Description, bool IsSystemRole, IReadOnlyList<string> PermissionCodes);

public record PermissionDto(Guid Id, string Code, string Module, string Description);

public class SetRolePermissionsRequest
{
    [Required]
    public List<string> PermissionCodes { get; set; } = [];
}