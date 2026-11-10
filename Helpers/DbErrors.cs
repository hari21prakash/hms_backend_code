using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalManagement.Api.Helpers;

/// <summary>Classifies database exceptions so services can return a friendly 409 instead of a 500.</summary>
public static class DbErrors
{
    public static bool IsUniqueViolation(Exception exception) =>
        Find(exception)?.SqlState == PostgresErrorCodes.UniqueViolation;

    public static bool IsCheckViolation(Exception exception) =>
        Find(exception)?.SqlState == PostgresErrorCodes.CheckViolation;

    /// <summary>Serialization failures, deadlocks and optimistic-concurrency failures: the caller should refresh and retry.</summary>
    public static bool IsConcurrencyConflict(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        Find(exception)?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;

    private static PostgresException? Find(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is PostgresException postgres) return postgres;
            exception = exception.InnerException;
        }

        return null;
    }
}
