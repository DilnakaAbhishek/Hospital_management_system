using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Api.DTOs
{
    public class AppointmentDto
    {
        public int AppointmentId { get; set; }
        public int DoctorTimeSlotId { get; set; }
        public int? DoctorId { get; set; }
        public int? PatientId { get; set; }
        public int AppointmentNumber { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientPhone { get; set; } = string.Empty;
        public string PatientEmail { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public int? RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Floor { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int SlotCapacity { get; set; }
        public int BookedCount { get; set; }
        public decimal ConsultationFee { get; set; }
        public string AppointmentType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CancellationReason { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateAppointmentDto
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int DoctorTimeSlotId { get; set; }
        public int? PatientId { get; set; }
        [Required, MaxLength(150)]
        public string PatientName { get; set; } = string.Empty;
        [Required, MaxLength(30)]
        public string PatientPhone { get; set; } = string.Empty;
        [EmailAddress, MaxLength(200)]
        public string? PatientEmail { get; set; }
        [Required, MaxLength(80)]
        public string AppointmentType { get; set; } = "Consultation";
    }

    public class UpdateAppointmentDto : CreateAppointmentDto
    {
        [Required, MaxLength(30)]
        public string Status { get; set; } = "Confirmed";
    }

    public class CancelAppointmentDto
    {
        [Required, MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class RescheduleAppointmentDto
    {
        [Required]
        public int DoctorTimeSlotId { get; set; }
    }

    public class DoctorTimeSlotDto
    {
        public int DoctorTimeSlotId { get; set; }
        public int? DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int Capacity { get; set; }
        public int BookedCount { get; set; }
        public decimal ConsultationFee { get; set; }
        public IEnumerable<int> BookedAppointmentNumbers { get; set; } = [];
        public int AvailableCount => Math.Max(0, Capacity - BookedCount);
        public int NextAppointmentNumber { get; set; }
        public bool IsActive { get; set; }
        public int? RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Floor { get; set; } = string.Empty;
    }

    public class DoctorLookupDto
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
    }

    public class CreateDoctorTimeSlotDto
    {
        public int? DoctorId { get; set; }
        public int? RoomId { get; set; }
        [Required, MaxLength(150)]
        public string DoctorName { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string Specialty { get; set; } = string.Empty;
        [Required]
        public DateTime StartAt { get; set; }
        [Required]
        public DateTime EndAt { get; set; }
        [Range(1, 50, ErrorMessage = "Slot capacity must be between 1 and 50.")]
        public int Capacity { get; set; } = 1;
        [Range(typeof(decimal), "0.01", "1000000.00")]
        public decimal ConsultationFee { get; set; }
    }

    public class UpdateDoctorTimeSlotDto : CreateDoctorTimeSlotDto
    {
        public bool IsActive { get; set; } = true;
    }

    public class CancelDoctorTimeSlotDto
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}
