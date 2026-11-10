using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IPatientService
{
    Task<PatientPageDto> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task<PatientListItemDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PatientListItemDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The record of a signed-in Patient-role account. If the account predates automatic record creation
    /// (or the record was lost) it is created on the spot, so a portal account can never end up without details.
    /// Returns null for accounts that are not patient accounts.
    /// </summary>
    Task<PatientListItemDto?> GetOrCreateForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<PatientListItemDto> CreateAsync(CreatePatientRequest request, CancellationToken cancellationToken);
    Task<PatientListItemDto> UpdateMineAsync(Guid userId, UpdatePatientProfileRequest request, CancellationToken cancellationToken);
    Task<PatientListItemDto> UpdateAsync(Guid id, UpdatePatientProfileRequest request, CancellationToken cancellationToken);
}
