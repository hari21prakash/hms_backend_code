using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

/// <summary>Every operation here changes stock or batch data, runs in a transaction and writes the pharmacy ledger.</summary>
public interface IPharmacyStockService
{
    Task<BatchListItemDto> ReceiveStockAsync(ReceiveStockRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<BatchListItemDto> UpdateBatchAsync(Guid batchId, UpdateBatchRequest request, CancellationToken cancellationToken);
    Task<BatchListItemDto> AdjustStockAsync(Guid batchId, AdjustStockRequest request, Guid actorUserId, CancellationToken cancellationToken);
    //Task<BatchListItemDto> AdjustStockAsync(Guid batchId, AdjustStockRequest request, Guid actorUserId, CancellationToken cancellationToken);
}
