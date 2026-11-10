using HospitalManagement.Api.Constants;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.Helpers;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HospitalManagement.Api.Services;

/// <summary>
/// Alerts that are DERIVED from committed data rather than raised by a single request: pharmacy low stock and
/// expiry, and appointment reminders. They run after the business transaction (low stock) or on a schedule
/// (everything), always in their own transaction, and are safe to run repeatedly: every alert carries a dedup key.
/// </summary>
public interface INotificationAlertService
{
    /// <summary>Alerts pharmacy staff about medicines at or below their reorder level. Null = check every active medicine.</summary>
    Task<int> EvaluateLowStockAsync(IReadOnlyCollection<Guid>? medicineIds, CancellationToken cancellationToken);

    /// <summary>Best-effort variant for use right after a stock change: never throws, because the stock change itself already succeeded.</summary>
    Task TryEvaluateLowStockAsync(IReadOnlyCollection<Guid> medicineIds, CancellationToken cancellationToken);

    /// <summary>One alert per batch that is expired (with stock left) or expiring within <see cref="PharmacyRules.ExpiringSoonDays"/> days.</summary>
    Task<int> SendMedicineExpiryAlertsAsync(CancellationToken cancellationToken);

    /// <summary>One reminder per appointment starting within the next <see cref="ReminderWindow"/>.</summary>
    Task<int> SendAppointmentRemindersAsync(CancellationToken cancellationToken);

    /// <summary>Runs every scan once. Used by the background scheduler.</summary>
    Task<int> RunAllAsync(CancellationToken cancellationToken);
}

public class NotificationAlertService(
    AppDbContext dbContext,
    INotificationPublisher publisher,
    INotificationRecipientResolver recipients,
    TimeProvider timeProvider,
    ILogger<NotificationAlertService> logger) : INotificationAlertService
{
    public static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(24);

    private static readonly string[] PharmacyOversightRoles = [RoleNames.Pharmacist, RoleNames.HospitalAdmin, RoleNames.SuperAdmin];

    public async Task<int> RunAllAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        total += await SendAppointmentRemindersAsync(cancellationToken);
        total += await EvaluateLowStockAsync(null, cancellationToken);
        total += await SendMedicineExpiryAlertsAsync(cancellationToken);
        return total;
    }

    public async Task TryEvaluateLowStockAsync(IReadOnlyCollection<Guid> medicineIds, CancellationToken cancellationToken)
    {
        try
        {
            await EvaluateLowStockAsync(medicineIds, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The stock change is already committed. The scheduled scan will raise the alert later.
            DetachPending();
            logger.LogWarning(exception, "Low-stock check after a stock change failed; the scheduled scan will retry");
        }
    }

    public async Task<int> EvaluateLowStockAsync(IReadOnlyCollection<Guid>? medicineIds, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var query = dbContext.Medicines.AsNoTracking().Where(medicine => medicine.Status == MedicineStatus.Active);
        if (medicineIds is not null)
        {
            if (medicineIds.Count == 0) return 0;
            query = query.Where(medicine => medicineIds.Contains(medicine.Id));
        }

        var stock = await query
            .Select(medicine => new
            {
                medicine.Id,
                medicine.Name,
                medicine.Strength,
                medicine.ReorderLevel,
                Usable = medicine.Batches.Where(batch => batch.ExpiryDate >= today).Sum(batch => (int?)batch.QuantityOnHand) ?? 0,
            })
            .ToListAsync(cancellationToken);

        var low = stock.Where(row => row.Usable <= row.ReorderLevel).ToList();
        if (low.Count == 0) return 0;

        var audience = await recipients.InRolesAsync(cancellationToken, PharmacyOversightRoles);
        var created = 0;

        foreach (var row in low)
        {
            // At most one OUTSTANDING (unread) low-stock alert per medicine and person; once it is read, the next
            // day's check may raise a fresh one if the stock is still low.
            var alreadyAlerted = (await dbContext.Notifications
                    .AsNoTracking()
                    .Where(item => item.Type == NotificationType.LowMedicineStock && !item.IsRead && item.RelatedEntityId == row.Id)
                    .Select(item => item.UserId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var users = audience.Where(id => !alreadyAlerted.Contains(id)).ToList();
            var label = $"{row.Name} {row.Strength}".Trim();
            var message = row.Usable <= 0
                ? $"{label} is out of usable stock (reorder level {row.ReorderLevel})."
                : $"{label} is low: {row.Usable} unit(s) left (reorder level {row.ReorderLevel}).";

            created += await PublishAsync(
                NotificationType.LowMedicineStock, nameof(Medicine), row.Id,
                $"lowstock:{row.Id}:{today:yyyyMMdd}",
                [new NotificationAudience(users, "Low medicine stock", message)],
                cancellationToken);
        }

        return created;
    }

    public async Task<int> SendMedicineExpiryAlertsAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var horizon = today.AddDays(PharmacyRules.ExpiringSoonDays);

        var batches = await dbContext.MedicineBatches
            .AsNoTracking()
            .Where(batch => batch.QuantityOnHand > 0 && batch.ExpiryDate <= horizon && batch.Medicine.Status == MedicineStatus.Active)
            .Select(batch => new { batch.Id, batch.BatchNumber, batch.ExpiryDate, batch.QuantityOnHand, batch.Medicine.Name, batch.Medicine.Strength, batch.MedicineId })
            .ToListAsync(cancellationToken);
        if (batches.Count == 0) return 0;

        var audience = await recipients.InRolesAsync(cancellationToken, PharmacyOversightRoles);
        var created = 0;

        foreach (var batch in batches)
        {
            var expired = PharmacyRules.IsExpired(batch.ExpiryDate, today);
            var label = $"{batch.Name} {batch.Strength}".Trim();
            var type = expired ? NotificationType.MedicineExpired : NotificationType.MedicineExpiringSoon;
            var title = expired ? "Medicine batch expired" : "Medicine batch expiring soon";
            var message = expired
                ? $"Batch {batch.BatchNumber} of {label} expired on {batch.ExpiryDate:yyyy-MM-dd} with {batch.QuantityOnHand} unit(s) still on hand. Remove it from stock."
                : $"Batch {batch.BatchNumber} of {label} expires on {batch.ExpiryDate:yyyy-MM-dd} ({batch.QuantityOnHand} unit(s) on hand).";

            // One alert per batch and state: "expiring" is raised once, then "expired" once - never repeated.
            created += await PublishAsync(
                type, nameof(MedicineBatch), batch.Id,
                $"batch:{batch.Id}:{(expired ? "expired" : "expiring")}",
                [new NotificationAudience(audience, title, message)],
                cancellationToken);
        }

        return created;
    }

    public async Task<int> SendAppointmentRemindersAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var lastDay = DateOnly.FromDateTime(now + ReminderWindow);

        var candidates = await dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Include(appointment => appointment.Doctor).ThenInclude(doctor => doctor.User)
            .Where(appointment =>
                appointment.AppointmentDate >= today && appointment.AppointmentDate <= lastDay &&
                (appointment.Status == AppointmentStatus.Scheduled || appointment.Status == AppointmentStatus.Confirmed))
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var appointment in candidates)
        {
            var start = appointment.AppointmentDate.ToDateTime(appointment.StartTime, DateTimeKind.Utc);
            if (start <= now || start > now + ReminderWindow) continue;

            created += await PublishAsync(
                () => publisher.StageAppointmentReminderAsync(appointment, appointment.Patient, appointment.Doctor, cancellationToken),
                cancellationToken);
        }

        return created;
    }

    // ---- persistence (own transaction; never throws for a duplicate) ----

    private Task<int> PublishAsync(
        NotificationType type, string entityName, Guid entityId, string dedupKey,
        IReadOnlyList<NotificationAudience> audiences, CancellationToken cancellationToken) =>
        PublishAsync(() => publisher.StageAsync(type, entityName, entityId, dedupKey, audiences, cancellationToken), cancellationToken);

    private async Task<int> PublishAsync(Func<Task> stage, CancellationToken cancellationToken)
    {
        // Two attempts: if a concurrent worker inserted the same (user, dedup key) between our duplicate check and
        // the insert, the unique index rejects ours; we then re-stage, which now sees the existing row and skips it.
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var before = PendingCount();
            await stage();
            var staged = PendingCount() - before;
            if (staged <= 0) return 0;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return staged;
            }
            catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
            {
                logger.LogInformation("Notification already created by another worker; retrying duplicate check");
                DetachPending();
            }
        }

        return 0;
    }

    private int PendingCount() => dbContext.ChangeTracker.Entries<Notification>().Count(entry => entry.State == EntityState.Added);

    private void DetachPending()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<Notification>().Where(entry => entry.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
    }
}
