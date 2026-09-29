using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Api.DTOs;

public class RegisterDoctorDto
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(200)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(12)] public string NIC { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Specialization { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string SlmcLicenseNumber { get; set; } = string.Empty;
    [Required, MaxLength(15)] public string PhoneNumber { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
}

public class DoctorDto
{
    public int DoctorId { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string Email { get; set; } = string.Empty;
    public string NIC { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string SlmcLicenseNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
    public string? DeclineReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class UpdateDoctorProfileDto
{
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [Required, MaxLength(15)] public string PhoneNumber { get; set; } = string.Empty;
}

public class DeclineDoctorDto
{
    [MaxLength(500)] public string? Reason { get; set; }
}

public class RequestDoctorDeletionDto
{
    [MaxLength(500)] public string? Reason { get; set; }
}

public class DoctorRegistrationResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DoctorSearchResultDto
{
    public int DoctorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
}

public class DoctorPublicProfileDto
{
    public int DoctorId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SlmcLicenseNumber { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
}
