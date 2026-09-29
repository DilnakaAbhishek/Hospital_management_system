using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Repositories
{
    public interface IPatientRepository
    {
        Task<IEnumerable<Patient>> GetAllAsync(string? search, string? gender, string? bloodGroup, string? sortBy, string? sortDirection, int page, int pageSize);
        Task<int> GetTotalCountAsync(string? search, string? gender, string? bloodGroup);
        Task<PatientSummaryDto> GetSummaryAsync();
        Task<Patient?> GetByIdAsync(int id);
        Task<IEnumerable<Appointment>> GetAppointmentsByPatientIdAsync(int patientId);
        Task<Patient?> GetByEmailAsync(string email);
        Task<Patient> CreateAsync(Patient patient);
        Task<Patient> UpdateAsync(Patient patient);
        Task DeleteAsync(Patient patient);
        Task<bool> ExistsByEmailAsync(string email, int? excludeId = null);
        Task<bool> ExistsByNICAsync(string nic, int? excludeId = null);
    }
}
