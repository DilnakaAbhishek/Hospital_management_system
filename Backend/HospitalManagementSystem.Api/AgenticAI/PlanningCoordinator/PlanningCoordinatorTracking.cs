using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
using Microsoft.EntityFrameworkCore;
namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public sealed partial class PlanningCoordinatorAgent
{
    private async Task EnsureExecutionAsync(int patientId, AssistantConversation conversation, AssistantState state, string objective, CancellationToken token)
    {
        // An independent appointment request must get its own execution record.
        // Keep the paused assessment's record attached to its TriageWorkflow so it
        // can later resume the same WaitingForPatient step.
        var startsIndependentAppointment = System.Text.RegularExpressions.Regex.IsMatch(objective, @"\b(book|booking|schedule|create|reserve|appointment|doctor|specialist|cancel|cancellation|reschedule)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (state.ExecutionWorkflowId != null && state.Awaiting == "clinical-answer" && !startsIndependentAppointment)
        {
            activeExecution = await _store.GetAsync(state.ExecutionWorkflowId, token);
            return;
        }
        var prior = state.ExecutionWorkflowId == null ? null : await _store.GetAsync(state.ExecutionWorkflowId, token);
        var planned = await PlanAsync(new() { ExistingWorkflowId = startsIndependentAppointment ? null : state.ExecutionWorkflowId, PatientId = patientId, Objective = string.IsNullOrWhiteSpace(objective) ? "Continue assessment" : objective.Length < 2 ? "Patient message: " + objective : objective }, token);
        state.ExecutionWorkflowId = planned.WorkflowId;
        var record = (await _store.GetAsync(planned.WorkflowId, token))!;
        if (prior != null && state.PendingAction != null && record.Plan.WorkflowType is not ("SafeTriage" or "TriageThenAppointmentProposal"))
        {
            // Read-only conversation and textual assent do not erase an already executed proposal plan.
            record.Plan = prior.Plan; record.Steps = prior.Steps; record.CurrentAgent = prior.CurrentAgent; record.CurrentStep = prior.CurrentStep;
            record.Status = prior.Status; record.ErrorCode = prior.ErrorCode; record.ErrorSummary = prior.ErrorSummary;
            record.FailedStep = prior.FailedStep; record.FailedAt = prior.FailedAt;
            record.CompletedStages = prior.CompletedStages;
        }
        else if (prior != null && prior.Plan.WorkflowType == record.Plan.WorkflowType && prior.Steps.Count == record.Plan.RequiredSteps.Count)
        {
            record.Plan = prior.Plan; record.Steps = prior.Steps; record.CurrentAgent = prior.CurrentAgent; record.CurrentStep = prior.CurrentStep;
            record.Status = prior.Status; record.ErrorCode = prior.ErrorCode; record.ErrorSummary = prior.ErrorSummary;
            record.FailedStep = prior.FailedStep; record.FailedAt = prior.FailedAt;
            record.CompletedStages = prior.CompletedStages;
        }
        else SetSteps(record, record.Plan.RequiredSteps);
        activeExecution = record;
        record.AuditEvents.Add(new() { EventType = "ConversationLinked", Description = "Patient conversation linked to execution.", Metadata = conversation.AssistantConversationId.ToString() });
        await _store.SaveAsync(record, token);
    }

    internal static void SetSteps(PlanningWorkflowRecord record, IReadOnlyList<string> steps)
    {
        if (steps.Count == 0 || steps.Any(s => !PlanningWorkflowSteps.AllAllowedSteps.Contains(s)) || steps.Distinct().Count() != steps.Count)
            throw new ArgumentException("Unsupported or duplicate plan step.");
        if (!Enum.TryParse<PlanningWorkflowType>(record.Plan.WorkflowType, out var type) || !Enum.IsDefined(type) ||
            !steps.SequenceEqual(PlanningWorkflowSteps.DefaultStepsByWorkflow[type]))
            throw new ArgumentException("Invalid workflow step ordering.");
        record.Steps = [];
        foreach (var step in steps)
        {
            var item = new ExecutionPlanStep { StepType = step, Input = "Validated patient objective and persisted workflow context",
                AssignedAgent = step switch {
                    "SafetyCheck" or "SymptomExtraction" or "TriageAssessment" => "Clinical SafeTriage",
                    PlanningWorkflowSteps.IntakeAndInitialSafetyAgent or PlanningWorkflowSteps.ClinicalUnderstandingAgent or
                    PlanningWorkflowSteps.SafetyRoutingAgent or PlanningWorkflowSteps.GuidanceValidationAgent => step,
                    "DoctorLookup" or "SlotSearch" or "AppointmentProposal" => "Appointment Proposal",
                    "MedicalRecordLookup" or "MedicalRecordExplanation" => "Medical Records",
                    "PatientConfirmation" => "Safety Validation & Approval", _ => "Planning & Coordination" } };
            if (record.Steps.LastOrDefault() is { } prior) item.Dependencies.Add(prior.StepId);
            record.Steps.Add(item);
        }
    }

    private async Task DispatchAsync(AssistantState state, PlanningWorkflowType type, string agent, CancellationToken token)
    {
        if (state.ExecutionWorkflowId == null) return;
        var record = (await _store.GetAsync(state.ExecutionWorkflowId, token))!;
        var combined = record.Plan.WorkflowType == PlanningWorkflowType.TriageThenAppointmentProposal.ToString();
        if (!combined && record.Plan.WorkflowType != type.ToString())
        {
            record.Revision++;
            record.PreviousPlans.Add(System.Text.Json.JsonSerializer.Deserialize<PlanningPlanDto>(System.Text.Json.JsonSerializer.Serialize(record.Plan))!);
            record.AuditEvents.Add(new() { EventType = "Replanned", Description = "Validated current intent changed the supported execution plan.", Metadata = record.Plan.WorkflowType + " -> " + type });
            record.Plan.WorkflowType = type.ToString(); record.Plan.RequiredSteps = PlanningWorkflowSteps.DefaultStepsByWorkflow[type];
            SetSteps(record, record.Plan.RequiredSteps);
        }
        if (agent == "Appointment Proposal" && combined)
        {
            foreach (var clinical in record.Steps.Where(s => s.AssignedAgent == "Clinical SafeTriage"))
            {
                if (state.SafetyBlocked || state.Awaiting == "clinical-answer") throw new InvalidOperationException("Clinical assessment has not completed.");
                clinical.Status = "Completed"; clinical.ValidationStatus = "Passed";
            }
        }
        var step = record.Steps.FirstOrDefault(s => s.AssignedAgent == agent && s.Status != "Completed");
        if (step == null)
        {
            // The concrete SafeTriage dispatcher owns individual plan steps. Keep
            // this aggregate event for conversation-level audit compatibility.
            if (agent == "Clinical SafeTriage" && record.Steps.Any(s => PlanningWorkflowSteps.IsSafeTriageAgent(s.AssignedAgent)))
            {
                record.AuditEvents.Add(new() { EventType = "AgentDispatched", Description = agent, Metadata = "PlanDrivenSafeTriage" });
                await _store.SaveAsync(record, token);
            }
            return;
        }
        // Each clinical/proposal role executes its ordered internal stages as one existing pipeline.
        foreach (var prerequisite in record.Steps.TakeWhile(s => s != step))
            if (prerequisite.AssignedAgent != agent && prerequisite.Status != "Completed")
                throw new InvalidOperationException("Plan dependencies have not completed.");
        step.StartedAt = DateTimeOffset.UtcNow;
        step.Status = "Running"; step.ValidationStatus = "Passed";
        activeExecution = record;
        record.CurrentStep = step.StepId; record.CurrentAgent = agent; record.Status = "InProgress";
        record.AuditEvents.Add(new() { EventType = "AgentDispatched", Description = agent, Metadata = step.StepId });
        await _store.SaveAsync(record, token);
    }

    private async Task UpdateExecutionAsync(AssistantConversation conversation, AssistantState state, CancellationToken token)
    {
        if (state.ExecutionWorkflowId == null) return;
        var record = (await _store.GetAsync(state.ExecutionWorkflowId, token))!;
        if (record.PatientId != conversation.PatientId) throw new InvalidOperationException("Workflow ownership mismatch.");
        if (state.WorkflowId is int triageId)
        {
            var clinical = await db.TriageWorkflows.SingleOrDefaultAsync(w => w.TriageWorkflowId == triageId && w.PatientId == conversation.PatientId, token);
            if (clinical != null) { clinical.ExecutionWorkflowId ??= record.WorkflowId; record.ApprovalStatus = clinical.ApprovalStatus; }
        }
        if (state.PendingAction?.ProposalId is int proposalId)
        {
            var proposal = await db.AppointmentProposals.SingleAsync(p => p.AppointmentProposalId == proposalId && p.PatientId == conversation.PatientId, token);
            proposal.ExecutionWorkflowId = record.WorkflowId; record.ApprovalStatus = proposal.Status;
        }
        if (record.Status != "FailedSafely") record.Status = state.Awaiting switch {
            "clinical-answer" => "AwaitingPatientInput", "clinical-review" => "AwaitingClinicalReview",
            _ when state.PendingAction != null => "AwaitingPatientConfirmation",
            _ => state.State switch { "COMPLETED" => "Completed", "CANCELLED" => "Cancelled", "FAILED" => "FailedSafely", _ => "AwaitingPatientInput" } };
        if (record.Status == "FailedSafely" && record.ErrorCode == null)
        {
            record.ErrorCode = "ValidationRejected"; record.ErrorSummary = "The operation did not pass final validation.";
            record.FailedStep = record.CurrentStep ?? "PlanningAndCoordination"; record.FailedAt = DateTimeOffset.UtcNow; record.Errors.Add(record.ErrorCode);
        }
        var checkpointSteps = record.CurrentAgent == "Clinical SafeTriage"
            ? record.Steps.Where(s => s.StepId == record.CurrentStep)
            : record.Steps.Where(s => s.AssignedAgent == record.CurrentAgent);
        foreach (var step in checkpointSteps)
        {
            // The concrete SafeTriage runner owns its individual step lifecycle.
            // A patient-input boundary is not a completed dependency, and must
            // survive this conversation-level checkpoint unchanged.
            if (record.CurrentAgent == "Clinical SafeTriage" && record.Status == "AwaitingClinicalReview")
            {
                step.Status = "WaitingForClinicalReview";
                // Keep the actual safety validation result and review reason.
                // Waiting for review does not turn a failed validation into Passed.
                record.CompletedStages.Remove(step.StepId);
                continue;
            }
            if (record.Status == "FailedSafely")
                step.Status = "Failed";
            else if (record.CurrentAgent != "Clinical SafeTriage" || record.Status is not ("AwaitingPatientInput" or "AwaitingClinicalReview"))
                step.Status = "Completed";
            step.EndedAt = DateTimeOffset.UtcNow;
            step.OutputSummary = record.Status; step.ValidationStatus = record.Status == "FailedSafely" ? "Failed" : "Passed";
            if (step.Status == "Completed" && !record.CompletedStages.Contains(step.StepId)) record.CompletedStages.Add(step.StepId);
        }
        record.FinalOutcome = record.Status is "Completed" or "Cancelled" ? state.Messages.LastOrDefault(m => m.Role == "assistant")?.Text : null;
        record.ToolResultsSummary = record.ToolResultsSummary.Concat(new[] { "Triage: " + state.WorkflowId, "Proposal: " + state.PendingAction?.ProposalId }).Distinct().ToList();
        record.ValidationResults = ["Authenticated patient ownership verified", "Deterministic safety and confirmation boundaries enforced"];
        record.AuditEvents.Add(new() { EventType = "ExecutionCheckpoint", Description = record.Status, Metadata = record.CurrentStep });
        await _store.SaveAsync(record, token);
    }

    private async Task FailExecutionAsync(AssistantState state, string code, CancellationToken token)
    {
        if (state.ExecutionWorkflowId == null) return;
        var record = (await _store.GetAsync(state.ExecutionWorkflowId, token))!;
        record.Status = "FailedSafely"; record.ErrorCode = code; record.ErrorSummary = "The requested step could not be completed safely.";
        record.FailedStep = record.CurrentStep ?? "PlanningAndCoordination"; record.FailedAt = DateTimeOffset.UtcNow; record.Errors.Add(code);
        await _store.SaveAsync(record, token);
    }
}
