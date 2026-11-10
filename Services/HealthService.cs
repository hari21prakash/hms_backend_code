using System.Diagnostics;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class HealthService(
    AppDbContext db,
    IHostEnvironment environment,
    ILogger<HealthService> logger) : IHealthService
{
    public async Task<HealthStatusDto> CheckAsync(CancellationToken cancellationToken)
    {
        var database = await CheckDatabaseAsync(cancellationToken);

        return new HealthStatusDto(
            Status: database.Connected ? "Healthy" : "Unhealthy",
            Application: "Hospital Management System API",
            Version: typeof(HealthService).Assembly.GetName().Version?.ToString() ?? "unknown",
            Environment: environment.EnvironmentName,
            TimestampUtc: DateTime.UtcNow,
            Database: database);
    }

    private async Task<DatabaseHealthDto> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        var provider = db.Database.ProviderName ?? "unknown";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var connected = await db.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            if (!connected)
                return new DatabaseHealthDto(false, null, null, provider);

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            return new DatabaseHealthDto(true, stopwatch.ElapsedMilliseconds, pending, provider);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Database health check failed");
            return new DatabaseHealthDto(false, null, null, provider);
        }
    }
}
