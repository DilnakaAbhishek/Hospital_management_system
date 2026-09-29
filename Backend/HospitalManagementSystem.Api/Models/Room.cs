namespace HospitalManagementSystem.Api.Models;

public class Room
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string Floor { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsConfirmed { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User CreatedByUser { get; set; } = null!;
    public ICollection<DoctorTimeSlot> DoctorTimeSlots { get; set; } = new List<DoctorTimeSlot>();
}
