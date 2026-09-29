using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services;

public interface IRoomService
{
    Task<IEnumerable<RoomDto>> GetAllAsync(DateTime? startAt, DateTime? endAt, bool confirmedOnly = false, int? excludeScheduleId = null);
    Task<RoomDto?> GetByIdAsync(int id);
    Task<RoomDto> CreateAsync(CreateRoomDto dto, int adminUserId);
    Task<RoomDto?> UpdateAsync(int id, UpdateRoomDto dto);
    Task<RoomDto?> ConfirmAsync(int id);
}
