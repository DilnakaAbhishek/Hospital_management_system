namespace HospitalManagementSystem.Api.Models;

public class AppointmentNotification
{
    public int AppointmentNotificationId { get; set; }
    public int AppointmentId { get; set; }
    public Appointment Appointment { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
