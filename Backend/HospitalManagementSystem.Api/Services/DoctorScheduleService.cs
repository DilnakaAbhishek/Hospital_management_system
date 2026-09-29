using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalManagementSystem.Api.Services;

public class DoctorScheduleService : IDoctorScheduleService
{
    private static readonly HashSet<string> OccupyingStatuses = ["Confirmed", "Completed"];
    private readonly ApplicationDbContext _db;
    private readonly AppointmentSmsNotifier _sms;
    public DoctorScheduleService(ApplicationDbContext db, AppointmentSmsNotifier sms)
    {
        _db = db;
        _sms = sms;
    }

    public async Task<IEnumerable<DoctorScheduleDto>> GetMineAsync(int userId)
    {
        var doctor = await ApprovedDoctorAsync(userId);
        return (await ScheduleQuery().Where(slot => slot.DoctorId == doctor.DoctorId)
            .OrderByDescending(slot => slot.StartAt).ToListAsync()).Select(Map);
    }

    public async Task<DoctorScheduleDto?> GetMineByIdAsync(int userId, int slotId)
    {
        var doctor = await ApprovedDoctorAsync(userId);
        var slot = await ScheduleQuery().SingleOrDefaultAsync(value => value.DoctorTimeSlotId == slotId && value.DoctorId == doctor.DoctorId);
        return slot is null ? null : Map(slot);
    }

    public async Task<DoctorScheduleDto> CreateAsync(int userId, CreateDoctorScheduleDto dto)
    {
        if (dto.Capacity < 1 || dto.Capacity > 50)
            throw new ArgumentException("Slot capacity must be between 1 and 50.");

        var doctor = await ApprovedDoctorAsync(userId);
        var activeSchedulesCount = await _db.DoctorTimeSlots
            .CountAsync(s => s.DoctorId == doctor.DoctorId && s.IsActive && s.EndAt > DateTime.UtcNow);
        if (activeSchedulesCount >= 50)
            throw new InvalidOperationException("You have reached the maximum limit of 50 active appointment schedules. Please complete or cancel existing schedules before creating new ones.");

        var room = await ValidRoomAsync(dto.RoomId);
        var startAt = Utc(dto.StartAt); var endAt = Utc(dto.EndAt);
        RoomService.ValidateRange(startAt, endAt);
        ValidateFee(dto.ConsultationFee);
        await EnsureNoOverlapAsync(doctor.DoctorId, room.RoomId, startAt, endAt, null);

        var slot = new DoctorTimeSlot
        {
            DoctorId = doctor.DoctorId, RoomId = room.RoomId,
            DoctorName = $"Dr. {doctor.FirstName} {doctor.LastName}", Specialty = doctor.Specialization,
            StartAt = startAt, EndAt = endAt, Capacity = dto.Capacity,
            ConsultationFee = decimal.Round(dto.ConsultationFee, 2), IsActive = true
        };
        _db.DoctorTimeSlots.Add(slot);
        await SaveWithConflictTranslationAsync();
        return Map((await ScheduleQuery().SingleAsync(value => value.DoctorTimeSlotId == slot.DoctorTimeSlotId)));
    }

    public async Task<DoctorScheduleDto?> UpdateAsync(int userId, int slotId, UpdateDoctorScheduleDto dto)
    {
        if (dto.Capacity < 1 || dto.Capacity > 50)
            throw new ArgumentException("Slot capacity must be between 1 and 50.");

        var doctor = await ApprovedDoctorAsync(userId);
        var slot = await ScheduleQuery().SingleOrDefaultAsync(value => value.DoctorTimeSlotId == slotId && value.DoctorId == doctor.DoctorId);
        if (slot is null) return null;
        if (!slot.IsActive) throw new InvalidOperationException("Cancelled schedules cannot be updated.");
        if (slot.EndAt <= DateTime.UtcNow) throw new InvalidOperationException("Completed schedules cannot be edited.");
        var activeCount = slot.Appointments.Count(a => OccupyingStatuses.Contains(a.Status));
        if (dto.Capacity < activeCount) throw new InvalidOperationException("Capacity cannot be less than the number of booked appointments.");
        if (slot.Appointments.Any(a => OccupyingStatuses.Contains(a.Status) && a.AppointmentNumber > dto.Capacity))
            throw new InvalidOperationException("Capacity cannot exclude an existing appointment number.");
        var room = await ValidRoomAsync(dto.RoomId);
        var startAt = Utc(dto.StartAt); var endAt = Utc(dto.EndAt);
        RoomService.ValidateRange(startAt, endAt);
        ValidateFee(dto.ConsultationFee);
        await EnsureNoOverlapAsync(doctor.DoctorId, room.RoomId, startAt, endAt, slotId);
        var changed = slot.RoomId != room.RoomId || slot.StartAt != startAt || slot.EndAt != endAt || slot.Capacity != dto.Capacity || slot.ConsultationFee != dto.ConsultationFee;
        slot.RoomId = room.RoomId; slot.Room = room; slot.StartAt = startAt; slot.EndAt = endAt;
        slot.Capacity = dto.Capacity; slot.ConsultationFee = decimal.Round(dto.ConsultationFee, 2); slot.UpdatedAt = DateTime.UtcNow;
        ScheduleAppointmentUpdates.Apply(slot, changed);
        await SaveWithConflictTranslationAsync();
        if (changed)
            foreach (var appointment in slot.Appointments.Where(a => a.Status == "Confirmed"))
                await _sms.NotifyAsync(appointment.AppointmentId, "schedule updated");
        return Map(slot);
    }

    public async Task<DoctorScheduleDto?> CancelAsync(int userId, int slotId)
    {
        var doctor = await ApprovedDoctorAsync(userId);
        var slot = await ScheduleQuery().SingleOrDefaultAsync(value => value.DoctorTimeSlotId == slotId && value.DoctorId == doctor.DoctorId);
        if (slot is null) return null;
        if (slot.Appointments.Count != 0)
            throw new InvalidOperationException("Schedules with patient bookings cannot be cancelled.");
        slot.IsActive = false; slot.UpdatedAt = DateTime.UtcNow;
        foreach (var appointment in slot.Appointments.Where(a => OccupyingStatuses.Contains(a.Status)))
        {
            appointment.Status = "Cancelled";
            appointment.CancellationReason = "Doctor schedule was cancelled.";
            appointment.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return Map(slot);
    }

    public async Task<bool> DeleteAsync(int userId, int slotId)
    {
        var doctor = await ApprovedDoctorAsync(userId);
        var slot = await ScheduleQuery().SingleOrDefaultAsync(value => value.DoctorTimeSlotId == slotId && value.DoctorId == doctor.DoctorId);
        if (slot is null) return false;
        if (slot.Appointments.Count != 0)
            throw new InvalidOperationException("Schedules with patient bookings cannot be deleted.");
        _db.DoctorTimeSlots.Remove(slot);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<Doctor> ApprovedDoctorAsync(int userId)
    {
        var doctor = await _db.Doctors.SingleOrDefaultAsync(value => value.UserId == userId);
        if (doctor is null || doctor.RegistrationStatus != DoctorRegistrationStatuses.Approved)
            throw new InvalidOperationException("An approved doctor profile is required.");
        return doctor;
    }

    private async Task<Room> ValidRoomAsync(int roomId)
    {
        var room = await _db.Rooms.SingleOrDefaultAsync(value => value.RoomId == roomId);
        if (room is null || !room.IsConfirmed) throw new InvalidOperationException("Selected room is not confirmed or does not exist.");
        return room;
    }

    private async Task EnsureNoOverlapAsync(int doctorId, int roomId, DateTime startAt, DateTime endAt, int? excludeId)
    {
        var bufferedStart = startAt.AddMinutes(-RoomService.RoomTurnoverMinutes);
        var bufferedEnd = endAt.AddMinutes(RoomService.RoomTurnoverMinutes);

        if (await _db.DoctorTimeSlots.AnyAsync(slot => slot.IsActive && slot.RoomId == roomId && slot.StartAt < bufferedEnd && bufferedStart < slot.EndAt && slot.DoctorTimeSlotId != excludeId))
            throw new InvalidOperationException("This room is already booked for the selected time period.");
        if (await _db.DoctorTimeSlots.AnyAsync(slot => slot.IsActive && slot.DoctorId == doctorId && slot.StartAt < endAt && startAt < slot.EndAt && slot.DoctorTimeSlotId != excludeId))
            throw new InvalidOperationException("You already have a schedule during the selected time period.");
    }

    private async Task SaveWithConflictTranslationAsync()
    {
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23P01" })
        { throw new InvalidOperationException("This room or doctor was booked by another request for the selected time period."); }
    }

    private static void ValidateFee(decimal fee)
    {
        if (fee <= 0 || fee > 1_000_000m)
            throw new ArgumentException("Consultation fee must be between LKR 0.01 and LKR 1,000,000.00.");
        if (decimal.Round(fee, 2) != fee)
            throw new ArgumentException("Consultation fee cannot contain more than two decimal places.");
    }

    private IQueryable<DoctorTimeSlot> ScheduleQuery() => _db.DoctorTimeSlots.Include(slot => slot.Room).Include(slot => slot.Appointments);
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DoctorScheduleDto Map(DoctorTimeSlot slot) => new()
    {
        DoctorTimeSlotId = slot.DoctorTimeSlotId, DoctorId = slot.DoctorId!.Value, RoomId = slot.RoomId!.Value,
        RoomNumber = slot.Room?.RoomNumber ?? "", RoomName = slot.Room?.RoomName ?? "", Floor = slot.Room?.Floor ?? "",
        DoctorName = slot.DoctorName, Specialty = slot.Specialty, StartAt = slot.StartAt, EndAt = slot.EndAt,
        Capacity = slot.Capacity, ConsultationFee = slot.ConsultationFee,
        BookedCount = slot.Appointments.Count(a => OccupyingStatuses.Contains(a.Status)),
        HasPatientBookings = slot.Appointments.Count != 0, IsActive = slot.IsActive
    };
}
