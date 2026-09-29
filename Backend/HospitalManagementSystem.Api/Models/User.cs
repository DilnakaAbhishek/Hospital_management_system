using System;

namespace HospitalManagementSystem.Api.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "Admin", "Doctor", "Patient"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Patient? PatientProfile { get; set; }
        public Doctor? DoctorProfile { get; set; }
    }
}
