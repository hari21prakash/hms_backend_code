using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class ReportService(AppDbContext dbContext) : IReportService
{
    public async Task<ReportsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var totalPatients = await dbContext.Patients.CountAsync(cancellationToken);
        var activePatients = await dbContext.Patients.CountAsync(patient => patient.Status == PatientStatus.Active, cancellationToken);

        var totalAppointments = await dbContext.Appointments.CountAsync(cancellationToken);
        var appointmentsToday = await dbContext.Appointments.CountAsync(appointment => appointment.AppointmentDate == today, cancellationToken);

        var totalAdmissions = await dbContext.Admissions.CountAsync(cancellationToken);

        var totalInvoices = await dbContext.Invoices.CountAsync(cancellationToken);
        var paidInvoices = await dbContext.Invoices.CountAsync(invoice => invoice.Status == InvoiceStatus.Paid, cancellationToken);
        var pendingInvoices = await dbContext.Invoices.CountAsync(invoice => invoice.Status == InvoiceStatus.Pending, cancellationToken);
        var partiallyPaidInvoices = await dbContext.Invoices.CountAsync(invoice => invoice.Status == InvoiceStatus.PartiallyPaid, cancellationToken);
        var totalRevenue = await dbContext.Invoices.SumAsync(invoice => (decimal?)invoice.TotalAmount, cancellationToken) ?? 0m;
        var averageInvoiceValue = totalInvoices > 0 ? totalRevenue / totalInvoices : 0m;

        var openLabOrders = await dbContext.LabOrders.CountAsync(order =>
            order.Status == LabOrderStatus.Ordered ||
            order.Status == LabOrderStatus.SampleCollected ||
            order.Status == LabOrderStatus.Processing, cancellationToken);

        var unreadNotifications = await dbContext.Notifications.CountAsync(notification => !notification.IsRead, cancellationToken);
        var totalUsers = await dbContext.Users.CountAsync(cancellationToken);

        return new ReportsSummaryDto(
            totalRevenue,
            averageInvoiceValue,
            paidInvoices,
            pendingInvoices,
            partiallyPaidInvoices,
            totalPatients,
            activePatients,
            totalAppointments,
            appointmentsToday,
            totalAdmissions,
            openLabOrders,
            unreadNotifications,
            totalUsers,
            DateTime.UtcNow);
    }
}
