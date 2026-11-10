using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

public interface IFacilityService
{
    Task<IReadOnlyList<WardOverviewDto>> GetWardsAsync(CancellationToken cancellationToken);
    Task<WardOverviewDto> CreateWardAsync(CreateWardRequest request, CancellationToken cancellationToken);
    Task<RoomOverviewDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BedOverviewDto>> CreateBedsAsync(CreateBedsRequest request, CancellationToken cancellationToken);
    Task<BedOverviewDto> UpdateBedStatusAsync(Guid bedId, UpdateBedStatusRequest request, CancellationToken cancellationToken);
}
