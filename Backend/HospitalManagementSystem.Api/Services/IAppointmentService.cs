using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services
{
    public interface IAppointmentService
    {
        Task<PagedResult<AppointmentDto>> GetAllAppointmentsAsync(string? search, string? status, string? doctorName, DateTime? date, string? sortBy, string? sortDirection, int page, int pageSize, int? patientId = null, string? patientEmail = null, int? doctorId = null);
        Task<AppointmentDto?> GetAppointmentByIdAsync(int id);
        Task<AppointmentDto> CreateAppointmentAsync(CreateAppointmentDto dto);
        Task<AppointmentDto?> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto);
        Task<AppointmentDto?> CancelAppointmentAsync(int id, string reason);
        Task<AppointmentDto?> RescheduleAppointmentAsync(int id, int doctorTimeSlotId);
        Task<IEnumerable<DoctorLookupDto>> GetDoctorsAsync(string? specialty = null);
        Task<IEnumerable<string>> GetSpecializationsAsync();
        Task<IEnumerable<DoctorTimeSlotDto>> GetSlotsAsync(string? doctorName, DateTime? date, bool onlyAvailable, int? doctorId = null);
        Task<DoctorTimeSlotDto> CreateSlotAsync(CreateDoctorTimeSlotDto dto);
        Task<DoctorTimeSlotDto?> UpdateSlotAsync(int id, UpdateDoctorTimeSlotDto dto);
        Task<DoctorTimeSlotDto?> CancelSlotAsync(int id, string? reason);
    }
}
