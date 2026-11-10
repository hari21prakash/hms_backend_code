using HospitalManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalManagement.Api.Data.Configurations;

public class VitalSignConfiguration : IEntityTypeConfiguration<VitalSign>
{
    public void Configure(EntityTypeBuilder<VitalSign> builder)
    {
        builder.Property(v => v.TemperatureCelsius).HasPrecision(4, 1);
        builder.Property(v => v.WeightKg).HasPrecision(6, 2);
        builder.Property(v => v.HeightCm).HasPrecision(5, 1);

        builder.HasIndex(v => new { v.PatientId, v.RecordedAt });

        builder.HasOne(v => v.Patient).WithMany(p => p.VitalSigns)
            .HasForeignKey(v => v.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Admission).WithMany()
            .HasForeignKey(v => v.AdmissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.RecordedByUser).WithMany()
            .HasForeignKey(v => v.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NursingNoteConfiguration : IEntityTypeConfiguration<NursingNote>
{
    public void Configure(EntityTypeBuilder<NursingNote> builder)
    {
        builder.Property(n => n.Note).IsRequired().HasMaxLength(4000);

        builder.HasIndex(n => new { n.PatientId, n.CreatedAt });

        builder.HasOne(n => n.Patient).WithMany()
            .HasForeignKey(n => n.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Admission).WithMany()
            .HasForeignKey(n => n.AdmissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.NurseUser).WithMany()
            .HasForeignKey(n => n.NurseUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
