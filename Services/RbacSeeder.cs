using HospitalManagement.Api.Data;
using HospitalManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public static class RbacSeeder
{
    private const string SuperAdminRole = "Super Admin";

    private static readonly HashSet<string> SuperAdminRequiredPermissions =
        ["dashboard.read", "roles.read", "roles.manage", "users.manage"];

    private static readonly (string Name, string Description)[] RoleCatalog =
    [
        ("Super Admin", "Full system administration"),
        ("Hospital Admin", "Hospital operations administration"),
        ("Doctor", "Clinical care and consultations"),
        ("Nurse", "Nursing care and vital signs"),
        ("Receptionist", "Patient registration and scheduling"),
        ("Pharmacist", "Medicine and prescription operations"),
        ("Lab Technician", "Laboratory workflow operations"),
        ("Accountant", "Billing and payment operations"),
        ("Patient", "Self-service patient account")
    ];

    private static readonly (string Code, string Description)[] PermissionCatalog =
    [
        ("dashboard.read", "View the application dashboard"),
        ("users.read", "View user accounts"),
        ("users.manage", "Manage user accounts and roles"),
        ("roles.read", "View roles and permissions"),
        ("roles.manage", "Change role permission assignments"),
        ("patients.read", "View patient records"),
        ("patients.create", "Register patients"),
        ("patients.manage", "Update patient records and status"),
        ("staff.read", "View staff profiles"),
        ("staff.manage", "Manage staff profiles"),
        ("departments.read", "View departments"),
        ("departments.manage", "Manage departments"),
        ("appointments.read", "View appointments"),
        ("appointments.manage", "Book and manage appointments"),
        ("consultations.read", "View consultations"),
        ("consultations.manage", "Create and update consultations"),
        ("medical-records.read", "View medical records"),
        ("medical-records.manage", "Create and update medical records"),
        ("prescriptions.read", "View prescriptions"),
        ("prescriptions.manage", "Create and manage prescriptions"),
        ("pharmacy.read", "View pharmacy inventory"),
        ("pharmacy.manage", "Manage medicines, suppliers, batches and stock adjustments"),
        ("pharmacy.dispense", "Dispense prescribed medicines from stock"),
        ("laboratory.read", "View laboratory orders and results"),
        ("laboratory.manage", "Manage laboratory workflow"),
        ("laboratory.order", "Order laboratory tests"),
        ("inpatient.read", "View wards, beds, and admissions"),
        ("inpatient.manage", "Manage beds and admissions"),
        ("nursing.read", "View nursing notes and vital signs"),
        ("nursing.manage", "Record nursing notes and vital signs"),
        ("billing.read", "View invoices and payments"),
        ("billing.manage", "Manage invoices and payments"),
        ("reports.read", "View operational reports"),
        ("audit.read", "View audit logs"),
        ("settings.manage", "Manage system settings")
    ];

    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var roles = await dbContext.Roles.ToDictionaryAsync(role => role.Name, cancellationToken);
        foreach (var (name, description) in RoleCatalog)
        {
            if (roles.ContainsKey(name)) continue;
            var role = new Role { Name = name, Description = description, IsSystemRole = true };
            roles.Add(name, role);
            dbContext.Roles.Add(role);
        }

        var permissions = await dbContext.Permissions.ToDictionaryAsync(permission => permission.Code, cancellationToken);
        var newPermissionCodes = new List<string>();
        foreach (var (code, description) in PermissionCatalog)
        {
            if (permissions.ContainsKey(code)) continue;
            var permission = new Permission
            {
                Code = code,
                Module = code.Split('.')[0],
                Description = description
            };
            permissions.Add(code, permission);
            newPermissionCodes.Add(code);
            dbContext.Permissions.Add(permission);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Grant rules (never destructive):
        //  * Fresh database (no grants at all): apply every default grant.
        //  * Existing database: apply default grants ONLY for permissions created during this run, so a
        //    newly introduced permission reaches its intended roles exactly once. Grants an administrator
        //    removed or added through the Roles page are never reset or overwritten.
        //  * Super Admin always keeps the permissions needed to manage roles and users (anti-lockout).
        var isFirstRun = !await dbContext.RolePermissions.AnyAsync(cancellationToken);
        var newCodes = newPermissionCodes.ToHashSet(StringComparer.Ordinal);

        var existingLinks = (await dbContext.RolePermissions
                .Select(link => new { link.RoleId, link.PermissionId })
                .ToListAsync(cancellationToken))
            .Select(link => (link.RoleId, link.PermissionId))
            .ToHashSet();

        foreach (var (roleName, permissionCodes) in CreateDefaultGrants())
        {
            foreach (var code in permissionCodes)
            {
                var shouldGrant = isFirstRun ||
                    newCodes.Contains(code) ||
                    (roleName == SuperAdminRole && SuperAdminRequiredPermissions.Contains(code));
                if (!shouldGrant) continue;

                var key = (roles[roleName].Id, permissions[code].Id);
                if (!existingLinks.Add(key)) continue;

                dbContext.RolePermissions.Add(new RolePermission { RoleId = key.Item1, PermissionId = key.Item2 });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, string[]> CreateDefaultGrants()
    {
        var all = PermissionCatalog.Select(permission => permission.Code).ToArray();
        return new Dictionary<string, string[]>
        {
            ["Super Admin"] = all,
            ["Hospital Admin"] = all.Where(code => code != "roles.manage").ToArray(),
            ["Doctor"] = ["dashboard.read", "patients.read", "appointments.read", "appointments.manage", "consultations.read", "consultations.manage", "medical-records.read", "medical-records.manage", "prescriptions.read", "prescriptions.manage", "laboratory.read", "laboratory.order"],
            ["Nurse"] = ["dashboard.read", "patients.read", "appointments.read", "medical-records.read", "nursing.read", "nursing.manage"],
            ["Receptionist"] = ["dashboard.read", "patients.read", "patients.create", "patients.manage", "appointments.read", "appointments.manage", "inpatient.read", "billing.read"],
            ["Pharmacist"] = ["dashboard.read", "prescriptions.read", "pharmacy.read", "pharmacy.manage", "pharmacy.dispense"],
            ["Lab Technician"] = ["dashboard.read", "laboratory.read", "laboratory.manage", "laboratory.order"],
            ["Accountant"] = ["dashboard.read", "billing.read", "billing.manage", "reports.read"],
            ["Patient"] = ["dashboard.read"]
        };
    }
}