using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IStaffAccountService
{
    Task<IReadOnlyList<StaffRoleOptionDto>> GetAssignableRolesAsync(string actorRole, CancellationToken cancellationToken);
    Task<StaffAccountDto> CreateAsync(CreateStaffAccountRequest request, string actorRole, CancellationToken cancellationToken);
}