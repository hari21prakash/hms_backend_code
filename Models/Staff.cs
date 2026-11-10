using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.Models;

public class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<DoctorDepartment> DoctorDepartments { get; set; } = new List<DoctorDepartment>();
}

/// <summary>
/// Doctor profile. Name, email and phone live on the linked User to avoid duplication.
/// </summary>
public class Doctor : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string EmployeeId { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
    public decimal ConsultationFee { get; set; }
    public DateOnly JoiningDate { get; set; }
    public StaffStatus Status { get; set; } = StaffStatus.Active;
    public string? ProfilePhotoPath { get; set; }

    public ICollection<DoctorDepartment> DoctorDepartments { get; set; } = new List<DoctorDepartment>();
    public ICollection<DoctorAvailability> Availabilities { get; set; } = new List<DoctorAvailability>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

/// <summary>Many-to-many join (composite key). Exactly one row per doctor may be primary.</summary>
public class DoctorDepartment
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public bool IsPrimary { get; set; }
}

/// <summary>A recurring weekly working window. A doctor may have several per day (e.g. morning + afternoon).</summary>
public class DoctorAvailability : BaseEntity
{
    public Guid DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int SlotDurationMinutes { get; set; } = 15;
    public bool IsActive { get; set; } = true;
}

public class Nurse : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string EmployeeId { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public NurseShift Shift { get; set; } = NurseShift.Morning;
    public DateOnly JoiningDate { get; set; }
    public StaffStatus Status { get; set; } = StaffStatus.Active;

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid? WardId { get; set; }
    public Ward? Ward { get; set; }
}
