using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class DispensingService(AppDbContext dbContext, INotificationAlertService? alerts = null) : IDispensingService
{
    public async Task<DispenseWorksheetDto> GetWorksheetAsync(Guid prescriptionId, CancellationToken cancellationToken)
    {
        var prescription = await dbContext.Prescriptions
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Doctor)
                .ThenInclude(doctor => doctor.User)
            .Include(item => item.Items)
                .ThenInclude(item => item.Medicine)
            .SingleOrDefaultAsync(item => item.Id == prescriptionId, cancellationToken);

        if (prescription is null)
            throw new NotFoundException(nameof(Prescription), prescriptionId);

        var today = PharmacyRules.Today();
        var medicineIds = prescription.Items.Select(item => item.MedicineId).Distinct().ToList();

        // First-expiry-first-out order. Expired and empty batches are never offered.
        var batches = await dbContext.MedicineBatches
            .AsNoTracking()
            .Where(batch => medicineIds.Contains(batch.MedicineId) && batch.QuantityOnHand > 0 && batch.ExpiryDate >= today)
            .OrderBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.BatchNumber)
            .ToListAsync(cancellationToken);

        var items = prescription.Items
            .OrderBy(item => item.CreatedAt)
            .Select(item =>
            {
                var options = batches
                    .Where(batch => batch.MedicineId == item.MedicineId)
                    .Select(batch => new DispenseBatchOptionDto(batch.Id, batch.BatchNumber, batch.ExpiryDate, batch.QuantityOnHand, batch.SellingPrice))
                    .ToList();

                return new DispenseItemDto(
                    item.Id,
                    item.MedicineId,
                    item.Medicine.Name,
                    item.Medicine.Strength,
                    item.Medicine.Form.ToString(),
                    item.Medicine.Status.ToString(),
                    item.Dosage,
                    item.Frequency,
                    item.Quantity,
                    item.QuantityDispensed,
                    Math.Max(0, item.Quantity - item.QuantityDispensed),
                    options.Sum(option => option.QuantityOnHand),
                    options);
            })
            .ToList();

        return new DispenseWorksheetDto(
            prescription.Id,
            prescription.PrescriptionNumber,
            prescription.Status.ToString(),
            prescription.PatientId,
            $"{prescription.Patient.FirstName} {prescription.Patient.LastName}",
            $"{prescription.Doctor.User.FirstName} {prescription.Doctor.User.LastName}",
            prescription.PrescribedAt,
            prescription.Instructions,
            items);
    }

    public async Task<DispenseWorksheetDto> DispenseAsync(
        DispenseRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Items is null || request.Items.Count == 0)
            throw new RequestValidationException("Select at least one prescription item to dispense.");

        var duplicateItems = request.Items.GroupBy(item => item.PrescriptionItemId).Any(group => group.Count() > 1);
        if (duplicateItems)
            throw new RequestValidationException("Each prescription item can appear only once. Add several batches to its allocations instead.");

        var touchedMedicines = new HashSet<Guid>();

        await PharmacyRules.RunInStockTransactionAsync(dbContext, async () =>
        {
            touchedMedicines.Clear();
            var today = PharmacyRules.Today();

            var prescription = await dbContext.Prescriptions
                .Include(item => item.Items)
                    .ThenInclude(item => item.Medicine)
                .SingleOrDefaultAsync(item => item.Id == request.PrescriptionId, cancellationToken);

            if (prescription is null)
                throw new NotFoundException(nameof(Prescription), request.PrescriptionId);

            if (prescription.Status == PrescriptionStatus.Cancelled)
                throw new RequestValidationException("This prescription is cancelled and cannot be dispensed.");
            if (prescription.Status == PrescriptionStatus.Dispensed)
                throw new RequestValidationException("This prescription has already been fully dispensed.");

            var batchIds = request.Items.SelectMany(item => item.Allocations).Select(allocation => allocation.BatchId).Distinct().ToList();
            var batches = await dbContext.MedicineBatches
                .Where(batch => batchIds.Contains(batch.Id))
                .ToDictionaryAsync(batch => batch.Id, cancellationToken);

            var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            foreach (var requested in request.Items)
            {
                var item = prescription.Items.SingleOrDefault(candidate => candidate.Id == requested.PrescriptionItemId);
                if (item is null)
                    throw new RequestValidationException("A selected item does not belong to this prescription.");

                if (requested.Allocations is null || requested.Allocations.Count == 0)
                    throw new RequestValidationException($"Choose at least one batch for {item.Medicine.Name}.");

                if (item.Medicine.Status != MedicineStatus.Active)
                    throw new RequestValidationException($"{item.Medicine.Name} is {item.Medicine.Status.ToString().ToLowerInvariant()} and cannot be dispensed.");

                var remaining = item.Quantity - item.QuantityDispensed;
                var requestedTotal = requested.Allocations.Sum(allocation => allocation.Quantity);
                if (requestedTotal <= 0)
                    throw new RequestValidationException($"Quantity for {item.Medicine.Name} must be greater than zero.");
                if (requestedTotal > remaining)
                    throw new RequestValidationException($"{item.Medicine.Name}: only {remaining} unit(s) remain to be dispensed, but {requestedTotal} were requested.");

                foreach (var allocation in requested.Allocations)
                {
                    if (allocation.Quantity <= 0)
                        throw new RequestValidationException("Dispensed quantities must be greater than zero.");

                    if (!batches.TryGetValue(allocation.BatchId, out var batch))
                        throw new NotFoundException(nameof(MedicineBatch), allocation.BatchId);

                    if (batch.MedicineId != item.MedicineId)
                        throw new RequestValidationException($"Batch {batch.BatchNumber} is not a batch of {item.Medicine.Name}.");

                    if (PharmacyRules.IsExpired(batch.ExpiryDate, today))
                        throw new RequestValidationException($"Batch {batch.BatchNumber} expired on {batch.ExpiryDate:yyyy-MM-dd} and cannot be dispensed.");

                    // QuantityOnHand is decremented as allocations are applied, so this check already accounts for
                    // earlier allocations from the same batch in this request.
                    if (allocation.Quantity > batch.QuantityOnHand)
                        throw new RequestValidationException($"Batch {batch.BatchNumber} has only {batch.QuantityOnHand} unit(s) available.");

                    batch.QuantityOnHand -= allocation.Quantity;
                    item.QuantityDispensed += allocation.Quantity;
                    touchedMedicines.Add(item.MedicineId);

                    dbContext.PharmacyTransactions.Add(new PharmacyTransaction
                    {
                        MedicineBatchId = batch.Id,
                        Type = PharmacyTransactionType.Dispense,
                        QuantityChange = -allocation.Quantity,
                        TransactionDate = DateTime.UtcNow,
                        PrescriptionItemId = item.Id,
                        PerformedByUserId = actorUserId,
                        Notes = notes,
                    });
                }
            }

            prescription.Status = prescription.Items.All(item => item.QuantityDispensed >= item.Quantity)
                ? PrescriptionStatus.Dispensed
                : PrescriptionStatus.PartiallyDispensed;

            await dbContext.SaveChangesAsync(cancellationToken);
            return prescription.Id;
        }, cancellationToken);

        dbContext.ChangeTracker.Clear();

        // After the stock transaction has committed: tell pharmacy staff if a medicine is now at/below its reorder level.
        if (alerts is not null && touchedMedicines.Count > 0)
            await alerts.TryEvaluateLowStockAsync(touchedMedicines.ToList(), cancellationToken);

        return await GetWorksheetAsync(request.PrescriptionId, cancellationToken);
    }
}
