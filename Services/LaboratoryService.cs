using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class LaboratoryService(AppDbContext dbContext, INotificationPublisher? notifications = null) : ILaboratoryService
{
    public async Task<LaboratoryPageDto> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.LabOrders
            .AsNoTracking()
            .Include(order => order.Patient)
            .Include(order => order.Doctor)
                .ThenInclude(doctor => doctor.User)
            .Include(order => order.LabTest)
            .Include(order => order.Result)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(order =>
                order.OrderNumber.Contains(term) ||
                order.Patient.FirstName.Contains(term) ||
                order.Patient.LastName.Contains(term) ||
                order.Doctor.User.FirstName.Contains(term) ||
                order.Doctor.User.LastName.Contains(term) ||
                order.LabTest.Name.Contains(term) ||
                order.ClinicalNotes != null && order.ClinicalNotes.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(order => order.OrderedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new LabOrderListItemDto(
                order.Id,
                order.OrderNumber,
                $"{order.Patient.FirstName} {order.Patient.LastName}",
                $"{order.Doctor.User.FirstName} {order.Doctor.User.LastName}",
                order.LabTest.Name,
                order.Priority.ToString(),
                order.Status.ToString(),
                order.OrderedAt))
            .ToListAsync(cancellationToken);

        return new LaboratoryPageDto(items, totalCount, page, pageSize);
    }

    public async Task<LaboratoryLookupDto> GetLookupAsync(CancellationToken cancellationToken)
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

        var tests = await dbContext.LabTests
            .AsNoTracking()
            .Where(test => test.IsActive)
            .OrderBy(test => test.Name)
            .Select(test => new LabTestOptionDto(
                test.Id,
                test.Code,
                test.Name,
                test.Category,
                test.Price,
                test.NormalRange,
                test.Unit))
            .ToListAsync(cancellationToken);

        return new LaboratoryLookupDto(patients, doctors, tests);
    }

    public async Task<LabOrderDetailDto> CreateAsync(
        CreateLabOrderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PatientId == Guid.Empty)
            throw new RequestValidationException("A patient must be selected.");

        if (request.DoctorId == Guid.Empty)
            throw new RequestValidationException("A doctor must be selected.");

        if (request.LabTestId == Guid.Empty)
            throw new RequestValidationException("A lab test must be selected.");

        var patient = await dbContext.Patients.SingleOrDefaultAsync(item => item.Id == request.PatientId, cancellationToken);
        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.PatientId);

        var doctor = await dbContext.Doctors
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == request.DoctorId, cancellationToken);
        if (doctor is null)
            throw new NotFoundException(nameof(Doctor), request.DoctorId);

        var labTest = await dbContext.LabTests
            .SingleOrDefaultAsync(item => item.Id == request.LabTestId, cancellationToken);
        if (labTest is null)
            throw new NotFoundException(nameof(LabTest), request.LabTestId);

        if (request.ConsultationId is Guid consultationId && consultationId != Guid.Empty)
        {
            var consultation = await dbContext.Consultations
                .SingleOrDefaultAsync(item => item.Id == consultationId, cancellationToken);
            if (consultation is null)
                throw new NotFoundException(nameof(Consultation), consultationId);
        }

        var sequenceNumber = await dbContext.Database
            .SqlQueryRaw<long>($"SELECT nextval('{DbSequences.LabOrderNumber}') AS \"Value\"")
            .SingleAsync(cancellationToken);

        var order = new Models.LabOrder
        {
            OrderNumber = $"LAB-{DateTime.UtcNow.Year}-{sequenceNumber:D6}",
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            ConsultationId = request.ConsultationId,
            LabTestId = request.LabTestId,
            OrderedAt = DateTime.UtcNow,
            Priority = Enum.TryParse<LabPriority>(request.Priority, true, out var parsedPriority)
                ? parsedPriority
                : LabPriority.Routine,
            Status = LabOrderStatus.Ordered,
            ClinicalNotes = request.ClinicalNotes,
        };

        dbContext.LabOrders.Add(order);

        if (notifications is not null)
            await notifications.StageLabOrderCreatedAsync(order, patient, labTest, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var savedOrder = await dbContext.LabOrders
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(item => item.User)
            .Include(item => item.LabTest)
            .Include(item => item.Result)
            .SingleAsync(item => item.Id == order.Id, cancellationToken);

        return new LabOrderDetailDto(
            savedOrder.Id,
            savedOrder.OrderNumber,
            savedOrder.PatientId,
            $"{savedOrder.Patient.FirstName} {savedOrder.Patient.LastName}",
            savedOrder.DoctorId,
            $"{savedOrder.Doctor.User.FirstName} {savedOrder.Doctor.User.LastName}",
            savedOrder.LabTestId,
            savedOrder.LabTest.Name,
            savedOrder.Priority.ToString(),
            savedOrder.Status.ToString(),
            savedOrder.OrderedAt,
            savedOrder.ClinicalNotes,
            savedOrder.Result?.ResultValue,
            savedOrder.Result?.Unit,
            savedOrder.Result?.NormalRange,
            savedOrder.Result?.IsAbnormal ?? false,
            savedOrder.Result?.Remarks);
    }

    public async Task<LabOrderDetailDto> RecordResultAsync(
        Guid labOrderId,
        RecordLabResultRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ResultValue))
            throw new RequestValidationException("A result value is required.");

        var order = await dbContext.LabOrders
            .Include(item => item.Patient)
            .Include(item => item.Doctor).ThenInclude(item => item.User)
            .Include(item => item.LabTest)
            .Include(item => item.Result)
            .SingleOrDefaultAsync(item => item.Id == labOrderId, cancellationToken);
        if (order is null)
            throw new NotFoundException(nameof(Models.LabOrder), labOrderId);

        if (order.Status == LabOrderStatus.Cancelled)
            throw new RequestValidationException("This lab order was cancelled and cannot receive a result.");

        if (order.Result is not null || order.Status == LabOrderStatus.Completed)
            throw new ConflictException("A result has already been recorded for this lab order.");

        var now = DateTime.UtcNow;
        order.Result = new Models.LabResult
        {
            LabOrderId = order.Id,
            ResultValue = request.ResultValue.Trim(),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? order.LabTest.Unit : request.Unit.Trim(),
            NormalRange = order.LabTest.NormalRange,
            IsAbnormal = request.IsAbnormal,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
            ResultDate = now,
            TechnicianUserId = actorUserId,
        };
        order.Status = LabOrderStatus.Completed;
        order.SampleCollectedAt ??= now;
        order.SampleCollectedByUserId ??= actorUserId;

        if (notifications is not null)
            await notifications.StageLabResultAvailableAsync(order, order.Patient, order.Doctor, order.LabTest, actorUserId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LabOrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.PatientId,
            $"{order.Patient.FirstName} {order.Patient.LastName}",
            order.DoctorId,
            $"{order.Doctor.User.FirstName} {order.Doctor.User.LastName}",
            order.LabTestId,
            order.LabTest.Name,
            order.Priority.ToString(),
            order.Status.ToString(),
            order.OrderedAt,
            order.ClinicalNotes,
            order.Result.ResultValue,
            order.Result.Unit,
            order.Result.NormalRange,
            order.Result.IsAbnormal,
            order.Result.Remarks);
    }
}
