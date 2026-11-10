using System.Globalization;
using HospitalManagement.Api.Constants;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>Who gets a notification and what it says. A user listed in several audiences receives only the first.</summary>
public sealed record NotificationAudience(IReadOnlyCollection<Guid> UserIds, string Title, string Message);

/// <summary>
/// Event-driven notification creation.
///
/// The Stage* methods only ADD <see cref="Notification"/> rows to the caller's <see cref="AppDbContext"/> change
/// tracker - they never call SaveChanges. The business service saves its own entities and the notifications in ONE
/// SaveChangesAsync (= one database transaction), so a notification exists if and only if the business operation
/// committed. Real-time delivery happens afterwards (see <c>NotificationBroadcastInterceptor</c>).
/// </summary>
public interface INotificationPublisher
{
    /// <summary>Low level: stage one event for the given audiences. Returns how many rows were staged (duplicates are skipped).</summary>
    Task<int> StageAsync(
        NotificationType type,
        string relatedEntityName,
        Guid relatedEntityId,
        string dedupKey,
        IReadOnlyList<NotificationAudience> audiences,
        CancellationToken cancellationToken);

    Task StageAppointmentBookedAsync(Appointment appointment, Patient patient, Doctor doctor, Guid actorUserId, CancellationToken cancellationToken);
    Task StageAppointmentCancelledAsync(Appointment appointment, Patient patient, Doctor doctor, Guid actorUserId, CancellationToken cancellationToken);
    Task StageAppointmentReminderAsync(Appointment appointment, Patient patient, Doctor doctor, CancellationToken cancellationToken);
    Task StagePrescriptionCreatedAsync(Prescription prescription, Patient patient, Doctor doctor, CancellationToken cancellationToken);
    Task StageLabOrderCreatedAsync(LabOrder order, Patient patient, LabTest test, CancellationToken cancellationToken);
    Task StageLabResultAvailableAsync(LabOrder order, Patient patient, Doctor doctor, LabTest test, Guid actorUserId, CancellationToken cancellationToken);
    Task StageInvoiceCreatedAsync(Invoice invoice, Patient patient, Guid actorUserId, CancellationToken cancellationToken);
    Task StagePaymentReceivedAsync(Payment payment, Invoice invoice, Patient patient, Guid actorUserId, CancellationToken cancellationToken);
}

public class NotificationPublisher(AppDbContext dbContext, INotificationRecipientResolver recipients) : INotificationPublisher
{
    private const int MaxTitle = 200;
    private const int MaxMessage = 1000;

    // ---------------------------------------------------------------------------------------------------------
    // Event -> recipient matrix (also documented in NOTIFICATIONS.md). The actor (the user who performed the
    // action) never notifies themselves.
    //
    //  AppointmentBooked     patient, booked doctor, nurses of the appointment's department
    //  AppointmentCancelled  patient, booked doctor, nurses of the department, receptionists
    //  AppointmentReminder   patient, booked doctor
    //  PrescriptionCreated   patient, pharmacists
    //  LabOrderCreated       lab technicians
    //  LabResultAvailable    patient, ordering doctor
    //  InvoiceCreated        patient, accountants
    //  PaymentReceived       patient, accountants
    //  LowMedicineStock / MedicineExpiringSoon / MedicineExpired
    //                        pharmacists, hospital admins, super admins   (see NotificationAlertService)
    // ---------------------------------------------------------------------------------------------------------

    public async Task<int> StageAsync(
        NotificationType type,
        string relatedEntityName,
        Guid relatedEntityId,
        string dedupKey,
        IReadOnlyList<NotificationAudience> audiences,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dedupKey);

        var seen = new HashSet<Guid>();
        var rows = new List<(Guid UserId, string Title, string Message)>();
        foreach (var audience in audiences)
        foreach (var userId in audience.UserIds)
        {
            if (userId != Guid.Empty && seen.Add(userId))
                rows.Add((userId, audience.Title, audience.Message));
        }

        if (rows.Count == 0) return 0;

        // Duplicate prevention: skip recipients that already have this event, whether committed or merely staged
        // earlier in the same unit of work. The unique index (UserId, DedupKey) is the final safety net.
        var userIds = rows.Select(row => row.UserId).ToList();
        var existing = (await dbContext.Notifications
                .AsNoTracking()
                .Where(item => item.DedupKey == dedupKey && userIds.Contains(item.UserId))
                .Select(item => item.UserId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var entry in dbContext.ChangeTracker.Entries<Notification>())
        {
            if (entry.State == EntityState.Added && entry.Entity.DedupKey == dedupKey)
                existing.Add(entry.Entity.UserId);
        }

        var staged = 0;
        foreach (var (userId, title, message) in rows)
        {
            if (existing.Contains(userId)) continue;

            dbContext.Notifications.Add(new Notification
            {
                UserId = userId,
                Type = type,
                Title = Truncate(title, MaxTitle),
                Message = Truncate(message, MaxMessage),
                IsRead = false,
                RelatedEntityName = relatedEntityName,
                RelatedEntityId = relatedEntityId,
                DedupKey = dedupKey,
            });
            staged++;
        }

        return staged;
    }

    public async Task StageAppointmentBookedAsync(Appointment appointment, Patient patient, Doctor doctor, Guid actorUserId, CancellationToken cancellationToken)
    {
        var when = Describe(appointment);
        var doctorName = DoctorName(doctor);
        var patientUsers = await recipients.EligibleAsync([patient.UserId], cancellationToken);
        var doctorUsers = await recipients.EligibleAsync([doctor.UserId], cancellationToken);
        var nurses = await recipients.NursesInDepartmentAsync(appointment.DepartmentId, cancellationToken);

        await StageAsync(NotificationType.AppointmentBooked, nameof(Appointment), appointment.Id, $"appointment:{appointment.Id}:booked",
        [
            new(Without(patientUsers, actorUserId), "Appointment booked",
                $"Your appointment {appointment.AppointmentNumber} with {doctorName} is booked for {when}."),
            new(Without(doctorUsers, actorUserId), "New appointment",
                $"{PatientName(patient)} is booked with you for {when} ({appointment.AppointmentNumber})."),
            new(Without(nurses, actorUserId), "New appointment in your department",
                $"{PatientName(patient)} is booked with {doctorName} for {when} ({appointment.AppointmentNumber})."),
        ], cancellationToken);
    }

    public async Task StageAppointmentCancelledAsync(Appointment appointment, Patient patient, Doctor doctor, Guid actorUserId, CancellationToken cancellationToken)
    {
        var when = Describe(appointment);
        var doctorName = DoctorName(doctor);
        var reason = string.IsNullOrWhiteSpace(appointment.CancellationReason) ? string.Empty : $" Reason: {appointment.CancellationReason.Trim()}";
        var patientUsers = await recipients.EligibleAsync([patient.UserId], cancellationToken);
        var doctorUsers = await recipients.EligibleAsync([doctor.UserId], cancellationToken);
        var nurses = await recipients.NursesInDepartmentAsync(appointment.DepartmentId, cancellationToken);
        var frontDesk = await recipients.InRolesAsync(cancellationToken, RoleNames.Receptionist);

        await StageAsync(NotificationType.AppointmentCancelled, nameof(Appointment), appointment.Id, $"appointment:{appointment.Id}:cancelled",
        [
            new(Without(patientUsers, actorUserId), "Appointment cancelled",
                $"Your appointment {appointment.AppointmentNumber} with {doctorName} on {when} was cancelled.{reason}"),
            new(Without(doctorUsers, actorUserId), "Appointment cancelled",
                $"The appointment with {PatientName(patient)} on {when} ({appointment.AppointmentNumber}) was cancelled.{reason}"),
            new(Without(nurses, actorUserId), "Appointment cancelled",
                $"{PatientName(patient)}'s appointment with {doctorName} on {when} was cancelled.{reason}"),
            new(Without(frontDesk, actorUserId), "Appointment cancelled",
                $"{appointment.AppointmentNumber} ({PatientName(patient)} with {doctorName}, {when}) was cancelled - the slot is free again.{reason}"),
        ], cancellationToken);
    }

    public async Task StageAppointmentReminderAsync(Appointment appointment, Patient patient, Doctor doctor, CancellationToken cancellationToken)
    {
        var when = Describe(appointment);
        var patientUsers = await recipients.EligibleAsync([patient.UserId], cancellationToken);
        var doctorUsers = await recipients.EligibleAsync([doctor.UserId], cancellationToken);

        await StageAsync(NotificationType.AppointmentReminder, nameof(Appointment), appointment.Id, $"appointment:{appointment.Id}:reminder",
        [
            new(patientUsers, "Appointment reminder",
                $"Reminder: you have an appointment with {DoctorName(doctor)} on {when} ({appointment.AppointmentNumber})."),
            new(doctorUsers, "Upcoming appointment",
                $"Reminder: {PatientName(patient)} is scheduled with you on {when} ({appointment.AppointmentNumber})."),
        ], cancellationToken);
    }

    public async Task StagePrescriptionCreatedAsync(Prescription prescription, Patient patient, Doctor doctor, CancellationToken cancellationToken)
    {
        var patientUsers = await recipients.EligibleAsync([patient.UserId], cancellationToken);
        var pharmacists = await recipients.InRolesAsync(cancellationToken, RoleNames.Pharmacist);
        var items = prescription.Items.Count;

        await StageAsync(NotificationType.PrescriptionCreated, nameof(Prescription), prescription.Id, $"prescription:{prescription.Id}:created",
        [
            new(patientUsers, "New prescription",
                $"{DoctorName(doctor)} issued prescription {prescription.PrescriptionNumber} ({items} medicine{(items == 1 ? "" : "s")})."),
            new(pharmacists, "New prescription to dispense",
                $"Prescription {prescription.PrescriptionNumber} for {PatientName(patient)} by {DoctorName(doctor)} is ready for dispensing ({items} medicine{(items == 1 ? "" : "s")})."),
        ], cancellationToken);
    }

    public async Task StageLabOrderCreatedAsync(LabOrder order, Patient patient, LabTest test, CancellationToken cancellationToken)
    {
        var technicians = await recipients.InRolesAsync(cancellationToken, RoleNames.LabTechnician);
        var priority = order.Priority == LabPriority.Routine ? string.Empty : $" [{order.Priority}]";

        await StageAsync(NotificationType.LabOrderCreated, nameof(LabOrder), order.Id, $"laborder:{order.Id}:created",
        [
            new(technicians, $"New lab order{priority}",
                $"{order.OrderNumber}: {test.Name} for {PatientName(patient)} is waiting for sample collection."),
        ], cancellationToken);
    }

    public async Task StageLabResultAvailableAsync(LabOrder order, Patient patient, Doctor doctor, LabTest test, Guid actorUserId, CancellationToken cancellationToken)
    {
        var patientUsers = await recipients.EligibleAsync([patient.UserId], cancellationToken);
        var doctorUsers = await recipients.EligibleAsync([doctor.UserId], cancellationToken);

        await StageAsync(NotificationType.LabResultAvailable, nameof(LabOrder), order.Id, $"laborder:{order.Id}:result",
        [
            new(Without(patientUsers, actorUserId), "Lab result available",
                $"Your result for {test.Name} ({order.OrderNumber}) is now available."),
            new(Without(doctorUsers, actorUserId), "Lab result available",
                $"{test.Name} result for {PatientName(patient)} ({order.OrderNumber}) is ready for review."),
        ], cancellationToken);
    }

    public async Task StageInvoiceCreatedAsync(Invoice invoice, Patient patient, Guid actorUserId, CancellationToken cancellationToken)
    {
        var patientUsers = await recipients.EligibleAsync([patient.UserId], cancellationToken);
        var accountants = await recipients.InRolesAsync(cancellationToken, RoleNames.Accountant);

        await StageAsync(NotificationType.InvoiceCreated, nameof(Invoice), invoice.Id, $"invoice:{invoice.Id}:created",
        [
            new(Without(patientUsers, actorUserId), "New invoice",
                $"Invoice {invoice.InvoiceNumber} for {Money(invoice.TotalAmount)} has been issued."),
            new(Without(accountants, actorUserId), "Invoice issued",
                $"Invoice {invoice.InvoiceNumber} for {PatientName(patient)} ({Money(invoice.TotalAmount)}) was issued."),
        ], cancellationToken);
    }

    public async Task StagePaymentReceivedAsync(Payment payment, Invoice invoice, Patient patient, Guid actorUserId, CancellationToken cancellationToken)
    {
        var patientUsers = await recipients.EligibleAsync([patient.UserId], cancellationToken);
        var accountants = await recipients.InRolesAsync(cancellationToken, RoleNames.Accountant);
        var settled = invoice.Status == InvoiceStatus.Paid ? " The invoice is now fully paid." : $" Remaining balance: {Money(invoice.BalanceAmount)}.";

        await StageAsync(NotificationType.PaymentReceived, nameof(Payment), payment.Id, $"payment:{payment.Id}:received",
        [
            new(Without(patientUsers, actorUserId), "Payment received",
                $"We received your payment {payment.PaymentNumber} of {Money(payment.Amount)} for invoice {invoice.InvoiceNumber}.{settled}"),
            new(Without(accountants, actorUserId), "Payment recorded",
                $"Payment {payment.PaymentNumber} of {Money(payment.Amount)} from {PatientName(patient)} for invoice {invoice.InvoiceNumber}.{settled}"),
        ], cancellationToken);
    }

    // ---- helpers ----

    private static string PatientName(Patient patient) => $"{patient.FirstName} {patient.LastName}".Trim();

    private static string DoctorName(Doctor doctor) =>
        doctor.User is null ? "your doctor" : $"Dr. {doctor.User.FirstName} {doctor.User.LastName}".Trim();

    private static string Describe(Appointment appointment) =>
        $"{appointment.AppointmentDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} at {appointment.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)}";

    private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

    private static IReadOnlyCollection<Guid> Without(IEnumerable<Guid> users, Guid actorUserId) =>
        users.Where(id => id != actorUserId).ToList();

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "\u2026";
}
