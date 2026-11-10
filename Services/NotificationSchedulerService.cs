using HospitalManagement.Api.Interfaces;
using Microsoft.Extensions.Hosting;

namespace HospitalManagement.Api.Services;

/// <summary>
/// Runs the derived-alert scans (appointment reminders, medicine expiry, low stock) periodically. Each run creates a DI
/// scope and swallows/logs failures so one bad run never stops the loop. Disable with <c>Notifications:SchedulerEnabled=false</c>.
/// </summary>
public class NotificationSchedulerService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<NotificationSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Notifications:SchedulerEnabled", true))
        {
            logger.LogInformation("Notification scheduler is disabled by configuration");
            return;
        }

        var minutes = Math.Max(1, configuration.GetValue("Notifications:ScanIntervalMinutes", 15));
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));

        // Small initial delay so application start-up (migrations, seeding) finishes first.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var alerts = scope.ServiceProvider.GetRequiredService<INotificationAlertService>();
                var created = await alerts.RunAllAsync(stoppingToken);
                if (created > 0) logger.LogInformation("Notification scan created {Count} notification(s)", created);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification scan failed; will retry on the next interval");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try { return await timer.WaitForNextTickAsync(cancellationToken); }
        catch (OperationCanceledException) { return false; }
    }
}
