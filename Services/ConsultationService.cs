using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class ConsultationService(AppDbContext dbContext) : IConsultationService
{
    public async Task<ConsultationPageDto> GetPageAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Consultations
            .AsNoTracking()
            .Include(consultation => consultation.Appointment)
                .ThenInclude(appointment => appointment.Patient)
            .Include(consultation => consultation.Appointment)
                .ThenInclude(appointment => appointment.Doctor)
                    .ThenInclude(doctor => doctor.User)
            .Include(consultation => consultation.Appointment)
                .ThenInclude(appointment => appointment.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(consultation =>
                consultation.Appointment.AppointmentNumber.Contains(term) ||
                consultation.Appointment.Patient.FirstName.Contains(term) ||
                consultation.Appointment.Patient.LastName.Contains(term) ||
                consultation.Appointment.Doctor.User.FirstName.Contains(term) ||
                consultation.Appointment.Doctor.User.LastName.Contains(term) ||
                consultation.Diagnosis != null && consultation.Diagnosis.Contains(term) ||
                consultation.ChiefComplaint != null && consultation.ChiefComplaint.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(consultation => consultation.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(consultation => new ConsultationListItemDto(
                consultation.Id,
                consultation.Appointment.AppointmentNumber,
                $"{consultation.Appointment.Patient.FirstName} {consultation.Appointment.Patient.LastName}",
                $"{consultation.Appointment.Doctor.User.FirstName} {consultation.Appointment.Doctor.User.LastName}",
                consultation.Appointment.Department.Name,
                consultation.Appointment.AppointmentDate,
                consultation.Appointment.StartTime,
                consultation.Appointment.EndTime,
                consultation.ChiefComplaint,
                consultation.Diagnosis,
                consultation.StartedAt,
                consultation.CompletedAt,
                consultation.FollowUpDate))
            .ToListAsync(cancellationToken);

        return new ConsultationPageDto(items, totalCount, page, pageSize);
    }

    public async Task<ConsultationDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var consultation = await dbContext.Consultations
            .AsNoTracking()
            .Include(item => item.Appointment)
                .ThenInclude(item => item.Patient)
            .Include(item => item.Appointment)
                .ThenInclude(item => item.Doctor)
                    .ThenInclude(item => item.User)
            .Include(item => item.Appointment)
                .ThenInclude(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (consultation is null)
            throw new NotFoundException(nameof(Consultation), id);

        return MapDetail(consultation);
    }

    public async Task<ConsultationLookupDto> GetLookupAsync(CancellationToken cancellationToken)
    {
        var appointments = await dbContext.Appointments
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(item => item.User)
            .Where(item => item.Status != AppointmentStatus.Cancelled && item.Status != AppointmentStatus.NoShow)
            .Where(item => !dbContext.Consultations.Any(consultation => consultation.AppointmentId == item.Id))
            .OrderByDescending(item => item.AppointmentDate)
            .ThenBy(item => item.StartTime)
            .Select(item => new ConsultationAppointmentOptionDto(
                item.Id,
                item.AppointmentNumber,
                $"{item.Patient.FirstName} {item.Patient.LastName}",
                $"{item.Doctor.User.FirstName} {item.Doctor.User.LastName}",
                item.AppointmentDate,
                item.StartTime,
                item.EndTime,
                item.Reason))
            .ToListAsync(cancellationToken);

        return new ConsultationLookupDto(appointments);
    }

    public async Task<ConsultationDetailDto> CreateAsync(
        CreateConsultationRequest request,
        Guid createdByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AppointmentId == Guid.Empty)
            throw new RequestValidationException("An appointment must be selected.");

        var appointment = await dbContext.Appointments
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(item => item.User)
            .Include(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == request.AppointmentId, cancellationToken);

        if (appointment is null)
            throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.NoShow)
            throw new ConflictException("Only active appointments may receive a consultation note.");

        if (await dbContext.Consultations.AnyAsync(item => item.AppointmentId == request.AppointmentId, cancellationToken))
            throw new ConflictException("A consultation already exists for this appointment.");

        var now = DateTime.UtcNow;

        var consultation = new Models.Consultation
        {
            AppointmentId = request.AppointmentId,
            ChiefComplaint = request.ChiefComplaint,
            Symptoms = request.Symptoms,
            Examination = request.Examination,
            Diagnosis = request.Diagnosis,
            TreatmentPlan = request.TreatmentPlan,
            DoctorNotes = request.DoctorNotes,
            FollowUpDate = request.FollowUpDate,
            StartedAt = now,
            CompletedAt = now,
        };

        dbContext.Consultations.Add(consultation);
        appointment.Status = AppointmentStatus.Completed;
        appointment.Notes = string.IsNullOrWhiteSpace(appointment.Notes) ? request.DoctorNotes : appointment.Notes;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(consultation.Id, cancellationToken);
    }

    public async Task<ConsultationDetailDto> UpdateAsync(
        Guid id,
        UpdateConsultationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var consultation = await dbContext.Consultations
            .Include(item => item.Appointment)
                .ThenInclude(item => item.Patient)
            .Include(item => item.Appointment)
                .ThenInclude(item => item.Doctor)
                    .ThenInclude(item => item.User)
            .Include(item => item.Appointment)
                .ThenInclude(item => item.Department)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (consultation is null)
            throw new NotFoundException(nameof(Consultation), id);

        consultation.ChiefComplaint = request.ChiefComplaint ?? consultation.ChiefComplaint;
        consultation.Symptoms = request.Symptoms ?? consultation.Symptoms;
        consultation.Examination = request.Examination ?? consultation.Examination;
        consultation.Diagnosis = request.Diagnosis ?? consultation.Diagnosis;
        consultation.TreatmentPlan = request.TreatmentPlan ?? consultation.TreatmentPlan;
        consultation.DoctorNotes = request.DoctorNotes ?? consultation.DoctorNotes;
        consultation.FollowUpDate = request.FollowUpDate ?? consultation.FollowUpDate;
        consultation.CompletedAt ??= DateTime.UtcNow;
        consultation.Appointment.Status = AppointmentStatus.Completed;

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDetail(consultation);
    }

    private static ConsultationDetailDto MapDetail(Models.Consultation consultation)
    {
        return new ConsultationDetailDto(
            consultation.Id,
            consultation.AppointmentId,
            consultation.Appointment.AppointmentNumber,
            $"{consultation.Appointment.Patient.FirstName} {consultation.Appointment.Patient.LastName}",
            $"{consultation.Appointment.Doctor.User.FirstName} {consultation.Appointment.Doctor.User.LastName}",
            consultation.Appointment.Department.Name,
            consultation.Appointment.AppointmentDate,
            consultation.Appointment.StartTime,
            consultation.Appointment.EndTime,
            consultation.ChiefComplaint,
            consultation.Symptoms,
            consultation.Examination,
            consultation.Diagnosis,
            consultation.TreatmentPlan,
            consultation.DoctorNotes,
            consultation.FollowUpDate,
            consultation.StartedAt,
            consultation.CompletedAt,
            consultation.Appointment.Reason,
            consultation.Appointment.Notes);
    }
}
