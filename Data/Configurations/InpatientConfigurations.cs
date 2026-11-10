using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalManagement.Api.Data.Configurations;

public class WardConfiguration : IEntityTypeConfiguration<Ward>
{
    public void Configure(EntityTypeBuilder<Ward> builder)
    {
        builder.Property(w => w.Name).IsRequired().HasMaxLength(100);
        builder.Property(w => w.Code).IsRequired().HasMaxLength(20);
        builder.Property(w => w.Description).HasMaxLength(500);

        builder.HasIndex(w => w.Name).IsUnique();
        builder.HasIndex(w => w.Code).IsUnique();

        builder.HasOne(w => w.Department).WithMany()
            .HasForeignKey(w => w.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.Property(r => r.RoomNumber).IsRequired().HasMaxLength(20);

        builder.HasIndex(r => r.RoomNumber).IsUnique();

        builder.HasOne(r => r.Ward).WithMany(w => w.Rooms)
            .HasForeignKey(r => r.WardId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("CK_Rooms_DailyRate", "\"DailyRate\" >= 0"));
    }
}

public class BedConfiguration : IEntityTypeConfiguration<Bed>
{
    public void Configure(EntityTypeBuilder<Bed> builder)
    {
        builder.Property(b => b.BedNumber).IsRequired().HasMaxLength(30);

        builder.HasIndex(b => b.BedNumber).IsUnique();
        builder.HasIndex(b => b.Status);

        builder.HasOne(b => b.Room).WithMany(r => r.Beds)
            .HasForeignKey(b => b.RoomId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AdmissionConfiguration : IEntityTypeConfiguration<Admission>
{
    public void Configure(EntityTypeBuilder<Admission> builder)
    {
        builder.Property(a => a.AdmissionNumber).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Reason).IsRequired().HasMaxLength(1000);

        builder.HasIndex(a => a.AdmissionNumber).IsUnique();
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.PatientId);

        // A patient can have only one active admission at a time.
        builder.HasIndex(a => a.PatientId, "UX_Admissions_PatientId_Active")
            .IsUnique()
            .HasFilter($"\"Status\" IN ('{AdmissionStatus.Admitted}', '{AdmissionStatus.UnderTreatment}')");

        builder.HasOne(a => a.Patient).WithMany(p => p.Admissions)
            .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Doctor).WithMany()
            .HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Department).WithMany()
            .HasForeignKey(a => a.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class BedAssignmentConfiguration : IEntityTypeConfiguration<BedAssignment>
{
    public void Configure(EntityTypeBuilder<BedAssignment> builder)
    {
        builder.HasIndex(b => b.BedId);
        builder.HasIndex(b => b.AdmissionId);

        // An occupied bed cannot be assigned twice: only one open assignment per bed.
        builder.HasIndex(b => b.BedId, "UX_BedAssignments_BedId_Open")
            .IsUnique()
            .HasFilter("\"ReleasedAt\" IS NULL");

        // An admission has exactly one current bed.
        builder.HasIndex(b => b.AdmissionId, "UX_BedAssignments_AdmissionId_Open")
            .IsUnique()
            .HasFilter("\"ReleasedAt\" IS NULL");

        builder.HasOne(b => b.Admission).WithMany(a => a.BedAssignments)
            .HasForeignKey(b => b.AdmissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Bed).WithMany()
            .HasForeignKey(b => b.BedId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("CK_BedAssignments_Rate", "\"DailyRateSnapshot\" >= 0"));
    }
}

public class DischargeConfiguration : IEntityTypeConfiguration<Discharge>
{
    public void Configure(EntityTypeBuilder<Discharge> builder)
    {
        builder.Property(d => d.Summary).IsRequired().HasMaxLength(4000);
        builder.Property(d => d.ConditionAtDischarge).HasMaxLength(1000);
        builder.Property(d => d.FollowUpInstructions).HasMaxLength(2000);

        // One discharge per admission (creates a unique index on AdmissionId).
        builder.HasOne(d => d.Admission).WithOne(a => a.Discharge)
            .HasForeignKey<Discharge>(d => d.AdmissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.DischargedByDoctor).WithMany()
            .HasForeignKey(d => d.DischargedByDoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}
