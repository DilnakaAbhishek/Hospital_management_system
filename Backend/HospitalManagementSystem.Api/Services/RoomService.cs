using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Services;

public class RoomService : IRoomService
{
    internal const int RoomTurnoverMinutes = 30;
    private readonly ApplicationDbContext _db;
    public RoomService(ApplicationDbContext db) => _db = db;

    public async Task<IEnumerable<RoomDto>> GetAllAsync(DateTime? startAt, DateTime? endAt, bool confirmedOnly = false, int? excludeScheduleId = null)
    {
        if (startAt.HasValue != endAt.HasValue)
            throw new ArgumentException("Both startAt and endAt are required when checking availability.");
        if (startAt.HasValue) ValidateRange(startAt.Value, endAt!.Value, allowPast: true);

        var query = _db.Rooms.AsNoTracking().AsQueryable();
        if (confirmedOnly) query = query.Where(room => room.IsConfirmed);
        var rooms = await query.OrderBy(room => room.RoomNumber).ToListAsync();

        var slotQuery = _db.DoctorTimeSlots.AsNoTracking()
            .Where(slot =>
                slot.IsActive &&
                slot.RoomId != null &&
                slot.DoctorTimeSlotId != excludeScheduleId);

        if (startAt.HasValue)
        {
            var bufferedStart = startAt.Value.AddMinutes(-RoomTurnoverMinutes);
            var bufferedEnd = endAt!.Value.AddMinutes(RoomTurnoverMinutes);
            slotQuery = slotQuery.Where(slot => slot.StartAt < bufferedEnd && bufferedStart < slot.EndAt);
        }
        else
        {
            var now = DateTime.UtcNow;
            var releaseWindowStart = now.AddMinutes(-RoomTurnoverMinutes);
            slotQuery = slotQuery.Where(slot => slot.StartAt <= now && releaseWindowStart < slot.EndAt);
        }

        var bookedRoomIds = await slotQuery.Select(slot => slot.RoomId!.Value).Distinct().ToListAsync();
        var booked = bookedRoomIds.ToHashSet();
        return rooms.Select(room => Map(room, booked.Contains(room.RoomId)));
    }

    public async Task<RoomDto?> GetByIdAsync(int id)
    {
        var room = await _db.Rooms.AsNoTracking().SingleOrDefaultAsync(value => value.RoomId == id);
        if (room is null) return null;
        var now = DateTime.UtcNow;
        var releaseWindowStart = now.AddMinutes(-RoomTurnoverMinutes);
        var isBooked = await _db.DoctorTimeSlots.AnyAsync(slot => slot.RoomId == id && slot.IsActive && slot.StartAt <= now && releaseWindowStart < slot.EndAt);
        return Map(room, isBooked);
    }

    public async Task<RoomDto> CreateAsync(CreateRoomDto dto, int adminUserId)
    {
        var roomNumber = Required(dto.RoomNumber, "Room number").ToUpperInvariant();
        if (await _db.Rooms.AnyAsync(room => room.RoomNumber == roomNumber))
            throw new InvalidOperationException("A room with this room number already exists.");
        var room = new Room
        {
            RoomNumber = roomNumber, RoomName = Required(dto.RoomName, "Room name"),
            Floor = Required(dto.Floor, "Floor"), Description = dto.Description?.Trim(),
            CreatedByUserId = adminUserId, IsConfirmed = false
        };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return Map(room, false);
    }

    public async Task<RoomDto?> UpdateAsync(int id, UpdateRoomDto dto)
    {
        var room = await _db.Rooms.SingleOrDefaultAsync(value => value.RoomId == id);
        if (room is null) return null;
        var roomNumber = Required(dto.RoomNumber, "Room number").ToUpperInvariant();
        if (await _db.Rooms.AnyAsync(value => value.RoomNumber == roomNumber && value.RoomId != id))
            throw new InvalidOperationException("A room with this room number already exists.");
        room.RoomNumber = roomNumber;
        room.RoomName = Required(dto.RoomName, "Room name");
        room.Floor = Required(dto.Floor, "Floor");
        room.Description = dto.Description?.Trim();
        room.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(room, false);
    }

    public async Task<RoomDto?> ConfirmAsync(int id)
    {
        var room = await _db.Rooms.SingleOrDefaultAsync(value => value.RoomId == id);
        if (room is null) return null;
        room.IsConfirmed = true;
        room.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(room, false);
    }

    internal static void ValidateRange(DateTime startAt, DateTime endAt, bool allowPast = false)
    {
        if (endAt <= startAt) throw new ArgumentException("Schedule end time must be after its start time.");
        if (!allowPast && startAt <= DateTime.UtcNow) throw new ArgumentException("A schedule cannot start in the past.");
        if (!allowPast && startAt.Date != endAt.Date) throw new ArgumentException("Start time and end time must be on the selected appointment date.");
    }

    private static string Required(string value, string label) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{label} is required.") : value.Trim();

    private static RoomDto Map(Room room, bool booked) => new()
    {
        RoomId = room.RoomId, RoomNumber = room.RoomNumber, RoomName = room.RoomName,
        Floor = room.Floor, Description = room.Description, IsConfirmed = room.IsConfirmed,
        Status = booked ? "Booked" : "Available", CreatedAt = room.CreatedAt, UpdatedAt = room.UpdatedAt
    };
}
