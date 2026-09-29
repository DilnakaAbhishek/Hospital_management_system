using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare;

[ApiController]
[Route("api/patient-care")]
[Authorize(Roles = "Patient")]
public sealed class PatientCareController(
    HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.HospitalAssistantService assistant,
    IPatientService patients) : ControllerBase
{
    [HttpPost("triage-appointment-proposal")]
    public async Task<IActionResult> CreateProposal([FromBody] TriageAppointmentProposalRequest request, CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var patient = string.IsNullOrWhiteSpace(email) ? null : await patients.GetPatientByEmailAsync(email);
        if (patient is null) return NotFound(new { message = "No patient profile was found for this account." });

        // Legacy mutation is redirected to the same persisted assistant execution.
        return Ok(await assistant.MessageAsync(patient, new() {
            RequestId = Guid.NewGuid(), Message = request.Symptoms + (request.RequestAppointmentProposal ? " Please book an appointment" + (string.IsNullOrWhiteSpace(request.Specialty) ? "." : " with " + request.Specialty) + (request.PreferredDate.HasValue ? " on " + request.PreferredDate.Value.ToString("yyyy-MM-dd") : "") : ""), Vitals = request.Vitals
        }, cancellationToken));
    }
}

public sealed class TriageAppointmentProposalRequest
{
    [Required, StringLength(4000, MinimumLength = 3)] public string Symptoms { get; set; } = string.Empty;
    [StringLength(100, MinimumLength = 2)] public string? Specialty { get; set; }
    public bool RequestAppointmentProposal { get; set; }
    public DateOnly? PreferredDate { get; set; }
    public TriageVitalsDto? Vitals { get; set; }
}
