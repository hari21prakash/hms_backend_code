using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.Models;

public class Ward : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public WardType Type { get; set; } = WardType.General;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}

public class Room : BaseEntity
{
    public Guid WardId { get; set; }
    public Ward Ward { get; set; } = null!;

    public string RoomNumber { get; set; } = string.Empty;
    public int? Floor { get; set; }
    public decimal DailyRate { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Bed> Beds { get; set; } = new List<Bed>();
}

public class Bed : BaseEntity
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public string BedNumber { get; set; } = string.Empty;
    public BedStatus Status { get; set; } = BedStatus.Available;
}

public class Admission : BaseEntity
{
    public string AdmissionNumber { get; set; } = string.Empty;

    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public DateTime AdmittedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AdmissionStatus Status { get; set; } = AdmissionStatus.Admitted;

    public ICollection<BedAssignment> BedAssignments { get; set; } = new List<BedAssignment>();
    public Discharge? Discharge { get; set; }
}

/// <summary>
/// Bed history for an admission (supports transfers and per-day room charges).
/// The current bed is the assignment where ReleasedAt is null.
/// </summary>
public class BedAssignment : BaseEntity
{
    public Guid AdmissionId { get; set; }
    public Admission Admission { get; set; } = null!;

    public Guid BedId { get; set; }
    public Bed Bed { get; set; } = null!;

    public DateTime AssignedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }

    /// <summary>Room daily rate captured at assignment time so later rate changes do not alter past bills.</summary>
    public decimal DailyRateSnapshot { get; set; }
}

public class Discharge : BaseEntity
{
    public Guid AdmissionId { get; set; }
    public Admission Admission { get; set; } = null!;

    public DateTime DischargedAt { get; set; }
    public DischargeType Type { get; set; } = DischargeType.Recovered;
    public string Summary { get; set; } = string.Empty;
    public string? ConditionAtDischarge { get; set; }
    public string? FollowUpInstructions { get; set; }

    public Guid DischargedByDoctorId { get; set; }
    public Doctor DischargedByDoctor { get; set; } = null!;
}
