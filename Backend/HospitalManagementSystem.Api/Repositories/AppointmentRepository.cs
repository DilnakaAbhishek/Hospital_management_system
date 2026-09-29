using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly ApplicationDbContext _context;
        private static readonly string[] OccupyingStatuses = ["Confirmed", "Completed"];

        public AppointmentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Appointment>> GetAllAsync(string? search, string? status, string? doctorName, DateTime? date, string? sortBy, string? sortDirection, int page, int pageSize, int? patientId = null, string? patientEmail = null, int? doctorId = null)
        {
            await AppointmentCompletionService.CompleteDueAsync(_context);
            var query = BuildAppointmentQuery(search, status, doctorName, date, patientId, patientEmail, doctorId);
            var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

            query = sortBy?.ToLower() switch
            {
                "patient" => descending ? query.OrderByDescending(a => a.PatientName) : query.OrderBy(a => a.PatientName),
                "doctor" => descending ? query.OrderByDescending(a => a.DoctorTimeSlot!.DoctorName) : query.OrderBy(a => a.DoctorTimeSlot!.DoctorName),
                "status" => descending ? query.OrderByDescending(a => a.Status) : query.OrderBy(a => a.Status),
                "created" => descending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
                _ => descending
                    ? query.OrderByDescending(a => a.DoctorTimeSlot!.StartAt).ThenByDescending(a => a.AppointmentNumber)
                    : query.OrderBy(a => a.DoctorTimeSlot!.StartAt).ThenBy(a => a.AppointmentNumber)
            };

            return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }

        public async Task<int> GetTotalCountAsync(string? search, string? status, string? doctorName, DateTime? date, int? patientId = null, string? patientEmail = null, int? doctorId = null) =>
            await BuildAppointmentQuery(search, status, doctorName, date, patientId, patientEmail, doctorId).CountAsync();

        public async Task<Appointment?> GetByIdAsync(int id)
        {
            await AppointmentCompletionService.CompleteDueAsync(_context);
            return await _context.Appointments
                .Include(a => a.DoctorTimeSlot).ThenInclude(slot => slot!.Room)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);
        }

        public async Task<Appointment> CreateAsync(Appointment appointment)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(_context, async (context, ct) =>
            {
                // Serialize bookings for the same session across API instances.
                // The existing unique index remains the final duplicate-number guard.
                await using var transaction = context.Database.IsRelational() && context.Database.CurrentTransaction is null
                    ? await context.Database.BeginTransactionAsync(ct)
                    : null;
                if (context.Database.IsNpgsql())
                {
                    await context.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT 1 FROM \"DoctorTimeSlots\" WHERE \"DoctorTimeSlotId\" = {appointment.DoctorTimeSlotId} FOR UPDATE", ct);
                }

                var slot = await context.DoctorTimeSlots.AsNoTracking()
                    .SingleOrDefaultAsync(s => s.DoctorTimeSlotId == appointment.DoctorTimeSlotId, ct);
                if (slot is null || !slot.IsActive)
                    throw new InvalidOperationException("Selected doctor time slot is not available.");
                if (slot.StartAt <= DateTime.UtcNow)
                    throw new InvalidOperationException("Past doctor time slots cannot be booked.");

                var bookedNumbers = (await GetBookedAppointmentNumbersAsync(slot.DoctorTimeSlotId)).ToHashSet();
                // Recalculate after acquiring the lock; the displayed preview is not a reservation.
                appointment.AppointmentNumber = AppointmentNumbering.NextAvailable(slot.Capacity, bookedNumbers);
                if (appointment.AppointmentNumber == 0)
                    throw new InvalidOperationException("Selected doctor time slot is fully booked.");

                context.Appointments.Add(appointment);
                await context.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return (await GetByIdAsync(appointment.AppointmentId))!;
            }, default);
        }

        public async Task<Appointment> UpdateAsync(Appointment appointment)
        {
            appointment.UpdatedAt = DateTime.UtcNow;
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
            return (await GetByIdAsync(appointment.AppointmentId))!;
        }

        public async Task<int> GetActiveBookingCountAsync(int doctorTimeSlotId, int? excludeAppointmentId = null) =>
            await _context.Appointments.CountAsync(a =>
                a.DoctorTimeSlotId == doctorTimeSlotId &&
                OccupyingStatuses.Contains(a.Status) &&
                (!excludeAppointmentId.HasValue || a.AppointmentId != excludeAppointmentId.Value));

        public async Task<IEnumerable<int>> GetBookedAppointmentNumbersAsync(int doctorTimeSlotId, int? excludeAppointmentId = null) =>
            await _context.Appointments
                .Where(a =>
                    a.DoctorTimeSlotId == doctorTimeSlotId &&
                    OccupyingStatuses.Contains(a.Status) &&
                    (!excludeAppointmentId.HasValue || a.AppointmentId != excludeAppointmentId.Value))
                .Select(a => a.AppointmentNumber)
                .ToListAsync();

        public async Task<DoctorTimeSlot?> GetSlotByIdAsync(int id) =>
            await _context.DoctorTimeSlots
                .Include(s => s.Appointments)
                .Include(s => s.Room)
                .FirstOrDefaultAsync(s => s.DoctorTimeSlotId == id);

        public async Task<IEnumerable<DoctorTimeSlot>> GetSlotsAsync(string? doctorName, DateTime? date, bool onlyAvailable, int? doctorId = null)
        {
            var query = _context.DoctorTimeSlots.Include(s => s.Appointments).Include(s => s.Room).AsQueryable();

            if (doctorId.HasValue)
            {
                query = query.Where(s => s.DoctorId == doctorId.Value);
            }

            if (!string.IsNullOrWhiteSpace(doctorName))
            {
                var term = doctorName.ToLower();
                query = query.Where(s => s.DoctorName.ToLower().Contains(term));
            }

            if (date.HasValue)
            {
                var start = date.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(s => s.StartAt >= start && s.StartAt < end);
            }

            if (onlyAvailable)
            {
                query = query.Where(s => s.IsActive && s.Appointments.Count(a => OccupyingStatuses.Contains(a.Status)) < s.Capacity);
            }

            return await query.OrderBy(s => s.StartAt).ToListAsync();
        }

        public async Task<IEnumerable<Doctor>> GetApprovedDoctorsAsync() =>
            await _context.Doctors
                .AsNoTracking()
                .Where(doctor => doctor.RegistrationStatus.Trim().ToLower() == DoctorRegistrationStatuses.Approved.ToLower())
                .OrderBy(doctor => doctor.FirstName)
                .ThenBy(doctor => doctor.LastName)
                .ToListAsync();

        public async Task<IEnumerable<string>> GetApprovedSpecializationsAsync() =>
            await _context.Doctors
                .AsNoTracking()
                .Where(doctor =>
                    doctor.RegistrationStatus.Trim().ToLower() == DoctorRegistrationStatuses.Approved.ToLower() &&
                    doctor.Specialization.Trim() != string.Empty)
                .Select(doctor => doctor.Specialization.Trim())
                .Distinct()
                .OrderBy(specialization => specialization)
                .ToListAsync();

        public async Task<Doctor?> GetApprovedDoctorByIdAsync(int id) =>
            await _context.Doctors
                .AsNoTracking()
                .SingleOrDefaultAsync(doctor =>
                    doctor.DoctorId == id &&
                    doctor.RegistrationStatus.Trim().ToLower() == DoctorRegistrationStatuses.Approved.ToLower());

        public async Task<Room?> GetRoomByIdAsync(int id) =>
            await _context.Rooms.AsNoTracking().SingleOrDefaultAsync(room => room.RoomId == id);

        public async Task<DoctorTimeSlot> CreateSlotAsync(DoctorTimeSlot slot)
        {
            _context.DoctorTimeSlots.Add(slot);
            await _context.SaveChangesAsync();
            return slot;
        }

        public async Task<DoctorTimeSlot> UpdateSlotAsync(DoctorTimeSlot slot)
        {
            slot.UpdatedAt = DateTime.UtcNow;
            _context.DoctorTimeSlots.Update(slot);
            await _context.SaveChangesAsync();
            return (await GetSlotByIdAsync(slot.DoctorTimeSlotId))!;
        }

        public async Task<bool> SlotOverlapsAsync(string doctorName, DateTime startAt, DateTime endAt, int? excludeSlotId = null, int? doctorId = null)
        {
            var query = _context.DoctorTimeSlots.Where(s =>
                s.StartAt < endAt &&
                startAt < s.EndAt &&
                s.IsActive &&
                (!excludeSlotId.HasValue || s.DoctorTimeSlotId != excludeSlotId.Value));

            if (doctorId.HasValue)
            {
                var doctorIdValue = doctorId.Value;
                query = query.Where(s => s.DoctorId == doctorIdValue);
            }
            else
            {
                var normalizedDoctorName = doctorName.ToLower();
                query = query.Where(s => s.DoctorName.ToLower() == normalizedDoctorName);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> RoomOverlapsAsync(int roomId, DateTime startAt, DateTime endAt, int? excludeSlotId = null) =>
            await _context.DoctorTimeSlots.AnyAsync(s =>
                s.RoomId == roomId &&
                s.StartAt < endAt.AddMinutes(RoomService.RoomTurnoverMinutes) &&
                startAt.AddMinutes(-RoomService.RoomTurnoverMinutes) < s.EndAt &&
                s.IsActive &&
                (!excludeSlotId.HasValue || s.DoctorTimeSlotId != excludeSlotId.Value));

        private IQueryable<Appointment> BuildAppointmentQuery(string? search, string? status, string? doctorName, DateTime? date, int? patientId = null, string? patientEmail = null, int? doctorId = null)
        {
            var query = _context.Appointments
                .Include(a => a.DoctorTimeSlot).ThenInclude(slot => slot!.Room)
                .Include(a => a.Patient)
                .AsQueryable();

            if (patientId.HasValue || !string.IsNullOrWhiteSpace(patientEmail))
            {
                var normalizedEmail = patientEmail?.Trim().ToLower();
                var hasPatientId = patientId.HasValue;
                var patientIdValue = patientId.GetValueOrDefault();
                var hasEmail = !string.IsNullOrWhiteSpace(normalizedEmail);
                query = query.Where(a =>
                    (hasPatientId && a.PatientId == patientIdValue) ||
                    (hasEmail && a.PatientEmail != null && a.PatientEmail.ToLower() == normalizedEmail));
            }

            if (doctorId.HasValue || !string.IsNullOrWhiteSpace(doctorName))
            {
                var term = !string.IsNullOrWhiteSpace(doctorName)
                    ? doctorName.Trim().ToLower().Replace("dr.", "").Trim()
                    : string.Empty;

                if (doctorId.HasValue && !string.IsNullOrWhiteSpace(term))
                {
                    query = query.Where(a => a.DoctorTimeSlot != null &&
                        (a.DoctorTimeSlot.DoctorId == doctorId.Value ||
                         a.DoctorTimeSlot.DoctorName.ToLower().Contains(term)));
                }
                else if (doctorId.HasValue)
                {
                    query = query.Where(a => a.DoctorTimeSlot != null && a.DoctorTimeSlot.DoctorId == doctorId.Value);
                }
                else if (!string.IsNullOrWhiteSpace(term))
                {
                    query = query.Where(a => a.DoctorTimeSlot != null && a.DoctorTimeSlot.DoctorName.ToLower().Contains(term));
                }
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.ToLower();
                query = query.Where(a =>
                    a.PatientName.ToLower().Contains(term) ||
                    a.PatientPhone.Contains(term) ||
                    (a.PatientEmail != null && a.PatientEmail.ToLower().Contains(term)) ||
                    (a.DoctorTimeSlot != null && a.DoctorTimeSlot.DoctorName.ToLower().Contains(term)) ||
                    a.Reason.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                query = query.Where(a => a.Status == status);
            }

            if (date.HasValue)
            {
                var start = date.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(a => a.DoctorTimeSlot!.StartAt >= start && a.DoctorTimeSlot.StartAt < end);
            }

            return query;
        }

        public async Task<int> GetActiveSlotsCountByDoctorIdAsync(int doctorId)
        {
            var now = DateTime.UtcNow;
            return await _context.DoctorTimeSlots
                .CountAsync(s => s.DoctorId == doctorId && s.IsActive && s.EndAt > now);
        }
    }
}
