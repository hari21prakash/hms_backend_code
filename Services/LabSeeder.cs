using HospitalManagement.Api.Data;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

/// <summary>Optional demo lab catalog. Only runs when SeedDemoData=true; safe to run repeatedly.</summary>
public static class LabSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var tests = new[]
        {
            new LabTest
            {
                Code = "CBC",
                Name = "Complete Blood Count",
                Category = "Hematology",
                Description = "Measures red cells, white cells, and platelets.",
                Price = 40m,
                NormalRange = "WBC 4.0-11.0 x10^9/L",
                Unit = "x10^9/L",
                IsActive = true,
            },
            new LabTest
            {
                Code = "BMP",
                Name = "Basic Metabolic Panel",
                Category = "Chemistry",
                Description = "Measures blood glucose and kidney function markers.",
                Price = 65m,
                NormalRange = "Glucose 70-110 mg/dL",
                Unit = "mg/dL",
                IsActive = true,
            },
            new LabTest
            {
                Code = "LFT",
                Name = "Liver Function Test",
                Category = "Chemistry",
                Description = "Assesses liver enzyme activity and liver function.",
                Price = 58m,
                NormalRange = "ALT 7-56 U/L",
                Unit = "U/L",
                IsActive = true,
            },
            new LabTest
            {
                Code = "URINALYSIS",
                Name = "Urinalysis",
                Category = "Urine",
                Description = "Checks for infection, blood, and glucose in urine.",
                Price = 30m,
                NormalRange = "Protein negative",
                Unit = "-",
                IsActive = true,
            }
        };

        // Idempotent: add only the demo tests whose code does not exist yet.
        var existingCodes = (await dbContext.LabTests.Select(test => test.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = tests.Where(test => !existingCodes.Contains(test.Code)).ToList();
        if (missing.Count == 0)
            return;

        dbContext.LabTests.AddRange(missing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
