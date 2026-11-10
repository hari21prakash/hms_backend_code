namespace HospitalManagement.Api.DTOs;

public record DatabaseHealthDto(
    bool Connected,
    long? LatencyMs,
    int? PendingMigrations,
    string Provider);

public record HealthStatusDto(
    string Status,
    string Application,
    string Version,
    string Environment,
    DateTime TimestampUtc,
    DatabaseHealthDto Database);
