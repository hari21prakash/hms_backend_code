using HospitalManagement.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>
/// Single source of truth for human-readable patient numbers (PAT-yyyy-nnnnnn).
/// PostgreSQL uses the dedicated sequence (gap-tolerant, concurrency-safe). Non-relational providers
/// (used only by unit tests) fall back to a row count because they have no sequences.
/// </summary>
public static class PatientNumberGenerator
{
    public static async Task<string> NextAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        long sequenceNumber;
        if (dbContext.Database.IsRelational())
        {
            sequenceNumber = await dbContext.Database
                .SqlQueryRaw<long>($"SELECT nextval('{DbSequences.PatientNumber}') AS \"Value\"")
                .SingleAsync(cancellationToken);
        }
        else
        {
            sequenceNumber = await dbContext.Patients.LongCountAsync(cancellationToken) + 1;
        }

        return $"PAT-{DateTime.UtcNow.Year}-{sequenceNumber:D6}";
    }
}
