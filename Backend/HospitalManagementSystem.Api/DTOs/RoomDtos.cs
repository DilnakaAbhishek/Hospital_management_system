using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Api.DTOs;

public class CreateRoomDto
{
    [Required, MaxLength(30)] public string RoomNumber { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string RoomName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string Floor { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
}

public class UpdateRoomDto : CreateRoomDto { }

public class RoomDto
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string Floor { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsConfirmed { get; set; }
    public string Status { get; set; } = "Available";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateDoctorScheduleDto
{
    [Required] public int RoomId { get; set; }
    [Required] public DateTime StartAt { get; set; }
    [Required] public DateTime EndAt { get; set; }
    [Range(1, 50, ErrorMessage = "Slot capacity must be between 1 and 50.")] public int Capacity { get; set; } = 1;
    [Range(typeof(decimal), "0.01", "1000000.00")] public decimal ConsultationFee { get; set; }
}

public class UpdateDoctorScheduleDto : CreateDoctorScheduleDto { }

public class DoctorScheduleDto
{
    public int DoctorTimeSlotId { get; set; }
    public int DoctorId { get; set; }
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string Floor { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int Capacity { get; set; }
    public decimal ConsultationFee { get; set; }
    public int BookedCount { get; set; }
    public bool HasPatientBookings { get; set; }
    public bool IsActive { get; set; }
}
