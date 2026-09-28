using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using HospitalManagementSystem.Api.AgenticAI.SafeTriage;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using static HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.AssistantPreferences;

using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
using Microsoft.Extensions.Logging.Abstractions;

namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

/// Coordinates existing agents and services. Only DecideAsync can execute an approved hospital mutation.
public sealed partial class PlanningCoordinatorAgent
{
    private readonly ApplicationDbContext db = null!;
    private readonly AssistantAgentRegistry registry = null!;
    private readonly ITriageWorkflowService workflows = null!;
    private readonly IHospitalAppointmentProposalAgent proposals = null!;
    private readonly ISafetyValidationApprovalAgent approval = null!;
    private readonly IAppointmentSearchTools tools = null!;
    private readonly IAppointmentService appointments = null!;
    private readonly AppointmentSmsNotifier sms = null!;
    private readonly IAssessmentIntentClient? intentClient;

    public PlanningCoordinatorAgent(
    ApplicationDbContext db,
    AssistantAgentRegistry registry,
    ITriageWorkflowService workflows,
    IHospitalAppointmentProposalAgent proposals,
    ISafetyValidationApprovalAgent approval,
    IAppointmentSearchTools tools,
    IAppointmentService appointments,
    AppointmentSmsNotifier sms,
    IAssessmentIntentClient? intentClient = null, IPlanningModelClient? modelClient = null, ILogger<PlanningCoordinatorAgent>? logger = null)
        : this(modelClient ?? new UnavailablePlanningClient(), new PlanningCoordinatorStore(db), logger ?? NullLogger<PlanningCoordinatorAgent>.Instance)
    {
        this.db = db; this.registry = registry; this.workflows = workflows;
        this.proposals = proposals; this.approval = approval; this.tools = tools;
        this.appointments = appointments; this.sms = sms; this.intentClient = intentClient;
    }

    private sealed class UnavailablePlanningClient : IPlanningModelClient
    {
        public Task<GeminiPlanningDecision> PlanObjectiveAsync(string objective, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Planning model unavailable.");
    }

    private PlanningWorkflowRecord? activeExecution;
    private TriageVitalsDto? legacyVitals;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // Fixed stripes avoid a lock dictionary growing with patient data. PostgreSQL locks also
    // serialize requests across API instances; stripes cover the in-memory test provider.
    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();

    public IReadOnlyList<AssistantCapability> Capabilities => registry.Capabilities;

    public async Task<object> HistoryAsync(int patientId, CancellationToken token) =>
        await db.AssistantConversations.AsNoTracking().Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.UpdatedAt).Take(50)
            .Select(c => new { conversationId = c.AssistantConversationId, c.Title, c.UpdatedAt }).ToListAsync(token);

    public Task<AssistantConversationResponse> GetAsync(int patientId, Guid id, CancellationToken token) =>
        LockedAsync(patientId, async () => {
            var entity = await OwnedAsync(patientId, id, token);
            var state = Read(entity);
            activeExecution = state.ExecutionWorkflowId == null ? null : await _store.GetAsync(state.ExecutionWorkflowId, token);
            if (state.Awaiting == "appointment-clinical-review")
            {
                // Compatibility for conversations saved before appointment clinical
                // approval was removed. The patient can start a fresh proposal.
                state.Awaiting = null;
                state.PendingAction = null;
                Reply(state, "This older appointment request needs a new search. Choose a session and confirm it yourself.", "CANCELLED");
            }
            if (state.PendingAction?.ExpiresAt <= DateTime.UtcNow)
            {
                await InvalidateAsync(state, "Expired", token);
                Reply(state, "The pending request expired. Ask me to search again for current options.", "CANCELLED");
            }
            if (state.WorkflowId.HasValue)
            {
                var workflow = await workflows.GetForPatientAsync(state.WorkflowId.Value, patientId);
                if (workflow?.ReviewedResponse is { Length: > 0 } reviewedResponse &&
                    !state.Messages.Any(message => message.Role == "assistant" && message.Text.Contains("Clinical review completed\n\n" + reviewedResponse, StringComparison.Ordinal)))
                {
                    ApplyWorkflow(state, workflow);
                    ClinicalReply(state, workflow);
                }
                else if (state.Awaiting == "clinical-review" && workflow != null &&
                         workflow.Status == TriageWorkflowStatuses.PendingClinicalReview &&
                         workflow.TriageLevel is not (TriageLevels.Emergency or TriageLevels.Urgent))
                {
                    // Self-heal conversations that were corrupted by the old CheckPatientSafetyAsync
                    // bug which overwrote state.WorkflowId with a ClinicalReview-level workflow from
                    // a different conversation. A non-blocking pending review must not freeze the
                    // conversation; clear the awaiting gate so the patient can continue.
                    state.Awaiting = null;
                    state.SafetyBlocked = false;
                    Reply(state, "Your assessment is still being reviewed by the care team. You can continue using the assistant in the meantime.", "COMPLETED");
                }
            }
            await SaveAsync(entity, state, token);
            return Response(entity, state);
        }, token);

    public Task<AssistantConversationResponse> MessageAsync(PatientDto patient, AssistantMessageRequest request, CancellationToken token)
    {
        legacyVitals = request.Vitals;
        var isRequirementAction = request.RequirementState == SafeTriageRequirementState.Declined && !string.IsNullOrWhiteSpace(request.RequirementId) && request.ConversationId.HasValue;
        if (request.RequestId == Guid.Empty || (!isRequirementAction && string.IsNullOrWhiteSpace(request.Message)) || request.Message.Length > 4000 ||
            (request.RequirementState is not null && !isRequirementAction) || (isRequirementAction && !string.IsNullOrWhiteSpace(request.Message)))
            throw new ArgumentException("Provide a message and a unique request ID.");
        return LockedAsync(patient.PatientId, async () => {
            var entity = request.ConversationId.HasValue
                ? await OwnedAsync(patient.PatientId, request.ConversationId.Value, token)
                : await db.AssistantConversations.SingleOrDefaultAsync(c => c.PatientId == patient.PatientId && c.InitialRequestId == request.RequestId, token);
            if (entity == null)
            {
                entity = new AssistantConversation {
                    PatientId = patient.PatientId, InitialRequestId = request.RequestId,
                    Title = request.Message.Trim()[..Math.Min(100, request.Message.Trim().Length)]
                };
                db.AssistantConversations.Add(entity);
            }
            var state = Read(entity);
            activeExecution = state.ExecutionWorkflowId == null ? null : await _store.GetAsync(state.ExecutionWorkflowId, token);
            var fingerprint = Fingerprint(JsonSerializer.Serialize(new { request.Message, request.RequirementId, request.RequirementState }));
            if (Seen(state, request.RequestId, fingerprint)) return Response(entity, state);
            if (state.Messages.Count >= 200) throw new ArgumentException("This conversation is full. Start a new conversation.");
            await EnsureExecutionAsync(patient.PatientId, entity, state, request.Message, token);
            state.State = "UNDERSTANDING";
            state.Messages.Add(new(Guid.NewGuid().ToString(), "user", isRequirementAction ? "Prefer not to answer" : request.Message.Trim(), DateTime.UtcNow, []));
            state.Appointments = []; state.Slots = []; state.Doctors = [];
            if (isRequirementAction)
            {
                if (state.Awaiting != "clinical-answer" || !state.WorkflowId.HasValue || state.Questions.FirstOrDefault()?.Id != request.RequirementId)
                    throw new ArgumentException("The assessment question changed. Refresh before declining.");
                var workflow = await workflows.ContinueForPatientAsync(state.WorkflowId.Value, patient.PatientId,
                    new() { Answers = [new() { QuestionId = request.RequirementId!, State = SafeTriageRequirementState.Declined }] }, state.ExecutionWorkflowId);
                if (workflow is null) throw new InvalidOperationException("The assessment is no longer waiting for answers.");
                RecordFollowUp(state, SafeTriageRequirementState.Declined);
                state.Answers = [];
                ApplyWorkflow(state, workflow);
                ClinicalReply(state, workflow);
            }
            else
            {
                try { await RouteAsync(patient, state, request.Message.Trim(), token); }
                catch (Exception ex) when (ex is not ArgumentException && ex is not OperationCanceledException && ex is not DbUpdateException)
                {
                    await FailExecutionAsync(state, "ExecutionUnavailable", token);
                    Reply(state, "The request could not be completed safely. Please try again or contact the hospital.", "FAILED");
                }
            }
            state.Requests[request.RequestId] = fingerprint;
            await SaveAsync(entity, state, token);
            return Response(entity, state);
        }, token);
    }

    public Task<AssistantConversationResponse> DecideAsync(PatientDto patient, Guid id, AssistantActionRequest request, CancellationToken token)
    {
        if (request.RequestId == Guid.Empty || request.ActionId == Guid.Empty || request.Decision is not ("confirm" or "cancel" or "chooseAnother"))
            throw new ArgumentException("Use an explicit confirm, cancel, or chooseAnother action.");
        return LockedAsync(patient.PatientId, async () => {
            var entity = await OwnedAsync(patient.PatientId, id, token);
            var state = Read(entity);
            activeExecution = state.ExecutionWorkflowId == null ? null : await _store.GetAsync(state.ExecutionWorkflowId, token);
            var fingerprint = Fingerprint(JsonSerializer.Serialize(withAction(), Json));
            if (Seen(state, request.RequestId, fingerprint)) return Response(entity, state);
            var action = state.PendingAction;
            if (action == null || action.ActionId != request.ActionId)
                throw new InvalidOperationException("This request is no longer awaiting confirmation. Refresh the conversation.");
            if (request.Decision == "chooseAnother")
            {
                state.ExcludedDoctorTimeSlotIds = state.ExcludedDoctorTimeSlotIds
                    .Concat(action.Slots.Select(slot => slot.DoctorTimeSlotId)).Distinct().ToArray();
                await InvalidateAsync(state, "Superseded", token);
                state.Slots = [];
                state.Doctors = [];
                state.AvailabilityChecked = false;
                state.WantsAppointment = true;
                state.Awaiting = "preferences";
                await SearchAsync(patient, state, token);
            }
            else if (request.Decision == "cancel")
            {
                await InvalidateAsync(state, "Cancelled", token);
                ClearAppointmentTask(state);
                Reply(state, DismissalReply(action.Type), "CANCELLED");
            }
            else if (action.ExpiresAt <= DateTime.UtcNow)
            {
                await InvalidateAsync(state, "Expired", token);
                Reply(state, "This request expired. Search again before confirming.", "CANCELLED");
            }
            else
            {
                // Recheck the persisted clinical workflow on the final action boundary.
                if (state.WorkflowId.HasValue)
                {
                    var workflow = await workflows.GetForPatientAsync(state.WorkflowId.Value, patient.PatientId);
                    if (workflow != null) ApplyWorkflow(state, workflow);
                }
                await CheckPatientSafetyAsync(patient.PatientId, state);
                if (state.SafetyBlocked)
                {
                    await InvalidateAsync(state, "BlockedByClinicalSafety", token);
                    await ReplySafetyBlockedAsync(patient.PatientId, state);
                }
                else if (action.Type == "book")
                {
                    if (!request.DoctorTimeSlotId.HasValue || !action.Slots.Any(s => s.DoctorTimeSlotId == request.DoctorTimeSlotId) || !action.ProposalId.HasValue)
                        throw new ArgumentException("Select one of the appointment options shown in this request.");
                    state.State = "EXECUTING";
                    await DispatchAsync(state, PlanningWorkflowType.AppointmentProposal, "Safety Validation & Approval", token);
                    var result = await approval.ConfirmAsync(new(action.ProposalId.Value, request.DoctorTimeSlotId.Value), patient, token);
                    // The proposal is retained as an audit trail, but its selectable
                    // availability must not remain visible after one option is booked.
                    foreach (var message in state.Messages.Where(m => m.ProposedAction?.ActionId == action.ActionId))
                    {
                        message.ProposedAction!.Status = "Confirmed";
                        message.ProposedAction.Slots = [];
                    }
                    state.PendingAction = null;
                    state.Awaiting = null;
                    ClearAppointmentTask(state);
                    if (result.AppointmentId.HasValue)
                    {
                        var booked = await appointments.GetAppointmentByIdAsync(result.AppointmentId.Value);
                        if (booked?.PatientId != patient.PatientId) throw new InvalidOperationException("Could not verify the booking result.");
                        state.Appointments = [booked];
                        Reply(state, $"Appointment confirmed with {booked.DoctorName}. Your appointment number is {booked.AppointmentNumber}. " +
                            $"Session: {Local(booked.StartAt):ddd, d MMM yyyy h:mm tt}. The session time is not an individual consultation time.",
                            "COMPLETED", ["Your confirmation was validated.", "Availability was checked again.", "The hospital assigned and saved your appointment number."]);
                    }
                    else Reply(state, result.Message, "FAILED");
                }
                else if (action.Type == "cancel")
                {
                    if (!request.AppointmentId.HasValue || !action.Appointments.Any(a => a.AppointmentId == request.AppointmentId))
                        throw new ArgumentException("Select an appointment from this cancellation request.");
                    var current = await appointments.GetAppointmentByIdAsync(request.AppointmentId.Value);
                    if (current?.PatientId != patient.PatientId || current.Status is "Cancelled" or "Completed" || current.EndAt <= DateTime.UtcNow)
                        throw new InvalidOperationException("That appointment is no longer available to cancel. Refresh your appointments.");
                    state.State = "EXECUTING";
                    await DispatchAsync(state, PlanningWorkflowType.AppointmentCancellation, "Safety Validation & Approval", token);
                    var cancelled = await appointments.CancelAppointmentAsync(current.AppointmentId, state.CancellationReason ?? "Patient confirmed cancellation through Hospital AI Assistant.");
                    if (cancelled == null) throw new InvalidOperationException("The appointment could not be found.");
                    await InvalidateAsync(state, "Confirmed", token);
                    ClearAppointmentTask(state);
                    state.Appointments = [cancelled];
                    Reply(state, $"Appointment No. {cancelled.AppointmentNumber} with {cancelled.DoctorName} was cancelled.", "COMPLETED",
                        ["Your cancellation was explicitly confirmed.", "Appointment ownership and status were checked.", "The cancellation was saved."]);
                }
                else if (action.Type == "reschedule-source")
                {
                    if (!request.AppointmentId.HasValue || !action.Appointments.Any(a => a.AppointmentId == request.AppointmentId))
                        throw new ArgumentException("Select one of your upcoming appointments.");
                    var current = await appointments.GetAppointmentByIdAsync(request.AppointmentId.Value);
                    if (current?.PatientId != patient.PatientId || current.Status is "Cancelled" or "Completed" || current.EndAt <= DateTime.UtcNow)
                        throw new InvalidOperationException("That appointment is no longer available to reschedule. Refresh your appointments.");
                    await InvalidateAsync(state, "Confirmed", token);
                    state.RescheduleAppointmentId = current.AppointmentId;
                    state.SearchQuery = current.DoctorName;
                    state.ExcludedDoctorTimeSlotIds = [current.DoctorTimeSlotId];
                    state.WantsAppointment = true;
                    state.ActiveTask = "reschedule";
                    state.Awaiting = "preferences";
                    await SearchAsync(patient, state, token);
                }
                else if (action.Type == "reschedule")
                {
                    if (!state.RescheduleAppointmentId.HasValue || !request.DoctorTimeSlotId.HasValue ||
                        !action.Slots.Any(s => s.DoctorTimeSlotId == request.DoctorTimeSlotId))
                        throw new ArgumentException("Select one of the replacement appointment options shown.");
                    var current = await appointments.GetAppointmentByIdAsync(state.RescheduleAppointmentId.Value);
                    if (current?.PatientId != patient.PatientId || current.Status is "Cancelled" or "Completed" || current.EndAt <= DateTime.UtcNow)
                        throw new InvalidOperationException("That appointment is no longer available to reschedule. Refresh your appointments.");
                    state.State = "EXECUTING";
                    await DispatchAsync(state, PlanningWorkflowType.AppointmentReschedule, "Safety Validation & Approval", token);
                    var moved = await appointments.RescheduleAppointmentAsync(current.AppointmentId, request.DoctorTimeSlotId.Value);
                    if (moved?.PatientId != patient.PatientId) throw new InvalidOperationException("Could not verify the rescheduling result.");
                    await InvalidateAsync(state, "Confirmed", token);
                    ClearAppointmentTask(state);
                    state.Appointments = [moved];
                    Reply(state, $"Appointment No. {moved.AppointmentNumber} with {moved.DoctorName} was rescheduled to {Local(moved.StartAt):ddd, d MMM yyyy h:mm tt}.",
                        "COMPLETED", ["Your confirmation was validated.", "The replacement session was checked again.", "The rescheduled appointment was saved."]);
                }
                else throw new ArgumentException("This action is not supported.");
            }
            state.Requests[request.RequestId] = fingerprint;
            await SaveAsync(entity, state, token);
            return Response(entity, state);

            object withAction() => new { request.ActionId, request.Decision, request.DoctorTimeSlotId, request.AppointmentId };
        }, token);
    }

    private async Task RouteAsync(PatientDto patient, AssistantState state, string text, CancellationToken token)
    {
        state.State = "ROUTING";
        var rawSafety = ClinicalSafetyTools.EvaluateRedFlags(text);
        var planned = state.ExecutionWorkflowId == null ? null : await _store.GetAsync(state.ExecutionWorkflowId, token);
        var informationQuestion = IsHealthInformationQuestion(text);
        var plannedClinical = !informationQuestion && planned?.Objective == text && planned.Plan.WorkflowType is "SafeTriage" or "TriageThenAppointmentProposal";
        // Compute appointmentIntent first so it can suppress soft symptom signals.
        var appointmentIntent = (planned?.Objective == text && planned.Plan.WorkflowType is ("AppointmentProposal" or "AppointmentStatus") &&
            (planned.Plan.AppointmentRequested || planned.Plan.WorkflowType == "AppointmentStatus")) ||
            Has(text, @"\b(appointment|appointments|book|booking|doctor|surgeon|specialist|cardiologist|ophthalmologist|dermatologist|neurologist|consultation|cardiology|ophthalmology)\b") ||
            (state.SearchQuery != null && Has(text, @"\bproceed\b"));
        // An explicit booking verb (book/schedule/reserve) suppresses the soft IsRoutine
        // and generic keyword symptom signals. Real safety red-flags and pre-planned
        // clinical workflows are always unconditional and are never suppressed.
        var hasExplicitBookingVerb = Has(text, @"\b(book|booking|schedule|reserve)\b");
        var symptoms = plannedClinical || rawSafety.HasEscalation ||
            (!hasExplicitBookingVerb && (ClinicalSafetyTools.IsRoutine(text, null) ||
            Has(text, @"\b(symptom|symptoms|pain|bleeding|fever|cough|sick|unwell|dizzy|headache|nausea|vomiting|breathing|rash|swollen|feel ill|hurt|suffering|nosebleed|shortness|feeling)\b")));
        var askingCancel = Has(text, @"\b(cancel|cancellation)\b") &&
            (appointmentIntent || Has(text, @"\b(my|next|current|existing|booking|reservation)\b"));
        var lookingForAlternative = appointmentIntent && Has(text, @"\b(another|alternative|cannot attend|can't attend)\b");
        var startsBookingTask = Has(text, @"\b(book|booking|schedule|create|reserve)\b");
        var resumesAssessment = state.WorkflowId.HasValue &&
            Has(text, @"\b(resume|continue|return to|go back to)\b.*\b(assessment|triage|question|questions|clinical review)\b");
        var defersAssessment = state.Awaiting == "clinical-answer" && state.WorkflowId.HasValue &&
            Has(text, @"\b(not now|later|stop asking|don't want to answer this now|do not want to answer this now)\b");

        // A raw safety flag always takes priority, including during a clarification or approval.
        if (rawSafety.HasEscalation)
        {
            await InvalidateAsync(state, "Superseded", token);
            if (!startsBookingTask) ClearAppointmentTask(state);
            await StartClinicalAsync(patient, state, text, token);
            return;
        }
        if (informationQuestion && state.Awaiting is not ("clinical-answer" or "cancellation-reason"))
        {
            // Informational questions do not create or continue a triage workflow.
            // Explicit red flags above retain priority over this branch.
            var answer = await _modelClient.AnswerHealthInformationAsync(text, token);
            Reply(state, answer ?? HealthInformationReply(text), "COMPLETED");
            return;
        }
        // A patient can explicitly return to an unfinished assessment after completing
        // an independent task. This restores the persisted workflow rather than treating
        // the request as an appointment preference or a new symptom report.
        if (resumesAssessment)
        {
            var workflow = await workflows.GetForPatientAsync(state.WorkflowId!.Value, patient.PatientId);
            if (workflow?.Status == TriageWorkflowStatuses.PendingPatientInput)
            {
                await InvalidateAsync(state, "Superseded", token);
                ClearAppointmentTask(state);
                state.ActiveTask = "triage";
                ApplyWorkflow(state, workflow);
                ClinicalReply(state, workflow);
                return;
            }
        }
        if (defersAssessment)
        {
            // Leave the workflow safely persisted, but do not keep presenting its
            // question while the patient has explicitly moved on to another task.
            state.ActiveTask = null;
            state.Awaiting = null;
            state.SafetyBlocked = false;
            Reply(state, "No problem. I have kept the assessment for later. You can ask to resume your assessment whenever you are ready.", "COMPLETED");
            return;
        }
        // Resolve actions before read-only small talk ("never mind") or a suspended assessment.
        if (state.PendingAction?.ExpiresAt <= DateTime.UtcNow)
        {
            await InvalidateAsync(state, "Expired", token);
            ClearAppointmentTask(state);
        }
        if ((state.PendingAction != null || state.WantsAppointment || state.Awaiting == "cancellation-reason") &&
            Has(text, @"^(cancel|stop|never mind|nevermind|dismiss|no|no thanks|no need|not needed|nothing else|that's all|that is all)( this| it| request| appointment request)?[.! ]*$"))
        {
            var actionType = state.PendingAction?.Type;
            await InvalidateAsync(state, "Cancelled", token);
            ClearAppointmentTask(state);
            Reply(state, DismissalReply(actionType), "CANCELLED");
            return;
        }
        if (state.PendingAction != null && Has(text, @"^(yes|okay|ok|confirm( this appointment)?|select this|book it|that looks good|go ahead|do it)[.! ]*$"))
        { Reply(state, "Choose your appointment below, then use the confirmation button to finish.", "WAITING_FOR_HUMAN_APPROVAL"); return; }
        // A reply to the cancellation-reason prompt belongs to that task, even
        // when it mentions illness. Explicit red flags and dismissal above still win.
        if (state.Awaiting == "cancellation-reason")
        {
            if (text.Length is < 3 or > 500) { Reply(state, "Please give a cancellation reason between 3 and 500 characters.", "GATHERING_INFORMATION"); return; }
            state.CancellationReason = text;
            state.Awaiting = null;
            await CancellationAsync(patient, state, token);
            return;
        }
        await CheckPatientSafetyAsync(patient.PatientId, state);
        // An acknowledgement of existing urgent guidance is not a new booking
        // request, even when the interrupted conversation still wants an appointment.
        // Keep the safety block and workflow intact; new symptoms and explicit
        // confirmation/assessment answers retain their normal routes.
        if (state.SafetyBlocked &&
            state.Clinical?.TriageLevel is (TriageLevels.Emergency or TriageLevels.Urgent) &&
            state.Awaiting is not ("clinical-answer" or "clinical-review-choice") &&
            Has(text, @"^(ok|okay|fine|alright|all right|got it|understood|thanks|thank you|bye|goodbye)[.! ]*$"))
        {
            var acknowledgement = Has(text, @"^(thanks|thank you)[.! ]*$")
                ? "You're welcome."
                : Has(text, @"^(bye|goodbye)[.! ]*$") ? "Goodbye."
                : Has(text, @"^(ok|okay)[.! ]*$") ? "Ok. If you need any help, please let me know."
                : Has(text, @"^fine[.! ]*$") ? "Fine. If you need any help, please let me know."
                : "Understood.";
            Reply(state, acknowledgement, "WAITING_FOR_HUMAN_APPROVAL");
            return;
        }
        // --- FailedSafely Self-Retry ---
        // If the patient is stuck on a FailedSafely block (technical failure, not a medical danger)
        // and explicitly asks to start fresh, clear the stale workflow and let them try again.
        var isFailedSafelyBlock = state.SafetyBlocked && state.Clinical?.FailedSafely == true
            && state.Clinical?.TriageLevel is not ("Emergency" or "Urgent");
        // A fresh symptom report or independent appointment request is itself a
        // safe retry. Do not force the patient to know a reset command or keep
        // replaying a stale technical-failure notice.
        if (isFailedSafelyBlock && symptoms)
        {
            await InvalidateAsync(state, "Superseded", token);
            state.WorkflowId = null;
            state.Clinical = null;
            state.SafetyBlocked = false;
            state.Awaiting = null;
            state.ActiveTask = null;
            state.Questions = [];
            state.Answers = [];
            isFailedSafelyBlock = false;
        }
        if (isFailedSafelyBlock && Has(text, @"\b(start new|start again|restart|try again|new assessment|reset|start over|begin again)\b"))
        {
            await InvalidateAsync(state, "Superseded", token);
            state.WorkflowId = null;
            state.Clinical = null;
            state.SafetyBlocked = false;
            state.Awaiting = null;
            state.ActiveTask = null;
            state.Questions = [];
            state.Answers = [];
            Reply(state, "Your previous session has been cleared. Please describe your symptoms or what you would like help with, and I will start a fresh assessment.", "GATHERING_INFORMATION");
            return;
        }
        if (state.Awaiting == "clinical-review-choice" && state.WorkflowId.HasValue)
        {
            if (Has(text, @"^(no|n|no thanks|not now|2|3|cancel|decline|monitor)[.! ]*$"))
            {
                var workflow = await workflows.SetPatientClinicalReviewChoiceAsync(state.WorkflowId.Value, patient.PatientId, requested: false);
                if (workflow is null) throw new InvalidOperationException("The optional clinical-review choice is no longer available.");
                state.Awaiting = null;
                Reply(state, "No Clinical Review was requested. You can ask for it later if you change your mind. Seek urgent care if symptoms become severe or rapidly worsen.", "COMPLETED");
                return;
            }

            if (Has(text, @"^(yes|y|please|request (?:a )?clinical review)[.! ]*$"))
            {
                var workflow = await workflows.SetPatientClinicalReviewChoiceAsync(state.WorkflowId.Value, patient.PatientId, requested: true);
                if (workflow is null) throw new InvalidOperationException("The optional clinical-review request is no longer available.");
                ApplyWorkflow(state, workflow);
                ClinicalReply(state, workflow);
                return;
            }

            var query = text.Trim();

            // If user typed '1', 'doctor', or asked to choose a doctor without specifying a name, list approved specialists
            if (Has(query, @"^(1|doctor|select doctor|choose doctor|specialist|specialists)[.! ]*$"))
            {
                var approvedDocs = await db.Doctors.AsNoTracking()
                    .Where(d => d.RegistrationStatus == DoctorRegistrationStatuses.Approved)
                    .OrderBy(d => d.FirstName)
                    .Take(5)
                    .ToListAsync();
                var docListText = string.Join("\n", approvedDocs.Select(d =>
                {
                    var name = d.FirstName.Trim().StartsWith("Dr", StringComparison.OrdinalIgnoreCase)
                        ? $"{d.FirstName.Trim()} {d.LastName.Trim()}"
                        : $"Dr. {d.FirstName.Trim()} {d.LastName.Trim()}";
                    return $"• {name} ({d.Specialization})";
                }));
                Reply(state, $"Please reply with the name of your preferred doctor or specialty:\n\n{docListText}", "GATHERING_INFORMATION");
                return;
            }

            // Check if patient provided a doctor name or specialty
            var searchClean = query.Replace("Dr.", "").Replace("Dr", "").Trim().ToLowerInvariant();
            var approvedDoctors = await db.Doctors.AsNoTracking()
                .Where(d => d.RegistrationStatus == DoctorRegistrationStatuses.Approved)
                .ToListAsync();
            var matchedDoctor = approvedDoctors.FirstOrDefault(d =>
                (d.FirstName + " " + d.LastName).ToLowerInvariant().Contains(searchClean) ||
                d.FirstName.ToLowerInvariant().Contains(searchClean) ||
                d.LastName.ToLowerInvariant().Contains(searchClean) ||
                (!string.IsNullOrWhiteSpace(d.Specialization) && d.Specialization.ToLowerInvariant().Contains(searchClean)) ||
                (!string.IsNullOrWhiteSpace(d.Specialization) && searchClean.Contains(d.Specialization.ToLowerInvariant())));

            if (matchedDoctor != null)
            {
                var workflow = await workflows.SetPatientClinicalReviewChoiceAsync(state.WorkflowId.Value, patient.PatientId, requested: true, preferredDoctorId: matchedDoctor.DoctorId);
                if (workflow is null) throw new InvalidOperationException("The optional clinical-review request is no longer available.");
                ApplyWorkflow(state, workflow);
                ClinicalReply(state, workflow);
                return;
            }

            // Check if patient provided a recognizable clinical specialty
            if (searchClean.Length >= 4 && (searchClean.Contains("pediatric") || searchClean.Contains("orthopedic") || searchClean.Contains("neurolog") || searchClean.Contains("dermatolog") || searchClean.Contains("oncolog") || searchClean.Contains("gyn") || searchClean.Contains("psychiatr") || searchClean.Contains("ent") || searchClean.Contains("urolog") || searchClean.Contains("general") || searchClean.Contains("medicine") || searchClean.Contains("cardio")))
            {
                var workflow = await workflows.SetPatientClinicalReviewChoiceAsync(state.WorkflowId.Value, patient.PatientId, requested: true, targetSpecialty: query);
                if (workflow is null) throw new InvalidOperationException("The optional clinical-review request is no longer available.");
                ApplyWorkflow(state, workflow);
                ClinicalReply(state, workflow);
                return;
            }

            Reply(state, "How would you like to proceed with your clinical review?\n\n" +
                "1. 🩺 Select a Specific Doctor (Reply with doctor's name or specialty, e.g. Dr. Silva or Cardiologist)\n" +
                "2. ✕ Not right now (I will monitor my symptoms)", "GATHERING_INFORMATION");
            return;
        }

        // For FailedSafely-blocked patients (non-urgent), allow read-only actions (view appointments)
        // and cancellations. Only Emergency/Urgent blocks are fully impenetrable.
        // Read-only doctor, availability and appointment-history requests remain
        // available during a safety block. TryReadAsync rejects all mutation verbs,
        // so this cannot create, reschedule or cancel an appointment.
        if (!symptoms && await TryReadAsync(patient, state, text, token)) return;
        // Allow cancellation even when FailedSafely-blocked — the patient already has a confirmed slot
        // and stopping that booking is safe and is never a clinical decision.
        if (isFailedSafelyBlock && askingCancel)
        {
            await InvalidateAsync(state, "Superseded", token);
            state.Awaiting = "cancellation-reason";
            state.CancellationReason = null;
            Reply(state, "What is your reason for cancelling? I will then show your appointments for explicit confirmation.", "GATHERING_INFORMATION");
            return;
        }
        // For a FailedSafely block (technical failure), show the improved message with self-retry option.
        if (isFailedSafelyBlock)
        {
            await ReplySafetyBlockedAsync(patient.PatientId, state);
            return;
        }
        if (state.Awaiting == "clinical-answer" && state.WorkflowId.HasValue && appointmentIntent)
        {
            // A routine, unfinished assessment remains persisted but does not own a
            // later independent booking request. High-risk routes still retain their
            // existing block and guidance.
            var workflow = await workflows.GetForPatientAsync(state.WorkflowId.Value, patient.PatientId);
            var highRisk = workflow?.TriageLevel is TriageLevels.Emergency or TriageLevels.Urgent ||
                workflow?.Status == TriageWorkflowStatuses.FailedSafely;
            if (workflow?.Status != TriageWorkflowStatuses.PendingPatientInput || highRisk)
            {
                await ReplySafetyBlockedAsync(patient.PatientId, state);
                return;
            }
            state.ActiveTask = "booking";
            state.Awaiting = null;
            state.SafetyBlocked = false;
        }
        if (state.Awaiting == "clinical-answer" && state.WorkflowId.HasValue)
        {
            if (text.Length > 500) { Reply(state, "Please keep this answer under 500 characters.", "GATHERING_INFORMATION"); return; }
            var question = state.Questions.FirstOrDefault(q => state.Answers.All(a => a.QuestionId != q.Id));
            var workflowContext = await workflows.GetForPatientAsync(state.WorkflowId.Value, patient.PatientId);
            var guidance = workflowContext?.Guidance;
            var metadata = guidance?.FollowUpItems.FirstOrDefault(q => q.Id == question?.Id);
            if (question == null || metadata == null || guidance == null)
            { Reply(state, "I cannot verify the current assessment question. Please refresh the conversation.", "GATHERING_INFORMATION"); return; }
            if (SafeTriageRequirementRules.UnavailableResponse(text) is { } unavailable)
            {
                var updated = await workflows.ContinueForPatientAsync(state.WorkflowId.Value, patient.PatientId,
                    new() { Answers = [new() { QuestionId = question.Id, State = unavailable }] }, state.ExecutionWorkflowId);
                if (updated is null) throw new InvalidOperationException("The assessment is no longer waiting for answers.");
                RecordFollowUp(state, unavailable);
                state.Answers = [];
                ApplyWorkflow(state, updated);
                ClinicalReply(state, updated);
                return;
            }
            var previousReply = state.Messages.LastOrDefault(m => m.Role == "assistant")?.Text;
            var interpretation = intentClient == null ? null : await intentClient.InterpretAsync(text, metadata, guidance, state.Answers, previousReply, token);
            if (interpretation?.Intent != "ANSWER")
            {
                var response = interpretation is { Intent: "QUESTION_HELP" or "CLARIFICATION" or "GENERAL_QUERY" or "UNCLEAR", Response: { Length: > 0 } }
                    ? interpretation.Response : AssessmentUnavailableReply(metadata);
                Reply(state, response, "GATHERING_INFORMATION");
                return;
            }
            var semanticUnavailable = SafeTriageRequirementRules.UnavailableResponse(interpretation.NormalizedAnswer ?? "");
            if (semanticUnavailable is null && !TryValidateAssessmentAnswer(metadata, interpretation.NormalizedAnswer, out _))
            { Reply(state, "I could not use that response. Please share a little more detail about the current question.", "GATHERING_INFORMATION"); return; }
            // Submit every useful answer immediately. The persisted workflow, rather
            // than this conversation cache, decides what remains missing next.
            var workflow = await workflows.ContinueForPatientAsync(state.WorkflowId.Value, patient.PatientId,
                new() { Answers = [new TriageAnswerDto { QuestionId = question.Id,
                    Value = semanticUnavailable is null ? text : "", State = semanticUnavailable }] }, state.ExecutionWorkflowId);
            if (workflow == null) throw new InvalidOperationException("The assessment is no longer waiting for answers. Refresh the conversation.");
            RecordFollowUp(state, semanticUnavailable ?? SafeTriageRequirementState.Answered);
            state.Answers = [];
            ApplyWorkflow(state, workflow);
            ClinicalReply(state, workflow);
            if (!state.SafetyBlocked && state.Awaiting != "clinical-review" && state.WantsAppointment) await SearchAsync(patient, state, token);
            return;
        }
        // "Yes" to an explicit no-availability follow-up broadens the existing
        // verified search instead of rerunning the same date/daypart constraint.
        if (state.Awaiting == "preferences" && state.WantsAppointment &&
            state.AvailabilityChecked && state.PendingAction == null &&
            (state.PreferredDate.HasValue || state.ThroughDate.HasValue || state.Period != null) &&
            Has(text, @"^(yes|okay|ok|go ahead|do it)[.! ]*$"))
        {
            state.PreferredDate = null;
            state.ThroughDate = null;
            state.Period = null;
            state.AvailabilityChecked = false;
            await SearchAsync(patient, state, token);
            return;
        }

        if (Has(text, @"\b(reschedule|rescheduling)\b"))
        {
            await InvalidateAsync(state, "Superseded", token);
            await RescheduleSelectionAsync(patient, state, token);
            return;
        }
        if (symptoms || state.Awaiting == "symptoms")
        {
            await InvalidateAsync(state, "Superseded", token);
            // Symptoms start a separate safety task. Preserve booking preferences only
            // when this very message explicitly asks for a booking after assessment.
            if (!startsBookingTask) ClearAppointmentTask(state);
            if (Has(text, @"^(i need help with my symptoms|patient help|help with symptoms)[.! ]*$"))
            { state.Awaiting = "symptoms"; Reply(state, "Please describe how you feel, when it started, and any current symptoms.", "GATHERING_INFORMATION"); return; }
            if (Has(text, @"\b(book|booking|schedule|create|reserve)\b"))
            {
                state.WantsAppointment = true;
                var doctors = await tools.FindDoctorsAsync("");
                state.SearchQuery = Query(text, doctors) ?? state.SearchQuery;
                var dateError = ApplyDates(text, state, Today());
                if (dateError != null) state.Awaiting = "preferences";
            }
            await StartClinicalAsync(patient, state, text, token);
            if (!state.SafetyBlocked && state.WantsAppointment) await SearchAsync(patient, state, token);
            return;
        }
        if (state.SafetyBlocked && (appointmentIntent || state.WantsAppointment || askingCancel))
        {
            // Re-evaluate persisted safety state. Conversations created before a
            // clinical-review decision may carry an old cached SafetyBlocked value.
            await CheckPatientSafetyAsync(patient.PatientId, state);
            if (state.SafetyBlocked)
            {
                await InvalidateAsync(state, "BlockedByClinicalSafety", token);
                await ReplySafetyBlockedAsync(patient.PatientId, state);
                return;
            }
        }
        // An explicit appointment action can mention a medical report or follow-up.
        // It must remain in the appointment workflow; otherwise the read-only
        // medical-report agent would receive the booking request and decline it.
        var appointmentMutation = Has(text, @"\b(book|booking|schedule|reschedule|reserve|make\s+(an\s+)?appointment|create|confirm|proceed)\b");
        var extension = appointmentMutation
            ? null
            : registry.AdditionalAgents.FirstOrDefault(agent => agent.Capability.Enabled && agent.CanHandle(text));
        if (extension != null)
        {
            await InvalidateAsync(state, "Superseded", token);
            if (extension.Capability.Id == "medical-reports")
                await DispatchAsync(state, PlanningWorkflowType.MedicalRecords, "Medical Records", token);
            Reply(state, await extension.ReadAsync(text, patient, token), "COMPLETED");
            return;
        }

        if (askingCancel)
        {
            await InvalidateAsync(state, "Superseded", token);
            state.Awaiting = "cancellation-reason";
            state.CancellationReason = null;
            Reply(state, "What is your reason for cancelling? I will then show your appointments for explicit confirmation.", "GATHERING_INFORMATION");
            return;
        }
        // A patient declining a further search must never be interpreted as a
        // doctor/specialty name (for example, "no need" used to become a search).
        if (state.Awaiting == "preferences" && Has(text, @"^(no need|not needed|nothing else|no thanks|that's all|that is all)[.! ]*$"))
        {
            state.WantsAppointment = false;
            state.Awaiting = null;
            Reply(state, "No problem. I have not created an appointment. You can ask me to find a doctor whenever you are ready.", "COMPLETED");
            return;
        }
        if (appointmentIntent || state.Awaiting == "preferences" || state.WantsAppointment)
        {
            var priorSearchQuery = state.SearchQuery;
            // An explicit new booking must not inherit date/time filters from a prior
            // availability or completed task. Preference-only replies keep booking context.
            if (startsBookingTask && state.ActiveTask != "booking") ClearAppointmentTask(state);
            var doctors = await tools.FindDoctorsAsync("");
            var query = Query(text, doctors);
            if (!appointmentIntent && query is null && !Has(text,
                @"\b(today|tomorrow|next week|monday|tuesday|wednesday|thursday|friday|saturday|sunday|morning|afternoon|evening|any time|anytime|any date|earliest|any day|first available)\b|\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}[/\-:]\d{1,2}\b|\b\d{1,2}\s*(am|pm)\b|\b(january|february|march|april|may|june|july|august|september|october|november|december)\s+\d"))
            {
                Reply(state, "You can choose an appointment below, change the doctor or date, or ask me about something else.",
                    state.PendingAction is null ? "GATHERING_INFORMATION" : "WAITING_FOR_HUMAN_APPROVAL");
                return;
            }
            await InvalidateAsync(state, "Superseded", token);
            if (query == null && Has(text, @"\b(that doctor|that specialist|that one)\b")) query = priorSearchQuery;
            if (query == null && lookingForAlternative)
            {
                var current = (await MyAppointmentsAsync(patient)).Where(a => a.Status == "Confirmed" && a.EndAt > DateTime.UtcNow).ToArray();
                if (current.Length == 1) query = current[0].DoctorName;
                Reply(state, "I can look for a separate appointment. Your current appointment will remain active unless you request and confirm its cancellation.", "GATHERING_INFORMATION");
            }
            if (query != null) state.SearchQuery = query;
            // Do not treat arbitrary conversational text as a specialty. Keep the
            // existing valid preference until the patient supplies a recognised
            // doctor/specialty or an explicit date/time preference.
            state.WantsAppointment = true;
            state.ActiveTask = "booking";
            state.ReadSearchMode = null;
            var error = ApplyDates(text, state, Today());
            if (error != null) { state.Awaiting = "preferences"; Reply(state, error, "GATHERING_INFORMATION"); return; }
            await SearchAsync(patient, state, token);
            return;
        }
        Reply(state, "I can help with symptoms, find a doctor, show your appointments, or prepare a booking or cancellation for your confirmation. What would you like to do?", "GATHERING_INFORMATION");
    }

    private static bool TryValidateAssessmentAnswer(TriageFollowUpQuestionDto question, string? value, out string accepted)
    {
        accepted = value?.Trim() ?? string.Empty;
        if (accepted.Length is < 1 or > 500) return false;
        switch (question.Type)
        {
            case "number":
            case "severityScale":
                if (!decimal.TryParse(accepted, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ||
                    (question.Minimum.HasValue && number < question.Minimum) ||
                    (question.Maximum.HasValue && number > question.Maximum)) return false;
                accepted = number.ToString(CultureInfo.InvariantCulture);
                return true;
            case "yesNo":
                return accepted is "Yes" or "No" or "yes" or "no";
            case "singleChoice":
                var choiceValue = accepted;
                return question.Options.Any(option => string.Equals(option, choiceValue, StringComparison.OrdinalIgnoreCase));
            case "multipleChoice":
                var choices = accepted.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                return choices.Length > 0 && choices.Distinct(StringComparer.OrdinalIgnoreCase).Count() == choices.Length &&
                    choices.All(choice => question.Options.Any(option => string.Equals(option, choice, StringComparison.OrdinalIgnoreCase)));
            case "shortText":
                // Free-text answers can be a single digit. Semantic extraction decides
                // which fields they supply; length is not a clinical completeness rule.
                return true;
            default:
                return false;
        }
    }

    private static string AssessmentUnavailableReply(TriageFollowUpQuestionDto question)
    {
        return "I cannot interpret your message right now. Please try again shortly; your assessment question is still available below.";
    }

    private async Task StartClinicalAsync(PatientDto patient, AssistantState state, string text, CancellationToken token)
    {
        state.ActiveTask = "triage";
        state.State = "GATHERING_INFORMATION";
        state.Answers = [];
        // A new symptom message must always be assessed afresh. Pending review
        // status is restored by GetAsync; reusing it here would replay an older
        // clinical-review result instead of classifying the new patient report.
        state.Clinical = null;
        state.SafetyBlocked = false;
        var workflow = await workflows.StartForPatientAsync(patient.PatientId, new() { Symptoms = text, Vitals = legacyVitals }, state.ExecutionWorkflowId);
        state.WorkflowId = workflow.WorkflowId;
        ApplyWorkflow(state, workflow);
        ClinicalReply(state, workflow);
    }

    private static void ApplyWorkflow(AssistantState state, TriageWorkflowDto workflow)
    {
        var unsafeResult = workflow.TriageLevel is "Emergency" or "Urgent" || workflow.Status == TriageWorkflowStatuses.FailedSafely;
        var needsReview = workflow.RequiresHumanReview &&
            workflow.ApprovalStatus is TriageApprovalStatuses.Pending or TriageApprovalStatuses.RevisionRequested;
        var finalizedCurrentReview = state.WorkflowId == workflow.WorkflowId &&
            workflow.Status == TriageWorkflowStatuses.Completed &&
            workflow.ApprovalStatus is TriageApprovalStatuses.Approved or TriageApprovalStatuses.Rejected or "ClinicianResponse";
        var optionalReview = !finalizedCurrentReview &&
            !workflow.RequiresHumanReview &&
            workflow.TriageLevel == TriageLevels.ClinicalReview;
        var alreadyUrgent = state.Clinical?.TriageLevel is "Emergency" or "Urgent" ||
            (state.Clinical?.FailedSafely == true && !finalizedCurrentReview);
        // A clinical review requires staff approval after the patient's explicit slot
        // selection; it must not prevent the patient from receiving a safe proposal.
        // Only urgent/emergency, failed-safe, and missing-required-information states
        // block normal appointment actions.
        // PendingPatientInput blocks only the active triage task. It is not a global
        // prohibition on unrelated appointment management.
        state.SafetyBlocked = unsafeResult || alreadyUrgent ||
            (state.ActiveTask == "triage" && workflow.Status == TriageWorkflowStatuses.PendingPatientInput);
        if (!alreadyUrgent)
            state.Clinical = new(workflow.TriageLevel, "Existing safety workflow", needsReview,
                workflow.Status == TriageWorkflowStatuses.FailedSafely, [], workflow.RedFlags, workflow.UrgentFlags,
                workflow.ClinicalReviewFlags, null, workflow.MissingInformation, []);
        state.Questions = workflow.Status == TriageWorkflowStatuses.PendingPatientInput
            ? (workflow.Guidance?.FollowUpItems ?? []).Select(q => new AssistantQuestion(q.Id, q.Prompt, q.Required) {
                Type = q.Type, Options = q.Options, Hint = q.Hint, Unit = q.Unit,
                Minimum = q.Minimum, Maximum = q.Maximum
            }).Take(1).ToList() : [];
        state.Awaiting = state.Questions.Count > 0 ? "clinical-answer" : needsReview ? "clinical-review" : optionalReview ? "clinical-review-choice" : null;
    }

    private static void ClinicalReply(AssistantState state, TriageWorkflowDto workflow)
    {
        if (workflow.ReviewedResponse is not null)
        {
            Reply(state, "Clinical review completed\n\n" + workflow.ReviewedResponse, "COMPLETED");
            return;
        }
        if (workflow.TriageLevel == TriageLevels.Emergency)
        {
            var flags = workflow.RedFlags.Count > 0
                ? "\nCritical warning signs identified: " + string.Join(", ", workflow.RedFlags) + "\n"
                : "";
            var emergencyMessage = "🚨 CRITICAL EMERGENCY ALERT\n\n" +
                "Immediate emergency care is required. Your symptoms match critical medical warning signs." +
                (string.IsNullOrWhiteSpace(flags) ? "\n\n" : $"\n{flags}\n") +
                "• Call 1990 (National Emergency Ambulance) or 911 immediately.\n" +
                "• Proceed to the nearest 24/7 Hospital Emergency Room (ER).\n" +
                "• Our Emergency Department has been automatically alerted of your case.\n\n" +
                "Do not wait for an online chat or message reply.";
            Reply(state, emergencyMessage, "WAITING_FOR_HUMAN_APPROVAL");
            return;
        }
        if (state.Awaiting == "clinical-review")
        {
            Reply(state, workflow.PatientMessage, "WAITING_FOR_HUMAN_APPROVAL");
            return;
        }
        if (state.Awaiting == "clinical-review-choice")
        {
            var prompt = (string.IsNullOrWhiteSpace(workflow.PatientMessage)
                ? "Based on the symptoms you reported, we recommend having a medical professional review this assessment."
                : workflow.PatientMessage) +
                "\n\nHow would you like to proceed with your clinical review?" +
                "\n1. 🩺 Select a Specific Doctor (Reply with doctor's name or specialty, e.g. Dr. Silva or Cardiologist)" +
                "\n2. ✕ Not right now (I will monitor my symptoms)";
            Reply(state, prompt, "GATHERING_INFORMATION");
            return;
        }
        var guidance = workflow.Guidance;
        var parts = new List<string> { workflow.PatientMessage };
        if (state.Questions.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(guidance?.Summary)) parts.Add(guidance.Summary);
            parts.AddRange(guidance?.Actions ?? []);
            parts.AddRange(guidance?.SeekHelpIf ?? []);
        }
        if (state.Awaiting == "clinical-review") parts.Add("Clinical review is pending. You can refresh this conversation to check its status.");
        Reply(state, string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct()),
            state.Questions.Count > 0 ? "GATHERING_INFORMATION" : state.SafetyBlocked || state.Awaiting == "clinical-review" ? "WAITING_FOR_HUMAN_APPROVAL" : "COMPLETED",
            []);
    }

    private async Task SearchAsync(PatientDto patient, AssistantState state, CancellationToken token)
    {
        await CheckPatientSafetyAsync(patient.PatientId, state);
        if (state.SafetyBlocked)
        {
            await ReplySafetyBlockedAsync(patient.PatientId, state);
            return;
        }
        if (string.IsNullOrWhiteSpace(state.SearchQuery))
        {
            state.Awaiting = "preferences";
            Reply(state, "Which doctor or specialty would you prefer? You can also include a date and morning, afternoon, or evening.", "GATHERING_INFORMATION");
            return;
        }
        // A doctor or specialty is enough to query real upcoming availability. Date
        // and daypart are optional filters, not prerequisites for seeing slots.
        state.State = "PROPOSING_ACTION";
        await DispatchAsync(state,
            state.RescheduleAppointmentId.HasValue ? PlanningWorkflowType.AppointmentReschedule : PlanningWorkflowType.AppointmentProposal,
            "Appointment Proposal", token);
        var proposal = await proposals.CreateAsync(new(patient.PatientId, state.Clinical, state.SearchQuery,
            state.PreferredDate, state.Period, state.ThroughDate, state.ExcludedDoctorTimeSlotIds), token);
        if (state.ExecutionWorkflowId != null)
        {
            var execution = (await _store.GetAsync(state.ExecutionWorkflowId, token))!;
            foreach (var trace in proposal.ToolTrace)
            {
                execution.ToolResultsSummary.Add(trace.Tool + ": " + trace.Status);
                execution.AuditEvents.Add(new() { EventType = "ToolExecuted", Description = trace.Outcome,
                    Metadata = trace.Tool + "; validation=" + trace.ValidationPassed + "; step=" + execution.CurrentStep });
            }
            await _store.SaveAsync(execution, token);
        }
        state.Doctors = proposal.Doctors;
        state.Slots = proposal.Slots;
        state.AvailabilityChecked = true;
        state.Awaiting = "preferences";
        if (proposal.ProposalId.HasValue && proposal.Slots.Count > 0)
        {
            var rescheduling = state.RescheduleAppointmentId.HasValue;
            state.PendingAction = new() {
                Type = rescheduling ? "reschedule" : "book",
                Title = rescheduling ? "Confirm replacement session" : "Confirm an appointment",
                ProposalId = proposal.ProposalId,
                Description = rescheduling
                    ? "Select a replacement session, then confirm. Your existing appointment is unchanged until confirmation succeeds."
                    : "Select an option, then confirm. The hospital will assign the final appointment number.",
                Slots = proposal.Slots
            };
            var preference = state.PreferredDate.HasValue ? $" for {state.PreferredDate:yyyy-MM-dd}" : "";
            if (state.ThroughDate.HasValue) preference += $" through {state.ThroughDate:yyyy-MM-dd}";
            if (state.Period != null) preference += $" ({state.Period})";
            Reply(state, $"I found {proposal.Slots.Count} available {(proposal.Slots.Count == 1 ? "appointment" : "appointments")}{preference}. Choose the time that works best for you.",
                "WAITING_FOR_HUMAN_APPROVAL", ["Approved doctors were found.", "Available hospital sessions were checked.", "Options were saved for your confirmation; no booking was made."]);
        }
        else Reply(state, NoAvailabilityMessage(state, proposal.Message), "GATHERING_INFORMATION",
            ["Approved doctors and available hospital sessions were checked."]);
    }

    private async Task CancellationAsync(PatientDto patient, AssistantState state, CancellationToken token)
    {
        await DispatchAsync(state, PlanningWorkflowType.AppointmentCancellation, "Planning & Coordination", token);
        var available = (await MyAppointmentsAsync(patient)).Where(a => a.Status is not ("Cancelled" or "Completed") && a.EndAt > DateTime.UtcNow).ToArray();
        if (available.Length == 0) { Reply(state, "You have no upcoming appointments available to cancel.", "COMPLETED"); return; }
        state.PendingAction = new() { Type = "cancel", Title = "Confirm cancellation",
            Description = "Select the appointment you want to cancel. Reason: " + state.CancellationReason,
            Appointments = available };
        Reply(state, "Select the appointment below and use Confirm cancellation. Nothing has been cancelled yet.", "WAITING_FOR_HUMAN_APPROVAL",
            ["Your upcoming appointments were checked."]);
    }

    private async Task RescheduleSelectionAsync(PatientDto patient, AssistantState state, CancellationToken token)
    {
        await DispatchAsync(state, PlanningWorkflowType.AppointmentReschedule, "Planning & Coordination", token);
        var available = (await MyAppointmentsAsync(patient))
            .Where(a => a.Status is not ("Cancelled" or "Completed") && a.EndAt > DateTime.UtcNow).ToArray();
        if (available.Length == 0)
        {
            Reply(state, "You have no upcoming appointments available to reschedule.", "COMPLETED");
            return;
        }
        ClearAppointmentTask(state);
        state.PendingAction = new() {
            Type = "reschedule-source", Title = "Choose appointment to reschedule",
            Description = "Select the existing appointment and confirm. Nothing will be changed until you later choose and confirm a replacement session.",
            Appointments = available
        };
        Reply(state, "Select the appointment you want to reschedule. This step will not change it.", "WAITING_FOR_HUMAN_APPROVAL",
            ["Your upcoming appointments were checked."]);
    }

    private async Task CheckPatientSafetyAsync(int patientId, AssistantState state)
    {
        // Opening a new conversation must not bypass an unresolved safety assessment.
        var history = await workflows.GetHistoryForPatientAsync(patientId);
        // Emergency/urgent results and incomplete failed-safe assessments block
        // appointment mutations. A technical failure must not silently become
        // permission to book without a trustworthy safety result.
        // ClinicalReview-level pending workflows are informational: the patient
        // acknowledged the notice and the care team will respond independently.
        // Overwriting state.WorkflowId with a different conversation's ClinicalReview
        // workflow corrupts the current conversation's awaiting state and locks it
        // permanently in WAITING_FOR_HUMAN_APPROVAL — that is the bug being fixed here.
        var latest = history.FirstOrDefault();
        var blocking = latest != null && (latest.Status == TriageWorkflowStatuses.FailedSafely ||
            (latest.ApprovalStatus is TriageApprovalStatuses.Pending or TriageApprovalStatuses.RevisionRequested &&
             latest.TriageLevel is TriageLevels.Emergency or TriageLevels.Urgent)) ? latest : null;
        if (blocking != null)
        {
            // A genuinely blocking workflow replaces the current workflow context so
            // that the patient sees emergency/urgent guidance and cannot bypass it.
            if (state.WorkflowId != blocking.WorkflowId) state.Answers = [];
            state.WorkflowId = blocking.WorkflowId;
            ApplyWorkflow(state, blocking);
            return;
        }
        // No globally blocking workflow found. Refresh only the current conversation's
        // workflow without inheriting a different conversation's clinical context.
        // This preserves state.Awaiting, state.WorkflowId, and state.SafetyBlocked
        // correctly for ClinicalReview-level or PendingPatientInput cases that belong
        // to a different conversation.
        if (state.WorkflowId.HasValue)
        {
            var current = await workflows.GetForPatientAsync(state.WorkflowId.Value, patientId);
            if (current != null) ApplyWorkflow(state, current);
        }
        else
        {
            state.SafetyBlocked = false;
        }
    }

    private async Task ReplySafetyBlockedAsync(int patientId, AssistantState state)
    {
        var workflow = state.WorkflowId.HasValue
            ? await workflows.GetForPatientAsync(state.WorkflowId.Value, patientId) : null;
        var urgent = state.Clinical?.TriageLevel is "Emergency" or "Urgent";
        var failedSafely = state.Clinical?.FailedSafely == true && !urgent;
        var missingAnswers = !urgent && workflow?.Status == TriageWorkflowStatuses.PendingPatientInput;
        string message;
        if (urgent)
        {
            var isEmergency = state.Clinical?.TriageLevel == "Emergency" || workflow?.TriageLevel == TriageLevels.Emergency;
            if (isEmergency)
            {
                var flags = (state.Clinical?.EmergencyFlags ?? []).Count > 0
                    ? "\nCritical warning signs identified: " + string.Join(", ", state.Clinical.EmergencyFlags) + "\n"
                    : (workflow?.RedFlags ?? []).Count > 0
                        ? "\nCritical warning signs identified: " + string.Join(", ", workflow.RedFlags) + "\n"
                        : "";
                message = "🚨 CRITICAL EMERGENCY ALERT\n\n" +
                          "Appointment booking is unavailable because your symptoms require immediate emergency medical care." +
                          (string.IsNullOrWhiteSpace(flags) ? "\n\n" : $"\n{flags}\n") +
                          "• Call 1990 (National Emergency Ambulance) or 911 immediately.\n" +
                          "• Proceed to the nearest 24/7 Hospital Emergency Room (ER).\n" +
                          "• Our Emergency Department has been alerted of your case.\n\n" +
                          "Do not wait for an online chat or message reply.";
            }
            else
            {
                message = "Appointment booking is paused because your assessment identified urgent safety concerns. Follow the assessment guidance and do not delay urgent care.";
            }
        }
        else if (missingAnswers)
        {
            message = "Appointment booking is paused because your assessment needs more information. Please answer the assessment questions to continue.";
        }
        else if (failedSafely)
        {
            // Technical failure — give the patient a clear, actionable message
            message = "Your previous assessment could not be completed safely because of a technical issue — this does not mean that anything dangerous was found in your message.\n\n" +
                      "**What you can still do:**\n" +
                      "• View or cancel your existing appointments\n" +
                      "• Say \"start new assessment\" to clear this and try again\n\n" +
                      "If you need urgent help, please call the clinic directly or visit your nearest emergency department.";
        }
        else
        {
            message = "Appointment booking is paused because your safety assessment could not be completed safely. Contact the care team to review the assessment.";
        }
        if (workflow != null)
        {
            var guidance = workflow.Guidance;
            var parts = new List<string> { message, workflow.PatientMessage, guidance?.Summary ?? "" };
            var safeMissing = workflow.MissingInformation
                .Where(m => !m.Contains(':') &&
                            !m.Contains("vital signs", StringComparison.OrdinalIgnoreCase) &&
                            !m.Contains("Missing.", StringComparison.OrdinalIgnoreCase) &&
                            !m.Contains("extraction", StringComparison.OrdinalIgnoreCase) &&
                            !m.Contains("untrusted", StringComparison.OrdinalIgnoreCase))
                .ToList();
            parts.AddRange(safeMissing);
            parts.AddRange(guidance?.Actions ?? []);
            parts.AddRange(guidance?.SeekHelpIf ?? []);
            message = string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct());
        }
        if (missingAnswers && state.Questions.Count == 0)
            message += "\n\nNo follow-up questions are available. Contact the care team to review this incomplete assessment.";
        Reply(state, message, missingAnswers && state.Questions.Count > 0
            ? "GATHERING_INFORMATION" : "WAITING_FOR_HUMAN_APPROVAL");
    }

    private async Task<IReadOnlyList<AppointmentDto>> MyAppointmentsAsync(PatientDto patient)
    {
        var all = new List<AppointmentDto>();
        for (var page = 1; ; page++)
        {
            var result = await appointments.GetAllAppointmentsAsync(null, null, null, null, "date", "asc", page, 100, patient.PatientId);
            all.AddRange(result.Data.Where(a => a.PatientId == patient.PatientId));
            if (result.Data.Count() < 100) break;
        }
        return all;
    }

    private static IEnumerable<AppointmentDto> FilterAppointments(IEnumerable<AppointmentDto> all, AssistantState state) => all.Where(a =>
        (!state.PreferredDate.HasValue || DateOnly.FromDateTime(Local(a.StartAt)) >= state.PreferredDate &&
         DateOnly.FromDateTime(Local(a.StartAt)) <= (state.ThroughDate ?? state.PreferredDate)) &&
        (state.Period == null || state.Period switch { "morning" => Local(a.StartAt).Hour < 12,
            "afternoon" => Local(a.StartAt).Hour is >= 12 and < 17, "evening" => Local(a.StartAt).Hour >= 17, _ => false }));

    private async Task InvalidateAsync(AssistantState state, string status, CancellationToken token)
    {
        if (state.PendingAction?.ProposalId is int proposalId)
        {
            var proposal = await db.AppointmentProposals.SingleOrDefaultAsync(p => p.AppointmentProposalId == proposalId, token);
            if (proposal?.Status == "PendingPatientConfirmation") proposal.Status = status;
        }
        if (state.PendingAction != null)
        {
            var actionId = state.PendingAction.ActionId;
            var terminalStatus = status == "Cancelled" ? "Dismissed" : status;
            state.PendingAction.Status = terminalStatus;
            foreach (var message in state.Messages.Where(m => m.ProposedAction?.ActionId == actionId))
                message.ProposedAction!.Status = terminalStatus;
        }
        state.PendingAction = null;
        if (status is "Cancelled" or "Expired") ClearAppointmentTask(state);
    }

    private static void ClearAppointmentTask(AssistantState state)
    {
        if (state.Awaiting is "preferences" or "cancellation-reason") state.Awaiting = null;
        state.CancellationReason = null;
        state.RescheduleAppointmentId = null;
        state.ActiveTask = null;
        state.ReadSearchMode = null;
        state.SearchQuery = null;
        state.PreferredDate = null;
        state.ThroughDate = null;
        state.Period = null;
        state.WantsAppointment = false;
        state.AvailabilityChecked = false;
        state.ExcludedDoctorTimeSlotIds = [];
        state.Doctors = [];
        state.Slots = [];
        state.Appointments = [];
    }

    private static string DismissalReply(string? type) => type switch {
        "cancel" => "Okay, your appointment will stay booked.",
        "reschedule" or "reschedule-source" => "Okay, your existing appointment has not been changed.",
        _ => "Okay, I've cancelled this appointment request."
    };

    private static void RecordFollowUp(AssistantState state, SafeTriageRequirementState status)
    {
        var index = state.Messages.FindLastIndex(m => m.Role == "user");
        if (index >= 0 && state.Questions.FirstOrDefault() is { } question)
            state.Messages[index] = state.Messages[index] with { FollowUpQuestion = question, FollowUpState = status };
    }

    private static string NoAvailabilityMessage(AssistantState state, string fallback)
    {
        var constraint = state.PreferredDate.HasValue ? $" for {state.PreferredDate:yyyy-MM-dd}" : "";
        if (state.ThroughDate.HasValue) constraint += $" through {state.ThroughDate:yyyy-MM-dd}";
        if (state.Period != null) constraint += $" in the {state.Period}";
        return state.Doctors.Count > 0
            ? $"Approved doctor matches were found, but no matching sessions are available{constraint}. Would you like me to check another date?"
            : fallback + " Would you like me to check another date?";
    }

    private static bool Seen(AssistantState state, Guid id, string fingerprint)
    {
        if (!state.Requests.TryGetValue(id, out var stored)) return false;
        if (stored != fingerprint) throw new ArgumentException("This request ID was already used for different content.");
        return true;
    }
    private static string Fingerprint(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    private static AssistantState Read(AssistantConversation entity) => JsonSerializer.Deserialize<AssistantState>(entity.StateJson, Json) ?? new();
    private static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), AppointmentAgentTools.HospitalTimeZone);
    private static DateOnly Today() => DateOnly.FromDateTime(Local(DateTime.UtcNow));
    private static void Reply(AssistantState state, string text, string status, IReadOnlyList<string>? progress = null)
    {
        state.State = status;
        state.Messages.Add(new(Guid.NewGuid().ToString(), "assistant", text, DateTime.UtcNow, progress ?? []) {
            Appointments = state.Appointments.ToArray(), Slots = state.Slots.ToArray(), Doctors = state.Doctors.ToArray(),
            AvailabilityChecked = state.AvailabilityChecked,
            ProposedAction = state.PendingAction != null && !state.Messages.Any(m => m.ProposedAction?.ActionId == state.PendingAction.ActionId)
                ? state.PendingAction : null
        });
    }
    private async Task<AssistantConversation> OwnedAsync(int patientId, Guid id, CancellationToken token) =>
        await db.AssistantConversations.SingleOrDefaultAsync(c => c.PatientId == patientId && c.AssistantConversationId == id, token)
            ?? throw new KeyNotFoundException("Conversation not found.");
    private async Task SaveAsync(AssistantConversation entity, AssistantState state, CancellationToken token)
    {
        state.ClinicalReviews = (await workflows.GetHistoryForPatientAsync(entity.PatientId))
            .Where(w => w.ApprovalStatus is TriageApprovalStatuses.Pending or TriageApprovalStatuses.RevisionRequested ||
                w.Status is TriageWorkflowStatuses.PendingPatientInput or TriageWorkflowStatuses.FailedSafely)
            .Select(w => new AssistantClinicalReview(w.WorkflowId, w.Status, w.ApprovalStatus, w.PatientMessage)).ToArray();
        await UpdateExecutionAsync(entity, state, token);
        entity.StateJson = JsonSerializer.Serialize(state, Json); entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(token);
    }
    private AssistantConversationResponse Response(AssistantConversation entity, AssistantState state) => new(
        entity.AssistantConversationId, entity.Title, state.State, entity.UpdatedAt, state.Messages, state.PendingAction,
        state.Questions.Where(q => state.Answers.All(a => a.QuestionId != q.Id)).Take(1).ToArray(),
        state.Appointments, state.Slots, state.Doctors, Capabilities) {
            ExecutionWorkflowId = state.ExecutionWorkflowId,
            ClinicalReviews = state.ClinicalReviews,
            AvailabilityChecked = state.AvailabilityChecked,
            AssessmentInputActive = state.ActiveTask == "triage" && state.Awaiting == "clinical-answer"
        };

    private async Task<T> LockedAsync<T>(int patientId, Func<Task<T>> action, CancellationToken token)
    {
        var gate = Gates[(patientId & int.MaxValue) % Gates.Length];
        await gate.WaitAsync(token);
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(db, async (context, ct) =>
            {
                await using var transaction = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(ct) : null;
                if (context.Database.IsNpgsql())
                    await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1396916552, {patientId})", ct);
                var result = await action();
                if (transaction != null)
                {
                    await transaction.CommitAsync(ct);
                    await sms.FlushCommittedAsync(transaction.TransactionId);
                }
                return result;
            }, token);
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not KeyNotFoundException)
        {
            // Transaction disposal above has rolled back incomplete effects. Persist failure in a fresh transaction.
            if (activeExecution != null)
            {
                db.ChangeTracker.Clear();
                using var failureTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    var record = await _store.GetAsync(activeExecution.WorkflowId, failureTimeout.Token) ?? activeExecution;
                    record.Status = "FailedSafely";
                    record.ErrorCode = ex is OperationCanceledException ? "Cancelled" : ex is DbUpdateException ? "DatabaseFailure" : "ExecutionFailure";
                    record.ErrorSummary = "The operation could not be completed safely.";
                    record.FailedStep = record.CurrentStep ?? "PlanningAndCoordination"; record.FailedAt = DateTimeOffset.UtcNow;
                    record.Errors.Add(record.ErrorCode);
                    record.AuditEvents.Add(new() { EventType = "FailedSafely", Description = record.ErrorCode });
                    await _store.SaveAsync(record, failureTimeout.Token);
                }
                catch (Exception) { _logger.LogError("Execution failure could not be persisted because storage is unavailable."); }
            }
            throw;
        }
        finally { gate.Release(); }
    }
}
