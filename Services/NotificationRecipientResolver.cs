using HospitalManagement.Api.Data;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>
/// Turns business relationships (the patient's portal account, the booked doctor, "all pharmacists", ...) into
/// the set of user ids that should receive a notification.
/// Every method only returns users who are ACTIVE and whose role currently holds <c>dashboard.read</c> - the
/// permission the notification endpoints and the SignalR hub require - so nothing is stored for someone who could
/// never read it.
/// </summary>
public interface INotificationRecipientResolver
{
    /// <summary>Filters the given user ids (nulls ignored) down to eligible recipients.</summary>
    Task<HashSet<Guid>> EligibleAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken);

    /// <summary>All eligible users holding any of the given roles.</summary>
    Task<HashSet<Guid>> InRolesAsync(CancellationToken cancellationToken, params string[] roleNames);

    /// <summary>Eligible active nurses assigned to the department.</summary>
    Task<HashSet<Guid>> NursesInDepartmentAsync(Guid departmentId, CancellationToken cancellationToken);
}

public class NotificationRecipientResolver(AppDbContext dbContext) : INotificationRecipientResolver
{
    public const string RequiredPermission = "dashboard.read";

    public async Task<HashSet<Guid>> EligibleAsync(IEnumerable<Guid?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => id.HasValue && id.Value != Guid.Empty).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];

        var eligible = await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id) &&
                           user.IsActive &&
                           user.Role.RolePermissions.Any(link => link.Permission.Code == RequiredPermission))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        return eligible.ToHashSet();
    }

    public async Task<HashSet<Guid>> InRolesAsync(CancellationToken cancellationToken, params string[] roleNames)
    {
        if (roleNames.Length == 0) return [];

        var ids = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive &&
                           roleNames.Contains(user.Role.Name) &&
                           user.Role.RolePermissions.Any(link => link.Permission.Code == RequiredPermission))
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public async Task<HashSet<Guid>> NursesInDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        var userIds = await dbContext.Nurses
            .AsNoTracking()
            .Where(nurse => nurse.DepartmentId == departmentId && nurse.Status == StaffStatus.Active)
            .Select(nurse => (Guid?)nurse.UserId)
            .ToListAsync(cancellationToken);
        return await EligibleAsync(userIds, cancellationToken);
    }
}
