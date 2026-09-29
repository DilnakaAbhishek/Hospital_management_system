using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services;

public interface IDoctorService
{
    Task RegisterAsync(RegisterDoctorDto dto);
    Task<IEnumerable<DoctorDto>> GetRegistrationsAsync(string? status);
    Task<DoctorDto?> GetByIdAsync(int doctorId);
    Task<DoctorDto?> ReviewAsync(int doctorId, int adminUserId, bool approve, string? declineReason);
    Task<DoctorDto?> GetByUserIdAsync(int userId);
    Task<DoctorDto?> UpdateProfileAsync(int userId, UpdateDoctorProfileDto dto);
    Task<DoctorDto?> RequestDeletionAsync(int userId, string? reason);
    Task<DoctorDto?> CancelDeletionByDoctorAsync(int userId);
    Task<bool> ApproveDeletionAsync(int doctorId, int adminUserId);
    Task<DoctorDto?> CancelDeletionAsync(int doctorId, int adminUserId, string? reason = null);
    Task<IReadOnlyList<DoctorSearchResultDto>> SearchApprovedDoctorsAsync(string? query, int limit = 20);
    Task<DoctorPublicProfileDto?> GetApprovedDoctorProfileAsync(int doctorId);
}
