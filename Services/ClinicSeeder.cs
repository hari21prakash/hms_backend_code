using HospitalManagement.Api.Data;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>
/// Optional demo data (department, doctor, patient). Only runs when SeedDemoData=true.
/// Every record is looked up by a natural key, so running it repeatedly never creates duplicates.
/// </summary>
public static class ClinicSeeder
{
    private const string DemoDoctorEmail = "doctor@hospital.local";
    private const string DemoPatientEmail = "alex.johnson@example.com";

    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var doctorRole = await dbContext.Roles.SingleOrDefaultAsync(role => role.Name == "Doctor", cancellationToken);
        if (doctorRole is null)
            return;

        var department = await dbContext.Departments.SingleOrDefaultAsync(item => item.Code == "GEN", cancellationToken);
        if (department is null)
        {
            department = new Department
            {
                Name = "General Medicine",
                Code = "GEN",
                Description = "Primary outpatient consultation department",
                IsActive = true,
            };

            dbContext.Departments.Add(department);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var doctorUser = await dbContext.Users.SingleOrDefaultAsync(user => user.Email == DemoDoctorEmail, cancellationToken);
        if (doctorUser is null)
        {
            doctorUser = new User
            {
                Email = DemoDoctorEmail,
                // Random, unrecoverable password: the demo doctor exists for dropdowns and cannot be logged into.
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                FirstName = "Dr. Emma",
                LastName = "Rivera",
                PhoneNumber = "+1-555-0101",
                RoleId = doctorRole.Id,
                IsActive = true,
            };

            dbContext.Users.Add(doctorUser);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var doctor = await dbContext.Doctors.SingleOrDefaultAsync(item => item.UserId == doctorUser.Id, cancellationToken);
        if (doctor is null)
        {
            doctor = new Doctor
            {
                UserId = doctorUser.Id,
                EmployeeId = "DOC-1001",
                Specialization = "General Medicine",
                Qualification = "MBBS, MD",
                YearsOfExperience = 12,
                ConsultationFee = 250m,
                JoiningDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
                Status = StaffStatus.Active,
            };

            dbContext.Doctors.Add(doctor);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.DoctorDepartments.AnyAsync(
                link => link.DoctorId == doctor.Id && link.DepartmentId == department.Id, cancellationToken))
        {
            dbContext.DoctorDepartments.Add(new DoctorDepartment
            {
                DoctorId = doctor.Id,
                DepartmentId = department.Id,
                IsPrimary = !await dbContext.DoctorDepartments.AnyAsync(link => link.DoctorId == doctor.Id, cancellationToken),
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.Patients.AnyAsync(patient => patient.Email == DemoPatientEmail, cancellationToken))
        {
            dbContext.Patients.Add(new Patient
            {
                PatientNumber = await PatientNumberGenerator.NextAsync(dbContext, cancellationToken),
                FirstName = "Alex",
                LastName = "Johnson",
                DateOfBirth = new DateOnly(1991, 4, 15),
                Gender = Gender.Male,
                Phone = "+1-555-0102",
                Email = DemoPatientEmail,
                Address = "14 Riverlane",
                City = "Boston",
                State = "MA",
                EmergencyContactName = "Morgan Johnson",
                EmergencyContactPhone = "+1-555-0103",
                RegisteredAt = DateTime.UtcNow,
                Status = PatientStatus.Active,
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
