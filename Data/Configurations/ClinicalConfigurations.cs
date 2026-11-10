using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalManagement.Api.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.Property(a => a.AppointmentNumber).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Reason).HasMaxLength(500);
        builder.Property(a => a.Notes).HasMaxLength(2000);
        builder.Property(a => a.CancellationReason).HasMaxLength(500);

        builder.HasIndex(a => a.AppointmentNumber).IsUnique();
        builder.HasIndex(a => new { a.PatientId, a.AppointmentDate });
        builder.HasIndex(a => new { a.AppointmentDate, a.Status });
        builder.HasIndex(a => a.DepartmentId);

        // Database-level guard against two live appointments starting at the same time for one doctor.
        // Overlapping (non-identical) ranges are rejected by the appointment service inside a transaction.
        builder.HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.StartTime }, "UX_Appointments_Doctor_Slot_Active")
            .IsUnique()
            .HasFilter($"\"Status\" NOT IN ('{AppointmentStatus.Cancelled}', '{AppointmentStatus.NoShow}')");

        builder.HasOne(a => a.Patient).WithMany(p => p.Appointments)
            .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Doctor).WithMany(d => d.Appointments)
            .HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Department).WithMany()
            .HasForeignKey(a => a.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.CreatedByUser).WithMany()
            .HasForeignKey(a => a.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("CK_Appointments_TimeRange", "\"EndTime\" > \"StartTime\""));
    }
}

public class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> builder)
    {
        builder.Property(c => c.ChiefComplaint).HasMaxLength(1000);
        builder.Property(c => c.Symptoms).HasMaxLength(4000);
        builder.Property(c => c.Examination).HasMaxLength(4000);
        builder.Property(c => c.Diagnosis).HasMaxLength(2000);
        builder.Property(c => c.TreatmentPlan).HasMaxLength(4000);
        builder.Property(c => c.DoctorNotes).HasMaxLength(4000);

        // One consultation per appointment (creates a unique index on AppointmentId).
        builder.HasOne(c => c.Appointment).WithOne(a => a.Consultation)
            .HasForeignKey<Consultation>(c => c.AppointmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> builder)
    {
        builder.Property(m => m.Diagnosis).IsRequired().HasMaxLength(2000);
        builder.Property(m => m.Symptoms).HasMaxLength(4000);
        builder.Property(m => m.Treatment).HasMaxLength(4000);
        builder.Property(m => m.Notes).HasMaxLength(4000);

        builder.HasIndex(m => new { m.PatientId, m.VisitDate });

        builder.HasOne(m => m.Patient).WithMany(p => p.MedicalRecords)
            .HasForeignKey(m => m.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Doctor).WithMany()
            .HasForeignKey(m => m.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Consultation).WithMany()
            .HasForeignKey(m => m.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(m => m.ConsultationId).IsUnique();
    }
}

public class MedicalRecordAttachmentConfiguration : IEntityTypeConfiguration<MedicalRecordAttachment>
{
    public void Configure(EntityTypeBuilder<MedicalRecordAttachment> builder)
    {
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
        builder.Property(a => a.StoredPath).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(100);

        builder.HasOne(a => a.MedicalRecord).WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MedicalRecordId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedByUser).WithMany()
            .HasForeignKey(a => a.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
