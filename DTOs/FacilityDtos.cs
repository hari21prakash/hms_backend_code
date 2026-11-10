using System.ComponentModel.DataAnnotations;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.DTOs;

public class CreateWardRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    public WardType Type { get; set; } = WardType.General;

    [StringLength(500)]
    public string? Description { get; set; }
}

public class CreateRoomRequest
{
    [Required]
    public Guid WardId { get; set; }

    [Required, StringLength(20)]
    public string RoomNumber { get; set; } = string.Empty;

    public int? Floor { get; set; }

    [Range(0, 1000000)]
    public decimal DailyRate { get; set; }
}

public class CreateBedsRequest
{
    [Required]
    public Guid RoomId { get; set; }

    /// <summary>Optional. Only used when Count is 1; otherwise numbers are generated as "{RoomNumber}-{n}".</summary>
    [StringLength(30)]
    public string? BedNumber { get; set; }

    [Range(1, 50)]
    public int Count { get; set; } = 1;
}

public class UpdateBedStatusRequest
{
    [Required]
    public BedStatus Status { get; set; }
}

public record BedOverviewDto(Guid Id, string BedNumber, BedStatus Status);

public record RoomOverviewDto(
    Guid Id,
    string RoomNumber,
    int? Floor,
    decimal DailyRate,
    IReadOnlyList<BedOverviewDto> Beds);

public record WardOverviewDto(
    Guid Id,
    string Name,
    string Code,
    WardType Type,
    string? Description,
    IReadOnlyList<RoomOverviewDto> Rooms);
