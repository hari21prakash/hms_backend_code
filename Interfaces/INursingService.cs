using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface INursingService
{
    Task<NursingPageDto> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<NursingLookupDto> GetLookupAsync(CancellationToken cancellationToken);
    Task<VitalSignListItemDto> CreateVitalSignAsync(CreateVitalSignRequest request, Guid actorUserId, CancellationToken cancellationToken);
    Task<NursingNoteListItemDto> CreateNursingNoteAsync(CreateNursingNoteRequest request, Guid actorUserId, CancellationToken cancellationToken);
}
