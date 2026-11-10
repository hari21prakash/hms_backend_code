using System.Runtime.CompilerServices;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Hubs;
using HospitalManagement.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Data.Common;

namespace HospitalManagement.Api.Services;

/// <summary>Pushes freshly COMMITTED notifications to the recipients' browsers.</summary>
public interface INotificationBroadcaster
{
    Task BroadcastAsync(IReadOnlyList<NotificationDto> notifications, CancellationToken cancellationToken = default);
}

public static class NotificationMapper
{
    public static NotificationDto ToDto(Notification item) => new(
        item.Id, item.UserId, item.Type, item.Title, item.Message, item.IsRead, item.ReadAt, item.CreatedAt,
        item.RelatedEntityName, item.RelatedEntityId);
}

/// <summary>
/// SignalR delivery. Each notification goes ONLY to the connections of its own recipient
/// (<c>Clients.User(userId)</c>, resolved from the authenticated JWT "sub" claim), so users never see each other's data.
/// Delivery is best-effort and can never fail the business request; clients also poll as a fallback.
/// </summary>
public class SignalRNotificationBroadcaster(IHubContext<NotificationHub> hub, ILogger<SignalRNotificationBroadcaster> logger) : INotificationBroadcaster
{
    public const string ClientMethod = "notificationReceived";

    public async Task BroadcastAsync(IReadOnlyList<NotificationDto> notifications, CancellationToken cancellationToken = default)
    {
        foreach (var notification in notifications)
        {
            try
            {
                await hub.Clients.User(notification.UserId.ToString()).SendAsync(ClientMethod, notification, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Real-time delivery of notification {NotificationId} failed; the recipient will receive it on the next poll", notification.Id);
            }
        }
    }
}

public sealed class NullNotificationBroadcaster : INotificationBroadcaster
{
    public Task BroadcastAsync(IReadOnlyList<NotificationDto> notifications, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Broadcasts notifications only AFTER the database transaction that stored them has committed:
///  * SaveChanges outside an explicit transaction: after the save succeeds.
///  * SaveChanges inside an explicit transaction (e.g. the serializable stock transaction): when that transaction commits;
///    if it rolls back the pending notifications are discarded and nothing is ever sent.
/// </summary>
public sealed class NotificationBroadcastInterceptor(INotificationBroadcaster broadcaster) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private sealed class State
    {
        public List<Notification> Saving { get; } = [];
        public List<Notification> AwaitingCommit { get; } = [];
    }

    private static readonly ConditionalWeakTable<DbContext, State> States = new();

    private static State For(DbContext context) => States.GetOrCreateValue(context);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await AfterSaveAsync(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        AfterSaveAsync(eventData.Context).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) For(context).Saving.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } context) For(context).Saving.Clear();
        base.SaveChangesFailed(eventData);
    }

    public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not { } context) return;
        var state = For(context);
        if (state.AwaitingCommit.Count == 0) return;

        var batch = state.AwaitingCommit.ToList();
        state.AwaitingCommit.Clear();
        await SendAsync(batch);
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (eventData.Context is not { } context) return;
        var state = For(context);
        if (state.AwaitingCommit.Count == 0) return;

        var batch = state.AwaitingCommit.ToList();
        state.AwaitingCommit.Clear();
        SendAsync(batch).GetAwaiter().GetResult();
    }

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) For(context).AwaitingCommit.Clear();
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        if (eventData.Context is { } context) For(context).AwaitingCommit.Clear();
    }

    private static void Capture(DbContext? context)
    {
        if (context is null) return;
        var state = For(context);
        state.Saving.Clear();
        state.Saving.AddRange(context.ChangeTracker.Entries<Notification>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity));
    }

    private async Task AfterSaveAsync(DbContext? context)
    {
        if (context is null) return;
        var state = For(context);
        if (state.Saving.Count == 0) return;

        var saved = state.Saving.ToList();
        state.Saving.Clear();

        // Relational providers expose the explicit transaction; if one is open the rows are not committed yet.
        if (context.Database.IsRelational() && context.Database.CurrentTransaction is not null)
        {
            state.AwaitingCommit.AddRange(saved);
            return;
        }

        await SendAsync(saved);
    }

    private async Task SendAsync(List<Notification> saved)
    {
        try
        {
            await broadcaster.BroadcastAsync(saved.Select(NotificationMapper.ToDto).ToList());
        }
        catch
        {
            // Best effort: a failed push must never affect the request that already committed.
        }
    }
}
