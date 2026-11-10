namespace HospitalManagement.Api.Models.Enums;

/// <summary>Unknown = not yet provided (e.g. a self-registered patient who has not completed their profile).</summary>
public enum Gender { Male, Female, Other, Unknown }

public enum BloodGroup
{
    Unknown, APositive, ANegative, BPositive, BNegative, ABPositive, ABNegative, OPositive, ONegative
}

public enum PatientStatus { Active, Inactive, Deceased }

public enum StaffStatus { Active, Inactive, OnLeave }

public enum NurseShift { Morning, Evening, Night }

public enum AllergySeverity { Mild, Moderate, Severe }
