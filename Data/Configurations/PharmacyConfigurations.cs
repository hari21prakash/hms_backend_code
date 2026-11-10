using HospitalManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalManagement.Api.Data.Configurations;

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.Property(p => p.PrescriptionNumber).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Instructions).HasMaxLength(2000);

        builder.HasIndex(p => p.PrescriptionNumber).IsUnique();
        builder.HasIndex(p => new { p.PatientId, p.PrescribedAt });
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.Patient).WithMany(pt => pt.Prescriptions)
            .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Doctor).WithMany()
            .HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Consultation).WithMany()
            .HasForeignKey(p => p.ConsultationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.Property(i => i.Dosage).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Frequency).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Route).IsRequired().HasMaxLength(50);
        builder.Property(i => i.Instructions).HasMaxLength(500);

        builder.HasOne(i => i.Prescription).WithMany(p => p.Items)
            .HasForeignKey(i => i.PrescriptionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Medicine).WithMany()
            .HasForeignKey(i => i.MedicineId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_PrescriptionItems_Quantities",
            "\"Quantity\" > 0 AND \"DurationDays\" > 0 AND \"QuantityDispensed\" >= 0 AND \"QuantityDispensed\" <= \"Quantity\""));
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(150);
        builder.Property(s => s.ContactPerson).HasMaxLength(100);
        builder.Property(s => s.Phone).HasMaxLength(30);
        builder.Property(s => s.Email).HasMaxLength(256);
        builder.Property(s => s.Address).HasMaxLength(500);

        builder.HasIndex(s => s.Name).IsUnique();
    }
}

public class MedicineConfiguration : IEntityTypeConfiguration<Medicine>
{
    public void Configure(EntityTypeBuilder<Medicine> builder)
    {
        builder.Property(m => m.Name).IsRequired().HasMaxLength(150);
        builder.Property(m => m.GenericName).HasMaxLength(150);
        builder.Property(m => m.Category).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Manufacturer).HasMaxLength(150);
        builder.Property(m => m.Strength).IsRequired().HasMaxLength(50);

        builder.HasIndex(m => new { m.Name, m.Strength, m.Form }).IsUnique();
        builder.HasIndex(m => m.GenericName);
        builder.HasIndex(m => m.Category);

        builder.ToTable(t => t.HasCheckConstraint("CK_Medicines_ReorderLevel", "\"ReorderLevel\" >= 0"));
    }
}

public class MedicineBatchConfiguration : IEntityTypeConfiguration<MedicineBatch>
{
    public void Configure(EntityTypeBuilder<MedicineBatch> builder)
    {
        builder.Property(b => b.BatchNumber).IsRequired().HasMaxLength(50);

        builder.HasIndex(b => new { b.MedicineId, b.BatchNumber }).IsUnique();
        builder.HasIndex(b => b.ExpiryDate);

        builder.HasOne(b => b.Medicine).WithMany(m => m.Batches)
            .HasForeignKey(b => b.MedicineId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Supplier).WithMany()
            .HasForeignKey(b => b.SupplierId).OnDelete(DeleteBehavior.Restrict);

        // Stock can never go negative, even if application logic has a bug.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_MedicineBatches_Stock",
            "\"QuantityOnHand\" >= 0 AND \"QuantityReceived\" >= 0 AND \"PurchasePrice\" >= 0 AND \"SellingPrice\" >= 0"));
    }
}

public class PharmacyTransactionConfiguration : IEntityTypeConfiguration<PharmacyTransaction>
{
    public void Configure(EntityTypeBuilder<PharmacyTransaction> builder)
    {
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasIndex(t => new { t.MedicineBatchId, t.TransactionDate });

        builder.HasOne(t => t.MedicineBatch).WithMany()
            .HasForeignKey(t => t.MedicineBatchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.PrescriptionItem).WithMany()
            .HasForeignKey(t => t.PrescriptionItemId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.PerformedByUser).WithMany()
            .HasForeignKey(t => t.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("CK_PharmacyTransactions_Quantity", "\"QuantityChange\" <> 0"));
    }
}
