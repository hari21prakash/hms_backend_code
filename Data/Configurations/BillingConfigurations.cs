using HospitalManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalManagement.Api.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.Property(i => i.InvoiceNumber).IsRequired().HasMaxLength(20);
        builder.Property(i => i.Notes).HasMaxLength(1000);

        builder.HasIndex(i => i.InvoiceNumber).IsUnique();
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.InvoiceDate);
        builder.HasIndex(i => new { i.PatientId, i.InvoiceDate });

        builder.HasOne(i => i.Patient).WithMany(p => p.Invoices)
            .HasForeignKey(i => i.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Appointment).WithMany()
            .HasForeignKey(i => i.AppointmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Admission).WithMany()
            .HasForeignKey(i => i.AdmissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.CreatedByUser).WithMany()
            .HasForeignKey(i => i.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        // The billing service computes the amounts; these constraints guarantee they stay consistent.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Invoices_NonNegative",
                "\"SubtotalAmount\" >= 0 AND \"DiscountAmount\" >= 0 AND \"TaxAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"TotalAmount\" >= 0");
            t.HasCheckConstraint("CK_Invoices_Total",
                "\"TotalAmount\" = \"SubtotalAmount\" - \"DiscountAmount\" + \"TaxAmount\"");
            t.HasCheckConstraint("CK_Invoices_Balance",
                "\"BalanceAmount\" = \"TotalAmount\" - \"PaidAmount\"");
        });
    }
}

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.Property(i => i.Description).IsRequired().HasMaxLength(300);

        builder.HasOne(i => i.Invoice).WithMany(inv => inv.Items)
            .HasForeignKey(i => i.InvoiceId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.LabOrder).WithMany()
            .HasForeignKey(i => i.LabOrderId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Prescription).WithMany()
            .HasForeignKey(i => i.PrescriptionId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_InvoiceItems_Amounts",
            "\"Quantity\" > 0 AND \"UnitPrice\" >= 0 AND \"LineTotal\" = \"Quantity\" * \"UnitPrice\""));
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.PaymentNumber).IsRequired().HasMaxLength(20);
        builder.Property(p => p.TransactionReference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(500);

        builder.HasIndex(p => p.PaymentNumber).IsUnique();
        builder.HasIndex(p => p.TransactionReference).IsUnique();
        builder.HasIndex(p => p.PaymentDate);

        builder.HasOne(p => p.Invoice).WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ReceivedByUser).WithMany()
            .HasForeignKey(p => p.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" > 0"));
    }
}
