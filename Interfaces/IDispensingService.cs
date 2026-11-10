using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IDispensingService
{
    Task<DispenseWorksheetDto> GetWorksheetAsync(Guid prescriptionId, CancellationToken cancellationToken);
    Task<DispenseWorksheetDto> DispenseAsync(DispenseRequest request, Guid actorUserId, CancellationToken cancellationToken);
}
