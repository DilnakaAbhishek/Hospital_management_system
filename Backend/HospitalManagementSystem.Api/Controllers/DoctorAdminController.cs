using System.Security.Claims;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/doctor-registrations")]
public class DoctorAdminController : ControllerBase
{
    private readonly IDoctorService _service;
    public DoctorAdminController(IDoctorService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        try { return Ok(await _service.GetRegistrationsAsync(status)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var doctor = await _service.GetByIdAsync(id);
        return doctor is null ? NotFound(new { message = "Doctor registration was not found." }) : Ok(doctor);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        try
        {
            var pendingApprovals = await _service.GetRegistrationsAsync("Pending");
            var deletionRequests = await _service.GetRegistrationsAsync("DeletionPending");

            var notifications = new List<object>();

            foreach (var d in deletionRequests)
            {
                notifications.Add(new
                {
                    id = $"doctor-deletion-{d.DoctorId}",
                    type = "doctor-deletion",
                    title = "Doctor Deletion Request",
                    message = $"Dr. {d.FullName} ({d.Specialization}) requested account deletion.",
                    time = d.CreatedAt,
                    targetUrl = "/doctors",
                    priority = "high"
                });
            }

            foreach (var d in pendingApprovals)
            {
                notifications.Add(new
                {
                    id = $"doctor-reg-{d.DoctorId}",
                    type = "doctor-registration",
                    title = "Doctor Registration Pending",
                    message = $"Dr. {d.FullName} ({d.Specialization}) registered and awaits approval.",
                    time = d.CreatedAt,
                    targetUrl = "/doctors",
                    priority = "normal"
                });
            }

            return Ok(notifications);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id) => Review(id, true, null);

    [HttpPost("{id:int}/decline")]
    public Task<IActionResult> Decline(int id, DeclineDoctorDto dto) => Review(id, false, dto.Reason);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> ApproveDeletion(int id)
    {
        try
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var deleted = await _service.ApproveDeletionAsync(id, adminId);
            return deleted ? Ok(new { message = "Doctor account deleted successfully." }) : NotFound(new { message = "Doctor was not found." });
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("{id:int}/cancel-deletion")]
    [HttpPost("{id:int}/reject-deletion")]
    public async Task<IActionResult> CancelDeletion(int id, [FromBody] DeclineDoctorDto? dto = null)
    {
        try
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var doctor = await _service.CancelDeletionAsync(id, adminId, dto?.Reason);
            return doctor is null ? NotFound(new { message = "Doctor was not found." }) : Ok(doctor);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    private async Task<IActionResult> Review(int id, bool approve, string? reason)
    {
        try
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var doctor = await _service.ReviewAsync(id, adminId, approve, reason);
            return doctor is null ? NotFound(new { message = "Doctor registration was not found." }) : Ok(doctor);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }
}
