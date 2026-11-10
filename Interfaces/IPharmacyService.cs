using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.Interfaces;

public interface IPharmacyService
{
    Task<PharmacyStockPageDto> GetStockAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<PharmacyLookupDto> GetLookupAsync(CancellationToken cancellationToken);

    Task<MedicinePageDto> GetMedicinesAsync(string? search, MedicineStatus? status, string? category, string? stockFilter, int page, int pageSize, CancellationToken cancellationToken);
    Task<MedicineDetailDto> GetMedicineAsync(Guid id, CancellationToken cancellationToken);
    Task<MedicineDetailDto> CreateMedicineAsync(MedicineRequest request, CancellationToken cancellationToken);
    Task<MedicineDetailDto> UpdateMedicineAsync(Guid id, MedicineRequest request, CancellationToken cancellationToken);
    Task<MedicineDetailDto> SetMedicineStatusAsync(Guid id, MedicineStatus status, CancellationToken cancellationToken);

    Task<SupplierPageDto> GetSuppliersAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);
    Task<SupplierDto> CreateSupplierAsync(SupplierRequest request, CancellationToken cancellationToken);
    Task<SupplierDto> UpdateSupplierAsync(Guid id, SupplierRequest request, CancellationToken cancellationToken);
    Task<SupplierDto> SetSupplierStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken);
    Task DeleteSupplierAsync(Guid id, CancellationToken cancellationToken);

    Task<BatchPageDto> GetBatchesAsync(string? search, Guid? medicineId, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<PharmacyTransactionPageDto> GetTransactionsAsync(string? search, Guid? medicineId, Guid? batchId, PharmacyTransactionType? type, int page, int pageSize, CancellationToken cancellationToken);
    
}
