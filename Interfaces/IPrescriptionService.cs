using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IPrescriptionService
{
    Task<PrescriptionPageDto> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<PrescriptionLookupDto> GetLookupAsync(CancellationToken cancellationToken);
    Task<PrescriptionDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PrescriptionDetailDto> CreateAsync(CreatePrescriptionRequest request, CancellationToken cancellationToken);
}
