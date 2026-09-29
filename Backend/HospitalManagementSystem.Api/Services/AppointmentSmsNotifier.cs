using System.Globalization;
using HospitalManagementSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Services;

public sealed class AppointmentSmsNotifier(ApplicationDbContext db, ISmsService sms,
    ILogger<AppointmentSmsNotifier> logger)
{
    private readonly List<(Guid TransactionId, int AppointmentId, string Action)> pending = [];

    public async Task NotifyAsync(int appointmentId, string action)
    {
        if (db.Database.CurrentTransaction is { } transaction)
        {
            pending.Add((transaction.TransactionId, appointmentId, action));
            return;
        }
        await SendAsync(appointmentId, action);
    }

    // Outer transaction owners call only after a successful commit. Rolled-back work is never sent.
    public async Task FlushCommittedAsync(Guid transactionId)
    {
        var committed = pending.Where(item => item.TransactionId == transactionId).ToArray();
        pending.RemoveAll(item => item.TransactionId == transactionId);
        foreach (var item in committed) await SendAsync(item.AppointmentId, item.Action);
    }

    private async Task SendAsync(int appointmentId, string action)
    {
        try
        {
            var appointment = await db.Appointments.AsNoTracking().Include(a => a.Patient)
                .Include(a => a.DoctorTimeSlot).SingleAsync(a => a.AppointmentId == appointmentId);
            // Use the current profile phone, never the caller-supplied appointment snapshot.
            var phone = appointment.Patient?.PhoneNumber;
            if (appointment.PatientId is null && !string.IsNullOrWhiteSpace(appointment.PatientEmail))
            {
                var email = appointment.PatientEmail.Trim().ToLowerInvariant();
                phone = await db.Patients.Where(p => p.Email != null && p.Email.ToLower() == email)
                    .Select(p => p.PhoneNumber).SingleOrDefaultAsync();
            }
            var recipient = SmsPhoneNumber.Normalize(phone);
            if (recipient is null || appointment.DoctorTimeSlot is not { } slot)
            {
                logger.LogWarning("SMS skipped for appointment {AppointmentId}: missing patient profile, valid phone, or slot.", appointmentId);
                return;
            }
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(slot.StartAt, DateTimeKind.Utc),
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo"));
            var message = $"Appointment {action}. Doctor: {slot.DoctorName}, Date: {local.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}, " +
                $"Time: {local.ToString("hh:mm tt", CultureInfo.InvariantCulture)} (Sri Lanka). Appointment no: {appointment.AppointmentNumber}.";
            if (action == "schedule updated") message += " Check your appointment for room and schedule details.";
            if (!await sms.SendAsync(recipient, message))
                logger.LogWarning("SMS failed for saved appointment {AppointmentId}; appointment retained.", appointmentId);
        }
        catch (Exception exception)
        {
            logger.LogWarning("SMS failed for saved appointment {AppointmentId} ({ErrorType}); appointment retained.",
                appointmentId, exception.GetType().Name);
        }
    }
}
