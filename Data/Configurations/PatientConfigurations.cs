using HospitalManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalManagement.Api.Data.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.Property(p => p.PatientNumber).IsRequired().HasMaxLength(20);
        builder.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.LastName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Phone).IsRequired().HasMaxLength(30);
        builder.Property(p => p.Email).HasMaxLength(256);
        builder.Property(p => p.Address).IsRequired().HasMaxLength(300);
        builder.Property(p => p.City).IsRequired().HasMaxLength(100);
        builder.Property(p => p.State).IsRequired().HasMaxLength(100);
        builder.Property(p => p.PostalCode).HasMaxLength(20);
        builder.Property(p => p.EmergencyContactName).IsRequired().HasMaxLength(150);
        builder.Property(p => p.EmergencyContactPhone).IsRequired().HasMaxLength(30);
        builder.Property(p => p.InsuranceProvider).HasMaxLength(150);
        builder.Property(p => p.InsuranceNumber).HasMaxLength(100);
        builder.Property(p => p.ProfilePhotoPath).HasMaxLength(500);

        builder.HasIndex(p => p.PatientNumber).IsUnique();
        builder.HasIndex(p => p.Phone);
        builder.HasIndex(p => p.Email);
        builder.HasIndex(p => new { p.LastName, p.FirstName });
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.User).WithMany()
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.UserId).IsUnique();
    }
}

public class PatientAllergyConfiguration : IEntityTypeConfiguration<PatientAllergy>
{
    public void Configure(EntityTypeBuilder<PatientAllergy> builder)
    {
        builder.Property(a => a.Allergen).IsRequired().HasMaxLength(150);
        builder.Property(a => a.Reaction).HasMaxLength(300);

        builder.HasOne(a => a.Patient).WithMany(p => p.Allergies)
            .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Cascade);
    }
}
