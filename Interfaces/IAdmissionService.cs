using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IAdmissionService
{
    Task<AdmissionPageDto> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdmissionLookupDto> GetLookupAsync(CancellationToken cancellationToken);
    Task<AdmissionDetailDto> CreateAsync(CreateAdmissionRequest request, CancellationToken cancellationToken);
    Task<DischargeResultDto> DischargeAsync(Guid admissionId, DischargeAdmissionRequest request, CancellationToken cancellationToken);
}
