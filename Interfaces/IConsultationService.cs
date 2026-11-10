using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IConsultationService
{
    Task<ConsultationPageDto> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<ConsultationDetailDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ConsultationLookupDto> GetLookupAsync(CancellationToken cancellationToken);
    Task<ConsultationDetailDto> CreateAsync(CreateConsultationRequest request, Guid createdByUserId, CancellationToken cancellationToken);
    Task<ConsultationDetailDto> UpdateAsync(Guid id, UpdateConsultationRequest request, CancellationToken cancellationToken);
}
