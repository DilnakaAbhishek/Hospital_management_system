using System.Security.Claims;
using HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Route("api/planning-coordinator")]
[Authorize(Roles = "Patient,Doctor,Admin")]
public sealed class PlanningCoordinatorController : ControllerBase
{
    private readonly IPlanningCoordinatorAgent _agent;
    private readonly IPlanningCoordinatorStore _store;
    private readonly IPatientService _patients;

    public PlanningCoordinatorController(
        IPlanningCoordinatorAgent agent,
        IPlanningCoordinatorStore store,
        IPatientService patients)
    {
        _agent = agent;
        _store = store;
        _patients = patients;
    }

    [HttpPost("plan")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> CreatePlan([FromBody] PlanningRequestDto request, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Patient"))
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var patient = string.IsNullOrWhiteSpace(email) ? null : await _patients.GetPatientByEmailAsync(email);
            if (patient is null) return NotFound();
            request.PatientId = patient.PatientId;
        }

        var response = await _agent.PlanAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("workflows/{workflowId}")]
    public async Task<IActionResult> GetWorkflow(string workflowId, CancellationToken cancellationToken)
    {
        var workflow = await _agent.GetWorkflowStatusAsync(workflowId, cancellationToken);
        if (workflow is null) return NotFound(new { message = "Workflow not found." });

        if (User.IsInRole("Patient"))
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var patient = string.IsNullOrWhiteSpace(email) ? null : await _patients.GetPatientByEmailAsync(email);
            var owned = await _store.GetAsync(workflowId, cancellationToken);
            if (patient is null || owned?.PatientId != patient.PatientId) return NotFound();
        }

        return Ok(workflow);
    }

    [HttpGet("workflows/{workflowId}/execution")]
    public async Task<IActionResult> GetExecution(string workflowId, CancellationToken token)
    {
        var record = await _store.GetAsync(workflowId, token);
        if (record == null) return NotFound();
        if (User.IsInRole("Patient"))
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var patient = string.IsNullOrWhiteSpace(email) ? null : await _patients.GetPatientByEmailAsync(email);
            if (patient?.PatientId != record.PatientId || patient == null) return NotFound();
        }
        return Ok(record);
    }

    [HttpGet("workflows")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> GetMyWorkflows(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var patient = string.IsNullOrWhiteSpace(email) ? null : await _patients.GetPatientByEmailAsync(email);
        if (patient is null) return NotFound(new { message = "No patient profile was found for this account." });

        var records = await _store.GetByPatientIdAsync(patient.PatientId, cancellationToken);
        return Ok(records.Select(r => new
        {
            r.WorkflowId,
            r.PatientId,
            r.Objective,
            r.Status,
            r.Plan,
            r.CompletedStages,
            r.Errors,
            auditCount = r.AuditEvents.Count,
            r.CreatedAt,
            r.UpdatedAt
        }));
    }
}
