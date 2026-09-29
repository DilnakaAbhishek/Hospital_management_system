using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Services;

public static class ScheduleAppointmentUpdates
{
    public static void Apply(DoctorTimeSlot slot, bool notify)
    {
        foreach (var appointment in slot.Appointments.Where(a => a.Status == "Confirmed"))
        {
            appointment.UpdatedAt = DateTime.UtcNow;
            if (notify)
            {
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(slot.StartAt,
                    TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo"));
                appointment.Notifications.Add(new AppointmentNotification
                {
                    Message = $"Your appointment #{appointment.AppointmentNumber} with {slot.DoctorName} has been updated. " +
                        $"New appointment time: {localTime:dd MMM yyyy, hh:mm tt} (Sri Lanka time). Please check your appointment for the latest room and schedule details."
                });
            }
        }
    }
}
