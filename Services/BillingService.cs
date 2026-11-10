using HospitalManagement.Api.Data;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Interfaces;
using HospitalManagement.Api.Models;
using HospitalManagement.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagement.Api.Services;

public class BillingService(AppDbContext dbContext, INotificationPublisher? notifications = null) : IBillingService
{
    public async Task<InvoicePageDto> GetPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Patient)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(invoice =>
                invoice.InvoiceNumber.Contains(term) ||
                invoice.Patient.FirstName.Contains(term) ||
                invoice.Patient.LastName.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var invoices = await query
            .OrderByDescending(invoice => invoice.InvoiceDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(invoice => new InvoiceListItemDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.Patient.FirstName + " " + invoice.Patient.LastName,
                invoice.InvoiceDate,
                invoice.Status,
                invoice.SubtotalAmount,
                invoice.DiscountAmount,
                invoice.TaxAmount,
                invoice.TotalAmount,
                invoice.PaidAmount,
                invoice.BalanceAmount))
            .ToListAsync(cancellationToken);

        return new InvoicePageDto(invoices, totalCount, page, pageSize);
    }

    public async Task<BillingLookupDto> GetLookupAsync(CancellationToken cancellationToken)
    {
        var patients = await dbContext.Patients
            .AsNoTracking()
            .Where(patient => patient.Status == PatientStatus.Active)
            .OrderBy(patient => patient.LastName)
            .ThenBy(patient => patient.FirstName)
            .Select(patient => new PatientOptionDto(
                patient.Id,
                patient.FirstName + " " + patient.LastName,
                patient.PatientNumber))
            .ToListAsync(cancellationToken);

        var admissions = await dbContext.Admissions
            .AsNoTracking()
            .Include(admission => admission.Patient)
            .Where(admission => admission.Status != AdmissionStatus.Discharged)
            .OrderByDescending(admission => admission.AdmittedAt)
            .Select(admission => new AdmissionOptionDto(
                admission.Id,
                admission.AdmissionNumber,
                admission.Patient.FirstName + " " + admission.Patient.LastName))
            .ToListAsync(cancellationToken);

        return new BillingLookupDto(patients, admissions);
    }

    public async Task<InvoiceDetailDto> CreateInvoiceAsync(CreateInvoiceRequest request, Guid actorUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PatientId == Guid.Empty)
            throw new RequestValidationException("A patient must be selected.");

        if (request.Items.Count == 0)
            throw new RequestValidationException("At least one invoice item is required.");

        var patient = await dbContext.Patients
            .SingleOrDefaultAsync(item => item.Id == request.PatientId, cancellationToken);
        if (patient is null)
            throw new NotFoundException(nameof(Patient), request.PatientId);

        var creator = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Id == actorUserId, cancellationToken);
        if (creator is null)
            throw new UnauthorizedAppException();

        if (request.AppointmentId is Guid appointmentId && appointmentId != Guid.Empty)
        {
            var appointmentExists = await dbContext.Appointments.AnyAsync(item => item.Id == appointmentId, cancellationToken);
            if (!appointmentExists)
                throw new NotFoundException(nameof(Appointment), appointmentId);
        }

        if (request.AdmissionId is Guid admissionId && admissionId != Guid.Empty)
        {
            var admissionExists = await dbContext.Admissions.AnyAsync(item => item.Id == admissionId, cancellationToken);
            if (!admissionExists)
                throw new NotFoundException(nameof(Admission), admissionId);
        }

        var invoice = new Invoice
        {
            InvoiceNumber = await GenerateInvoiceNumberAsync(cancellationToken),
            PatientId = request.PatientId,
            AppointmentId = request.AppointmentId,
            AdmissionId = request.AdmissionId,
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            Status = InvoiceStatus.Pending,
            DiscountAmount = request.DiscountAmount,
            TaxAmount = request.TaxAmount,
            Notes = request.Notes,
            CreatedByUserId = actorUserId,
        };

        decimal subtotal = 0;
        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                throw new RequestValidationException("Each invoice item needs a description.");

            if (item.Quantity <= 0)
                throw new RequestValidationException("Invoice quantities must be greater than zero.");

            if (item.UnitPrice < 0)
                throw new RequestValidationException("Invoice unit price cannot be negative.");

            if (item.LabOrderId is Guid labOrderId && labOrderId != Guid.Empty)
            {
                var exists = await dbContext.LabOrders.AnyAsync(entry => entry.Id == labOrderId, cancellationToken);
                if (!exists)
                    throw new NotFoundException(nameof(LabOrder), labOrderId);
            }

            if (item.PrescriptionId is Guid prescriptionId && prescriptionId != Guid.Empty)
            {
                var exists = await dbContext.Prescriptions.AnyAsync(entry => entry.Id == prescriptionId, cancellationToken);
                if (!exists)
                    throw new NotFoundException(nameof(Prescription), prescriptionId);
            }

            var lineTotal = item.Quantity * item.UnitPrice;
            subtotal += lineTotal;

            invoice.Items.Add(new InvoiceItem
            {
                ItemType = item.ItemType,
                Description = item.Description.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                LineTotal = lineTotal,
                LabOrderId = item.LabOrderId,
                PrescriptionId = item.PrescriptionId,
            });
        }

        invoice.SubtotalAmount = subtotal;
        invoice.TotalAmount = subtotal - invoice.DiscountAmount + invoice.TaxAmount;
        invoice.BalanceAmount = invoice.TotalAmount;
        invoice.Status = invoice.TotalAmount > 0 ? InvoiceStatus.Pending : InvoiceStatus.Draft;

        dbContext.Invoices.Add(invoice);

        if (notifications is not null)
            await notifications.StageInvoiceCreatedAsync(invoice, patient, actorUserId, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetDetailAsync(invoice.Id, cancellationToken);
    }

    public async Task<PaymentDto> RecordPaymentAsync(CreatePaymentRequest request, Guid actorUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.InvoiceId == Guid.Empty)
            throw new RequestValidationException("An invoice must be selected.");

        if (request.Amount <= 0)
            throw new RequestValidationException("Payment amount must be greater than zero.");

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == actorUserId, cancellationToken);
        if (user is null)
            throw new UnauthorizedAppException();

        var invoice = await dbContext.Invoices
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.Id == request.InvoiceId, cancellationToken);
        if (invoice is null)
            throw new NotFoundException(nameof(Invoice), request.InvoiceId);

        var remainingBalance = invoice.TotalAmount - invoice.PaidAmount;
        if (request.Amount > remainingBalance)
            throw new RequestValidationException($"Payment exceeds the remaining balance of {remainingBalance:C}.");

        var payment = new Payment
        {
            PaymentNumber = await GeneratePaymentNumberAsync(cancellationToken),
            InvoiceId = invoice.Id,
            Amount = request.Amount,
            Method = request.Method,
            Status = PaymentStatus.Completed,
            TransactionReference = request.TransactionReference,
            PaymentDate = DateTime.UtcNow,
            Notes = request.Notes,
            ReceivedByUserId = actorUserId,
        };

        invoice.PaidAmount += payment.Amount;
        invoice.BalanceAmount = invoice.TotalAmount - invoice.PaidAmount;
        invoice.Status = invoice.PaidAmount >= invoice.TotalAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        dbContext.Payments.Add(payment);

        if (notifications is not null)
        {
            var patient = await dbContext.Patients.SingleAsync(item => item.Id == invoice.PatientId, cancellationToken);
            await notifications.StagePaymentReceivedAsync(payment, invoice, patient, actorUserId, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new PaymentDto(
            payment.Id,
            payment.InvoiceId,
            payment.PaymentNumber,
            payment.Amount,
            payment.Method,
            payment.Status,
            payment.TransactionReference,
            payment.PaymentDate,
            payment.Notes);
    }

    private async Task<InvoiceDetailDto> GetDetailAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(item => item.Patient)
            .Include(item => item.Items)
            .Include(item => item.Payments)
            .Where(item => item.Id == invoiceId)
            .Select(item => new InvoiceDetailDto(
                item.Id,
                item.InvoiceNumber,
                item.Patient.FirstName + " " + item.Patient.LastName,
                item.InvoiceDate,
                item.Status,
                item.SubtotalAmount,
                item.DiscountAmount,
                item.TaxAmount,
                item.TotalAmount,
                item.PaidAmount,
                item.BalanceAmount,
                item.Notes,
                item.Items.Select(line => new InvoiceItemDto(
                    line.Id,
                    line.InvoiceId,
                    line.ItemType,
                    line.Description,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal,
                    line.LabOrderId,
                    line.PrescriptionId)).ToList(),
                item.Payments.Select(payment => new PaymentDto(
                    payment.Id,
                    payment.InvoiceId,
                    payment.PaymentNumber,
                    payment.Amount,
                    payment.Method,
                    payment.Status,
                    payment.TransactionReference,
                    payment.PaymentDate,
                    payment.Notes)).ToList()))
            .SingleAsync(cancellationToken);

        return invoice;
    }

    private async Task<string> GenerateInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var sequence = await dbContext.Invoices.CountAsync(cancellationToken);
        return $"INV-{DateTime.UtcNow:yyyyMMdd}-{(sequence + 1):D6}";
    }

    private async Task<string> GeneratePaymentNumberAsync(CancellationToken cancellationToken)
    {
        var sequence = await dbContext.Payments.CountAsync(cancellationToken);
        return $"PAY-{DateTime.UtcNow:yyyyMMdd}-{(sequence + 1):D6}";
    }
}
