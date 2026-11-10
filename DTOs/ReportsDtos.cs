namespace HospitalManagement.Api.DTOs;

public record ReportsSummaryDto(
    decimal TotalRevenue,
    decimal AverageInvoiceValue,
    int PaidInvoices,
    int PendingInvoices,
    int PartiallyPaidInvoices,
    int TotalPatients,
    int ActivePatients,
    int TotalAppointments,
    int AppointmentsToday,
    int TotalAdmissions,
    int OpenLabOrders,
    int UnreadNotifications,
    int TotalUsers,
    DateTime GeneratedAtUtc);
