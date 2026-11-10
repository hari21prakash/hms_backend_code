using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IHealthService
{
    Task<HealthStatusDto> CheckAsync(CancellationToken cancellationToken);
}
