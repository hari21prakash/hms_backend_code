using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Helpers;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class PharmacyStockService(AppDbContext dbContext, INotificationAlertService? alerts = null) : IPharmacyStockService
{
    public async Task<BatchListItemDto> ReceiveStockAsync(
        ReceiveStockRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today = PharmacyRules.Today();
        var batchNumber = RequireText(request.BatchNumber, "Batch number");
        var expiryDate = request.ExpiryDate ?? throw new RequestValidationException("Expiry date is required.");
        var receivedDate = request.ReceivedDate ?? today;

        if (request.Quantity <= 0)
            throw new RequestValidationException("Quantity must be greater than zero.");
        if (request.PurchasePrice < 0 || request.SellingPrice < 0)
            throw new RequestValidationException("Prices cannot be negative.");
        if (PharmacyRules.IsExpired(expiryDate, today))
            throw new RequestValidationException("This batch is already expired and cannot be received into stock.");
        if (receivedDate > today.AddDays(1))
            throw new RequestValidationException("Received date cannot be in the future.");

        var batchId = await PharmacyRules.RunInStockTransactionAsync(dbContext, async () =>
        {
            var medicine = await dbContext.Medicines.SingleOrDefaultAsync(item => item.Id == request.MedicineId, cancellationToken);
            if (medicine is null)
                throw new NotFoundException(nameof(Medicine), request.MedicineId);
            if (medicine.Status != MedicineStatus.Active)
                throw new RequestValidationException($"'{medicine.Name}' is {medicine.Status.ToString().ToLowerInvariant()}. Reactivate it before receiving stock.");

            if (request.SupplierId is Guid supplierId)
            {
                var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(item => item.Id == supplierId, cancellationToken);
                if (supplier is null)
                    throw new NotFoundException(nameof(Supplier), supplierId);
                if (!supplier.IsActive)
                    throw new RequestValidationException($"Supplier '{supplier.Name}' is inactive.");
            }

            var lowerBatch = batchNumber.ToLower();
            if (await dbContext.MedicineBatches.AnyAsync(
                    item => item.MedicineId == request.MedicineId && item.BatchNumber.ToLower() == lowerBatch,
                    cancellationToken))
            {
                throw new ConflictException($"Batch '{batchNumber}' already exists for {medicine.Name}. Use a stock adjustment to change its quantity.");
            }

            var batch = new MedicineBatch
            {
                Id = Guid.NewGuid(),
                MedicineId = request.MedicineId,
                SupplierId = request.SupplierId,
                BatchNumber = batchNumber,
                ExpiryDate = expiryDate,
                ReceivedDate = receivedDate,
                PurchasePrice = request.PurchasePrice,
                SellingPrice = request.SellingPrice,
                QuantityReceived = request.Quantity,
                QuantityOnHand = request.Quantity,
            };

            dbContext.MedicineBatches.Add(batch);
            dbContext.PharmacyTransactions.Add(new PharmacyTransaction
            {
                MedicineBatchId = batch.Id,
                Type = PharmacyTransactionType.Purchase,
                QuantityChange = request.Quantity,
                TransactionDate = DateTime.UtcNow,
                PerformedByUserId = actorUserId,
                Notes = BuildNotes("Stock received", request.Notes),
            });

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
            {
                throw new ConflictException($"Batch '{batchNumber}' already exists for this medicine.");
            }

            return batch.Id;
        }, cancellationToken);

        return await LoadBatchAsync(batchId, cancellationToken);
    }

    public async Task<BatchListItemDto> UpdateBatchAsync(
        Guid batchId,
        UpdateBatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batchNumber = RequireText(request.BatchNumber, "Batch number");
        var expiryDate = request.ExpiryDate ?? throw new RequestValidationException("Expiry date is required.");
        if (request.PurchasePrice < 0 || request.SellingPrice < 0)
            throw new RequestValidationException("Prices cannot be negative.");

        var batch = await dbContext.MedicineBatches.SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken);
        if (batch is null)
            throw new NotFoundException(nameof(MedicineBatch), batchId);

        if (request.SupplierId is Guid supplierId && supplierId != batch.SupplierId)
        {
            var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(item => item.Id == supplierId, cancellationToken);
            if (supplier is null)
                throw new NotFoundException(nameof(Supplier), supplierId);
            if (!supplier.IsActive)
                throw new RequestValidationException($"Supplier '{supplier.Name}' is inactive.");
        }

        var lowerBatch = batchNumber.ToLower();
        if (await dbContext.MedicineBatches.AnyAsync(
                item => item.Id != batchId && item.MedicineId == batch.MedicineId && item.BatchNumber.ToLower() == lowerBatch,
                cancellationToken))
        {
            throw new ConflictException($"Another batch numbered '{batchNumber}' already exists for this medicine.");
        }

        batch.SupplierId = request.SupplierId;
        batch.BatchNumber = batchNumber;
        batch.ExpiryDate = expiryDate;
        batch.PurchasePrice = request.PurchasePrice;
        batch.SellingPrice = request.SellingPrice;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw new ConflictException($"Another batch numbered '{batchNumber}' already exists for this medicine.");
        }

        return await LoadBatchAsync(batchId, cancellationToken);
    }

    public async Task<BatchListItemDto> AdjustStockAsync(
        Guid batchId,
        AdjustStockRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var type = request.Type ?? throw new RequestValidationException("Adjustment type is required.");
        if (type is not (PharmacyTransactionType.Adjustment or PharmacyTransactionType.WriteOff))
            throw new RequestValidationException("Only Adjustment and WriteOff are allowed here. Use Receive stock or Dispense for those movements.");
        if (request.Quantity <= 0)
            throw new RequestValidationException("Quantity must be greater than zero.");
        var reason = RequireText(request.Reason, "Reason");
        if (reason.Length < 3)
            throw new RequestValidationException("Please give a reason of at least 3 characters.");

        bool increase;
        if (type == PharmacyTransactionType.WriteOff)
        {
            increase = false;
        }
        else
        {
            var direction = request.Direction ?? throw new RequestValidationException("Choose whether the adjustment increases or decreases stock.");
            increase = direction == StockAdjustmentDirection.Increase;
        }

        var affectedMedicineId = Guid.Empty;

        await PharmacyRules.RunInStockTransactionAsync(dbContext, async () =>
        {
            var batch = await dbContext.MedicineBatches.SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken);
            if (batch is null)
                throw new NotFoundException(nameof(MedicineBatch), batchId);
            affectedMedicineId = batch.MedicineId;

            if (increase && PharmacyRules.IsExpired(batch.ExpiryDate, PharmacyRules.Today()))
                throw new RequestValidationException("Stock cannot be added to an expired batch.");

            var change = increase ? request.Quantity : -request.Quantity;
            if (batch.QuantityOnHand + change < 0)
                throw new RequestValidationException($"Cannot remove {request.Quantity} units: only {batch.QuantityOnHand} on hand in this batch.");

            batch.QuantityOnHand += change;
            dbContext.PharmacyTransactions.Add(new PharmacyTransaction
            {
                MedicineBatchId = batch.Id,
                Type = type,
                QuantityChange = change,
                TransactionDate = DateTime.UtcNow,
                PerformedByUserId = actorUserId,
                Notes = reason,
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return batch.Id;
        }, cancellationToken);

        // Stock was reduced and committed: warn pharmacy staff if the medicine is now at/below its reorder level.
        if (!increase && alerts is not null && affectedMedicineId != Guid.Empty)
            await alerts.TryEvaluateLowStockAsync([affectedMedicineId], cancellationToken);

        return await LoadBatchAsync(batchId, cancellationToken);
    }

    private async Task<BatchListItemDto> LoadBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await dbContext.MedicineBatches
            .AsNoTracking()
            .Include(item => item.Medicine)
            .Include(item => item.Supplier)
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken);

        if (batch is null)
            throw new NotFoundException(nameof(MedicineBatch), batchId);

        return PharmacyService.MapBatch(batch, PharmacyRules.Today());
    }

    private static string RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RequestValidationException($"{label} is required.");
        return value.Trim();
    }

    private static string BuildNotes(string prefix, string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? prefix : $"{prefix}: {notes.Trim()}";
}
