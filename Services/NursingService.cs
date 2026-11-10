using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class NursingService(AppDbContext dbContext) : INursingService
{
    public async Task<NursingPageDto> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var vitalSignsQuery = dbContext.VitalSigns
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Admission)
            .Include(item => item.RecordedByUser)
            .AsQueryable();

        var notesQuery = dbContext.NursingNotes
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Admission)
            .Include(item => item.NurseUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            vitalSignsQuery = vitalSignsQuery.Where(item =>
                item.Patient.FirstName.Contains(term) ||
                item.Patient.LastName.Contains(term) ||
                item.RecordedByUser.FirstName.Contains(term) ||
                item.RecordedByUser.LastName.Contains(term));

            notesQuery = notesQuery.Where(item =>
                item.Patient.FirstName.Contains(term) ||
                item.Patient.LastName.Contains(term) ||
                item.NurseUser.FirstName.Contains(term) ||
                item.NurseUser.LastName.Contains(term) ||
                item.Note.Contains(term));
        }

        var totalVitalSigns = await vitalSignsQuery.CountAsync(cancellationToken);
        var totalNotes = await notesQuery.CountAsync(cancellationToken);

        var vitalSigns = await vitalSignsQuery
            .OrderByDescending(item => item.RecordedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new VitalSignListItemDto(
                item.Id,
                $"{item.Patient.FirstName} {item.Patient.LastName}",
                item.Admission != null ? item.Admission.AdmissionNumber : null,
                $"{item.RecordedByUser.FirstName} {item.RecordedByUser.LastName}",
                item.RecordedAt,
                item.TemperatureCelsius,
                item.SystolicBp,
                item.DiastolicBp,
                item.HeartRate,
                item.RespiratoryRate,
                item.OxygenSaturation,
                item.WeightKg,
                item.HeightCm))
            .ToListAsync(cancellationToken);

        var notes = await notesQuery
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new NursingNoteListItemDto(
                item.Id,
                $"{item.Patient.FirstName} {item.Patient.LastName}",
                item.Admission != null ? item.Admission.AdmissionNumber : null,
                $"{item.NurseUser.FirstName} {item.NurseUser.LastName}",
                item.CreatedAt,
                item.Note))
            .ToListAsync(cancellationToken);

        return new NursingPageDto(vitalSigns, notes, totalVitalSigns, totalNotes, page, pageSize);
    }

    public async Task<NursingLookupDto> GetLookupAsync(CancellationToken cancellationToken)
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

        var admissions = await dbContext.Admissions
            .AsNoTracking()
            .Include(item => item.Patient)
            .Where(item => item.Status != AdmissionStatus.Discharged)
            .OrderByDescending(item => item.AdmittedAt)
            .Select(item => new AdmissionOptionDto(
                item.Id,
                item.AdmissionNumber,
                $"{item.Patient.FirstName} {item.Patient.LastName}"))
            .ToListAsync(cancellationToken);

        return new NursingLookupDto(patients, admissions);
    }

    public async Task<VitalSignListItemDto> CreateVitalSignAsync(
        CreateVitalSignRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PatientId == Guid.Empty)
            throw new RequestValidationException("A patient must be selected.");

        var patient = await dbContext.Patients.SingleOrDefaultAsync(item => item.Id == request.PatientId, cancellationToken);
        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.PatientId);

        var recorder = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == actorUserId, cancellationToken);
        if (recorder is null)
            throw new UnauthorizedAppException();

        if (request.AdmissionId is Guid admissionId && admissionId != Guid.Empty)
        {
            var admission = await dbContext.Admissions.SingleOrDefaultAsync(item => item.Id == admissionId, cancellationToken);
            if (admission is null)
                throw new NotFoundException(nameof(Admission), admissionId);
        }

        var vitalSign = new VitalSign
        {
            PatientId = request.PatientId,
            AdmissionId = request.AdmissionId,
            RecordedByUserId = actorUserId,
            RecordedAt = DateTime.UtcNow,
            TemperatureCelsius = request.TemperatureCelsius,
            SystolicBp = request.SystolicBp,
            DiastolicBp = request.DiastolicBp,
            HeartRate = request.HeartRate,
            RespiratoryRate = request.RespiratoryRate,
            OxygenSaturation = request.OxygenSaturation,
            WeightKg = request.WeightKg,
            HeightCm = request.HeightCm,
        };

        dbContext.VitalSigns.Add(vitalSign);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.VitalSigns
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Admission)
            .Include(item => item.RecordedByUser)
            .Where(item => item.Id == vitalSign.Id)
            .Select(item => new VitalSignListItemDto(
                item.Id,
                $"{item.Patient.FirstName} {item.Patient.LastName}",
                item.Admission != null ? item.Admission.AdmissionNumber : null,
                $"{item.RecordedByUser.FirstName} {item.RecordedByUser.LastName}",
                item.RecordedAt,
                item.TemperatureCelsius,
                item.SystolicBp,
                item.DiastolicBp,
                item.HeartRate,
                item.RespiratoryRate,
                item.OxygenSaturation,
                item.WeightKg,
                item.HeightCm))
            .SingleAsync(cancellationToken);
    }

    public async Task<NursingNoteListItemDto> CreateNursingNoteAsync(
        CreateNursingNoteRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PatientId == Guid.Empty)
            throw new RequestValidationException("A patient must be selected.");

        if (string.IsNullOrWhiteSpace(request.Note))
            throw new RequestValidationException("A nursing note is required.");

        var patient = await dbContext.Patients.SingleOrDefaultAsync(item => item.Id == request.PatientId, cancellationToken);
        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.PatientId);

        var nurse = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == actorUserId, cancellationToken);
        if (nurse is null)
            throw new UnauthorizedAppException();

        if (request.AdmissionId is Guid admissionId && admissionId != Guid.Empty)
        {
            var admission = await dbContext.Admissions.SingleOrDefaultAsync(item => item.Id == admissionId, cancellationToken);
            if (admission is null)
                throw new NotFoundException(nameof(Admission), admissionId);
        }

        var note = new NursingNote
        {
            PatientId = request.PatientId,
            AdmissionId = request.AdmissionId,
            NurseUserId = actorUserId,
            Note = request.Note.Trim(),
        };

        dbContext.NursingNotes.Add(note);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.NursingNotes
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Admission)
            .Include(item => item.NurseUser)
            .Where(item => item.Id == note.Id)
            .Select(item => new NursingNoteListItemDto(
                item.Id,
                $"{item.Patient.FirstName} {item.Patient.LastName}",
                item.Admission != null ? item.Admission.AdmissionNumber : null,
                $"{item.NurseUser.FirstName} {item.NurseUser.LastName}",
                item.CreatedAt,
                item.Note))
            .SingleAsync(cancellationToken);
    }
}
