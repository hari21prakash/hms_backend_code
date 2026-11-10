namespace HospitalManagement.Api.Models.Enums;

public enum InvoiceStatus { Draft, Pending, PartiallyPaid, Paid, Cancelled }

public enum InvoiceItemType { Consultation, LabTest, Medicine, RoomCharge, Procedure, Other }

public enum PaymentMethod { Cash, Card, Upi, BankTransfer }

public enum PaymentStatus { Pending, Completed, Failed, Refunded }

public enum NotificationType
{
    General,
    AppointmentBooked,
    AppointmentCancelled,
    AppointmentReminder,
    LabResultAvailable,
    PrescriptionCreated,
    PaymentReceived,
    LowMedicineStock,
    MedicineExpired,
    MedicineExpiringSoon,
    LabOrderCreated,
    InvoiceCreated
}
