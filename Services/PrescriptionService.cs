using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class PrescriptionService(AppDbContext dbContext, INotificationPublisher? notifications = null) : IPrescriptionService
{
    public async Task<PrescriptionPageDto> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Prescriptions
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(doctor => doctor.User)
            .Include(item => item.Items)
                .ThenInclude(item => item.Medicine)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item =>
                item.PrescriptionNumber.Contains(term) ||
                item.Patient.FirstName.Contains(term) ||
                item.Patient.LastName.Contains(term) ||
                item.Doctor.User.FirstName.Contains(term) ||
                item.Doctor.User.LastName.Contains(term) ||
                item.Instructions != null && item.Instructions.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(item => item.PrescribedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PrescriptionListItemDto(
                item.Id,
                item.PrescriptionNumber,
                $"{item.Patient.FirstName} {item.Patient.LastName}",
                $"{item.Doctor.User.FirstName} {item.Doctor.User.LastName}",
                item.PrescribedAt,
                item.Status.ToString(),
                item.Items.Count))
            .ToListAsync(cancellationToken);

        return new PrescriptionPageDto(items, totalCount, page, pageSize);
    }

    public async Task<PrescriptionLookupDto> GetLookupAsync(CancellationToken cancellationToken)
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

        // QuantityOnHand here means USABLE stock: expired batches are excluded. The UI disables medicines at 0.
        var today = PharmacyRules.Today();
        var medicines = await dbContext.Medicines
            .AsNoTracking()
            .Where(item => item.Status == MedicineStatus.Active)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Strength)
            .Select(item => new MedicineOptionDto(
                item.Id,
                item.Name,
                item.Strength,
                item.Form.ToString(),
                item.Category,
                item.Batches.Where(batch => batch.ExpiryDate >= today).Sum(batch => batch.QuantityOnHand)))
            .ToListAsync(cancellationToken);

        return new PrescriptionLookupDto(patients, doctors, medicines);
    }

    public async Task<PrescriptionDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var prescription = await dbContext.Prescriptions
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(doctor => doctor.User)
            .Include(item => item.Items)
                .ThenInclude(item => item.Medicine)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (prescription is null)
            throw new NotFoundException(nameof(Prescription), id);

        return MapDetail(prescription);
    }

    public async Task<PrescriptionDetailDto> CreateAsync(
        CreatePrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PatientId == Guid.Empty)
            throw new RequestValidationException("A patient must be selected.");

        if (request.DoctorId == Guid.Empty)
            throw new RequestValidationException("A doctor must be selected.");

        if (request.Items is null || request.Items.Count == 0)
            throw new RequestValidationException("At least one medication item is required.");

        var patient = await dbContext.Patients.SingleOrDefaultAsync(item => item.Id == request.PatientId, cancellationToken);
        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.PatientId);

        var doctor = await dbContext.Doctors
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == request.DoctorId, cancellationToken);
        if (doctor is null)
            throw new NotFoundException(nameof(Doctor), request.DoctorId);

        if (request.ConsultationId is Guid consultationId && consultationId != Guid.Empty)
        {
            var consultation = await dbContext.Consultations
                .SingleOrDefaultAsync(item => item.Id == consultationId, cancellationToken);
            if (consultation is null)
                throw new NotFoundException(nameof(Consultation), consultationId);
        }

        var prescription = new Prescription
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            ConsultationId = request.ConsultationId,
            PrescribedAt = DateTime.UtcNow,
            Status = PrescriptionStatus.Active,
            Instructions = request.Instructions,
            PrescriptionNumber = $"RX-{DateTime.UtcNow:yyyy}-{await GetNextSequenceAsync(cancellationToken):D6}",
        };

        foreach (var item in request.Items)
        {
            if (item.MedicineId == Guid.Empty)
                throw new RequestValidationException("Each prescription item must include a medicine.");

            if (item.DurationDays <= 0)
                throw new RequestValidationException("Medication duration must be greater than zero.");

            if (item.Quantity <= 0)
                throw new RequestValidationException("Medication quantity must be greater than zero.");

            var medicine = await dbContext.Medicines
                .Include(m => m.Batches)
                .SingleOrDefaultAsync(m => m.Id == item.MedicineId, cancellationToken);

            if (medicine is null)
                throw new NotFoundException(nameof(Medicine), item.MedicineId);

            if (medicine.Status != MedicineStatus.Active)
                throw new RequestValidationException($"Medicine '{medicine.Name}' is not active and cannot be prescribed.");

            var today = PharmacyRules.Today();
            var stock = medicine.Batches
                .Where(batch => !PharmacyRules.IsExpired(batch.ExpiryDate, today))
                .Sum(batch => batch.QuantityOnHand);
            if (stock < item.Quantity)
                throw new RequestValidationException($"Not enough usable (non-expired) stock for '{medicine.Name}': {stock} available, {item.Quantity} requested.");

            prescription.Items.Add(new PrescriptionItem
            {
                MedicineId = item.MedicineId,
                Dosage = item.Dosage,
                Frequency = item.Frequency,
                Route = string.IsNullOrWhiteSpace(item.Route) ? "Oral" : item.Route,
                DurationDays = item.DurationDays,
                Quantity = item.Quantity,
                QuantityDispensed = 0,
                Instructions = item.Instructions,
            });
        }

        dbContext.Prescriptions.Add(prescription);

        // Same unit of work as the prescription itself: both commit, or neither does.
        if (notifications is not null)
            await notifications.StagePrescriptionCreatedAsync(prescription, patient, doctor, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(prescription.Id, cancellationToken);
    }

    private async Task<long> GetNextSequenceAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Database
            .SqlQueryRaw<long>($"SELECT nextval('{DbSequences.PrescriptionNumber}') AS \"Value\"")
            .SingleAsync(cancellationToken);
    }

    private static PrescriptionDetailDto MapDetail(Prescription prescription)
    {
        return new PrescriptionDetailDto(
            prescription.Id,
            prescription.PrescriptionNumber,
            prescription.PatientId,
            $"{prescription.Patient.FirstName} {prescription.Patient.LastName}",
            prescription.DoctorId,
            $"{prescription.Doctor.User.FirstName} {prescription.Doctor.User.LastName}",
            prescription.ConsultationId,
            prescription.PrescribedAt,
            prescription.Status.ToString(),
            prescription.Instructions,
            prescription.Items
                .OrderBy(item => item.CreatedAt)
                .Select(item => new PrescriptionItemDto(
                    item.Id,
                    item.MedicineId,
                    item.Medicine.Name,
                    item.Dosage,
                    item.Frequency,
                    item.Route,
                    item.DurationDays,
                    item.Quantity,
                    item.QuantityDispensed,
                    item.Instructions))
                .ToList());
    }
}
