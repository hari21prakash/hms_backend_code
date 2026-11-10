using System.ComponentModel.DataAnnotations;
using HospitalManagement.Api.Models.Enums;

namespace HospitalManagement.Api.DTOs;

// ---------- Departments ----------
public class SaveDepartmentRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public record DepartmentDto(Guid Id, string Name, string Code, string? Description, bool IsActive, int DoctorCount);

// ---------- Staff profiles ----------
public record DoctorStaffDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string EmployeeId,
    string Specialization,
    string Qualification,
    int YearsOfExperience,
    decimal ConsultationFee,
    StaffStatus Status,
    Guid? DepartmentId,
    string? DepartmentName);

public class UpdateDoctorRequest
{
    [Required, StringLength(100)]
    public string Specialization { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Qualification { get; set; } = string.Empty;

    [Range(0, 70)]
    public int YearsOfExperience { get; set; }

    [Range(0, 1000000)]
    public decimal ConsultationFee { get; set; }

    public StaffStatus Status { get; set; } = StaffStatus.Active;

    public Guid? DepartmentId { get; set; }
}

public record NurseStaffDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    string EmployeeId,
    string Qualification,
    NurseShift Shift,
    StaffStatus Status,
    Guid? DepartmentId,
    string? DepartmentName,
    Guid? WardId,
    string? WardName);

public class UpdateNurseRequest
{
    [Required, StringLength(200)]
    public string Qualification { get; set; } = string.Empty;

    public NurseShift Shift { get; set; } = NurseShift.Morning;

    public StaffStatus Status { get; set; } = StaffStatus.Active;

    public Guid? DepartmentId { get; set; }

    public Guid? WardId { get; set; }
}

// ---------- Lab test catalogue ----------
public class SaveLabTestRequest
{
    [Required, StringLength(30)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 1000000)]
    public decimal Price { get; set; }

    [StringLength(200)]
    public string? NormalRange { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    public bool IsActive { get; set; } = true;
}

public record LabTestDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string? Description,
    decimal Price,
    string? NormalRange,
    string? Unit,
    bool IsActive);

// ---------- Discharge ----------
public class DischargeAdmissionRequest
{
    public DischargeType Type { get; set; } = DischargeType.Recovered;

    [Required, StringLength(2000)]
    public string Summary { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ConditionAtDischarge { get; set; }

    [StringLength(1000)]
    public string? FollowUpInstructions { get; set; }

    public DateTime? DischargedAt { get; set; }
}

public record DischargeResultDto(
    Guid AdmissionId,
    string AdmissionNumber,
    string PatientName,
    Guid PatientId,
    DateTime AdmittedAt,
    DateTime DischargedAt,
    int DaysStayed,
    decimal RoomCharges);

// ---------- Appointment status ----------
public class UpdateAppointmentStatusRequest
{
    [Required]
    public AppointmentStatus Status { get; set; }
}
