namespace HospitalManagement.Api.Data;

/// <summary>
/// PostgreSQL sequences used to generate human-readable, collision-free numbers
/// (e.g. PAT-2026-000123). The numbering services call nextval() on these.
/// </summary>
public static class DbSequences
{
    public const string PatientNumber = "patient_number_seq";
    public const string AppointmentNumber = "appointment_number_seq";
    public const string PrescriptionNumber = "prescription_number_seq";
    public const string LabOrderNumber = "lab_order_number_seq";
    public const string AdmissionNumber = "admission_number_seq";
    public const string InvoiceNumber = "invoice_number_seq";
    public const string PaymentNumber = "payment_number_seq";

    public static readonly string[] All =
    [
        PatientNumber, AppointmentNumber, PrescriptionNumber, LabOrderNumber,
        AdmissionNumber, InvoiceNumber, PaymentNumber
    ];
}
