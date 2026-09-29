namespace HospitalManagementSystem.Api.Models
{
    public class Appointment
    {
        public int AppointmentId { get; set; }
        public int DoctorTimeSlotId { get; set; }
        public DoctorTimeSlot? DoctorTimeSlot { get; set; }
        public int? PatientId { get; set; }
        public Patient? Patient { get; set; }
        public int AppointmentNumber { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientPhone { get; set; } = string.Empty;
        public string? PatientEmail { get; set; }
        public string AppointmentType { get; set; } = "Consultation";
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Confirmed";
        public string? CancellationReason { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<AppointmentNotification> Notifications { get; set; } = new List<AppointmentNotification>();
    }
}
