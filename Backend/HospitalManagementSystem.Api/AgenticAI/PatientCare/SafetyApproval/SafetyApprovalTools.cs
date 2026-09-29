using System.Text.Json;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using ProposalEntity = HospitalManagementSystem.Api.Models.AppointmentProposal;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;

public interface ISafetyApprovalTools
{
    Task<ProposalEntity?> GetOwnedProposalAsync(int proposalId, int patientId, CancellationToken cancellationToken);
    Task<AgentSlot?> ValidateSelectedSlotAsync(ProposalEntity proposal, int slotId, CancellationToken cancellationToken);
    Task<AgentBooking> FinalizeBookingAsync(AgentSlot slot, PatientDto patient, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
    Task<bool> CanBookPatientAsync(int patientId, CancellationToken token) => Task.FromResult(true);
}

public sealed class SafetyApprovalTools(ApplicationDbContext db, IAppointmentAgentTools appointmentTools) : ISafetyApprovalTools
{
    public Task<ProposalEntity?> GetOwnedProposalAsync(int proposalId, int patientId, CancellationToken token) =>
        db.AppointmentProposals.SingleOrDefaultAsync(p => p.AppointmentProposalId == proposalId && p.PatientId == patientId, token);
    public async Task<AgentSlot?> ValidateSelectedSlotAsync(ProposalEntity proposal, int slotId, CancellationToken token)
    {
        var candidates = JsonSerializer.Deserialize<List<AgentSlot>>(proposal.CandidateSlotsJson) ?? [];
        var selected = candidates.SingleOrDefault(s => s.DoctorTimeSlotId == slotId);
        if (selected is null) return null;
        var fresh = await appointmentTools.FindSlotsAsync(new AgentDoctor($"D{selected.DoctorId}", selected.DoctorId, selected.DoctorName, selected.Specialty), DateOnly.FromDateTime(selected.StartAt.DateTime));
        var current = fresh.SingleOrDefault(s => s.DoctorTimeSlotId == slotId);
        // A patient approves these session details, not just an ID. The queue number
        // and remaining capacity may change before booking; the session itself may not.
        return current != null && current.DoctorId == selected.DoctorId &&
            current.DoctorName == selected.DoctorName && current.Specialty == selected.Specialty &&
            current.StartAt == selected.StartAt && current.EndAt == selected.EndAt &&
            current.ConsultationFee == selected.ConsultationFee && current.Location == selected.Location
                ? current : null;
    }
    public Task<AgentBooking> FinalizeBookingAsync(AgentSlot slot, PatientDto patient, CancellationToken token) => appointmentTools.BookAsync(slot, patient);
    public async Task<bool> CanBookPatientAsync(int patientId, CancellationToken token) => !await db.TriageWorkflows.AnyAsync(w =>
        w.PatientId == patientId &&
        ((w.TriageLevel == TriageLevels.Emergency || w.TriageLevel == TriageLevels.Urgent) &&
        (w.ApprovalStatus == TriageApprovalStatuses.Pending || w.ApprovalStatus == TriageApprovalStatuses.RevisionRequested)), token);
    public async Task SaveAsync(CancellationToken token)
    {
        foreach (var entry in db.ChangeTracker.Entries<ProposalEntity>().Where(e => e.State == EntityState.Modified).ToList())
        {
            var proposal = entry.Entity;
            if (proposal.ExecutionWorkflowId == null) continue;
            var store = new PlanningCoordinator.PlanningCoordinatorStore(db);
            var record = await store.GetAsync(proposal.ExecutionWorkflowId, token);
            if (record == null || record.PatientId != proposal.PatientId) throw new InvalidOperationException("Workflow ownership mismatch.");
            record.ApprovalStatus = proposal.Status;
            record.Status = proposal.Status == "Booked" ? "Completed" : proposal.Status;
            record.FinalOutcome = proposal.Status == "Booked" ? "Appointment " + proposal.AppointmentId + " booked" : null;
            record.AuditEvents.Add(new() { EventType = "ApprovalDecision", Description = proposal.Status,
                Metadata = "Patient confirmation and deterministic validation completed." });
            await store.SaveAsync(record, token);
        }
        await db.SaveChangesAsync(token);
    }
}
