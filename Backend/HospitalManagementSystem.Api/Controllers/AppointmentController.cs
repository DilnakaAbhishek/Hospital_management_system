using System.Security.Claims;
using HospitalManagementSystem.Api.Data;
using Microsoft.AspNetCore.Mvc;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentController : ControllerBase
    {
        private const string StaffRoles = "Admin,Doctor";
        private const string AppointmentReaderRoles = "Admin,Doctor,Patient";
        private const string AppointmentBookingRoles = "Admin,Patient";
        private const string SlotReaderRoles = "Admin,Doctor,Patient";

        private readonly IAppointmentService _service;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<AppointmentController> _logger;

        public AppointmentController(IAppointmentService service, ApplicationDbContext db, ILogger<AppointmentController> logger)
        {
            _service = service;
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = AppointmentReaderRoles)]
        [ProducesResponseType(typeof(PagedResult<AppointmentDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] string? doctorName,
            [FromQuery] DateTime? date,
            [FromQuery] string? sortBy = "startAt",
            [FromQuery] string? sortDirection = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var access = await GetAppointmentAccessAsync();
            if (access.Result is not null) return access.Result;

            var resolvedDoctorName = doctorName;
            if (User.IsInRole("Doctor") && string.IsNullOrWhiteSpace(resolvedDoctorName))
            {
                var doctor = await GetApprovedDoctorForCurrentUserAsync();
                if (doctor is not null)
                {
                    resolvedDoctorName = $"{doctor.FirstName} {doctor.LastName}".Trim();
                }
                else
                {
                    resolvedDoctorName = User.FindFirstValue(ClaimTypes.Name);
                }
            }

            var result = await _service.GetAllAppointmentsAsync(
                search, status, resolvedDoctorName, date, sortBy, sortDirection, page, pageSize,
                access.PatientId, access.PatientEmail, access.DoctorId);
            return Ok(result);
        }

        [HttpGet("notifications")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetNotifications()
        {
            var access = await GetAppointmentAccessAsync();
            if (access.Result is not null) return access.Result;
            var patientId = access.PatientId;
            var email = access.PatientEmail;
            var notifications = await _db.AppointmentNotifications.AsNoTracking()
                .Where(n => (patientId.HasValue && n.Appointment.PatientId == patientId) ||
                    (email != null && n.Appointment.PatientEmail != null && n.Appointment.PatientEmail.ToLower() == email))
                .OrderByDescending(n => n.CreatedAt).Take(100)
                .Select(n => new { n.AppointmentNotificationId, n.AppointmentId, n.Message, CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc) })
                .ToListAsync();
            return Ok(notifications);
        }

        [HttpGet("staff-notifications")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> GetStaffNotifications()
        {
            var access = await GetAppointmentAccessAsync();
            if (access.Result is not null) return access.Result;

            var query = _db.AppointmentNotifications.AsNoTracking()
                .Include(n => n.Appointment)
                    .ThenInclude(a => a.DoctorTimeSlot)
                .Where(n => !n.Message.ToLower().Contains("rescheduled"))
                .AsQueryable();

            if (User.IsInRole("Doctor"))
            {
                var doctor = await GetApprovedDoctorForCurrentUserAsync();
                var doctorId = doctor?.DoctorId ?? access.DoctorId;
                var doctorName = doctor != null ? $"{doctor.FirstName} {doctor.LastName}".Trim() : null;

                var doctorLastName = doctor != null && !string.IsNullOrWhiteSpace(doctor.LastName) ? doctor.LastName.Trim().ToLower() : null;
                var doctorNameLower = doctorName?.ToLower();

                query = query.Where(n =>
                    (n.Appointment.DoctorTimeSlot != null && (
                        (doctorId.HasValue && n.Appointment.DoctorTimeSlot.DoctorId == doctorId.Value) ||
                        (doctorNameLower != null && n.Appointment.DoctorTimeSlot.DoctorName.ToLower().Contains(doctorNameLower)) ||
                        (doctorLastName != null && n.Appointment.DoctorTimeSlot.DoctorName.ToLower().Contains(doctorLastName)))) ||
                    (doctorNameLower != null && n.Message.ToLower().Contains(doctorNameLower)) ||
                    (doctorLastName != null && n.Message.ToLower().Contains(doctorLastName)));
            }

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt).Take(50)
                .Select(n => new
                {
                    n.AppointmentNotificationId,
                    n.AppointmentId,
                    n.Message,
                    CreatedAt = DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc)
                })
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = AppointmentReaderRoles)]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var appointment = await _service.GetAppointmentByIdAsync(id);
            if (appointment is null)
                return NotFound(new { message = $"Appointment with ID {id} not found." });

            var access = await GetAppointmentAccessAsync();
            if (access.Result is not null) return access.Result;
            if (!CanAccessAppointment(appointment, access))
                return Forbid();

            return Ok(appointment);
        }

        /// <summary>
        /// Books the selected doctor session. The server assigns both the
        /// appointment ID and the next available number in that session.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = AppointmentBookingRoles)]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var access = await GetAppointmentAccessAsync();
            if (access.Result is not null) return access.Result;
            if (User.IsInRole("Patient"))
            {
                dto.PatientId = access.PatientId;
                dto.PatientEmail = access.PatientEmail;
            }

            try
            {
                var created = await _service.CreateAppointmentAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.AppointmentId }, created);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Appointment creation rejected: {Message}", ex.Message);
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var updated = await _service.UpdateAppointmentAsync(id, dto);
                return updated is null
                    ? NotFound(new { message = $"Appointment with ID {id} not found." })
                    : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("{id:int}/cancel")]
        [Authorize(Roles = AppointmentReaderRoles)]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(int id, [FromBody] CancelAppointmentDto dto)
        {
            try
            {
                var appointment = await _service.GetAppointmentByIdAsync(id);
                if (appointment is null)
                    return NotFound(new { message = $"Appointment with ID {id} not found." });

                var access = await GetAppointmentAccessAsync();
                if (access.Result is not null) return access.Result;
                if (!CanAccessAppointment(appointment, access))
                    return Forbid();

                var updated = await _service.CancelAppointmentAsync(id, dto.Reason);
                return updated is null
                    ? NotFound(new { message = $"Appointment with ID {id} not found." })
                    : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("{id:int}/reschedule")]
        [Authorize(Roles = AppointmentBookingRoles)]
        [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reschedule(int id, [FromBody] RescheduleAppointmentDto dto)
        {
            try
            {
                var appointment = await _service.GetAppointmentByIdAsync(id);
                if (appointment is null)
                    return NotFound(new { message = $"Appointment with ID {id} not found." });

                var access = await GetAppointmentAccessAsync();
                if (access.Result is not null) return access.Result;
                if (!CanAccessAppointment(appointment, access))
                    return Forbid();

                var updated = await _service.RescheduleAppointmentAsync(id, dto.DoctorTimeSlotId);
                return updated is null
                    ? NotFound(new { message = $"Appointment with ID {id} not found." })
                    : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpGet("slots")]
        [Authorize(Roles = SlotReaderRoles)]
        [ProducesResponseType(typeof(IEnumerable<DoctorTimeSlotDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSlots([FromQuery] string? doctorName, [FromQuery] DateTime? date, [FromQuery] bool onlyAvailable = false)
        {
            var doctorId = await GetSlotReaderDoctorIdAsync();
            if (doctorId.Result is not null) return doctorId.Result;

            var slots = await _service.GetSlotsAsync(doctorName, date, onlyAvailable, doctorId.Value);
            return Ok(slots);
        }

        [HttpGet("doctors")]
        [Authorize(Roles = SlotReaderRoles)]
        [ProducesResponseType(typeof(IEnumerable<DoctorLookupDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDoctors([FromQuery] string? specialty)
        {
            var doctors = await _service.GetDoctorsAsync(specialty);
            return Ok(doctors);
        }

        [HttpGet("specializations")]
        [Authorize(Roles = SlotReaderRoles)]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSpecializations()
        {
            var specializations = await _service.GetSpecializationsAsync();
            return Ok(specializations);
        }

        [HttpPost("slots")]
        [Authorize(Roles = StaffRoles)]
        [ProducesResponseType(typeof(DoctorTimeSlotDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateSlot([FromBody] CreateDoctorTimeSlotDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var slotAccessResult = await PrepareSlotMutationAsync(dto);
            if (slotAccessResult is not null) return slotAccessResult;

            try
            {
                var created = await _service.CreateSlotAsync(dto);
                return CreatedAtAction(nameof(GetSlots), new { doctorName = created.DoctorName }, created);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPut("slots/{id:int}")]
        [Authorize(Roles = StaffRoles)]
        [ProducesResponseType(typeof(DoctorTimeSlotDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateSlot(int id, [FromBody] UpdateDoctorTimeSlotDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var slotAccessResult = await PrepareSlotMutationAsync(dto, id);
            if (slotAccessResult is not null) return slotAccessResult;

            try
            {
                var updated = await _service.UpdateSlotAsync(id, dto);
                return updated is null
                    ? NotFound(new { message = $"Doctor time slot with ID {id} not found." })
                    : Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("slots/{id:int}/cancel")]
        [Authorize(Roles = StaffRoles)]
        [ProducesResponseType(typeof(DoctorTimeSlotDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelSlot(int id, [FromBody] CancelDoctorTimeSlotDto? dto)
        {
            var slotAccessResult = await PrepareSlotMutationAsync(null, id);
            if (slotAccessResult is not null) return slotAccessResult;

            var cancelled = await _service.CancelSlotAsync(id, dto?.Reason);
            return cancelled is null
                ? NotFound(new { message = $"Doctor time slot with ID {id} not found." })
                : Ok(cancelled);
        }

        [HttpGet("available-slots")]
        [Authorize(Roles = SlotReaderRoles)]
        [ProducesResponseType(typeof(IEnumerable<DoctorTimeSlotDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SuggestAvailableSlots([FromQuery] string? doctorName, [FromQuery] DateTime? date)
        {
            var doctorId = await GetSlotReaderDoctorIdAsync();
            if (doctorId.Result is not null) return doctorId.Result;

            var slots = await _service.GetSlotsAsync(doctorName, date, true, doctorId.Value);
            return Ok(slots);
        }

        private async Task<AppointmentAccess> GetAppointmentAccessAsync()
        {
            if (User.IsInRole("Admin"))
                return new AppointmentAccess(null, null, null, null);

            if (User.IsInRole("Doctor"))
            {
                var doctor = await GetApprovedDoctorForCurrentUserAsync();
                return doctor is null
                    ? new AppointmentAccess(null, null, null, Forbid())
                    : new AppointmentAccess(null, null, doctor.DoctorId, null);
            }

            if (User.IsInRole("Patient"))
            {
                var email = User.FindFirstValue(ClaimTypes.Email);
                if (string.IsNullOrWhiteSpace(email))
                    return new AppointmentAccess(null, null, null, Unauthorized(new { message = "The access token does not contain a user email." }));

                var normalizedEmail = email.Trim().ToLowerInvariant();
                var patientId = await _db.Patients
                    .Where(patient => patient.Email != null && patient.Email.ToLower() == normalizedEmail)
                    .Select(patient => (int?)patient.PatientId)
                    .SingleOrDefaultAsync();

                return new AppointmentAccess(patientId, normalizedEmail, null, null);
            }

            return new AppointmentAccess(null, null, null, Forbid());
        }

        private static bool CanAccessAppointment(AppointmentDto appointment, AppointmentAccess access)
        {
            if (access.DoctorId.HasValue)
                return appointment.DoctorId == access.DoctorId.Value;

            if (access.PatientId.HasValue || !string.IsNullOrWhiteSpace(access.PatientEmail))
            {
                return (access.PatientId.HasValue && appointment.PatientId == access.PatientId.Value) ||
                    (!string.IsNullOrWhiteSpace(access.PatientEmail) &&
                        string.Equals(appointment.PatientEmail, access.PatientEmail, StringComparison.OrdinalIgnoreCase));
            }

            return true;
        }

        private async Task<SlotReaderAccess> GetSlotReaderDoctorIdAsync()
        {
            if (!User.IsInRole("Doctor"))
                return new SlotReaderAccess(null, null);

            var doctor = await GetApprovedDoctorForCurrentUserAsync();
            return doctor is null
                ? new SlotReaderAccess(null, Forbid())
                : new SlotReaderAccess(doctor.DoctorId, null);
        }

        private async Task<IActionResult?> PrepareSlotMutationAsync(CreateDoctorTimeSlotDto? dto, int? slotId = null)
        {
            DoctorTimeSlot? slot = null;
            if (slotId.HasValue)
            {
                slot = await _db.DoctorTimeSlots.AsNoTracking()
                    .SingleOrDefaultAsync(value => value.DoctorTimeSlotId == slotId.Value);
                if (slot is null)
                    return NotFound(new { message = $"Doctor time slot with ID {slotId.Value} not found." });

                if (User.IsInRole("Doctor"))
                {
                    var doctor = await GetApprovedDoctorForCurrentUserAsync();
                    if (doctor is null || slot.DoctorId != doctor.DoctorId)
                        return Forbid();

                    if (dto is not null)
                        ApplyDoctorProfile(dto, doctor);
                }
                else if (User.IsInRole("Admin") && dto is not null && !dto.DoctorId.HasValue)
                {
                    dto.DoctorId = slot.DoctorId;
                }
            }

            if (User.IsInRole("Doctor") && !slotId.HasValue)
            {
                var doctor = await GetApprovedDoctorForCurrentUserAsync();
                if (doctor is null)
                    return Forbid();

                if (dto is not null)
                    ApplyDoctorProfile(dto, doctor);
            }

            if (User.IsInRole("Admin") && dto is not null && !dto.DoctorId.HasValue)
                return BadRequest(new { message = "Select an approved registered doctor." });

            if (User.IsInRole("Admin") && dto?.DoctorId is int doctorId)
            {
                var doctor = await _db.Doctors.AsNoTracking()
                    .SingleOrDefaultAsync(value => value.DoctorId == doctorId &&
                        value.RegistrationStatus.Trim().ToLower() == DoctorRegistrationStatuses.Approved.ToLower());
                if (doctor is null)
                    return BadRequest(new { message = "Selected doctor is not approved or does not exist." });

                ApplyDoctorProfile(dto, doctor);
            }

            return null;
        }

        private async Task<Doctor?> GetApprovedDoctorForCurrentUserAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdValue, out var userId))
            {
                var doc = await _db.Doctors.AsNoTracking()
                    .FirstOrDefaultAsync(doctor => doctor.UserId == userId);
                if (doc is not null) return doc;
            }

            var doctorIdValue = User.FindFirstValue("doctorId");
            if (int.TryParse(doctorIdValue, out var docId))
            {
                var doc = await _db.Doctors.AsNoTracking()
                    .FirstOrDefaultAsync(doctor => doctor.DoctorId == docId);
                if (doc is not null) return doc;
            }

            return null;
        }

        private static void ApplyDoctorProfile(CreateDoctorTimeSlotDto dto, Doctor doctor)
        {
            dto.DoctorId = doctor.DoctorId;
            dto.DoctorName = $"Dr. {doctor.FirstName} {doctor.LastName}";
            dto.Specialty = doctor.Specialization;
        }

        private sealed record AppointmentAccess(int? PatientId, string? PatientEmail, int? DoctorId, IActionResult? Result);
        private sealed record SlotReaderAccess(int? Value, IActionResult? Result);
    }
}
