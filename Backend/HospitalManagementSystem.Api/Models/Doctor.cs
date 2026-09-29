namespace HospitalManagementSystem.Api.Models;

public static class DoctorRegistrationStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Declined = "Declined";
    public const string DeletionPending = "DeletionPending";
}

public class Doctor
{
    public int DoctorId { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string NIC { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string SlmcLicenseNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = DoctorRegistrationStatuses.Pending;
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? DeclineReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public User? ReviewedByUser { get; set; }
    public ICollection<DoctorTimeSlot> DoctorTimeSlots { get; set; } = new List<DoctorTimeSlot>();
}
