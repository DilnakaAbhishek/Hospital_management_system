using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IAppointmentRepository _repository;
        private readonly AppointmentSmsNotifier _sms;
        private static readonly HashSet<string> ValidStatuses = ["Confirmed", "Completed", "Cancelled"];
        private static readonly HashSet<string> TerminalStatuses = ["Completed", "Cancelled"];
        private static readonly HashSet<string> OccupyingStatuses = ["Confirmed", "Completed"];

        public AppointmentService(IAppointmentRepository repository, AppointmentSmsNotifier sms)
        {
            _repository = repository;
            _sms = sms;
        }

        public async Task<PagedResult<AppointmentDto>> GetAllAppointmentsAsync(string? search, string? status, string? doctorName, DateTime? date, string? sortBy, string? sortDirection, int page, int pageSize, int? patientId = null, string? patientEmail = null, int? doctorId = null)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(1, page);

            var appointments = await _repository.GetAllAsync(search, status, doctorName, date, sortBy, sortDirection, page, pageSize, patientId, patientEmail, doctorId);
            var totalCount = await _repository.GetTotalCountAsync(search, status, doctorName, date, patientId, patientEmail, doctorId);

            return new PagedResult<AppointmentDto>
            {
                Data = appointments.Select(MapAppointment),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AppointmentDto?> GetAppointmentByIdAsync(int id)
        {
            var appointment = await _repository.GetByIdAsync(id);
            return appointment is null ? null : MapAppointment(appointment);
        }

        public async Task<AppointmentDto> CreateAppointmentAsync(CreateAppointmentDto dto)
        {
            await GetActiveSlotAsync(dto.DoctorTimeSlotId);

            var appointment = new Appointment
            {
                DoctorTimeSlotId = dto.DoctorTimeSlotId,
                PatientId = dto.PatientId,
                PatientName = dto.PatientName.Trim(),
                PatientPhone = dto.PatientPhone.Trim(),
                PatientEmail = dto.PatientEmail?.Trim().ToLower(),
                AppointmentType = dto.AppointmentType.Trim(),
                Reason = string.Empty,
                Status = "Confirmed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var saved = await _repository.CreateAsync(appointment);
            await _sms.NotifyAsync(saved.AppointmentId, "confirmed");
            return MapAppointment(saved);
        }

        private async Task<DoctorTimeSlot> GetActiveSlotAsync(int slotId)
        {
            var slot = await _repository.GetSlotByIdAsync(slotId);
            if (slot is null || !slot.IsActive)
                throw new InvalidOperationException("Selected doctor time slot is not available.");

            if (slot.StartAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Past doctor time slots cannot be booked.");

            return slot;
        }

        public async Task<AppointmentDto?> UpdateAppointmentAsync(int id, UpdateAppointmentDto dto)
        {
            if (!ValidStatuses.Contains(dto.Status))
                throw new InvalidOperationException("Invalid appointment status.");

            var appointment = await _repository.GetByIdAsync(id);
            if (appointment is null) return null;

            var previousStatus = appointment.Status;
            var previousSlotId = appointment.DoctorTimeSlotId;
            var previousNumber = appointment.AppointmentNumber;

            if (appointment.DoctorTimeSlotId != dto.DoctorTimeSlotId)
            {
                var slot = await ValidateSlotCapacityAsync(dto.DoctorTimeSlotId, id);
                var appointmentNumber = await GetNextAppointmentNumberAsync(slot, id);
                appointment.AppointmentNumber = appointmentNumber;

                var localTime = TimeZoneInfo.ConvertTimeFromUtc(slot.StartAt,
                    TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo"));
                appointment.Notifications.Add(new AppointmentNotification
                {
                    Message = $"Your appointment (previously #{previousNumber}) with {slot.DoctorName} was rescheduled. New appointment number: #{appointmentNumber}. New appointment time: {localTime:dd MMM yyyy, hh:mm tt} (Sri Lanka time)."
                });
            }

            appointment.DoctorTimeSlotId = dto.DoctorTimeSlotId;
            appointment.PatientId = dto.PatientId;
            appointment.PatientName = dto.PatientName.Trim();
            appointment.PatientPhone = dto.PatientPhone.Trim();
            appointment.PatientEmail = dto.PatientEmail?.Trim().ToLower();
            appointment.AppointmentType = dto.AppointmentType.Trim();
            appointment.Status = dto.Status;

            var saved = await _repository.UpdateAsync(appointment);
            if (saved.Status == "Cancelled" && previousStatus != "Cancelled")
                await _sms.NotifyAsync(id, "cancelled");
            else if (saved.Status == "Confirmed" && (previousSlotId != saved.DoctorTimeSlotId || previousStatus != "Confirmed"))
                await _sms.NotifyAsync(id, previousSlotId != saved.DoctorTimeSlotId ? "rescheduled" : "confirmed");
            return MapAppointment(saved);
        }

        public async Task<AppointmentDto?> CancelAppointmentAsync(int id, string reason)
        {
            var appointment = await _repository.GetByIdAsync(id);
            if (appointment is null) return null;

            if (appointment.Status == "Completed")
                throw new InvalidOperationException("Completed appointments cannot be cancelled.");

            var notify = appointment.Status != "Cancelled";
            appointment.Status = "Cancelled";
            appointment.CancellationReason = reason.Trim();
            if (notify)
            {
                var docName = appointment.DoctorTimeSlot?.DoctorName ?? "doctor";
                appointment.Notifications.Add(new AppointmentNotification
                {
                    Message = $"Appointment #{appointment.AppointmentNumber} with {docName} was cancelled. Reason: {reason.Trim()}."
                });
            }
            var saved = await _repository.UpdateAsync(appointment);
            if (notify) await _sms.NotifyAsync(id, "cancelled");
            return MapAppointment(saved);
        }

        public async Task<AppointmentDto?> RescheduleAppointmentAsync(int id, int doctorTimeSlotId)
        {
            var appointment = await _repository.GetByIdAsync(id);
            if (appointment is null) return null;
            if (TerminalStatuses.Contains(appointment.Status))
                throw new InvalidOperationException("Terminal appointments cannot be rescheduled.");

            var slot = await ValidateSlotCapacityAsync(doctorTimeSlotId, id);
            var notify = appointment.DoctorTimeSlotId != doctorTimeSlotId;
            var previousNumber = appointment.AppointmentNumber;
            var appointmentNumber = await GetNextAppointmentNumberAsync(slot, id);
            appointment.DoctorTimeSlotId = doctorTimeSlotId;
            appointment.AppointmentNumber = appointmentNumber;
            appointment.Status = "Confirmed";
            if (notify)
            {
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(slot.StartAt,
                    TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo"));
                appointment.Notifications.Add(new AppointmentNotification
                {
                    Message = $"Your appointment (previously #{previousNumber}) with {slot.DoctorName} was rescheduled. New appointment number: #{appointmentNumber}. New appointment time: {localTime:dd MMM yyyy, hh:mm tt} (Sri Lanka time)."
                });
            }
            var saved = await _repository.UpdateAsync(appointment);
            if (notify) await _sms.NotifyAsync(id, "rescheduled");
            return MapAppointment(saved);
        }

        public async Task<IEnumerable<DoctorLookupDto>> GetDoctorsAsync(string? specialty = null)
        {
            var doctors = await _repository.GetApprovedDoctorsAsync();
            if (!string.IsNullOrWhiteSpace(specialty))
            {
                var normalizedSpecialty = specialty.Trim();
                doctors = doctors.Where(doctor =>
                    string.Equals(doctor.Specialization?.Trim(), normalizedSpecialty, StringComparison.OrdinalIgnoreCase));
            }

            return doctors
                .Select(doctor => new DoctorLookupDto
                {
                    DoctorId = doctor.DoctorId,
                    DoctorName = FormatDoctorName(doctor),
                    Specialty = doctor.Specialization.Trim()
                })
                .OrderBy(doctor => doctor.DoctorName);
        }

        public async Task<IEnumerable<string>> GetSpecializationsAsync() =>
            await _repository.GetApprovedSpecializationsAsync();

        public async Task<IEnumerable<DoctorTimeSlotDto>> GetSlotsAsync(string? doctorName, DateTime? date, bool onlyAvailable, int? doctorId = null)
        {
            var slots = await _repository.GetSlotsAsync(doctorName, date, onlyAvailable, doctorId);
            if (onlyAvailable)
                slots = slots.Where(s => s.StartAt > DateTime.UtcNow);

            return slots.Select(MapSlot);
        }

        public async Task<DoctorTimeSlotDto> CreateSlotAsync(CreateDoctorTimeSlotDto dto)
        {
            if (dto.Capacity < 1 || dto.Capacity > 50)
                throw new InvalidOperationException("Slot capacity must be between 1 and 50.");

            var doctor = await GetApprovedDoctorAsync(dto.DoctorId);
            var activeSlotsCount = await _repository.GetActiveSlotsCountByDoctorIdAsync(doctor.DoctorId);
            if (activeSlotsCount >= 50)
                throw new InvalidOperationException("This doctor has reached the maximum limit of 50 active appointment schedules. Please complete or cancel existing schedules before creating new ones.");

            var doctorName = FormatDoctorName(doctor);
            var specialty = doctor.Specialization.Trim();
            var startAt = DateTime.SpecifyKind(dto.StartAt, DateTimeKind.Utc);
            var endAt = DateTime.SpecifyKind(dto.EndAt, DateTimeKind.Utc);

            if (string.IsNullOrWhiteSpace(doctorName) || string.IsNullOrWhiteSpace(specialty))
                throw new InvalidOperationException("Doctor name and specialty are required.");

            if (endAt <= startAt)
                throw new InvalidOperationException("Slot end time must be after start time.");

            ValidateConsultationFee(dto.ConsultationFee);

            if (await _repository.SlotOverlapsAsync(doctorName, startAt, endAt, doctorId: dto.DoctorId))
                throw new InvalidOperationException("This doctor already has an overlapping time slot.");

            if (dto.RoomId.HasValue)
                await ValidateAvailableRoomAsync(dto.RoomId.Value, startAt, endAt);

            var slot = new DoctorTimeSlot
            {
                DoctorId = doctor.DoctorId,
                RoomId = dto.RoomId,
                DoctorName = doctorName,
                Specialty = specialty,
                StartAt = startAt,
                EndAt = endAt,
                Capacity = dto.Capacity,
                ConsultationFee = decimal.Round(dto.ConsultationFee, 2),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return MapSlot(await _repository.CreateSlotAsync(slot));
        }

        public async Task<DoctorTimeSlotDto?> UpdateSlotAsync(int id, UpdateDoctorTimeSlotDto dto)
        {
            if (dto.Capacity < 1 || dto.Capacity > 50)
                throw new InvalidOperationException("Slot capacity must be between 1 and 50.");

            var slot = await _repository.GetSlotByIdAsync(id);
            if (slot is null) return null;

            var doctor = await GetApprovedDoctorAsync(dto.DoctorId);
            var doctorName = FormatDoctorName(doctor);
            var specialty = doctor.Specialization.Trim();
            var startAt = DateTime.SpecifyKind(dto.StartAt, DateTimeKind.Utc);
            var endAt = DateTime.SpecifyKind(dto.EndAt, DateTimeKind.Utc);
            var activeAppointments = slot.Appointments
                .Where(a => OccupyingStatuses.Contains(a.Status))
                .OrderBy(a => a.AppointmentNumber)
                .ToList();

            if (string.IsNullOrWhiteSpace(doctorName) || string.IsNullOrWhiteSpace(specialty))
                throw new InvalidOperationException("Doctor name and specialty are required.");

            if (endAt <= startAt)
                throw new InvalidOperationException("Slot end time must be after start time.");

            ValidateConsultationFee(dto.ConsultationFee);

            if (slot.EndAt <= DateTime.UtcNow || !slot.IsActive)
                throw new InvalidOperationException("Completed or cancelled slots cannot be edited.");

            if (dto.Capacity < activeAppointments.Count)
                throw new InvalidOperationException("Slot capacity cannot be less than the number of booked appointments.");

            if (activeAppointments.Any(a => a.AppointmentNumber > dto.Capacity))
                throw new InvalidOperationException("Capacity cannot exclude an existing appointment number.");

            if (await _repository.SlotOverlapsAsync(doctorName, startAt, endAt, id, dto.DoctorId))
                throw new InvalidOperationException("This doctor already has an overlapping time slot.");

            if (dto.RoomId.HasValue)
                await ValidateAvailableRoomAsync(dto.RoomId.Value, startAt, endAt, id);

            var changed = slot.StartAt != startAt || slot.EndAt != endAt || slot.Capacity != dto.Capacity ||
                slot.RoomId != dto.RoomId || slot.DoctorId != doctor.DoctorId || slot.ConsultationFee != dto.ConsultationFee || slot.IsActive != dto.IsActive;
            slot.DoctorId = doctor.DoctorId;
            slot.RoomId = dto.RoomId;
            slot.DoctorName = doctorName;
            slot.Specialty = specialty;
            slot.StartAt = startAt;
            slot.EndAt = endAt;
            slot.Capacity = dto.Capacity;
            slot.ConsultationFee = decimal.Round(dto.ConsultationFee, 2);
            slot.IsActive = dto.IsActive;

            ScheduleAppointmentUpdates.Apply(slot, changed);

            var updated = await _repository.UpdateSlotAsync(slot);
            if (changed)
                foreach (var appointment in activeAppointments.Where(a => a.Status == "Confirmed"))
                    await _sms.NotifyAsync(appointment.AppointmentId, slot.IsActive ? "schedule updated" : "session unavailable");
            return MapSlot(updated);
        }

        public async Task<DoctorTimeSlotDto?> CancelSlotAsync(int id, string? reason)
        {
            var slot = await _repository.GetSlotByIdAsync(id);
            if (slot is null) return null;

            var affectedAppointments = slot.Appointments
                .Where(a => a.Status == "Confirmed")
                .OrderBy(a => a.AppointmentNumber)
                .ToList();

            slot.IsActive = false;
            foreach (var appointment in affectedAppointments)
            {
                appointment.Status = "Cancelled";
                appointment.CancellationReason = string.IsNullOrWhiteSpace(reason)
                    ? "Doctor time slot cancelled."
                    : reason.Trim();
                appointment.Notifications.Add(new AppointmentNotification
                {
                    Message = $"Appointment #{appointment.AppointmentNumber} with {slot.DoctorName} was cancelled because the session was cancelled. Reason: {appointment.CancellationReason}"
                });
            }

            var updated = await _repository.UpdateSlotAsync(slot);
            foreach (var appointment in affectedAppointments)
                await _sms.NotifyAsync(appointment.AppointmentId, "cancelled");
            return MapSlot(updated);
        }

        private async Task<DoctorTimeSlot> ValidateSlotCapacityAsync(int slotId, int? excludeAppointmentId = null)
        {
            var slot = await GetActiveSlotAsync(slotId);

            var bookedCount = await _repository.GetActiveBookingCountAsync(slotId, excludeAppointmentId);
            if (bookedCount >= slot.Capacity)
                throw new InvalidOperationException("Selected doctor time slot is fully booked.");

            return slot;
        }

        private async Task<Doctor> GetApprovedDoctorAsync(int? doctorId)
        {
            if (!doctorId.HasValue)
                throw new InvalidOperationException("Select an approved registered doctor.");

            var doctor = await _repository.GetApprovedDoctorByIdAsync(doctorId.Value);
            return doctor ?? throw new InvalidOperationException("Selected doctor is not approved or does not exist.");
        }

        private static string FormatDoctorName(Doctor doctor) =>
            $"Dr. {doctor.FirstName} {doctor.LastName}".Trim();

        private async Task ValidateAvailableRoomAsync(int roomId, DateTime startAt, DateTime endAt, int? excludeSlotId = null)
        {
            var room = await _repository.GetRoomByIdAsync(roomId);
            if (room is null || !room.IsConfirmed)
                throw new InvalidOperationException("Selected room is not confirmed or does not exist.");

            if (await _repository.RoomOverlapsAsync(roomId, startAt, endAt, excludeSlotId))
                throw new InvalidOperationException("This room is already booked for the selected time period.");
        }

        private static void ValidateConsultationFee(decimal fee)
        {
            if (fee <= 0 || fee > 1_000_000m)
                throw new InvalidOperationException("Consultation fee must be between LKR 0.01 and LKR 1,000,000.00.");

            if (decimal.Round(fee, 2) != fee)
                throw new InvalidOperationException("Consultation fee can contain a maximum of two decimal places.");
        }

        private async Task<int> GetNextAppointmentNumberAsync(DoctorTimeSlot slot, int? excludeAppointmentId = null)
        {
            var bookedNumbers = (await _repository.GetBookedAppointmentNumbersAsync(slot.DoctorTimeSlotId, excludeAppointmentId)).ToHashSet();
            var number = AppointmentNumbering.NextAvailable(slot.Capacity, bookedNumbers);
            if (number > 0) return number;

            throw new InvalidOperationException("Selected doctor time slot is fully booked.");
        }

        private static AppointmentDto MapAppointment(Appointment a)
        {
            var slot = a.DoctorTimeSlot;
            var bookedCount = slot?.Appointments.Count(x => x.Status is "Confirmed" or "Completed") ?? 0;

            return new AppointmentDto
            {
                AppointmentId = a.AppointmentId,
                DoctorTimeSlotId = a.DoctorTimeSlotId,
                DoctorId = slot?.DoctorId,
                PatientId = a.PatientId,
                AppointmentNumber = a.AppointmentNumber,
                PatientName = a.PatientName,
                PatientPhone = a.PatientPhone,
                PatientEmail = a.PatientEmail ?? string.Empty,
                DoctorName = slot?.DoctorName ?? string.Empty,
                Specialty = slot?.Specialty ?? string.Empty,
                RoomId = slot?.RoomId,
                RoomNumber = slot?.Room?.RoomNumber ?? string.Empty,
                RoomName = slot?.Room?.RoomName ?? string.Empty,
                Floor = slot?.Room?.Floor ?? string.Empty,
                StartAt = slot?.StartAt ?? DateTime.MinValue,
                EndAt = slot?.EndAt ?? DateTime.MinValue,
                SlotCapacity = slot?.Capacity ?? 0,
                BookedCount = bookedCount,
                ConsultationFee = slot?.ConsultationFee ?? 0,
                AppointmentType = a.AppointmentType,
                Reason = a.Reason,
                Status = a.Status,
                CancellationReason = a.CancellationReason ?? string.Empty,
                Notes = a.Notes ?? string.Empty,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            };
        }

        private static DoctorTimeSlotDto MapSlot(DoctorTimeSlot s) => new()
        {
            BookedAppointmentNumbers = s.Appointments
                .Where(a => a.Status is "Confirmed" or "Completed")
                .Select(a => a.AppointmentNumber)
                .OrderBy(n => n),
            DoctorTimeSlotId = s.DoctorTimeSlotId,
            DoctorId = s.DoctorId,
            DoctorName = s.DoctorName,
            Specialty = s.Specialty,
            StartAt = s.StartAt,
            EndAt = s.EndAt,
            Capacity = s.Capacity,
            BookedCount = s.Appointments.Count(a => a.Status is "Confirmed" or "Completed"),
            ConsultationFee = s.ConsultationFee,
            NextAppointmentNumber = GetNextAppointmentNumber(s),
            IsActive = s.IsActive,
            RoomId = s.RoomId,
            RoomNumber = s.Room?.RoomNumber ?? string.Empty,
            RoomName = s.Room?.RoomName ?? string.Empty,
            Floor = s.Room?.Floor ?? string.Empty
        };

        private static int GetNextAppointmentNumber(DoctorTimeSlot s)
        {
            var bookedNumbers = s.Appointments
                .Where(a => a.Status is "Confirmed" or "Completed")
                .Select(a => a.AppointmentNumber)
                .ToHashSet();

            return AppointmentNumbering.NextAvailable(s.Capacity, bookedNumbers);
        }

    }
}
