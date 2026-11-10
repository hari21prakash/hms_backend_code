using System.Data;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>Single source of truth for what counts as usable stock, shared by every pharmacy code path.</summary>
public static class PharmacyRules
{
    public const int ExpiringSoonDays = 90;

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>A batch can be used through its expiry date and is expired from the next day. Expired stock never counts as available.</summary>
    public static bool IsExpired(DateOnly expiryDate, DateOnly today) => expiryDate < today;

    public static string GetBatchStatus(int quantityOnHand, DateOnly expiryDate, DateOnly today)
    {
        if (IsExpired(expiryDate, today)) return quantityOnHand > 0 ? "Expired" : "Depleted";
        if (quantityOnHand <= 0) return "Depleted";
        return expiryDate <= today.AddDays(ExpiringSoonDays) ? "ExpiringSoon" : "Available";
    }

    /// <summary>
    /// Runs stock-changing work in one SERIALIZABLE transaction: everything commits or nothing does, and two people
    /// changing the same batch at once cannot both succeed on stale numbers (the loser gets a 409 and retries).
    /// </summary>
    public static async Task<T> RunInStockTransactionAsync<T>(
        AppDbContext dbContext,
        Func<Task<T>> work,
        CancellationToken cancellationToken)
    {
        // Non-relational providers (the EF InMemory provider used by unit tests) have no transactions to start.
        // PostgreSQL in production is always relational, so this branch never runs there.
        if (!dbContext.Database.IsRelational())
            return await work();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await work();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (DbErrors.IsConcurrencyConflict(exception))
        {
            throw new ConflictException("Stock was changed by another user while saving. Please refresh and try again.");
        }
        catch (Exception exception) when (DbErrors.IsCheckViolation(exception))
        {
            throw new ConflictException("The change would make stock negative or inconsistent. Please refresh and try again.");
        }
    }
}
