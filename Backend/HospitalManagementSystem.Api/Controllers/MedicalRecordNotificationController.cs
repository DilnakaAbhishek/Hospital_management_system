using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.Api.Controllers;

/// <summary>
/// Provides notification endpoints for medical record events:
/// - Patients see notifications when a doctor/admin adds or finalizes a record.
/// - Doctors and Admins see notifications when a patient uploads an attachment.
/// </summary>
[ApiController]
[Route("api/medicalrecord")]
[Authorize]
public class MedicalRecordNotificationController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<MedicalRecordNotificationController> _logger;

    public MedicalRecordNotificationController(
        ApplicationDbContext db,
        ILogger<MedicalRecordNotificationController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Patient: GET /api/medicalrecord/notifications/patient
    // Returns medical record notifications for the currently logged-in patient.
    [HttpGet("notifications/patient")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientNotifications()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { message = "Token missing email claim." });

        var patient = await _db.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Email != null &&
                p.Email.ToLower() == email.Trim().ToLowerInvariant());

        if (patient == null)
            return Ok(Array.Empty<object>());

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email != null &&
                u.Email.ToLower() == email.Trim().ToLowerInvariant());

        var notifications = await _db.MedicalRecordNotifications
            .AsNoTracking()
            .Where(n => n.RecipientRole == "Patient" &&
                n.MedicalRecord.PatientId == patient.PatientId &&
                (n.RecipientUserId == null || n.RecipientUserId == (user != null ? user.UserId : -1)))
            .Include(n => n.MedicalRecord)
                .ThenInclude(r => r.Doctor)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new
            {
                medicalRecordNotificationId = n.MedicalRecordNotificationId,
                medicalRecordId = n.MedicalRecordId,
                message = n.Message,
                eventType = n.EventType,
                isRead = n.IsRead,
                createdAt = n.CreatedAt,
                recordType = n.MedicalRecord.RecordType,
                recordDate = n.MedicalRecord.RecordDate,
                doctorName = n.MedicalRecord.Doctor != null
                    ? $"Dr. {n.MedicalRecord.Doctor.FirstName} {n.MedicalRecord.Doctor.LastName}".Trim()
                    : "Hospital Clinician",
            })
            .ToListAsync();

        return Ok(notifications);
    }

    // ── Staff: GET /api/medicalrecord/notifications/staff
    // Returns medical record attachment notifications for doctors and admins.
    [HttpGet("notifications/staff")]
    [Authorize(Roles = "Admin,Doctor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStaffNotifications()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var roleClaim = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role") ?? string.Empty;
        var isDoctor = roleClaim.Equals("Doctor", StringComparison.OrdinalIgnoreCase);

        // Doctors only see notifications linked to records they are assigned to.
        int? doctorId = null;
        if (isDoctor && !string.IsNullOrWhiteSpace(email))
        {
            var doctor = await _db.Doctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.User.Email != null &&
                    d.User.Email.ToLower() == email.Trim().ToLowerInvariant());
            doctorId = doctor?.DoctorId;
        }

        var query = _db.MedicalRecordNotifications
            .AsNoTracking()
            .Where(n => n.RecipientRole == "Staff")
            .Include(n => n.MedicalRecord)
                .ThenInclude(r => r.Patient)
            .AsQueryable();

        if (doctorId.HasValue)
            query = query.Where(n => n.MedicalRecord.DoctorId == doctorId.Value);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new
            {
                medicalRecordNotificationId = n.MedicalRecordNotificationId,
                medicalRecordId = n.MedicalRecordId,
                message = n.Message,
                eventType = n.EventType,
                isRead = n.IsRead,
                createdAt = n.CreatedAt,
                recordType = n.MedicalRecord.RecordType,
                patientName = n.MedicalRecord.Patient != null
                    ? $"{n.MedicalRecord.Patient.FirstName} {n.MedicalRecord.Patient.LastName}".Trim()
                    : "Patient",
            })
            .ToListAsync();

        return Ok(notifications);
    }

    // ── PATCH /api/medicalrecord/notifications/{id}/read
    // Mark a single notification as read.
    [HttpPatch("notifications/{id:int}/read")]
    [Authorize(Roles = "Admin,Doctor,Patient")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id)
    {
        var notif = await _db.MedicalRecordNotifications.FindAsync(id);
        if (notif == null)
            return NotFound();

        notif.IsRead = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
