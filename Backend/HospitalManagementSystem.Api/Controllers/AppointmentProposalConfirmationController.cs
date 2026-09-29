using System.Security.Claims;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.Services;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController, Route("api/appointment-proposals"), Authorize(Roles = "Patient")]
public sealed class AppointmentProposalConfirmationController(ISafetyValidationApprovalAgent agent, IPatientService patients,
    ApplicationDbContext db, ITriageWorkflowService workflows, AppointmentSmsNotifier sms) : ControllerBase
{
    [HttpPost("{proposalId:int}/confirm")]
    public async Task<IActionResult> Confirm(int proposalId, [FromBody] ConfirmProposalDto request, CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var patient = string.IsNullOrWhiteSpace(email) ? null : await patients.GetPatientByEmailAsync(email);
        if (patient is null) return NotFound();
        // Share the assistant's patient lock so old clients cannot race a unified confirmation.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(db, async (context, ct) =>
        {
            await using var transaction = context.Database.IsRelational()
                ? await context.Database.BeginTransactionAsync(ct) : null;
            if (context.Database.IsNpgsql())
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1396916552, {patient.PatientId})", ct);
            var history = await workflows.GetHistoryForPatientAsync(patient.PatientId);
            // Only unresolved urgent or emergency assessments block confirmation.
            // A technical SafeTriage failure or an unrelated incomplete assessment
            // must not invalidate an already approved appointment proposal.
            if (history.Any(w =>
                (w.TriageLevel is TriageLevels.Emergency or TriageLevels.Urgent &&
                 w.ApprovalStatus is TriageApprovalStatuses.Pending or TriageApprovalStatuses.RevisionRequested)))
                return Ok(new SafetyApprovalResult("Rejected", true, "Complete the urgent safety assessment before confirming an appointment."));
            var result = await agent.ConfirmAsync(new(proposalId, request.DoctorTimeSlotId), patient, ct);
            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
                await sms.FlushCommittedAsync(transaction.TransactionId);
            }
            return Ok(result);
        }, cancellationToken);
    }
}
public sealed class ConfirmProposalDto { public int DoctorTimeSlotId { get; set; } }
