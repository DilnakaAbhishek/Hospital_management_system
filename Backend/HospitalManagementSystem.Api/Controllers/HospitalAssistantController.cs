using System.Security.Claims;
using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController, Route("api/hospital-assistant"), Authorize(Roles = "Patient")]
public sealed class HospitalAssistantController(HospitalAssistantService assistant, IPatientService patients) : ControllerBase
{
    [HttpGet("capabilities")]
    public IActionResult Capabilities() => Ok(assistant.Capabilities);

    [HttpGet("conversations")]
    public Task<IActionResult> History(CancellationToken token) => WithPatient(async patient =>
        Ok(await assistant.HistoryAsync(patient.PatientId, token)));

    [HttpGet("conversations/{id:guid}")]
    public Task<IActionResult> Conversation(Guid id, CancellationToken token) => WithPatient(async patient =>
        Ok(await assistant.GetAsync(patient.PatientId, id, token)));

    [HttpPost("messages")]
    public Task<IActionResult> Message(AssistantMessageRequest request, CancellationToken token) => WithPatient(async patient =>
        Ok(await assistant.MessageAsync(patient, request, token)));

    [HttpPost("conversations/{id:guid}/actions")]
    public Task<IActionResult> Action(Guid id, AssistantActionRequest request, CancellationToken token) => WithPatient(async patient =>
        Ok(await assistant.DecideAsync(patient, id, request, token)));

    private async Task<IActionResult> WithPatient(Func<PatientDto, Task<IActionResult>> action)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var patient = string.IsNullOrWhiteSpace(email) ? null : await patients.GetPatientByEmailAsync(email);
        if (patient == null) return NotFound(new { message = "No patient profile was found for this account." });
        try { return await action(patient); }
        catch (KeyNotFoundException) { return NotFound(new { message = "Conversation not found." }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
