using System.Security.Claims;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Route("api/triage-workflows")]
[Authorize]
public sealed class TriageWorkflowController : ControllerBase
{
    private readonly IPatientService _patients;
    private readonly ITriageWorkflowService _workflows;
    private readonly ApplicationDbContext _db;

    public TriageWorkflowController(IPatientService patients, ITriageWorkflowService workflows, ApplicationDbContext db)
    {
        _patients = patients;
        _workflows = workflows;
        _db = db;
    }

    [HttpPost]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(TriageWorkflowDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Start([FromBody] StartTriageWorkflowDto request)
    {
        var patient = await CurrentPatient();
        if (patient is null) return NotFound(new { message = "No patient profile found for this account." });
        var workflow = await _workflows.StartForPatientAsync(patient.PatientId, request);
        return CreatedAtAction(nameof(GetMine), new { id = workflow.WorkflowId }, workflow);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> GetMine(int id)
    {
        var patient = await CurrentPatient();
        if (patient is null) return NotFound(new { message = "No patient profile found for this account." });
        var workflow = await _workflows.GetForPatientAsync(id, patient.PatientId);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpGet("history")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(IReadOnlyList<TriageWorkflowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory()
    {
        var patient = await CurrentPatient();
        if (patient is null) return NotFound(new { message = "No patient profile found for this account." });
        return Ok(await _workflows.GetHistoryForPatientAsync(patient.PatientId));
    }

    [HttpGet("notifications")]
    [Authorize]
    public async Task<IActionResult> GetClinicalReviewNotifications()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role is "Doctor" or "Admin")
        {
            int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : null;
            var pendingReviews = await _workflows.GetPendingClinicalReviewsAsync(userId);
            var doctorNotifications = pendingReviews.Take(30).Select(workflow => new {
                triageWorkflowId = workflow.WorkflowId,
                message = (workflow.PriorityLevel == "Critical" || workflow.TriageLevel == "Emergency" ? "🚨 CRITICAL EMERGENCY: " : "📋 Clinical Review: ") +
                          (!string.IsNullOrWhiteSpace(workflow.OriginalComplaint) ? workflow.OriginalComplaint : workflow.PatientMessage),
                priorityLevel = workflow.PriorityLevel,
                isEmergency = workflow.PriorityLevel == "Critical" || workflow.TriageLevel == "Emergency",
                createdAt = workflow.CreatedAt
            }).ToList();
            return Ok(doctorNotifications);
        }

        var patient = await CurrentPatient();
        if (patient is null) return NotFound(new { message = "No patient profile found for this account." });
        var notifications = await _db.TriageWorkflows.AsNoTracking()
            .Where(workflow => workflow.PatientId == patient.PatientId && workflow.ReviewedAt != null &&
                (workflow.ApprovalStatus == TriageApprovalStatuses.Approved || workflow.ApprovalStatus == "ClinicianResponse"))
            .OrderByDescending(workflow => workflow.ReviewedAt).Take(50)
            .Select(workflow => new {
                workflow.TriageWorkflowId,
                message = workflow.FinalOutcome,
                priorityLevel = workflow.PriorityLevel,
                isEmergency = false,
                createdAt = workflow.ReviewedAt
            }).ToListAsync();
        return Ok(notifications);
    }

    [HttpPost("{id:int}/continue")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> Continue(int id, [FromBody] ContinueTriageWorkflowDto request)
    {
        var patient = await CurrentPatient();
        if (patient is null) return NotFound(new { message = "No patient profile found for this account." });
        try
        {
            var workflow = await _workflows.ContinueForPatientAsync(id, patient.PatientId, request);
            return workflow is null ? NotFound(new { message = "A workflow waiting for your input was not found." }) : Ok(workflow);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "This assessment was already updated. Refresh before answering again." });
        }
    }

    [HttpGet("{id:int}/clinical-review")]
    [Authorize(Roles = "Admin,Doctor")]
    public async Task<IActionResult> GetForReview(int id)
    {
        var workflow = await _workflows.GetForClinicalReviewerAsync(id);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpGet("clinical-review/pending")]
    [Authorize(Roles = "Admin,Doctor")]
    [ProducesResponseType(typeof(IReadOnlyList<TriageWorkflowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReviews()
    {
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : null;
        return Ok(await _workflows.GetPendingClinicalReviewsAsync(userId));
    }

    [HttpGet("{id:int}/audit-events")]
    [Authorize(Roles = "Admin,Doctor")]
    [ProducesResponseType(typeof(IReadOnlyList<TriageWorkflowEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditEvents(int id)
    {
        var events = await _workflows.GetAuditEventsAsync(id);
        return events is null ? NotFound() : Ok(events);
    }

    [HttpPost("{id:int}/review")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewTriageWorkflowDto request)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var workflow = await _workflows.ReviewAsync(id, userId, request);
            return workflow is null ? NotFound(new { message = "A pending workflow was not found." }) : Ok(workflow);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { return Conflict(new { message = "This review was already updated. Refresh the assessment." }); }
    }

    private async Task<PatientDto?> CurrentPatient()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email) ? null : await _patients.GetPatientByEmailAsync(email);
    }
}
