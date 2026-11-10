using HospitalManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalManagement.Api.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Code).IsRequired().HasMaxLength(20);
        builder.Property(d => d.Description).HasMaxLength(500);

        builder.HasIndex(d => d.Name).IsUnique();
        builder.HasIndex(d => d.Code).IsUnique();
    }
}

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.Property(d => d.EmployeeId).IsRequired().HasMaxLength(30);
        builder.Property(d => d.Specialization).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Qualification).IsRequired().HasMaxLength(200);
        builder.Property(d => d.ProfilePhotoPath).HasMaxLength(500);

        builder.HasIndex(d => d.EmployeeId).IsUnique();
        builder.HasIndex(d => d.Specialization);
        builder.HasIndex(d => d.Status);

        builder.HasOne(d => d.User).WithMany()
            .HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(d => d.UserId).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Doctors_NonNegative", "\"ConsultationFee\" >= 0 AND \"YearsOfExperience\" >= 0"));
    }
}

public class DoctorDepartmentConfiguration : IEntityTypeConfiguration<DoctorDepartment>
{
    public void Configure(EntityTypeBuilder<DoctorDepartment> builder)
    {
        builder.HasKey(dd => new { dd.DoctorId, dd.DepartmentId });

        builder.HasOne(dd => dd.Doctor).WithMany(d => d.DoctorDepartments)
            .HasForeignKey(dd => dd.DoctorId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(dd => dd.Department).WithMany(d => d.DoctorDepartments)
            .HasForeignKey(dd => dd.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        // At most one primary department per doctor.
        builder.HasIndex(dd => dd.DoctorId, "UX_DoctorDepartments_DoctorId_Primary")
            .IsUnique()
            .HasFilter("\"IsPrimary\" = true");
    }
}

public class DoctorAvailabilityConfiguration : IEntityTypeConfiguration<DoctorAvailability>
{
    public void Configure(EntityTypeBuilder<DoctorAvailability> builder)
    {
        builder.HasOne(a => a.Doctor).WithMany(d => d.Availabilities)
            .HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.DoctorId, a.DayOfWeek });

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_DoctorAvailabilities_Window", "\"EndTime\" > \"StartTime\" AND \"SlotDurationMinutes\" > 0"));
    }
}

public class NurseConfiguration : IEntityTypeConfiguration<Nurse>
{
    public void Configure(EntityTypeBuilder<Nurse> builder)
    {
        builder.Property(n => n.EmployeeId).IsRequired().HasMaxLength(30);
        builder.Property(n => n.Qualification).IsRequired().HasMaxLength(200);

        builder.HasIndex(n => n.EmployeeId).IsUnique();

        builder.HasOne(n => n.User).WithMany()
            .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => n.UserId).IsUnique();

        builder.HasOne(n => n.Department).WithMany()
            .HasForeignKey(n => n.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Ward).WithMany()
            .HasForeignKey(n => n.WardId).OnDelete(DeleteBehavior.Restrict);
    }
}
