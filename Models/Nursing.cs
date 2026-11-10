namespace HospitalManagement.Api.Models;

public class VitalSign : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid? AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public Guid RecordedByUserId { get; set; }
    public User RecordedByUser { get; set; } = null!;

    public DateTime RecordedAt { get; set; }

    public decimal? TemperatureCelsius { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? HeartRate { get; set; }
    public int? RespiratoryRate { get; set; }
    public int? OxygenSaturation { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
}

public class NursingNote : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid? AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public Guid NurseUserId { get; set; }
    public User NurseUser { get; set; } = null!;

    public string Note { get; set; } = string.Empty;
}
