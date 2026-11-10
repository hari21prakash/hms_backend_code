using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}
