namespace HospitalManagement.Api.DTOs;

public record DashboardSummaryDto(
    int TotalPatients,
    int ActivePatients,
    int TotalAppointments,
    int AppointmentsToday,
    int TotalAdmissions,
    int ActiveAdmissions,
    int TotalInvoices,
    int PendingInvoices,
    decimal TotalRevenue,
    int TotalLabOrders,
    int OpenLabOrders,
    int TotalNotifications,
    int UnreadNotifications,
    int TotalUsers);
