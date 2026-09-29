using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public sealed partial class PlanningCoordinatorAgent : IPlanningCoordinatorAgent
{
    private readonly IPlanningModelClient _modelClient;
    private readonly IPlanningCoordinatorStore _store;
    private readonly ILogger<PlanningCoordinatorAgent> _logger;

    private static readonly string[] PromptInjectionPatterns =
    [
        "ignore previous",
        "ignore all previous",
        "system override",
        "act as admin",
        "bypass safety",
        "bypass confirmation",
        "book immediately without confirmation",
        "reveal prompt",
        "drop table",
        "grant admin",
        "sudo "
    ];

    private static readonly string[] ExplicitBookingKeywords =
    [
        "book",
        "booking",
        "schedule",
        "reschedule",
        "reserve",
        "make an appointment",
        "appointment with",
        "see a doctor",
        "want to see",
        "see an ",
        "consult a doctor",
        "need an appointment"
    ];

    private static readonly string[] SymptomKeywords =
    [
        "cough",
        "fever",
        "pain",
        "headache",
        "ache",
        "dizzy",
        "nausea",
        "vomit",
        "breath",
        "bleeding",
        "chest",
        "rash",
        "cold",
        "flu",
        "sore",
        "swollen"
    ];

    public PlanningCoordinatorAgent(
        IPlanningModelClient modelClient,
        IPlanningCoordinatorStore store,
        ILogger<PlanningCoordinatorAgent> logger)
    {
        _modelClient = modelClient;
        _store = store;
        _logger = logger;
    }

    public async Task<PlanningResponseDto> PlanAsync(PlanningRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var objective = (request.Objective ?? string.Empty).Trim();
        if (objective.Length is < 2 or > 4000)
        {
            throw new ArgumentException("A valid patient objective must be provided.", nameof(request));
        }

        var previous = request.ExistingWorkflowId == null ? null : await _store.GetAsync(request.ExistingWorkflowId, cancellationToken);
        if (previous != null && previous.PatientId != request.PatientId) throw new InvalidOperationException("Workflow ownership mismatch.");
        var workflowRecord = previous ?? new PlanningWorkflowRecord
        {
            PatientId = request.PatientId,
            Objective = objective,
            Status = "Created",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        activeExecution = workflowRecord;
        if (previous != null) { workflowRecord.PreviousPlans.Add(workflowRecord.Plan); workflowRecord.Revision++; workflowRecord.Objective = objective; }
        workflowRecord.AuditEvents.Add(new PlanningAuditEvent
        {
            EventType = previous == null ? "WorkflowInitialized" : "Replanned",
            Description = "Planning workflow record created for patient objective.",
            Metadata = $"PatientId: {request.PatientId}"
        });

        // 1. Prompt injection / Adversarial heuristic check
        if (IsPromptInjection(objective))
        {
            return await HandlePromptInjectionAsync(workflowRecord, request, cancellationToken);
        }

        // 2. Query Gemini structured output model
        GeminiPlanningDecision? rawDecision = null;
        var errors = new List<string>();

        try
        {
            try { rawDecision = await _modelClient.PlanObjectiveAsync(objective, cancellationToken); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or HttpRequestException || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
            {
                workflowRecord.RetryCount++;
                workflowRecord.AuditEvents.Add(new() { EventType = "ModelRetry", Description = "Retrying structured planning once." });
                rawDecision = await _modelClient.PlanObjectiveAsync(objective, cancellationToken);
            }
            workflowRecord.AuditEvents.Add(new PlanningAuditEvent
            {
                EventType = "ModelPlanningCompleted",
                Description = "Gemini model returned structured JSON planning decision.",
                Metadata = $"RawWorkflowType: {rawDecision.WorkflowType}"
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            workflowRecord.Status = "FailedSafely"; workflowRecord.ErrorCode = "Cancelled";
            workflowRecord.ErrorSummary = "Planning was cancelled."; workflowRecord.FailedStep = "PlanningStage";
            workflowRecord.FailedAt = DateTimeOffset.UtcNow;
            using var failureTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _store.SaveAsync(workflowRecord, failureTimeout.Token);
            throw;
        }
        catch (Exception)
        {
            _logger.LogWarning( "Gemini planning client threw an exception for objective. Falling back to deterministic planner.");
            errors.Add("Planning model unavailable; validated fallback used.");
            workflowRecord.AuditEvents.Add(new PlanningAuditEvent
            {
                EventType = "ModelPlanningFailed",
                Description = "Gemini model failed or returned malformed JSON; deterministic safety fallback engaged.",
                Metadata = "ModelUnavailable"
            });
            rawDecision = DeterministicRuleBasedFallback(objective, request, previous);
        }

        // 3. Post-process, sanitize, and strictly enforce hospital safety rules
        var plan = BuildAndSanitizePlan(rawDecision, objective, request, workflowRecord);

        workflowRecord.Plan = plan;
        if (previous != null && previous.Plan?.WorkflowType == plan.WorkflowType && previous.Steps.Count == plan.RequiredSteps.Count)
        {
            workflowRecord.Steps = previous.Steps;
            workflowRecord.CurrentAgent = previous.CurrentAgent;
            workflowRecord.CurrentStep = previous.CurrentStep;
            workflowRecord.CompletedStages = previous.CompletedStages;
        }
        else
        {
            SetSteps(workflowRecord, plan.RequiredSteps);
        }
        workflowRecord.Status = plan.WorkflowType == PlanningWorkflowType.Unsupported.ToString()
            ? "Unsupported"
            : "Planned";

        if (workflowRecord.Status == "Planned")
        {
            workflowRecord.ErrorCode = null; workflowRecord.ErrorSummary = null; workflowRecord.FailedStep = null; workflowRecord.FailedAt = null;
            if (!workflowRecord.CompletedStages.Contains("PlanningStage")) workflowRecord.CompletedStages.Add("PlanningStage");
        }

        workflowRecord.Errors.AddRange(errors);
        if (errors.Count > 0 && workflowRecord.Status == "Unsupported")
        {
            workflowRecord.Status = "FailedSafely"; workflowRecord.ErrorCode = "PlanningUnavailable";
            workflowRecord.ErrorSummary = "A supported plan could not be produced safely.";
            workflowRecord.FailedStep = "PlanningStage"; workflowRecord.FailedAt = DateTimeOffset.UtcNow;
        }
        workflowRecord.UpdatedAt = DateTimeOffset.UtcNow;

        workflowRecord.AuditEvents.Add(new PlanningAuditEvent
        {
            EventType = "PlanFinalized",
            Description = $"Allow-listed plan finalized with workflow type: {plan.WorkflowType}.",
            Metadata = $"AppointmentRequested: {plan.AppointmentRequested}, PatientConfirmationRequired: {plan.PatientConfirmationRequired}"
        });

        await _store.SaveAsync(workflowRecord, cancellationToken);

        return MapToResponseDto(workflowRecord);
    }

    public async Task<PlanningResponseDto?> GetWorkflowStatusAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var record = await _store.GetAsync(workflowId, cancellationToken);
        return record is null ? null : MapToResponseDto(record);
    }

    private static bool IsPromptInjection(string text)
    {
        var lower = text.ToLowerInvariant();
        return PromptInjectionPatterns.Any(pattern => lower.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<PlanningResponseDto> HandlePromptInjectionAsync(
        PlanningWorkflowRecord record,
        PlanningRequestDto request,
        CancellationToken cancellationToken)
    {
        record.Status = "Unsupported";
        record.Errors.Add("Prompt injection or adversarial instruction detected.");
        record.AuditEvents.Add(new PlanningAuditEvent
        {
            EventType = "SecuritySafetyIntervention",
            Description = "Adversarial prompt or policy violation trapped by heuristic filter.",
            Metadata = "Action: Defaulted to Unsupported with safe controlled response."
        });

        record.Plan = new PlanningPlanDto
        {
            WorkflowType = PlanningWorkflowType.Unsupported.ToString(),
            AppointmentRequested = false,
            PatientConfirmationRequired = true,
            RequiredSteps = PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.Unsupported],
            FollowUpQuestions = [],
            Rationale = "Input contained prohibited instructions or security override attempts.",
            SafeResponse = "I can only assist with hospital triage assessments, doctor information, and appointment inquiries. Please specify your health concern or appointment question."
        };

        SetSteps(record, record.Plan.RequiredSteps);
        record.UpdatedAt = DateTimeOffset.UtcNow;
        await _store.SaveAsync(record, cancellationToken);

        return MapToResponseDto(record);
    }

    private PlanningPlanDto BuildAndSanitizePlan(
        GeminiPlanningDecision decision,
        string objective,
        PlanningRequestDto request,
        PlanningWorkflowRecord record)
    {
        // 1. Resolve workflow type to allow-listed enum
        if (!Enum.TryParse<PlanningWorkflowType>(decision.WorkflowType, true, out var workflowType) || !Enum.IsDefined(workflowType))
        {
            workflowType = PlanningWorkflowType.Unsupported;
            record.AuditEvents.Add(new PlanningAuditEvent
            {
                EventType = "WorkflowTypeSanitized",
                Description = $"Unknown workflow '{decision.WorkflowType}' sanitized to Unsupported.",
                Metadata = null
            });
        }

        // 2. ENFORCE CONSENT RULE: Never infer consent to book from symptoms alone.
        var hasExplicitBookingIntent = HasExplicitBookingIntent(objective);
        var hasSymptoms = HasSymptomKeywords(objective);
        var medicalRecordsIntent = IsMedicalRecordsIntent(objective);
        var cancellationIntent = IsAppointmentCancellationIntent(objective);
        var rescheduleIntent = IsAppointmentRescheduleIntent(objective);

        // A definition request is neither a patient symptom report nor a diagnosis
        // request. Keep it out of the clinical workflow even if a model labels the
        // named condition as a triage concept.
        if (IsHealthInformationQuestion(objective) && !medicalRecordsIntent)
        {
            workflowType = PlanningWorkflowType.Unsupported;
            hasSymptoms = false;
            record.AuditEvents.Add(new() { EventType = "InformationalIntentDetected", Description = "Condition-information question excluded from clinical triage.", Metadata = null });
        }

        // Routing is based on the patient's expressed purpose, not specialty/body-part words
        // returned by a model. An appointment-only request never enters triage.
        var isRecordedInquiry = Regex.IsMatch(objective, @"\b(recorded|past|previous|history|in\s+my\s+record|from\s+my\s+record|my\s+diagnosis|what\s+was\s+my|what\s+did\s+the\s+doctor\s+prescribe)\b", RegexOptions.IgnoreCase);
        if (medicalRecordsIntent && (!hasSymptoms || isRecordedInquiry))
        {
            workflowType = PlanningWorkflowType.MedicalRecords;
        }
        else if (rescheduleIntent && !hasSymptoms)
        {
            workflowType = PlanningWorkflowType.AppointmentReschedule;
        }
        else if (cancellationIntent && !hasSymptoms)
        {
            workflowType = PlanningWorkflowType.AppointmentCancellation;
        }
        else if (hasExplicitBookingIntent && !hasSymptoms)
        {
            if (workflowType != PlanningWorkflowType.AppointmentProposal)
                record.AuditEvents.Add(new() { EventType = "IntentRouteCorrected", Description = "Appointment-only request routed to appointment workflow.", Metadata = "No symptoms detected" });
            workflowType = PlanningWorkflowType.AppointmentProposal;
        }
        else if (hasSymptoms)
        {
            workflowType = hasExplicitBookingIntent
                ? PlanningWorkflowType.TriageThenAppointmentProposal
                : PlanningWorkflowType.SafeTriage;
        }

        var appointmentRequested = decision.AppointmentRequested;
        if (hasSymptoms && !hasExplicitBookingIntent)
        {
            // Even if model said true, force false because symptoms alone cannot grant booking consent
            if (appointmentRequested)
            {
                record.AuditEvents.Add(new PlanningAuditEvent
                {
                    EventType = "ConsentRuleEnforced",
                    Description = "Overrode appointmentRequested to false because user only reported symptoms without explicit booking consent.",
                    Metadata = "Rule: Never infer consent to book from symptoms."
                });
            }
            appointmentRequested = false;
        }
        else if (hasExplicitBookingIntent && !cancellationIntent && !rescheduleIntent)
        {
            appointmentRequested = true;
        }

        if (workflowType == PlanningWorkflowType.TriageThenAppointmentProposal && !appointmentRequested) workflowType = PlanningWorkflowType.SafeTriage;

        // 3. ENFORCE PATIENT CONFIRMATION RULE
        // Booking or proposing always requires explicit patient confirmation
        var confirmationRequired = workflowType is PlanningWorkflowType.TriageThenAppointmentProposal or PlanningWorkflowType.AppointmentProposal
            or PlanningWorkflowType.AppointmentCancellation or PlanningWorkflowType.AppointmentReschedule
                                   || decision.PatientConfirmationRequired;

        // 4. Sanitize required steps against predefined allow-list
        var sanitizedSteps = new List<string>();
        if (decision.RequiredSteps is { Length: > 0 })
        {
            foreach (var step in decision.RequiredSteps)
            {
                if (PlanningWorkflowSteps.AllAllowedSteps.Contains(step) && !sanitizedSteps.Contains(step))
                {
                    sanitizedSteps.Add(step);
                }
            }
        }

        if (sanitizedSteps.Count == 0)
        {
            sanitizedSteps.AddRange(PlanningWorkflowSteps.DefaultStepsByWorkflow[workflowType]);
        }

        sanitizedSteps = PlanningWorkflowSteps.DefaultStepsByWorkflow[workflowType].ToList();

        // 5. ENFORCE MAXIMUM 3 FOLLOW-UP QUESTIONS
        // Combine, clean, and take at most 3 questions
        var questionLimit = workflowType is PlanningWorkflowType.SafeTriage or PlanningWorkflowType.TriageThenAppointmentProposal ? 1 : 3;
        var questions = (decision.FollowUpQuestions ?? [])
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q.Trim())
            .Distinct()
            .Take(questionLimit)
            .ToList();

        if ((decision.FollowUpQuestions?.Length ?? 0) > questionLimit)
        {
            record.AuditEvents.Add(new PlanningAuditEvent
            {
                EventType = "QuestionLimitEnforced",
                Description = $"Model proposed {decision.FollowUpQuestions!.Length} questions; bounded to {questionLimit} for this workflow.",
                Metadata = null
            });
        }

        // 6. Dates & Times
        var preferredDate = !string.IsNullOrWhiteSpace(request.PreferredDate)
            ? request.PreferredDate
            : decision.PreferredDate;

        var preferredTime = !string.IsNullOrWhiteSpace(request.PreferredTime)
            ? request.PreferredTime
            : decision.PreferredTime;

        // 7. Safe response
        var safeResponse = !string.IsNullOrWhiteSpace(decision.SafeResponse)
            ? decision.SafeResponse
            : GetDefaultSafeResponse(workflowType);

        return new PlanningPlanDto
        {
            WorkflowType = workflowType.ToString(),
            AppointmentRequested = appointmentRequested,
            PatientConfirmationRequired = confirmationRequired,
            RequiredSteps = sanitizedSteps,
            PreferredDate = preferredDate,
            PreferredTime = preferredTime,
            FollowUpQuestions = questions,
            Rationale = "Validated workflow selection",
            SafeResponse = safeResponse
        };
    }

    private static bool HasExplicitBookingIntent(string text)
    {
        var lower = text.ToLowerInvariant();
        return ExplicitBookingKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasSymptomKeywords(string text)
    {
        var lower = text.ToLowerInvariant();
        return SymptomKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsHealthInformationQuestion(string text) =>
        Regex.IsMatch(text.Trim(), @"^(?:what\s+is|what'?s|tell\s+me\s+about|explain|information\s+about)\s+.+[?!.]*$", RegexOptions.IgnoreCase) &&
        !Regex.IsMatch(text, @"\b(i|my|me)\b.*\b(have|had|feel|felt|was|were|bitten|scratched|exposed|hurt|record|records|report|reports|lab|labs|test|tests|result|results|finding|findings|diagnosis|prescription|prescriptions|medicine|medicines|medication|medications|doctor|symptom|symptoms|cholesterol)\b", RegexOptions.IgnoreCase);

    private static bool IsMedicalRecordsIntent(string text) => Regex.IsMatch(text,
        @"\b(my|latest|last|previous)\b.*\b(medical records?|medical reports?|recorded diagnosis|prescriptions?|prescribed|medications?|medicines?|lab results?|test results?|lab\s*findings?|cholesterol|follow-up instructions?|visit history|typed\s*(medical\s*)?data|treatment\s*plan|symptoms|recorded\s+symptoms?|attachments?)\b|\b(summarize|show|explain|get|view|tell me about|details? of)\b.*\b(my (?:medical )?records?|my (?:medical )?reports?|my prescriptions?|my lab results?|my\s+.*lab|my\s+.*cholesterol|medical records?|treatment plan|typed data|uploaded (?:file|files|document|documents)|attachments?)\b|\b(what\s+is\s+in\s+my\s+medical\s+record|medical\s+record\s+details?|what\s+was\s+my\s+diagnosis|what\s+are\s+my\s+recorded\s+symptoms|what\s+did\s+the\s+doctor\s+prescribe)\b|\b(diagnosis|prescriptions?|medications?|prescribed)\b.*\b(doctor|record|records|visit)\b",
        RegexOptions.IgnoreCase);

    private static bool IsAppointmentCancellationIntent(string text) =>
        Regex.IsMatch(text, @"\b(cancel|cancellation)\b.*\b(appointment|booking|reservation)\b|\b(cancel)\s+(it|mine)\b", RegexOptions.IgnoreCase);

    private static bool IsAppointmentRescheduleIntent(string text) =>
        Regex.IsMatch(text, @"\b(reschedule|move|change)\b.*\b(appointment|booking|visit|date|time)\b", RegexOptions.IgnoreCase);

    internal static string HealthInformationReply(string text)
    {
        return "I could not generate the requested general health information right now. Please try again shortly. If this relates to symptoms, an injury, or a possible exposure affecting you, describe what happened and when so the safety-triage workflow can assess it.";
    }

    private static GeminiPlanningDecision DeterministicRuleBasedFallback(string objective, PlanningRequestDto request, PlanningWorkflowRecord? previous = null)
    {
        var lower = objective.ToLowerInvariant();

        var medicalRecords = IsMedicalRecordsIntent(objective);
        var cancellation = IsAppointmentCancellationIntent(objective);
        var reschedule = IsAppointmentRescheduleIntent(objective);

        if (medicalRecords)
        {
            return Decision(PlanningWorkflowType.MedicalRecords, false, false,
                "User requested read-only access to verified medical records.",
                "I can help explain your finalized medical records.");
        }
        if (reschedule)
        {
            return Decision(PlanningWorkflowType.AppointmentReschedule, false, true,
                "User requested an appointment reschedule.",
                "I will verify your appointment and show replacement sessions for your confirmation.");
        }
        if (cancellation)
        {
            return Decision(PlanningWorkflowType.AppointmentCancellation, false, true,
                "User requested appointment cancellation.",
                "I will verify your appointment and ask for confirmation before cancellation.");
        }

        if (lower.Contains("status") || lower.Contains("my appointment") || lower.Contains("when is my"))
        {
            return new GeminiPlanningDecision
            {
                WorkflowType = PlanningWorkflowType.AppointmentStatus.ToString(),
                AppointmentRequested = false,
                PatientConfirmationRequired = false,
                RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.AppointmentStatus]],
                FollowUpQuestions = [],
                Rationale = "User requested appointment status lookup.",
                SafeResponse = "I will check your current appointment status."
            };
        }

        var hasSymptoms = HasSymptomKeywords(objective);
        var hasBooking = HasExplicitBookingIntent(objective);

        if (hasSymptoms)
        {
            return new GeminiPlanningDecision
            {
                WorkflowType = PlanningWorkflowType.TriageThenAppointmentProposal.ToString(),
                AppointmentRequested = hasBooking,
                PatientConfirmationRequired = true,
                RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.TriageThenAppointmentProposal]],
                FollowUpQuestions = ["How long have you experienced these symptoms?"],
                Rationale = "Clinical symptoms detected requiring safety triage assessment.",
                SafeResponse = "Your symptoms will be evaluated through our clinical safety triage protocol."
            };
        }

        if (hasBooking)
        {
            return new GeminiPlanningDecision
            {
                WorkflowType = PlanningWorkflowType.AppointmentProposal.ToString(),
                AppointmentRequested = true,
                PatientConfirmationRequired = true,
                RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.AppointmentProposal]],
                FollowUpQuestions = ["Do you have a preferred doctor or medical specialty?"],
                Rationale = "User requested to book or explore appointment options.",
                SafeResponse = "I can help search for available doctors and appointment slots."
            };
        }

        // Maintain active in-flight workflow when the patient provides a follow-up answer or clinical choice
        if (previous != null && Enum.TryParse<PlanningWorkflowType>(previous.Plan?.WorkflowType, out var prevType) &&
            prevType != PlanningWorkflowType.Unsupported &&
            previous.Status is not ("Completed" or "Cancelled" or "FailedSafely"))
        {
            return new GeminiPlanningDecision
            {
                WorkflowType = prevType.ToString(),
                AppointmentRequested = previous.Plan.AppointmentRequested,
                PatientConfirmationRequired = previous.Plan.PatientConfirmationRequired,
                RequiredSteps = [.. previous.Plan.RequiredSteps],
                FollowUpQuestions = [],
                Rationale = $"Continuing active in-flight workflow: {prevType}.",
                SafeResponse = previous.Plan.SafeResponse
            };
        }

        return new GeminiPlanningDecision
        {
            WorkflowType = PlanningWorkflowType.Unsupported.ToString(),
            AppointmentRequested = false,
            PatientConfirmationRequired = true,
            RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.Unsupported]],
            FollowUpQuestions = [],
            Rationale = "Request does not match supported triage or appointment workflows.",
            SafeResponse = "I can assist with symptom triage assessments, finding doctors, and checking appointments. Please let me know how I can help with your care."
        };

        static GeminiPlanningDecision Decision(PlanningWorkflowType type, bool requested, bool confirmation, string rationale, string response) => new()
        {
            WorkflowType = type.ToString(), AppointmentRequested = requested,
            PatientConfirmationRequired = confirmation,
            RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[type]],
            FollowUpQuestions = [], Rationale = rationale, SafeResponse = response
        };
    }

    private static string GetDefaultSafeResponse(PlanningWorkflowType workflowType) => workflowType switch
    {
        PlanningWorkflowType.TriageThenAppointmentProposal => "We will guide your symptoms through the clinical safety triage process before evaluating appointment options.",
        PlanningWorkflowType.AppointmentProposal => "We will search for eligible doctors and available appointment slots based on your preferences.",
        PlanningWorkflowType.AppointmentStatus => "We will check your current appointment records.",
        PlanningWorkflowType.AppointmentCancellation => "We will verify the appointment and require your confirmation before cancelling it.",
        PlanningWorkflowType.AppointmentReschedule => "We will verify the appointment and show current replacement sessions for your confirmation.",
        PlanningWorkflowType.MedicalRecords => "We will retrieve and explain only your finalized medical records.",
        _ => "I can assist with symptom triage, medical records, doctors, and appointment inquiries."
    };

    private static PlanningResponseDto MapToResponseDto(PlanningWorkflowRecord record)
    {
        return new PlanningResponseDto(
            record.WorkflowId,
            record.Status,
            record.Objective,
            record.Plan,
            record.CompletedStages,
            record.Errors,
            record.AuditEvents.Select(a => new PlanningAuditEventDto(a.Timestamp, a.EventType, a.Description, a.Metadata)).ToList(),
            record.CreatedAt);
    }
}
