namespace HospitalManagement.Api.Models.Enums;

public enum AppointmentType { Consultation, FollowUp, Emergency, Procedure }

public enum AppointmentStatus { Scheduled, Confirmed, CheckedIn, InConsultation, Completed, Cancelled, NoShow }

public enum PrescriptionStatus { Active, PartiallyDispensed, Dispensed, Cancelled }

public enum MedicineForm { Tablet, Capsule, Syrup, Injection, Ointment, Drops, Inhaler, Other }

public enum MedicineStatus { Active, Inactive, Discontinued }

public enum PharmacyTransactionType { Purchase, Dispense, Adjustment, WriteOff }

public enum LabOrderStatus { Ordered, SampleCollected, Processing, Completed, Cancelled }

public enum LabPriority { Routine, Urgent, Stat }

public enum AdmissionStatus { Admitted, UnderTreatment, Discharged, Transferred }

public enum BedStatus { Available, Occupied, Reserved, Maintenance }

public enum WardType { General, Icu, Private, Emergency, Maternity, Pediatric }

public enum DischargeType { Recovered, Referred, AgainstMedicalAdvice, Deceased }
