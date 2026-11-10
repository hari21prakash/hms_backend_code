using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class AppointmentService(AppDbContext dbContext, INotificationPublisher? notifications = null) : IAppointmentService
{
    public async Task<AppointmentPageDto> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Doctor)
                .ThenInclude(doctor => doctor.User)
            .Include(appointment => appointment.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(appointment =>
                appointment.AppointmentNumber.Contains(term) ||
                appointment.Patient.FirstName.Contains(term) ||
                appointment.Patient.LastName.Contains(term) ||
                appointment.Doctor.User.FirstName.Contains(term) ||
                appointment.Doctor.User.LastName.Contains(term) ||
                appointment.Department.Name.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(appointment => new AppointmentListItemDto(
                appointment.Id,
                appointment.AppointmentNumber,
                $"{appointment.Patient.FirstName} {appointment.Patient.LastName}",
                $"{appointment.Doctor.User.FirstName} {appointment.Doctor.User.LastName}",
                appointment.Department.Name,
                appointment.AppointmentDate,
                appointment.StartTime,
                appointment.EndTime,
                appointment.Type,
                appointment.Status,
                appointment.Reason,
                appointment.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AppointmentPageDto(items, totalCount, page, pageSize);
    }

    public async Task<AppointmentLookupDto> GetLookupAsync(CancellationToken cancellationToken)
    {
        var patients = await dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.Status == PatientStatus.Active)
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .Select(patient => new PatientOptionDto(
                patient.Id,
                $"{patient.FirstName} {patient.LastName}",
                patient.PatientNumber))
            .ToListAsync(cancellationToken);

        var doctors = await dbContext.Doctors
            .AsNoTracking()
            .Include(doctor => doctor.User)
            .Include(doctor => doctor.DoctorDepartments)
                .ThenInclude(doctorDepartment => doctorDepartment.Department)
            .Where(doctor => doctor.Status == StaffStatus.Active)
            .OrderBy(doctor => doctor.User.LastName)
            .ThenBy(doctor => doctor.User.FirstName)
            .Select(doctor => new DoctorOptionDto(
                doctor.Id,
                $"{doctor.User.FirstName} {doctor.User.LastName}",
                doctor.Specialization,
                doctor.EmployeeId,
                doctor.DoctorDepartments
                    .Where(doctorDepartment => doctorDepartment.IsPrimary || !doctor.DoctorDepartments.Any(item => item.IsPrimary))
                    .Select(doctorDepartment => doctorDepartment.Department.Name)
                    .FirstOrDefault() ?? "General Medicine"))
            .ToListAsync(cancellationToken);

        return new AppointmentLookupDto(doctors, patients);
    }

    public async Task<AppointmentListItemDto> CreateAsync(
        CreateAppointmentRequest request,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        if (request.AppointmentDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new RequestValidationException("Appointment date cannot be in the past.");

        if (request.StartTime >= request.EndTime)
            throw new RequestValidationException("End time must be later than the start time.");

        var patient = await dbContext.Patients.SingleOrDefaultAsync(item => item.Id == request.PatientId, cancellationToken);
        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.PatientId);

        var doctor = await dbContext.Doctors
            .Include(item => item.User)
            .Include(item => item.DoctorDepartments)
                .ThenInclude(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == request.DoctorId, cancellationToken);

        if (doctor is null)
            throw new NotFoundException(nameof(Doctor), request.DoctorId);

        var departmentId = doctor.DoctorDepartments
            .Where(item => item.IsPrimary)
            .Select(item => item.DepartmentId)
            .FirstOrDefault();

        if (departmentId == Guid.Empty)
        {
            departmentId = doctor.DoctorDepartments
                .Select(item => item.DepartmentId)
                .FirstOrDefault();
        }

        if (departmentId == Guid.Empty)
        {
            var department = await dbContext.Departments
                .SingleOrDefaultAsync(item => item.Code == "GEN", cancellationToken);

            if (department is null)
            {
                department = new Department { Name = "General Medicine", Code = "GEN", Description = "Default department for appointments." };
                dbContext.Departments.Add(department);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            departmentId = department.Id;
        }

        var isBooked = await dbContext.Appointments.AnyAsync(item =>
            item.DoctorId == request.DoctorId &&
            item.AppointmentDate == request.AppointmentDate &&
            item.Status != AppointmentStatus.Cancelled &&
            item.Status != AppointmentStatus.NoShow &&
            item.StartTime < request.EndTime &&
            request.StartTime < item.EndTime,
            cancellationToken);

        if (isBooked)
            throw new ConflictException("This doctor already has an appointment scheduled for the selected time slot.");

        var sequenceNumber = await dbContext.Database
            .SqlQueryRaw<long>($"SELECT nextval('{DbSequences.AppointmentNumber}') AS \"Value\"")
            .SingleAsync(cancellationToken);

        var appointment = new Appointment
        {
            AppointmentNumber = $"APT-{DateTime.UtcNow.Year}-{sequenceNumber:D6}",
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            DepartmentId = departmentId,
            AppointmentDate = request.AppointmentDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Type = request.Type,
            Status = AppointmentStatus.Scheduled,
            Reason = request.Reason,
            Notes = request.Notes,
            CreatedByUserId = createdByUserId,
        };

        dbContext.Appointments.Add(appointment);

        // Staged in the same unit of work: the notifications commit together with the appointment or not at all.
        if (notifications is not null)
            await notifications.StageAppointmentBookedAsync(appointment, patient, doctor, createdByUserId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.Appointments
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(item => item.User)
            .Include(item => item.Department)
            .Where(item => item.Id == appointment.Id)
            .Select(item => new AppointmentListItemDto(
                item.Id,
                item.AppointmentNumber,
                $"{item.Patient.FirstName} {item.Patient.LastName}",
                $"{item.Doctor.User.FirstName} {item.Doctor.User.LastName}",
                item.Department.Name,
                item.AppointmentDate,
                item.StartTime,
                item.EndTime,
                item.Type,
                item.Status,
                item.Reason,
                item.CreatedAt))
            .SingleAsync(cancellationToken);
    }

    public async Task<AppointmentListItemDto> UpdateStatusAsync(
        Guid appointmentId,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments
            .Include(item => item.Patient)
            .Include(item => item.Doctor).ThenInclude(item => item.User)
            .Include(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
        if (appointment is null)
            throw new NotFoundException(nameof(Appointment), appointmentId);

        // Cancelling has its own endpoint (reason + notifications); Completed is set when the consultation is saved.
        var allowed = request.Status switch
        {
            AppointmentStatus.Confirmed => appointment.Status == AppointmentStatus.Scheduled,
            AppointmentStatus.CheckedIn => appointment.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed,
            AppointmentStatus.InConsultation => appointment.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn,
            AppointmentStatus.NoShow => appointment.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn,
            _ => false,
        };
        if (!allowed)
            throw new RequestValidationException($"An appointment that is {appointment.Status} cannot be changed to {request.Status}.");

        appointment.Status = request.Status;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AppointmentListItemDto(
            appointment.Id,
            appointment.AppointmentNumber,
            $"{appointment.Patient.FirstName} {appointment.Patient.LastName}",
            $"{appointment.Doctor.User.FirstName} {appointment.Doctor.User.LastName}",
            appointment.Department.Name,
            appointment.AppointmentDate,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Type,
            appointment.Status,
            appointment.Reason,
            appointment.CreatedAt);
    }

    public async Task<AppointmentListItemDto> CancelAsync(
        Guid appointmentId,
        CancelAppointmentRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments
            .Include(item => item.Patient)
            .Include(item => item.Doctor).ThenInclude(item => item.User)
            .Include(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == appointmentId, cancellationToken);
        if (appointment is null)
            throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.Status == AppointmentStatus.Cancelled)
            throw new ConflictException("This appointment is already cancelled.");

        if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.InConsultation or AppointmentStatus.NoShow)
            throw new RequestValidationException($"A {appointment.Status} appointment cannot be cancelled.");

        var reason = request?.Reason?.Trim();
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancellationReason = string.IsNullOrEmpty(reason) ? null : reason;

        if (notifications is not null)
            await notifications.StageAppointmentCancelledAsync(appointment, appointment.Patient, appointment.Doctor, actorUserId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AppointmentListItemDto(
            appointment.Id,
            appointment.AppointmentNumber,
            $"{appointment.Patient.FirstName} {appointment.Patient.LastName}",
            $"{appointment.Doctor.User.FirstName} {appointment.Doctor.User.LastName}",
            appointment.Department.Name,
            appointment.AppointmentDate,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Type,
            appointment.Status,
            appointment.Reason,
            appointment.CreatedAt);
    }
}
