using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class DashboardService(AppDbContext dbContext) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var totalPatients = await dbContext.Patients.CountAsync(cancellationToken);
        var activePatients = await dbContext.Patients.CountAsync(patient => patient.Status == PatientStatus.Active, cancellationToken);

        var totalAppointments = await dbContext.Appointments.CountAsync(cancellationToken);
        var appointmentsToday = await dbContext.Appointments.CountAsync(appointment =>
            appointment.AppointmentDate == today, cancellationToken);

        var totalAdmissions = await dbContext.Admissions.CountAsync(cancellationToken);
        var activeAdmissions = await dbContext.Admissions.CountAsync(admission =>
            admission.Status != AdmissionStatus.Discharged, cancellationToken);

        var totalInvoices = await dbContext.Invoices.CountAsync(cancellationToken);
        var pendingInvoices = await dbContext.Invoices.CountAsync(invoice =>
            invoice.Status == InvoiceStatus.Pending || invoice.Status == InvoiceStatus.PartiallyPaid, cancellationToken);
        var totalRevenue = await dbContext.Invoices.SumAsync(invoice => (decimal?)invoice.TotalAmount, cancellationToken) ?? 0m;

        var totalLabOrders = await dbContext.LabOrders.CountAsync(cancellationToken);
        var openLabOrders = await dbContext.LabOrders.CountAsync(order =>
            order.Status == LabOrderStatus.Ordered ||
            order.Status == LabOrderStatus.SampleCollected ||
            order.Status == LabOrderStatus.Processing, cancellationToken);

        var totalNotifications = await dbContext.Notifications.CountAsync(cancellationToken);
        var unreadNotifications = await dbContext.Notifications.CountAsync(notification => !notification.IsRead, cancellationToken);

        var totalUsers = await dbContext.Users.CountAsync(cancellationToken);

        return new DashboardSummaryDto(
            totalPatients,
            activePatients,
            totalAppointments,
            appointmentsToday,
            totalAdmissions,
            activeAdmissions,
            totalInvoices,
            pendingInvoices,
            totalRevenue,
            totalLabOrders,
            openLabOrders,
            totalNotifications,
            unreadNotifications,
            totalUsers);
    }
}
