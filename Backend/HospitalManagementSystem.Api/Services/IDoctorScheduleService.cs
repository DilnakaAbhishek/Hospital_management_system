using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services;

public interface IDoctorScheduleService
{
    Task<IEnumerable<DoctorScheduleDto>> GetMineAsync(int userId);
    Task<DoctorScheduleDto?> GetMineByIdAsync(int userId, int slotId);
    Task<DoctorScheduleDto> CreateAsync(int userId, CreateDoctorScheduleDto dto);
    Task<DoctorScheduleDto?> UpdateAsync(int userId, int slotId, UpdateDoctorScheduleDto dto);
    Task<DoctorScheduleDto?> CancelAsync(int userId, int slotId);
    Task<bool> DeleteAsync(int userId, int slotId);
}
