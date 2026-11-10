using System.ComponentModel.DataAnnotations;
using System.Text;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class StaffAccountService(AppDbContext dbContext) : IStaffAccountService
{
    private static readonly string[] StandardStaffRoles =
    [
        "Doctor", "Nurse", "Receptionist", "Pharmacist", "Lab Technician", "Accountant"
    ];

    public async Task<IReadOnlyList<StaffRoleOptionDto>> GetAssignableRolesAsync(
        string actorRole,
        CancellationToken cancellationToken)
    {
        var roleNames = actorRole == "Super Admin"
            ? StandardStaffRoles.Concat(["Hospital Admin", "Super Admin"]).ToArray()
            : StandardStaffRoles;

        return await dbContext.Roles
            .AsNoTracking()
            .Where(role => roleNames.Contains(role.Name))
            .OrderBy(role => role.Name)
            .Select(role => new StaffRoleOptionDto(role.Name, role.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<StaffAccountDto> CreateAsync(
        CreateStaffAccountRequest request,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var roleName = request.Role.Trim();
        if (Encoding.UTF8.GetByteCount(request.Password) > 72)
            throw new RequestValidationException("Password must be no more than 72 UTF-8 bytes.");
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !new PhoneAttribute().IsValid(request.PhoneNumber))
            throw new RequestValidationException("Phone number is invalid.");
        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
            throw new ConflictException("An account with this email already exists.");

        var allowedRoleNames = actorRole == "Super Admin"
            ? StandardStaffRoles.Concat(["Hospital Admin", "Super Admin"]).ToArray()
            : StandardStaffRoles;
        if (!allowedRoleNames.Contains(roleName, StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenException("You cannot create an account with the selected role.");

        var role = await dbContext.Roles
            .SingleOrDefaultAsync(candidate => candidate.Name.ToLower() == roleName.ToLower(), cancellationToken);
        if (role is null)
            throw new RequestValidationException("The selected role is not available.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            RoleId = role.Id,
            Role = role,
            IsActive = true
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        // A login alone is not enough: Doctor/Nurse accounts also need a profile row,
        // otherwise they never appear in dropdowns or the Doctors table.
        await EnsureProfileAsync(dbContext, user, role.Name, cancellationToken);

        return new StaffAccountDto(user.Id, user.Email, user.FirstName, user.LastName, role.Name, user.IsActive);
    }

    /// <summary>Creates the Doctor / Nurse profile for a user if it does not exist yet. Idempotent.</summary>
    public static async Task EnsureProfileAsync(
        AppDbContext dbContext,
        User user,
        string roleName,
        CancellationToken cancellationToken)
    {
        if (roleName.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
        {
            if (await dbContext.Doctors.AnyAsync(item => item.UserId == user.Id, cancellationToken))
                return;

            var department = await GetOrCreateDefaultDepartmentAsync(dbContext, cancellationToken);
            var doctor = new Doctor
            {
                UserId = user.Id,
                EmployeeId = await NextEmployeeIdAsync(dbContext, "DOC", isDoctor: true, cancellationToken),
                Specialization = "General Medicine",
                Qualification = "MBBS",
                YearsOfExperience = 0,
                ConsultationFee = 0m,
                JoiningDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = user.IsActive ? StaffStatus.Active : StaffStatus.Inactive,
            };
            dbContext.Doctors.Add(doctor);
            dbContext.DoctorDepartments.Add(new DoctorDepartment
            {
                Doctor = doctor,
                DepartmentId = department.Id,
                IsPrimary = true,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (roleName.Equals("Nurse", StringComparison.OrdinalIgnoreCase))
        {
            if (await dbContext.Nurses.AnyAsync(item => item.UserId == user.Id, cancellationToken))
                return;

            var department = await GetOrCreateDefaultDepartmentAsync(dbContext, cancellationToken);
            dbContext.Nurses.Add(new Nurse
            {
                UserId = user.Id,
                EmployeeId = await NextEmployeeIdAsync(dbContext, "NUR", isDoctor: false, cancellationToken),
                Qualification = "BSc Nursing",
                Shift = NurseShift.Morning,
                JoiningDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = user.IsActive ? StaffStatus.Active : StaffStatus.Inactive,
                DepartmentId = department.Id,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Startup backfill: gives every existing Doctor/Nurse login a profile row.</summary>
    public static async Task BackfillProfilesAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var doctorUsers = await dbContext.Users
            .Include(user => user.Role)
            .Where(user => user.Role.Name == "Doctor" && !dbContext.Doctors.Any(d => d.UserId == user.Id))
            .ToListAsync(cancellationToken);
        foreach (var user in doctorUsers)
            await EnsureProfileAsync(dbContext, user, "Doctor", cancellationToken);

        var nurseUsers = await dbContext.Users
            .Include(user => user.Role)
            .Where(user => user.Role.Name == "Nurse" && !dbContext.Nurses.Any(n => n.UserId == user.Id))
            .ToListAsync(cancellationToken);
        foreach (var user in nurseUsers)
            await EnsureProfileAsync(dbContext, user, "Nurse", cancellationToken);
    }

    private static async Task<Department> GetOrCreateDefaultDepartmentAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var department = await dbContext.Departments
            .SingleOrDefaultAsync(item => item.Code == "GEN", cancellationToken);
        if (department is not null)
            return department;

        department = new Department
        {
            Name = "General Medicine",
            Code = "GEN",
            Description = "Primary outpatient consultation department",
            IsActive = true,
        };
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(cancellationToken);
        return department;
    }

    private static async Task<string> NextEmployeeIdAsync(
        AppDbContext dbContext,
        string prefix,
        bool isDoctor,
        CancellationToken cancellationToken)
    {
        var existing = isDoctor
            ? await dbContext.Doctors.Select(item => item.EmployeeId).ToListAsync(cancellationToken)
            : await dbContext.Nurses.Select(item => item.EmployeeId).ToListAsync(cancellationToken);

        var max = 1000;
        foreach (var id in existing)
        {
            if (id.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(id[(prefix.Length + 1)..], out var number) && number > max)
                max = number;
        }

        return $"{prefix}-{max + 1}";
    }
}
