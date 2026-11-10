using HospitalManagement.Api.Data;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>Optional demo ward, room and beds. Only runs when SeedDemoData=true; safe to run repeatedly.</summary>
public static class AdmissionSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var department = await dbContext.Departments
            .SingleOrDefaultAsync(item => item.Code == "GEN", cancellationToken);
        if (department is null)
        {
            department = new Department
            {
                Name = "General Medicine",
                Code = "GEN",
                Description = "Primary inpatient and consultation department",
                IsActive = true,
            };

            dbContext.Departments.Add(department);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var ward = await dbContext.Wards.SingleOrDefaultAsync(item => item.Code == "GEN-WARD", cancellationToken);
        if (ward is null)
        {
            ward = new Ward
            {
                Name = "General Ward",
                Code = "GEN-WARD",
                Type = WardType.General,
                Description = "Standard inpatient ward",
                DepartmentId = department.Id,
                IsActive = true,
            };

            dbContext.Wards.Add(ward);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var room = await dbContext.Rooms
            .SingleOrDefaultAsync(item => item.WardId == ward.Id && item.RoomNumber == "A-101", cancellationToken);
        if (room is null)
        {
            room = new Room
            {
                WardId = ward.Id,
                RoomNumber = "A-101",
                Floor = 1,
                DailyRate = 180m,
                IsActive = true,
            };

            dbContext.Rooms.Add(room);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var existingBeds = (await dbContext.Beds
                .Where(bed => bed.RoomId == room.Id)
                .Select(bed => bed.BedNumber)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingBeds = new[] { "A-101-1", "A-101-2", "A-101-3" }
            .Where(number => !existingBeds.Contains(number))
            .Select(number => new Bed { RoomId = room.Id, BedNumber = number, Status = BedStatus.Available })
            .ToList();

        if (missingBeds.Count == 0)
            return;

        dbContext.Beds.AddRange(missingBeds);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
