using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Identity & access
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // People & organisation
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorDepartment> DoctorDepartments => Set<DoctorDepartment>();
    public DbSet<DoctorAvailability> DoctorAvailabilities => Set<DoctorAvailability>();
    public DbSet<Nurse> Nurses => Set<Nurse>();

    // Clinical
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<MedicalRecordAttachment> MedicalRecordAttachments => Set<MedicalRecordAttachment>();

    // Pharmacy
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicineBatch> MedicineBatches => Set<MedicineBatch>();
    public DbSet<PharmacyTransaction> PharmacyTransactions => Set<PharmacyTransaction>();

    // Laboratory
    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<LabResult> LabResults => Set<LabResult>();

    // Inpatient
    public DbSet<Ward> Wards => Set<Ward>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<Admission> Admissions => Set<Admission>();
    public DbSet<BedAssignment> BedAssignments => Set<BedAssignment>();
    public DbSet<Discharge> Discharges => Set<Discharge>();

    // Nursing
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<NursingNote> NursingNotes => Set<NursingNote>();

    // Billing
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();

    // System
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Money and other decimals default to 12,2 (vitals override this explicitly).
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);

        // Store enums as readable strings so the database stays self-describing and migration-safe.
        configurationBuilder.Properties<Gender>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<BloodGroup>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<PatientStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<StaffStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<NurseShift>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<AllergySeverity>().HaveConversion<string>().HaveMaxLength(30);

        configurationBuilder.Properties<AppointmentType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<AppointmentStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<PrescriptionStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<MedicineForm>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<MedicineStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<PharmacyTransactionType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<LabOrderStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<LabPriority>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<AdmissionStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<BedStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<WardType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<DischargeType>().HaveConversion<string>().HaveMaxLength(30);

        configurationBuilder.Properties<InvoiceStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<InvoiceItemType>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(30);
        configurationBuilder.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var sequence in DbSequences.All)
            modelBuilder.HasSequence<long>(sequence).StartsAt(1).IncrementsBy(1);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
