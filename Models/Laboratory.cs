using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.Models;

public class LabTest : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? NormalRange { get; set; }
    public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>One ordered test. Ordering several tests creates several orders.</summary>
public class LabOrder : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public Guid? ConsultationId { get; set; }
    public Consultation? Consultation { get; set; }

    public Guid LabTestId { get; set; }
    public LabTest LabTest { get; set; } = null!;

    public DateTime OrderedAt { get; set; }
    public LabPriority Priority { get; set; } = LabPriority.Routine;
    public LabOrderStatus Status { get; set; } = LabOrderStatus.Ordered;
    public string? ClinicalNotes { get; set; }

    public DateTime? SampleCollectedAt { get; set; }
    public Guid? SampleCollectedByUserId { get; set; }
    public User? SampleCollectedByUser { get; set; }

    public LabResult? Result { get; set; }
}

public class LabResult : BaseEntity
{
    public Guid LabOrderId { get; set; }
    public LabOrder LabOrder { get; set; } = null!;

    public string ResultValue { get; set; } = string.Empty;
    public string? Unit { get; set; }
    /// <summary>Snapshot of the normal range at the time of the result.</summary>
    public string? NormalRange { get; set; }
    public bool IsAbnormal { get; set; }
    public string? Remarks { get; set; }
    public DateTime ResultDate { get; set; }

    public Guid TechnicianUserId { get; set; }
    public User TechnicianUser { get; set; } = null!;

    public Guid? ReviewedByDoctorId { get; set; }
    public Doctor? ReviewedByDoctor { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
