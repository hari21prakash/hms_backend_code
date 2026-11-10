using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IReportService
{
    Task<ReportsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}
