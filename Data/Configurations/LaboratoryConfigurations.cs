using HospitalManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalManagement.Api.Data.Configurations;

public class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> builder)
    {
        builder.Property(t => t.Code).IsRequired().HasMaxLength(30);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(150);
        builder.Property(t => t.Category).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.NormalRange).HasMaxLength(200);
        builder.Property(t => t.Unit).HasMaxLength(50);

        builder.HasIndex(t => t.Code).IsUnique();
        builder.HasIndex(t => t.Category);

        builder.ToTable(t => t.HasCheckConstraint("CK_LabTests_Price", "\"Price\" >= 0"));
    }
}

public class LabOrderConfiguration : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> builder)
    {
        builder.Property(o => o.OrderNumber).IsRequired().HasMaxLength(20);
        builder.Property(o => o.ClinicalNotes).HasMaxLength(1000);

        builder.HasIndex(o => o.OrderNumber).IsUnique();
        builder.HasIndex(o => new { o.Status, o.Priority });
        builder.HasIndex(o => new { o.PatientId, o.OrderedAt });

        builder.HasOne(o => o.Patient).WithMany(p => p.LabOrders)
            .HasForeignKey(o => o.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Doctor).WithMany()
            .HasForeignKey(o => o.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Consultation).WithMany()
            .HasForeignKey(o => o.ConsultationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.LabTest).WithMany()
            .HasForeignKey(o => o.LabTestId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.SampleCollectedByUser).WithMany()
            .HasForeignKey(o => o.SampleCollectedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class LabResultConfiguration : IEntityTypeConfiguration<LabResult>
{
    public void Configure(EntityTypeBuilder<LabResult> builder)
    {
        builder.Property(r => r.ResultValue).IsRequired().HasMaxLength(500);
        builder.Property(r => r.Unit).HasMaxLength(50);
        builder.Property(r => r.NormalRange).HasMaxLength(200);
        builder.Property(r => r.Remarks).HasMaxLength(1000);

        // One result per order (creates a unique index on LabOrderId).
        builder.HasOne(r => r.LabOrder).WithOne(o => o.Result)
            .HasForeignKey<LabResult>(r => r.LabOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TechnicianUser).WithMany()
            .HasForeignKey(r => r.TechnicianUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReviewedByDoctor).WithMany()
            .HasForeignKey(r => r.ReviewedByDoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}
