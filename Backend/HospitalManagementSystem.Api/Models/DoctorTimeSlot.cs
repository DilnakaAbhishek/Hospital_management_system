namespace HospitalManagementSystem.Api.Models
{
    public class DoctorTimeSlot
    {
        public int DoctorTimeSlotId { get; set; }
        public int? DoctorId { get; set; }
        public Doctor? Doctor { get; set; }
        public int? RoomId { get; set; }
        public Room? Room { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int Capacity { get; set; }
        public decimal ConsultationFee { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
