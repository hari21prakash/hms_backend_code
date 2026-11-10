using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>Manages the physical inpatient structure: Ward -> Room -> Bed.</summary>
public class FacilityService(AppDbContext dbContext) : IFacilityService
{
    public async Task<IReadOnlyList<WardOverviewDto>> GetWardsAsync(CancellationToken cancellationToken)
    {
        var wards = await dbContext.Wards
            .AsNoTracking()
            .Include(ward => ward.Rooms)
                .ThenInclude(room => room.Beds)
            .Where(ward => ward.IsActive)
            .OrderBy(ward => ward.Name)
            .ToListAsync(cancellationToken);

        return wards.Select(ToDto).ToList();
    }

    public async Task<WardOverviewDto> CreateWardAsync(CreateWardRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var code = request.Code.Trim().ToUpperInvariant();
        if (name.Length == 0 || code.Length == 0)
            throw new RequestValidationException("Ward name and code are required.");

        if (await dbContext.Wards.AnyAsync(w => w.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new ConflictException("A ward with this name already exists.");
        if (await dbContext.Wards.AnyAsync(w => w.Code == code, cancellationToken))
            throw new ConflictException("A ward with this code already exists.");

        var ward = new Ward
        {
            Name = name,
            Code = code,
            Type = request.Type,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
        };
        dbContext.Wards.Add(ward);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(ward);
    }

    public async Task<RoomOverviewDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var roomNumber = request.RoomNumber.Trim();
        if (roomNumber.Length == 0)
            throw new RequestValidationException("Room number is required.");
        if (request.DailyRate < 0)
            throw new RequestValidationException("Daily rate cannot be negative.");

        if (!await dbContext.Wards.AnyAsync(w => w.Id == request.WardId && w.IsActive, cancellationToken))
            throw new NotFoundException(nameof(Ward), request.WardId);
        if (await dbContext.Rooms.AnyAsync(r => r.RoomNumber.ToLower() == roomNumber.ToLower(), cancellationToken))
            throw new ConflictException("A room with this number already exists.");

        var room = new Room
        {
            WardId = request.WardId,
            RoomNumber = roomNumber,
            Floor = request.Floor,
            DailyRate = request.DailyRate,
            IsActive = true,
        };
        dbContext.Rooms.Add(room);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RoomOverviewDto(room.Id, room.RoomNumber, room.Floor, room.DailyRate, []);
    }

    public async Task<IReadOnlyList<BedOverviewDto>> CreateBedsAsync(CreateBedsRequest request, CancellationToken cancellationToken)
    {
        var room = await dbContext.Rooms
            .SingleOrDefaultAsync(r => r.Id == request.RoomId && r.IsActive, cancellationToken);
        if (room is null)
            throw new NotFoundException(nameof(Room), request.RoomId);

        var existing = (await dbContext.Beds.Select(b => b.BedNumber).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var numbers = new List<string>();
        var custom = request.BedNumber?.Trim();
        if (request.Count == 1 && !string.IsNullOrEmpty(custom))
        {
            numbers.Add(custom);
        }
        else
        {
            var next = 1;
            while (numbers.Count < request.Count)
            {
                var candidate = $"{room.RoomNumber}-{next++}";
                if (!existing.Contains(candidate))
                    numbers.Add(candidate);
            }
        }

        var clash = numbers.FirstOrDefault(existing.Contains);
        if (clash is not null)
            throw new ConflictException($"Bed number '{clash}' already exists.");

        var beds = numbers
            .Select(number => new Bed { RoomId = room.Id, BedNumber = number, Status = BedStatus.Available })
            .ToList();
        dbContext.Beds.AddRange(beds);
        await dbContext.SaveChangesAsync(cancellationToken);
        return beds.Select(b => new BedOverviewDto(b.Id, b.BedNumber, b.Status)).ToList();
    }

    public async Task<BedOverviewDto> UpdateBedStatusAsync(Guid bedId, UpdateBedStatusRequest request, CancellationToken cancellationToken)
    {
        var bed = await dbContext.Beds.SingleOrDefaultAsync(b => b.Id == bedId, cancellationToken);
        if (bed is null)
            throw new NotFoundException(nameof(Bed), bedId);

        // Occupied is controlled by admissions/discharges, never set by hand.
        if (request.Status == BedStatus.Occupied)
            throw new RequestValidationException("A bed becomes occupied automatically when a patient is admitted.");
        if (bed.Status == BedStatus.Occupied)
            throw new ConflictException("This bed is occupied. Discharge the patient before changing its status.");

        bed.Status = request.Status;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new BedOverviewDto(bed.Id, bed.BedNumber, bed.Status);
    }

    private static WardOverviewDto ToDto(Ward ward) => new(
        ward.Id,
        ward.Name,
        ward.Code,
        ward.Type,
        ward.Description,
        ward.Rooms
            .Where(room => room.IsActive)
            .OrderBy(room => room.RoomNumber)
            .Select(room => new RoomOverviewDto(
                room.Id,
                room.RoomNumber,
                room.Floor,
                room.DailyRate,
                room.Beds
                    .OrderBy(bed => bed.BedNumber)
                    .Select(bed => new BedOverviewDto(bed.Id, bed.BedNumber, bed.Status))
                    .ToList()))
            .ToList());
}
