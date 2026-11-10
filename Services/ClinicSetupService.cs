using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class ClinicSetupService(AppDbContext dbContext) : IClinicSetupService
{
    // ------------------------------------------------------------------ Departments
    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken)
        => await dbContext.Departments
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new DepartmentDto(
                item.Id, item.Name, item.Code, item.Description, item.IsActive, item.DoctorDepartments.Count))
            .ToListAsync(cancellationToken);

    public async Task<DepartmentDto> CreateDepartmentAsync(SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var (name, code) = NormalizeDepartment(request);
        await EnsureDepartmentUniqueAsync(null, name, code, cancellationToken);

        var department = new Department
        {
            Name = name,
            Code = code,
            Description = Clean(request.Description),
            IsActive = request.IsActive,
        };
        dbContext.Departments.Add(department);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DepartmentDto(department.Id, department.Name, department.Code, department.Description, department.IsActive, 0);
    }

    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, SaveDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await dbContext.Departments
            .Include(item => item.DoctorDepartments)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (department is null)
            throw new NotFoundException(nameof(Department), id);

        var (name, code) = NormalizeDepartment(request);
        await EnsureDepartmentUniqueAsync(id, name, code, cancellationToken);

        department.Name = name;
        department.Code = code;
        department.Description = Clean(request.Description);
        department.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DepartmentDto(department.Id, department.Name, department.Code, department.Description, department.IsActive, department.DoctorDepartments.Count);
    }

    private static (string Name, string Code) NormalizeDepartment(SaveDepartmentRequest request)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (name.Length == 0 || code.Length == 0)
            throw new RequestValidationException("Department name and code are required.");
        return (name, code);
    }

    private async Task EnsureDepartmentUniqueAsync(Guid? selfId, string name, string code, CancellationToken cancellationToken)
    {
        if (await dbContext.Departments.AnyAsync(item => item.Id != selfId && item.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException("A department with this name already exists.");
        if (await dbContext.Departments.AnyAsync(item => item.Id != selfId && item.Code == code, cancellationToken))
            throw new ConflictException("A department with this code already exists.");
    }

    // ------------------------------------------------------------------ Doctors
    public async Task<IReadOnlyList<DoctorStaffDto>> GetDoctorsAsync(CancellationToken cancellationToken)
    {
        var doctors = await dbContext.Doctors
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.DoctorDepartments).ThenInclude(item => item.Department)
            .OrderBy(item => item.User.LastName).ThenBy(item => item.User.FirstName)
            .ToListAsync(cancellationToken);
        return doctors.Select(item => ToDto(item)).ToList();
    }

    public async Task<DoctorStaffDto> UpdateDoctorAsync(Guid id, UpdateDoctorRequest request, CancellationToken cancellationToken)
    {
        var doctor = await dbContext.Doctors
            .Include(item => item.User)
            .Include(item => item.DoctorDepartments).ThenInclude(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (doctor is null)
            throw new NotFoundException(nameof(Doctor), id);

        var specialization = request.Specialization.Trim();
        var qualification = request.Qualification.Trim();
        if (specialization.Length == 0 || qualification.Length == 0)
            throw new RequestValidationException("Specialization and qualification are required.");

        Department? newDepartment = null;
        if (request.DepartmentId is Guid departmentId && departmentId != Guid.Empty)
        {
            newDepartment = await dbContext.Departments.SingleOrDefaultAsync(item => item.Id == departmentId, cancellationToken);
            if (newDepartment is null)
                throw new NotFoundException(nameof(Department), departmentId);
        }

        doctor.Specialization = specialization;
        doctor.Qualification = qualification;
        doctor.YearsOfExperience = request.YearsOfExperience;
        doctor.ConsultationFee = request.ConsultationFee;
        doctor.Status = request.Status;
        // An Inactive staff member can no longer sign in.
        doctor.User.IsActive = request.Status != StaffStatus.Inactive;

        // Only one primary department per doctor is allowed (unique filtered index), so clear first, then set.
        if (newDepartment is not null)
        {
            foreach (var link in doctor.DoctorDepartments.Where(link => link.IsPrimary && link.DepartmentId != newDepartment.Id))
                link.IsPrimary = false;
            await dbContext.SaveChangesAsync(cancellationToken);

            var existing = doctor.DoctorDepartments.FirstOrDefault(link => link.DepartmentId == newDepartment.Id);
            if (existing is not null)
                existing.IsPrimary = true;
            else
                dbContext.DoctorDepartments.Add(new DoctorDepartment { DoctorId = doctor.Id, DepartmentId = newDepartment.Id, IsPrimary = true });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(doctor).Collection(item => item.DoctorDepartments).Query()
            .Include(item => item.Department).LoadAsync(cancellationToken);
        return ToDto(doctor);
    }

    private static DoctorStaffDto ToDto(Doctor doctor)
    {
        var primary = doctor.DoctorDepartments.FirstOrDefault(link => link.IsPrimary) ?? doctor.DoctorDepartments.FirstOrDefault();
        return new DoctorStaffDto(
            doctor.Id,
            doctor.UserId,
            $"{doctor.User.FirstName} {doctor.User.LastName}",
            doctor.User.Email,
            doctor.User.PhoneNumber,
            doctor.EmployeeId,
            doctor.Specialization,
            doctor.Qualification,
            doctor.YearsOfExperience,
            doctor.ConsultationFee,
            doctor.Status,
            primary?.DepartmentId,
            primary?.Department.Name);
    }

    // ------------------------------------------------------------------ Nurses
    public async Task<IReadOnlyList<NurseStaffDto>> GetNursesAsync(CancellationToken cancellationToken)
    {
        var nurses = await dbContext.Nurses
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Department)
            .Include(item => item.Ward)
            .OrderBy(item => item.User.LastName).ThenBy(item => item.User.FirstName)
            .ToListAsync(cancellationToken);
        return nurses.Select(item => ToDto(item)).ToList();
    }

    public async Task<NurseStaffDto> UpdateNurseAsync(Guid id, UpdateNurseRequest request, CancellationToken cancellationToken)
    {
        var nurse = await dbContext.Nurses
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (nurse is null)
            throw new NotFoundException(nameof(Nurse), id);

        var qualification = request.Qualification.Trim();
        if (qualification.Length == 0)
            throw new RequestValidationException("Qualification is required.");

        Guid? departmentId = request.DepartmentId == Guid.Empty ? null : request.DepartmentId;
        Guid? wardId = request.WardId == Guid.Empty ? null : request.WardId;
        if (departmentId is not null && !await dbContext.Departments.AnyAsync(item => item.Id == departmentId, cancellationToken))
            throw new NotFoundException(nameof(Department), departmentId);
        if (wardId is not null && !await dbContext.Wards.AnyAsync(item => item.Id == wardId, cancellationToken))
            throw new NotFoundException(nameof(Ward), wardId);

        nurse.Qualification = qualification;
        nurse.Shift = request.Shift;
        nurse.Status = request.Status;
        nurse.DepartmentId = departmentId;
        nurse.WardId = wardId;
        nurse.User.IsActive = request.Status != StaffStatus.Inactive;
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(nurse).Reference(item => item.Department).LoadAsync(cancellationToken);
        await dbContext.Entry(nurse).Reference(item => item.Ward).LoadAsync(cancellationToken);
        return ToDto(nurse);
    }

    private static NurseStaffDto ToDto(Nurse nurse) => new(
        nurse.Id,
        nurse.UserId,
        $"{nurse.User.FirstName} {nurse.User.LastName}",
        nurse.User.Email,
        nurse.User.PhoneNumber,
        nurse.EmployeeId,
        nurse.Qualification,
        nurse.Shift,
        nurse.Status,
        nurse.DepartmentId,
        nurse.Department?.Name,
        nurse.WardId,
        nurse.Ward?.Name);

    // ------------------------------------------------------------------ Lab tests
    public async Task<IReadOnlyList<LabTestDto>> GetLabTestsAsync(CancellationToken cancellationToken)
        => await dbContext.LabTests
            .AsNoTracking()
            .OrderBy(item => item.Category).ThenBy(item => item.Name)
            .Select(item => new LabTestDto(
                item.Id, item.Code, item.Name, item.Category, item.Description, item.Price, item.NormalRange, item.Unit, item.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<LabTestDto> CreateLabTestAsync(SaveLabTestRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.LabTests.AnyAsync(item => item.Code == code, cancellationToken))
            throw new ConflictException("A lab test with this code already exists.");

        var test = new LabTest();
        Apply(test, request, code);
        dbContext.LabTests.Add(test);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(test);
    }

    public async Task<LabTestDto> UpdateLabTestAsync(Guid id, SaveLabTestRequest request, CancellationToken cancellationToken)
    {
        var test = await dbContext.LabTests.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (test is null)
            throw new NotFoundException(nameof(LabTest), id);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.LabTests.AnyAsync(item => item.Id != id && item.Code == code, cancellationToken))
            throw new ConflictException("A lab test with this code already exists.");

        Apply(test, request, code);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(test);
    }

    private static void Apply(LabTest test, SaveLabTestRequest request, string code)
    {
        var name = request.Name.Trim();
        var category = request.Category.Trim();
        if (code.Length == 0 || name.Length == 0 || category.Length == 0)
            throw new RequestValidationException("Code, name and category are required.");

        test.Code = code;
        test.Name = name;
        test.Category = category;
        test.Description = Clean(request.Description);
        test.Price = request.Price;
        test.NormalRange = Clean(request.NormalRange);
        test.Unit = Clean(request.Unit);
        test.IsActive = request.IsActive;
    }

    private static LabTestDto ToDto(LabTest item) => new(
        item.Id, item.Code, item.Name, item.Category, item.Description, item.Price, item.NormalRange, item.Unit, item.IsActive);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
