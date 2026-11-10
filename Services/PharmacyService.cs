using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Helpers;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>Pharmacy catalogue: medicines, suppliers, lookups and read-only views of batches and the stock ledger.</summary>
public class PharmacyService(AppDbContext dbContext) : IPharmacyService
{
    public async Task<PharmacyStockPageDto> GetStockAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MedicineBatches
            .AsNoTracking()
            .Include(batch => batch.Medicine)
            .Where(batch => batch.Medicine.Status == MedicineStatus.Active)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(batch =>
                batch.Medicine.Name.Contains(term) ||
                batch.BatchNumber.Contains(term) ||
                batch.Medicine.GenericName != null && batch.Medicine.GenericName.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(batch => batch.Medicine.Name)
            .ThenBy(batch => batch.ExpiryDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(batch => new MedicineStockItemDto(
                batch.MedicineId,
                batch.Medicine.Name,
                batch.BatchNumber,
                batch.Medicine.Strength,
                batch.ExpiryDate,
                batch.QuantityOnHand,
                batch.SellingPrice))
            .ToListAsync(cancellationToken);

        return new PharmacyStockPageDto(items, totalCount);
    }

    public async Task<PharmacyLookupDto> GetLookupAsync(CancellationToken cancellationToken)
    {
        var medicines = (await dbContext.Medicines
                .AsNoTracking()
                .Where(medicine => medicine.Status == MedicineStatus.Active)
                .OrderBy(medicine => medicine.Name)
                .ThenBy(medicine => medicine.Strength)
                .ToListAsync(cancellationToken))
            .Select(medicine => new PharmacyMedicineOptionDto(medicine.Id, medicine.Name, medicine.Strength, medicine.Form.ToString()))
            .ToList();

        var suppliers = await dbContext.Suppliers
            .AsNoTracking()
            .Where(supplier => supplier.IsActive)
            .OrderBy(supplier => supplier.Name)
            .Select(supplier => new SupplierOptionDto(supplier.Id, supplier.Name))
            .ToListAsync(cancellationToken);

        var categories = await dbContext.Medicines
            .AsNoTracking()
            .Select(medicine => medicine.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToListAsync(cancellationToken);

        return new PharmacyLookupDto(medicines, suppliers, categories);
    }

    // ---------------------------------------------------------------- medicines

    public async Task<MedicinePageDto> GetMedicinesAsync(
        string? search,
        MedicineStatus? status,
        string? category,
        string? stockFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var today = PharmacyRules.Today();

        var query = dbContext.Medicines.AsNoTracking().AsQueryable();

        if (status is not null)
            query = query.Where(medicine => medicine.Status == status);

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryTerm = category.Trim().ToLower();
            query = query.Where(medicine => medicine.Category.ToLower() == categoryTerm);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(medicine =>
                medicine.Name.ToLower().Contains(term) ||
                (medicine.GenericName != null && medicine.GenericName.ToLower().Contains(term)) ||
                (medicine.Manufacturer != null && medicine.Manufacturer.ToLower().Contains(term)) ||
                medicine.Category.ToLower().Contains(term));
        }

        var projected = query.Select(medicine => new
        {
            Medicine = medicine,
            Usable = medicine.Batches.Where(batch => batch.ExpiryDate >= today).Sum(batch => batch.QuantityOnHand),
            Expired = medicine.Batches.Where(batch => batch.ExpiryDate < today).Sum(batch => batch.QuantityOnHand),
        });

        projected = (stockFilter ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "low" => projected.Where(row => row.Medicine.Status == MedicineStatus.Active && row.Usable <= row.Medicine.ReorderLevel),
            "out" => projected.Where(row => row.Usable == 0),
            "expired" => projected.Where(row => row.Expired > 0),
            _ => projected,
        };

        var totalCount = await projected.CountAsync(cancellationToken);

        var rows = await projected
            .OrderBy(row => row.Medicine.Name)
            .ThenBy(row => row.Medicine.Strength)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new MedicineListItemDto(
                row.Medicine.Id,
                row.Medicine.Name,
                row.Medicine.GenericName,
                row.Medicine.Category,
                row.Medicine.Manufacturer,
                row.Medicine.Strength,
                row.Medicine.Form.ToString(),
                row.Medicine.ReorderLevel,
                row.Medicine.Status.ToString(),
                row.Usable,
                row.Expired,
                row.Medicine.Status == MedicineStatus.Active && row.Usable <= row.Medicine.ReorderLevel))
            .ToList();

        return new MedicinePageDto(items, totalCount, page, pageSize);
    }

    public async Task<MedicineDetailDto> GetMedicineAsync(Guid id, CancellationToken cancellationToken)
    {
        var medicine = await dbContext.Medicines
            .AsNoTracking()
            .Include(item => item.Batches)
                .ThenInclude(batch => batch.Supplier)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (medicine is null)
            throw new NotFoundException(nameof(Medicine), id);

        return MapMedicineDetail(medicine, PharmacyRules.Today());
    }

    public async Task<MedicineDetailDto> CreateMedicineAsync(MedicineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values = NormalizeMedicine(request);

        if (await MedicineExistsAsync(values.Name, values.Strength, values.Form, excludeId: null, cancellationToken))
            throw new ConflictException($"A {values.Form} named '{values.Name}' with strength '{values.Strength}' already exists.");

        var medicine = new Medicine
        {
            Name = values.Name,
            GenericName = values.GenericName,
            Category = values.Category,
            Manufacturer = values.Manufacturer,
            Strength = values.Strength,
            Form = values.Form,
            ReorderLevel = request.ReorderLevel,
            Status = MedicineStatus.Active,
        };

        dbContext.Medicines.Add(medicine);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw new ConflictException("This medicine already exists.");
        }

        return await GetMedicineAsync(medicine.Id, cancellationToken);
    }

    public async Task<MedicineDetailDto> UpdateMedicineAsync(Guid id, MedicineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values = NormalizeMedicine(request);

        var medicine = await dbContext.Medicines.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (medicine is null)
            throw new NotFoundException(nameof(Medicine), id);

        var identityChanged =
            !string.Equals(medicine.Strength, values.Strength, StringComparison.OrdinalIgnoreCase) ||
            medicine.Form != values.Form;

        if (identityChanged)
        {
            // Strength and form describe the physical product on the shelf and on past prescriptions.
            var inUse = await dbContext.MedicineBatches.AnyAsync(batch => batch.MedicineId == id, cancellationToken) ||
                        await dbContext.PrescriptionItems.AnyAsync(item => item.MedicineId == id, cancellationToken);
            if (inUse)
                throw new ConflictException("Strength and form cannot be changed once the medicine has stock or prescriptions. Add it as a new medicine instead.");
        }

        if (await MedicineExistsAsync(values.Name, values.Strength, values.Form, excludeId: id, cancellationToken))
            throw new ConflictException($"A {values.Form} named '{values.Name}' with strength '{values.Strength}' already exists.");

        medicine.Name = values.Name;
        medicine.GenericName = values.GenericName;
        medicine.Category = values.Category;
        medicine.Manufacturer = values.Manufacturer;
        medicine.Strength = values.Strength;
        medicine.Form = values.Form;
        medicine.ReorderLevel = request.ReorderLevel;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw new ConflictException("Another medicine with the same name, strength and form already exists.");
        }

        return await GetMedicineAsync(id, cancellationToken);
    }

    public async Task<MedicineDetailDto> SetMedicineStatusAsync(Guid id, MedicineStatus status, CancellationToken cancellationToken)
    {
        var medicine = await dbContext.Medicines.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (medicine is null)
            throw new NotFoundException(nameof(Medicine), id);

        if (medicine.Status != status)
        {
            medicine.Status = status;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await GetMedicineAsync(id, cancellationToken);
    }

    // ---------------------------------------------------------------- suppliers

    public async Task<SupplierPageDto> GetSuppliersAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Suppliers.AsNoTracking().AsQueryable();

        if (isActive is not null)
            query = query.Where(supplier => supplier.IsActive == isActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(supplier =>
                supplier.Name.ToLower().Contains(term) ||
                (supplier.ContactPerson != null && supplier.ContactPerson.ToLower().Contains(term)) ||
                (supplier.Phone != null && supplier.Phone.ToLower().Contains(term)) ||
                (supplier.Email != null && supplier.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(supplier => supplier.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(supplier => new
            {
                Supplier = supplier,
                BatchCount = dbContext.MedicineBatches.Count(batch => batch.SupplierId == supplier.Id),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => MapSupplier(row.Supplier, row.BatchCount)).ToList();
        return new SupplierPageDto(items, totalCount, page, pageSize);
    }

    public async Task<SupplierDto> CreateSupplierAsync(SupplierRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = RequireText(request.Name, "Supplier name");

        if (await dbContext.Suppliers.AnyAsync(item => item.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"A supplier named '{name}' already exists.");

        var supplier = new Supplier { IsActive = true };
        ApplySupplier(supplier, request, name);
        dbContext.Suppliers.Add(supplier);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw new ConflictException($"A supplier named '{name}' already exists.");
        }

        return MapSupplier(supplier, 0);
    }

    public async Task<SupplierDto> UpdateSupplierAsync(Guid id, SupplierRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = RequireText(request.Name, "Supplier name");

        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (supplier is null)
            throw new NotFoundException(nameof(Supplier), id);

        if (await dbContext.Suppliers.AnyAsync(item => item.Id != id && item.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException($"A supplier named '{name}' already exists.");

        ApplySupplier(supplier, request, name);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (DbErrors.IsUniqueViolation(exception))
        {
            throw new ConflictException($"A supplier named '{name}' already exists.");
        }

        var batchCount = await dbContext.MedicineBatches.CountAsync(batch => batch.SupplierId == id, cancellationToken);
        return MapSupplier(supplier, batchCount);
    }

    public async Task<SupplierDto> SetSupplierStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (supplier is null)
            throw new NotFoundException(nameof(Supplier), id);

        if (supplier.IsActive != isActive)
        {
            supplier.IsActive = isActive;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var batchCount = await dbContext.MedicineBatches.CountAsync(batch => batch.SupplierId == id, cancellationToken);
        return MapSupplier(supplier, batchCount);
    }

    public async Task DeleteSupplierAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (supplier is null)
            throw new NotFoundException(nameof(Supplier), id);

        if (await dbContext.MedicineBatches.AnyAsync(batch => batch.SupplierId == id, cancellationToken))
            throw new ConflictException("This supplier has supplied stock and cannot be deleted. Deactivate it instead.");

        dbContext.Suppliers.Remove(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- batches & ledger (read)

    public async Task<BatchPageDto> GetBatchesAsync(
        string? search,
        Guid? medicineId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var today = PharmacyRules.Today();
        var expiringLimit = today.AddDays(PharmacyRules.ExpiringSoonDays);

        var query = dbContext.MedicineBatches
            .AsNoTracking()
            .Include(batch => batch.Medicine)
            .Include(batch => batch.Supplier)
            .AsQueryable();

        if (medicineId is Guid medicineFilter && medicineFilter != Guid.Empty)
            query = query.Where(batch => batch.MedicineId == medicineFilter);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(batch =>
                batch.BatchNumber.ToLower().Contains(term) ||
                batch.Medicine.Name.ToLower().Contains(term) ||
                (batch.Medicine.GenericName != null && batch.Medicine.GenericName.ToLower().Contains(term)));
        }

        query = (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "usable" => query.Where(batch => batch.QuantityOnHand > 0 && batch.ExpiryDate >= today),
            "expired" => query.Where(batch => batch.QuantityOnHand > 0 && batch.ExpiryDate < today),
            "expiring" => query.Where(batch => batch.QuantityOnHand > 0 && batch.ExpiryDate >= today && batch.ExpiryDate <= expiringLimit),
            "depleted" => query.Where(batch => batch.QuantityOnHand == 0),
            _ => query,
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var batches = await query
            .OrderBy(batch => batch.Medicine.Name)
            .ThenBy(batch => batch.ExpiryDate)
            .ThenBy(batch => batch.BatchNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new BatchPageDto(batches.Select(batch => MapBatch(batch, today)).ToList(), totalCount, page, pageSize);
    }

    public async Task<PharmacyTransactionPageDto> GetTransactionsAsync(
        string? search,
        Guid? medicineId,
        Guid? batchId,
        PharmacyTransactionType? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PharmacyTransactions
            .AsNoTracking()
            .Include(item => item.MedicineBatch)
                .ThenInclude(batch => batch.Medicine)
            .Include(item => item.PerformedByUser)
            .Include(item => item.PrescriptionItem)
                .ThenInclude(item => item!.Prescription)
            .AsQueryable();

        if (medicineId is Guid medicineFilter && medicineFilter != Guid.Empty)
            query = query.Where(item => item.MedicineBatch.MedicineId == medicineFilter);

        if (batchId is Guid batchFilter && batchFilter != Guid.Empty)
            query = query.Where(item => item.MedicineBatchId == batchFilter);

        if (type is not null)
            query = query.Where(item => item.Type == type);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(item =>
                item.MedicineBatch.BatchNumber.ToLower().Contains(term) ||
                item.MedicineBatch.Medicine.Name.ToLower().Contains(term) ||
                (item.Notes != null && item.Notes.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(item => item.TransactionDate)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(item => new PharmacyTransactionDto(
                item.Id,
                item.Type.ToString(),
                item.QuantityChange,
                item.TransactionDate,
                item.MedicineBatch.MedicineId,
                item.MedicineBatch.Medicine.Name,
                item.MedicineBatch.BatchNumber,
                $"{item.PerformedByUser.FirstName} {item.PerformedByUser.LastName}",
                item.Notes,
                item.PrescriptionItem?.Prescription.PrescriptionNumber))
            .ToList();

        return new PharmacyTransactionPageDto(items, totalCount, page, pageSize);
    }

    // ---------------------------------------------------------------- helpers

    internal static BatchListItemDto MapBatch(MedicineBatch batch, DateOnly today) => new(
        batch.Id,
        batch.MedicineId,
        batch.Medicine.Name,
        batch.Medicine.Strength,
        batch.Medicine.Form.ToString(),
        batch.SupplierId,
        batch.Supplier?.Name,
        batch.BatchNumber,
        batch.ExpiryDate,
        batch.ReceivedDate,
        batch.PurchasePrice,
        batch.SellingPrice,
        batch.QuantityReceived,
        batch.QuantityOnHand,
        PharmacyRules.IsExpired(batch.ExpiryDate, today),
        PharmacyRules.GetBatchStatus(batch.QuantityOnHand, batch.ExpiryDate, today));

    private static MedicineDetailDto MapMedicineDetail(Medicine medicine, DateOnly today)
    {
        foreach (var batch in medicine.Batches)
            batch.Medicine = medicine;

        var usable = medicine.Batches.Where(batch => !PharmacyRules.IsExpired(batch.ExpiryDate, today)).Sum(batch => batch.QuantityOnHand);
        var expired = medicine.Batches.Where(batch => PharmacyRules.IsExpired(batch.ExpiryDate, today)).Sum(batch => batch.QuantityOnHand);

        return new MedicineDetailDto(
            medicine.Id,
            medicine.Name,
            medicine.GenericName,
            medicine.Category,
            medicine.Manufacturer,
            medicine.Strength,
            medicine.Form.ToString(),
            medicine.ReorderLevel,
            medicine.Status.ToString(),
            usable,
            expired,
            medicine.Status == MedicineStatus.Active && usable <= medicine.ReorderLevel,
            medicine.CreatedAt,
            medicine.UpdatedAt,
            medicine.Batches
                .OrderBy(batch => batch.ExpiryDate)
                .ThenBy(batch => batch.BatchNumber)
                .Select(batch => MapBatch(batch, today))
                .ToList());
    }

    private static SupplierDto MapSupplier(Supplier supplier, int batchCount) => new(
        supplier.Id,
        supplier.Name,
        supplier.ContactPerson,
        supplier.Phone,
        supplier.Email,
        supplier.Address,
        supplier.IsActive,
        batchCount,
        supplier.CreatedAt);

    private static void ApplySupplier(Supplier supplier, SupplierRequest request, string name)
    {
        supplier.Name = name;
        supplier.ContactPerson = NullIfBlank(request.ContactPerson);
        supplier.Phone = NullIfBlank(request.Phone);
        supplier.Email = NullIfBlank(request.Email);
        supplier.Address = NullIfBlank(request.Address);
    }

    private async Task<bool> MedicineExistsAsync(string name, string strength, MedicineForm form, Guid? excludeId, CancellationToken cancellationToken)
    {
        var lowerName = name.ToLower();
        var lowerStrength = strength.ToLower();
        return await dbContext.Medicines.AnyAsync(item =>
            (excludeId == null || item.Id != excludeId) &&
            item.Form == form &&
            item.Name.ToLower() == lowerName &&
            item.Strength.ToLower() == lowerStrength,
            cancellationToken);
    }

    private static (string Name, string? GenericName, string Category, string? Manufacturer, string Strength, MedicineForm Form) NormalizeMedicine(MedicineRequest request)
    {
        var form = request.Form ?? throw new RequestValidationException("Medicine form is required.");
        return (
            RequireText(request.Name, "Medicine name"),
            NullIfBlank(request.GenericName),
            RequireText(request.Category, "Category"),
            NullIfBlank(request.Manufacturer),
            RequireText(request.Strength, "Strength"),
            form);
    }

    private static string RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RequestValidationException($"{label} is required.");
        return value.Trim();
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
