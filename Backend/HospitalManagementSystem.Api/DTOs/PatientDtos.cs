using System;
using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Api.DTOs
{
    // Used when returning patient data to clients (React / Flutter)
    public class PatientDto
    {
        public int PatientId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
        public DateTime DateOfBirth { get; set; }
        public int Age => DateTime.UtcNow.Year - DateOfBirth.Year;
        public string Gender { get; set; } = string.Empty;
        public string NIC { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string BloodGroup { get; set; } = string.Empty;
        public string EmergencyContactName { get; set; } = string.Empty;
        public string EmergencyContactPhone { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Used when creating a new patient (POST /api/patients)
    public class CreatePatientDto
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;
        [Required]
        public DateTime DateOfBirth { get; set; }
        [Required, MaxLength(20)]
        public string Gender { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string NIC { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required, EmailAddress, MaxLength(200)]
        public string? Email { get; set; }
        [Required, MaxLength(500)]
        public string? Address { get; set; }
        public string? BloodGroup { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
    }

    // Used when updating an existing patient (PUT /api/patients/{id})
    public class UpdatePatientDto
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;
        [Required]
        public DateTime DateOfBirth { get; set; }
        [Required, MaxLength(20)]
        public string Gender { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string NIC { get; set; } = string.Empty;
        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required, EmailAddress, MaxLength(200)]
        public string? Email { get; set; }
        [Required, MaxLength(500)]
        public string? Address { get; set; }
        public string? BloodGroup { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? ProfileImageUrl { get; set; }
    }

    public class PatientAppointmentHistoryDto
    {
        public int AppointmentId { get; set; }
        public DateTime ScheduledAt { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string AppointmentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class PatientSummaryDto
    {
        public int TotalPatients { get; set; }
        public int RegisteredThisMonth { get; set; }
        public int MaleCount { get; set; }
        public int FemaleCount { get; set; }
        public int OtherCount { get; set; }
    }
}
