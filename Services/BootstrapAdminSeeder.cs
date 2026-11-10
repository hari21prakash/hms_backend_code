using System.Text;
using HospitalManagement.Api.Data;
using HospitalManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public static class BootstrapAdminSeeder
{
    public static async Task SeedAsync(IConfiguration configuration, AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var email = configuration["BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
            return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("BootstrapAdmin:Email and BootstrapAdmin:Password must both be configured.");
        if (password.Length < 16 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new InvalidOperationException("The bootstrap administrator password must be at least 16 characters and no more than 72 UTF-8 bytes.");

        var role = await dbContext.Roles.SingleAsync(candidate => candidate.Name == "Super Admin", cancellationToken);
        var existing = await dbContext.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
        if (existing is not null)
        {
            if (existing.RoleId != role.Id)
                throw new InvalidOperationException("The configured bootstrap email is already assigned to a different role.");
            return;
        }

        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = configuration["BootstrapAdmin:FirstName"]?.Trim() is { Length: > 0 } firstName ? firstName : "System",
            LastName = configuration["BootstrapAdmin:LastName"]?.Trim() is { Length: > 0 } lastName ? lastName : "Administrator",
            RoleId = role.Id,
            IsActive = true
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}