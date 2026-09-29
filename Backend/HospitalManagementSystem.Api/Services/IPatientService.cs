using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services
{
    public interface IPatientService
    {
        Task<PagedResult<PatientDto>> GetAllPatientsAsync(string? search, string? gender, string? bloodGroup, string? sortBy, string? sortDirection, int page, int pageSize);
        Task<PatientSummaryDto> GetSummaryAsync();
        Task<PatientDto?> GetPatientByIdAsync(int id);
        Task<PatientDto?> GetPatientByEmailAsync(string email);
        Task<IEnumerable<PatientAppointmentHistoryDto>?> GetAppointmentHistoryAsync(int patientId);
        Task<PatientDto> CreatePatientAsync(CreatePatientDto dto);
        Task<PatientDto?> UpdatePatientAsync(int id, UpdatePatientDto dto);
        Task<bool> DeletePatientAsync(int id);
    }

    public class PagedResult<T>
    {
        public IEnumerable<T> Data { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
