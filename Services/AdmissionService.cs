using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class AdmissionService(AppDbContext dbContext) : IAdmissionService
{
    public async Task<AdmissionPageDto> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Admissions
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(item => item.User)
            .Include(item => item.Department)
            .Include(item => item.BedAssignments)
                .ThenInclude(item => item.Bed)
                    .ThenInclude(item => item.Room)
                        .ThenInclude(item => item.Ward)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                item.AdmissionNumber.Contains(term) ||
                item.Patient.FirstName.Contains(term) ||
                item.Patient.LastName.Contains(term) ||
                item.Doctor.User.FirstName.Contains(term) ||
                item.Doctor.User.LastName.Contains(term) ||
                item.Department.Name.Contains(term) ||
                item.Reason.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(item => item.AdmittedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AdmissionListItemDto(
                item.Id,
                item.AdmissionNumber,
                $"{item.Patient.FirstName} {item.Patient.LastName}",
                $"{item.Doctor.User.FirstName} {item.Doctor.User.LastName}",
                item.Department.Name,
                item.BedAssignments
                    .Where(assignment => assignment.ReleasedAt == null)
                    .Select(assignment => assignment.Bed.Room.Ward.Name)
                    .FirstOrDefault() ?? "Unassigned",
                item.BedAssignments
                    .Where(assignment => assignment.ReleasedAt == null)
                    .Select(assignment => assignment.Bed.Room.RoomNumber)
                    .FirstOrDefault() ?? "—",
                item.BedAssignments
                    .Where(assignment => assignment.ReleasedAt == null)
                    .Select(assignment => assignment.Bed.BedNumber)
                    .FirstOrDefault() ?? "—",
                item.Status,
                item.AdmittedAt,
                item.Reason))
            .ToListAsync(cancellationToken);

        return new AdmissionPageDto(items, totalCount, page, pageSize);
    }

    public async Task<AdmissionLookupDto> GetLookupAsync(CancellationToken cancellationToken)
    {
        var patients = await dbContext.Patients
            .AsNoTracking()
            .Where(item => item.Status == PatientStatus.Active)
            .OrderBy(item => item.LastName)
            .ThenBy(item => item.FirstName)
            .Select(item => new PatientOptionDto(
                item.Id,
                $"{item.FirstName} {item.LastName}",
                item.PatientNumber))
            .ToListAsync(cancellationToken);

        var doctors = await dbContext.Doctors
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.Status == StaffStatus.Active)
            .OrderBy(item => item.User.LastName)
            .ThenBy(item => item.User.FirstName)
            .Select(item => new DoctorOptionDto(
                item.Id,
                $"{item.User.FirstName} {item.User.LastName}",
                item.Specialization,
                item.EmployeeId,
                item.DoctorDepartments
                    .Select(department => department.Department.Name)
                    .FirstOrDefault() ?? "General Medicine"))
            .ToListAsync(cancellationToken);

        var departments = await dbContext.Departments
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new DepartmentOptionDto(item.Id, item.Name, item.Code))
            .ToListAsync(cancellationToken);

        var wards = await dbContext.Wards
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new WardOptionDto(
                item.Id,
                item.Name,
                item.Code,
                item.Type,
                item.Description))
            .ToListAsync(cancellationToken);

        var beds = await dbContext.Beds
            .AsNoTracking()
            .Include(item => item.Room)
                .ThenInclude(item => item.Ward)
            .Where(item => item.Status == BedStatus.Available)
            .OrderBy(item => item.Room.Ward.Name)
            .ThenBy(item => item.Room.RoomNumber)
            .ThenBy(item => item.BedNumber)
            .Select(item => new BedOptionDto(
                item.Id,
                item.Room.Ward.Name,
                item.Room.RoomNumber,
                item.BedNumber,
                item.Room.DailyRate,
                item.Status))
            .ToListAsync(cancellationToken);

        return new AdmissionLookupDto(patients, doctors, departments, wards, beds);
    }

    public async Task<AdmissionDetailDto> CreateAsync(
        CreateAdmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PatientId == Guid.Empty)
            throw new RequestValidationException("A patient must be selected.");

        if (request.DoctorId == Guid.Empty)
            throw new RequestValidationException("A doctor must be selected.");

        if (request.DepartmentId == Guid.Empty)
            throw new RequestValidationException("A department must be selected.");

        if (request.BedId == Guid.Empty)
            throw new RequestValidationException("A bed must be selected.");

        var patient = await dbContext.Patients.SingleOrDefaultAsync(item => item.Id == request.PatientId, cancellationToken);
        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.PatientId);

        var doctor = await dbContext.Doctors
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == request.DoctorId, cancellationToken);
        if (doctor is null)
            throw new NotFoundException(nameof(Doctor), request.DoctorId);

        var department = await dbContext.Departments.SingleOrDefaultAsync(item => item.Id == request.DepartmentId, cancellationToken);
        if (department is null)
            throw new NotFoundException(nameof(Department), request.DepartmentId);

        var bed = await dbContext.Beds
            .Include(item => item.Room)
                .ThenInclude(item => item.Ward)
            .SingleOrDefaultAsync(item => item.Id == request.BedId, cancellationToken);
        if (bed is null)
            throw new NotFoundException(nameof(Bed), request.BedId);

        if (bed.Status != BedStatus.Available)
            throw new ConflictException("The selected bed is not available.");

        var admittedAt = request.AdmittedAt ?? DateTime.UtcNow;
        var sequenceNumber = await dbContext.Database
            .SqlQueryRaw<long>($"SELECT nextval('{DbSequences.AdmissionNumber}') AS \"Value\"")
            .SingleAsync(cancellationToken);

        var admission = new Admission
        {
            AdmissionNumber = $"ADM-{DateTime.UtcNow.Year}-{sequenceNumber:D6}",
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            DepartmentId = request.DepartmentId,
            AdmittedAt = admittedAt,
            Reason = request.Reason,
            Status = AdmissionStatus.Admitted,
        };

        dbContext.Admissions.Add(admission);
        dbContext.BedAssignments.Add(new BedAssignment
        {
            Admission = admission,
            BedId = bed.Id,
            AssignedAt = admittedAt,
            DailyRateSnapshot = bed.Room.DailyRate,
        });
        bed.Status = BedStatus.Occupied;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(admission.Id, cancellationToken);
    }

    public async Task<DischargeResultDto> DischargeAsync(
        Guid admissionId,
        DischargeAdmissionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var summary = request.Summary?.Trim();
        if (string.IsNullOrEmpty(summary))
            throw new RequestValidationException("A discharge summary is required.");

        var admission = await dbContext.Admissions
            .Include(item => item.Patient)
            .Include(item => item.Discharge)
            .Include(item => item.BedAssignments)
                .ThenInclude(item => item.Bed)
            .SingleOrDefaultAsync(item => item.Id == admissionId, cancellationToken);
        if (admission is null)
            throw new NotFoundException(nameof(Admission), admissionId);

        if (admission.Status == AdmissionStatus.Discharged || admission.Discharge is not null)
            throw new ConflictException("This patient has already been discharged.");

        var dischargedAt = request.DischargedAt is null
            ? DateTime.UtcNow
            : request.DischargedAt.Value.Kind switch
            {
                DateTimeKind.Utc => request.DischargedAt.Value,
                DateTimeKind.Local => request.DischargedAt.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(request.DischargedAt.Value, DateTimeKind.Utc),
            };
        if (dischargedAt < admission.AdmittedAt)
            throw new RequestValidationException("Discharge time cannot be earlier than the admission time.");

        // Free the bed(s) and close the bed history.
        foreach (var assignment in admission.BedAssignments.Where(item => item.ReleasedAt == null))
        {
            assignment.ReleasedAt = dischargedAt;
            if (assignment.Bed.Status == BedStatus.Occupied)
                assignment.Bed.Status = BedStatus.Available;
        }

        dbContext.Discharges.Add(new Discharge
        {
            AdmissionId = admission.Id,
            DischargedAt = dischargedAt,
            Type = request.Type,
            Summary = summary,
            ConditionAtDischarge = string.IsNullOrWhiteSpace(request.ConditionAtDischarge) ? null : request.ConditionAtDischarge.Trim(),
            FollowUpInstructions = string.IsNullOrWhiteSpace(request.FollowUpInstructions) ? null : request.FollowUpInstructions.Trim(),
            DischargedByDoctorId = admission.DoctorId,
        });
        admission.Status = AdmissionStatus.Discharged;

        // Room charge = whole days per bed stay (minimum one day) x the daily rate captured when the bed was assigned.
        decimal roomCharges = 0m;
        foreach (var assignment in admission.BedAssignments)
        {
            var end = assignment.ReleasedAt ?? dischargedAt;
            var days = Math.Max(1, (int)Math.Ceiling((end - assignment.AssignedAt).TotalDays));
            roomCharges += days * assignment.DailyRateSnapshot;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var daysStayed = Math.Max(1, (int)Math.Ceiling((dischargedAt - admission.AdmittedAt).TotalDays));
        return new DischargeResultDto(
            admission.Id,
            admission.AdmissionNumber,
            $"{admission.Patient.FirstName} {admission.Patient.LastName}",
            admission.PatientId,
            admission.AdmittedAt,
            dischargedAt,
            daysStayed,
            roomCharges);
    }

    public async Task<AdmissionDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var admission = await dbContext.Admissions
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(item => item.User)
            .Include(item => item.Department)
            .Include(item => item.BedAssignments)
                .ThenInclude(item => item.Bed)
                    .ThenInclude(item => item.Room)
                        .ThenInclude(item => item.Ward)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (admission is null)
            throw new NotFoundException(nameof(Admission), id);

        var activeAssignment = admission.BedAssignments
            .Where(item => item.ReleasedAt == null)
            .OrderByDescending(item => item.AssignedAt)
            .FirstOrDefault();

        return new AdmissionDetailDto(
            admission.Id,
            admission.AdmissionNumber,
            $"{admission.Patient.FirstName} {admission.Patient.LastName}",
            $"{admission.Doctor.User.FirstName} {admission.Doctor.User.LastName}",
            admission.Department.Name,
            activeAssignment?.Bed.Room.Ward.Name ?? "Unassigned",
            activeAssignment?.Bed.Room.RoomNumber ?? "—",
            activeAssignment?.Bed.BedNumber ?? "—",
            admission.Status,
            admission.AdmittedAt,
            admission.Reason,
            activeAssignment?.DailyRateSnapshot ?? 0m);
    }
}
