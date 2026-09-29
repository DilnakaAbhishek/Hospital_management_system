using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

namespace HospitalManagementSystem.Api.AgenticAI.SafeTriage;

/// <summary>The response state of an individual SafeTriage information requirement.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SafeTriageRequirementState>))]
public enum SafeTriageRequirementState
{
    /// <summary>No response has been provided.</summary>
    Missing = 0,
    /// <summary>The requested information has been provided.</summary>
    Answered = 1,
    /// <summary>The patient explicitly chose not to provide the information.</summary>
    Declined = 2,
    /// <summary>The patient explicitly indicated that they do not know the answer.</summary>
    Unknown = 3,
    /// <summary>The requirement does not apply to this patient or situation.</summary>
    NotApplicable = 4
}

/// <summary>
/// Tracks an information requirement independently of whether a question has been asked.
/// Declined, unknown, and inapplicable responses are distinct from answered information.
/// </summary>
public sealed record SafeTriageRequirement(
    string Key,
    SafeTriageRequirementState State = SafeTriageRequirementState.Missing,
    string? Value = null,
    DateTime? UpdatedAt = null,
    string? Evidence = null);

public sealed class SafeTriageOptions
{
    public const int MinimumFollowUpQuestions = 3;
    public const int HardFollowUpLimit = 5;
    public int MaxFollowUpQuestions { get; set; } = HardFollowUpLimit;
    public int EffectiveMaxFollowUpQuestions => Math.Clamp(MaxFollowUpQuestions, MinimumFollowUpQuestions, HardFollowUpLimit);
}

public static class SafeTriageRequirementRules
{
    // Canonical field identifiers are shared by extraction, persisted state and planning.
    public static string CanonicalKey(string key)
    {
        var normalized = System.Text.RegularExpressions.Regex.Replace(key.Trim(), "([a-z])([A-Z])", "$1_$2").ToLowerInvariant();
        return normalized switch { "severity" => "severity_score", "trend" => "progression", _ => normalized };
    }

    public static void Merge(List<SafeTriageRequirement> state, IEnumerable<SafeTriageRequirement> updates)
    {
        foreach (var update in updates)
        {
            var key = CanonicalKey(update.Key);
            if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z][a-z0-9_]{1,79}$") || !Enum.IsDefined(update.State)) continue;
            if (update.State == SafeTriageRequirementState.Answered && string.IsNullOrWhiteSpace(update.Value)) continue;
            var index = state.FindIndex(item => item.Key == key);
            if (index >= 0 && update.State == SafeTriageRequirementState.Missing) continue;
            var merged = update with { Key = key, Value = update.State == SafeTriageRequirementState.Answered ? update.Value : null,
                UpdatedAt = update.UpdatedAt ?? DateTime.UtcNow };
            if (index < 0) state.Add(merged); else state[index] = merged;
        }
    }

    public static SafeTriageRequirementState? UnavailableResponse(string text) => text.Trim().TrimEnd('.', '!', '?').ToLowerInvariant().Replace('’', '\'') switch
    {
        "i'd rather not answer" or "prefer not to answer" or "i prefer not to answer" or "i don't want to answer" or "i do not want to answer" => SafeTriageRequirementState.Declined,
        "i don't know" or "i do not know" or "not sure" or "i'm not sure" => SafeTriageRequirementState.Unknown,
        "that doesn't apply to me" or "that does not apply to me" or "not applicable" => SafeTriageRequirementState.NotApplicable,
        _ => null
    };

    public static ClinicalFactSet? MergeFacts(ClinicalFactSet? previous, ClinicalFactSet? incoming)
    {
        if (incoming is null) return previous;
        var merged = JsonSerializer.SerializeToNode(previous ?? new ClinicalFactSet())!.AsObject();
        var next = JsonSerializer.SerializeToNode(incoming)!.AsObject();
        foreach (var field in next.Where(p => p.Key != nameof(ClinicalFactSet.Evidence)))
        {
            if (!incoming.Evidence.Any(e => CanonicalKey(e.Field) == CanonicalKey(field.Key))) continue;
            if (field.Value is System.Text.Json.Nodes.JsonArray array)
            {
                var old = merged[field.Key]?.Deserialize<List<string>>() ?? [];
                var grounded = (array.Deserialize<List<string>>() ?? []).Where(value => incoming.Evidence.Any(e =>
                    CanonicalKey(e.Field) == CanonicalKey(field.Key) && string.Equals(e.Value, value, StringComparison.OrdinalIgnoreCase)));
                merged[field.Key] = JsonSerializer.SerializeToNode(old.Concat(grounded).Distinct(StringComparer.OrdinalIgnoreCase));
            }
            else if (field.Value is not null) merged[field.Key] = field.Value.DeepClone();
        }
        merged[nameof(ClinicalFactSet.Evidence)] = JsonSerializer.SerializeToNode((previous?.Evidence ?? []).Concat(incoming.Evidence).Distinct());
        return merged.Deserialize<ClinicalFactSet>();
    }

    public static ClinicalFactSet? FactsFromCurrentAnswer(ClinicalFactSet? facts, string? currentAnswerText)
    {
        if (facts is null || currentAnswerText is null) return facts;
        var node = JsonSerializer.SerializeToNode(facts)!.AsObject();
        node[nameof(ClinicalFactSet.Evidence)] = JsonSerializer.SerializeToNode(facts.Evidence.Where(e =>
            !string.IsNullOrWhiteSpace(e.Quote) && currentAnswerText.Contains(e.Quote, StringComparison.OrdinalIgnoreCase)));
        return node.Deserialize<ClinicalFactSet>();
    }
}

/// <summary>
/// Typed, least-privilege contracts for the SafeTriage workflow. Each agent has one purpose and
/// declares the single tool (if any) it may use. The coordinator, not the model, owns execution order.
/// </summary>
public interface ISafeTriageAgent
{
    string Name { get; }
    string ToolName { get; }
    Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default);
}

public sealed class SafeTriageAgentContext
{
    public SafeTriageAgentContext(StartTriageWorkflowDto request)
    {
        Request = request;
        Symptoms = request.Symptoms.Trim();
    }

    public StartTriageWorkflowDto Request { get; }
    public string Symptoms { get; }
    public List<string> ValidationProblems { get; } = [];
    public List<string> RedFlags { get; } = [];
    public List<string> UrgentFlags { get; } = [];
    public List<string> ClinicalReviewFlags { get; } = [];
    public List<string> PlannedInformationNeeds { get; } = [];
    public List<SafeTriageRequirement> Requirements { get; } = [];
    public List<TriageFollowUpQuestionDto> PlannedQuestions { get; } = [];
    public ClinicalExtractionResult? Extraction { get; set; }
    public ClinicalFactSet? PreviousFacts { get; set; }
    public string? CurrentAnswerText { get; set; }
    public int FollowUpCount { get; set; }
    public int MaxFollowUpQuestions { get; set; } = SafeTriageOptions.HardFollowUpLimit;
    public string? ProposedRoute { get; set; }
    public bool IsWithinValidatedRoutineScope { get; set; }
    public bool RequiresClinicalApproval { get; set; }
    public bool FailedSafely { get; set; }
}

public sealed record SafeTriageAgentExecution(
    string Agent, string Tool, string Status, bool ValidationPassed,
    string Outcome, int DurationMs, int RetryCount = 0, string? ErrorCode = null);

public sealed class IntakeValidationAgent : ISafeTriageAgent
{
    public string Name => nameof(IntakeValidationAgent);
    public string ToolName => "ValidateVitalsTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        context.ValidationProblems.AddRange(SafeTriageRules.Validate(context.Request));
        context.FailedSafely = context.ValidationProblems.Count > 0;
        watch.Stop();
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName,
            context.FailedSafely ? "FailedSafely" : "Completed", !context.FailedSafely,
            context.FailedSafely ? "Input rejected by deterministic vital-sign validation." : "Patient input passed deterministic validation.",
            (int)watch.ElapsedMilliseconds, 0, context.FailedSafely ? "InvalidOrSuspiciousInput" : null));
    }
}

public sealed class SafetyRedFlagAgent : ISafeTriageAgent
{
    public string Name => nameof(SafetyRedFlagAgent);
    public string ToolName => "EvaluateRedFlagsTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        context.RedFlags.AddRange(SafeTriageRules.FindEmergencyFlags(context.Symptoms));
        context.UrgentFlags.AddRange(SafeTriageRules.FindUrgentFlags(context.Symptoms));
        context.ClinicalReviewFlags.AddRange(SafeTriageRules.FindClinicalReviewFlags(context.Symptoms));
        context.IsWithinValidatedRoutineScope = SafeTriageRules.IsWithinValidatedRoutineScope(context.Symptoms);
        context.RequiresClinicalApproval = context.RedFlags.Count > 0 || context.UrgentFlags.Count > 0 || context.ClinicalReviewFlags.Count > 0;
        watch.Stop();
        var status = context.RedFlags.Count > 0 ? "EmergencyEscalation"
            : context.UrgentFlags.Count > 0 ? "UrgentAssessment"
            : context.ClinicalReviewFlags.Count > 0 ? "ClinicalReviewRequired"
            : "Completed";
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, status, true,
            context.RequiresClinicalApproval ? "Configured safety rule requires clinical review." : "No configured escalation rule matched.",
            (int)watch.ElapsedMilliseconds));
    }
}

public sealed class ClinicalInformationExtractionWorkflowAgent(ISafeTriageSemanticExtractionAgent extractionTool) : ISafeTriageAgent
{
    public string Name => "ClinicalInformationExtractionAgent";
    public string ToolName => "GeminiStructuredExtractionTool";

    public async Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        const int maxAttempts = 2;
        var attempts = 0;
        do
        {
            attempts++;
            context.Extraction = await extractionTool.ExtractWithRequirementsAsync(context.Symptoms, context.Request.IsFollowUp, context.Requirements, cancellationToken);
        } while (context.Extraction.Status != "Completed" && attempts < maxAttempts && !cancellationToken.IsCancellationRequested);
        watch.Stop();
        var completed = context.Extraction.Status == "Completed";
        if (!completed) context.FailedSafely = true;
        if (completed)
        {
            var updates = context.Extraction.Requirements ?? [];
            // A historical quote must not undo a more recent answer or explicit decline.
            SafeTriageRequirementRules.Merge(context.Requirements, updates.Where(item => item.State == SafeTriageRequirementState.Missing ||
                context.CurrentAnswerText is null || (!string.IsNullOrWhiteSpace(item.Evidence) && context.CurrentAnswerText.Contains(item.Evidence, StringComparison.OrdinalIgnoreCase))));
        }
        context.Extraction = context.Extraction with { Facts = SafeTriageRequirementRules.MergeFacts(context.PreviousFacts,
            SafeTriageRequirementRules.FactsFromCurrentAnswer(context.Extraction.Facts, context.CurrentAnswerText)), Requirements = context.Requirements.ToArray() };
        return new SafeTriageAgentExecution(Name, ToolName, context.Extraction.Status, completed,
            completed ? "Structured non-diagnostic extraction passed schema and safety validation." : "Structured extraction was unavailable; safe fallback required.",
            (int)watch.ElapsedMilliseconds, attempts - 1, context.Extraction.ErrorCode);
    }
}

public sealed class CareRoutingAgent : ISafeTriageAgent
{
    public string Name => nameof(CareRoutingAgent);
    public string ToolName => "CreateEscalationProposalTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        context.ProposedRoute = context.RedFlags.Count > 0 ? "Emergency Department"
            : context.UrgentFlags.Count > 0 ? "Urgent medical assessment"
            : context.ClinicalReviewFlags.Count > 0 ? "Professional clinical review"
            : context.IsWithinValidatedRoutineScope ? "Routine information with safety-netting"
            : context.Request.IsFollowUp ? "Professional clinical review"
            : "Clarification required";
        context.RequiresClinicalApproval |= context.RedFlags.Count > 0 || context.UrgentFlags.Count > 0 ||
                                            context.ClinicalReviewFlags.Count > 0 ||
                                            (!context.IsWithinValidatedRoutineScope && context.Request.IsFollowUp);
        watch.Stop();
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, "Proposed", true,
            $"Controlled route proposed: {context.ProposedRoute}.", (int)watch.ElapsedMilliseconds));
    }
}

public sealed class AdaptiveQuestionPlanningAgent(ISafeTriageQuestionPlanningAgent planner) : ISafeTriageAgent
{
    public string Name => nameof(AdaptiveQuestionPlanningAgent);
    public string ToolName => "RankMissingInformationTool";

    public async Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var missing = context.Requirements.Where(r => r.State == SafeTriageRequirementState.Missing).Select(r => r.Key).ToHashSet();
        var excluded = context.Requirements.Where(r => r.State != SafeTriageRequirementState.Missing).Select(r => r.Key).ToArray();
        var plan = missing.Count == 0 || context.FollowUpCount >= context.MaxFollowUpQuestions
            ? new SafeTriageQuestionPlan([], "Completed")
            : await planner.PlanAsync(context.Extraction ?? new ClinicalExtractionResult([], [], null, "FailedSafely"), excluded, cancellationToken);
        context.PlannedQuestions.AddRange(plan.Questions.Where(q => missing.Contains(SafeTriageRequirementRules.CanonicalKey(q.Id)))
            .Take(Math.Min(1, Math.Max(0, context.MaxFollowUpQuestions - context.FollowUpCount)))
            .Select(q => { q.Id = SafeTriageRequirementRules.CanonicalKey(q.Id); return q; }));
        context.PlannedInformationNeeds.AddRange(context.PlannedQuestions.Select(item => item.Prompt));
        watch.Stop();
        return new SafeTriageAgentExecution(Name, ToolName, plan.Status, plan.Status == "Completed",
            plan.Status == "Completed" ? $"Gemini planned {context.PlannedInformationNeeds.Count} validated follow-up questions." : "Question planning failed safely.",
            (int)watch.ElapsedMilliseconds, 0, plan.ErrorCode);
    }
}

public sealed class StructuredSafetyAssessmentAgent : ISafeTriageAgent
{
    public string Name => nameof(StructuredSafetyAssessmentAgent);
    public string ToolName => "EvaluateGroundedClinicalFactsTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var facts = context.Extraction?.Facts;
        if (facts is not null)
        {
            context.RedFlags.AddRange(SafeTriageRules.FindEmergencyFlags(facts));
            context.UrgentFlags.AddRange(SafeTriageRules.FindUrgentFlags(facts));
            context.ClinicalReviewFlags.AddRange(SafeTriageRules.FindClinicalReviewFlags(facts));
            if (SafeTriageRules.HasGroundedConcept(facts)) context.IsWithinValidatedRoutineScope = true;
        }
        Deduplicate(context.RedFlags);
        Deduplicate(context.UrgentFlags);
        Deduplicate(context.ClinicalReviewFlags);
        context.RequiresClinicalApproval = context.RedFlags.Count > 0 || context.UrgentFlags.Count > 0 || context.ClinicalReviewFlags.Count > 0;
        watch.Stop();
        var status = context.RedFlags.Count > 0 ? "EmergencyEscalation"
            : context.UrgentFlags.Count > 0 ? "UrgentAssessment"
            : context.ClinicalReviewFlags.Count > 0 ? "ClinicalReviewRequired"
            : "Completed";
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, status, true,
            facts is null ? "No structured facts were available; raw-text safety results were retained."
                : "Grounded structured clinical facts were evaluated by deterministic safety policy.",
            (int)watch.ElapsedMilliseconds));
    }

    private static void Deduplicate(List<string> values)
    {
        var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        values.Clear();
        values.AddRange(distinct);
    }
}

public sealed class SafetyValidationAgent : ISafeTriageAgent
{
    public string Name => nameof(SafetyValidationAgent);
    public string ToolName => "ValidateWorkflowOutcomeTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var valid = !string.IsNullOrWhiteSpace(context.ProposedRoute) &&
                    (!context.RequiresClinicalApproval || context.RedFlags.Count > 0 || context.UrgentFlags.Count > 0 ||
                     context.ClinicalReviewFlags.Count > 0 || context.Extraction?.Status != "Completed" ||
                     !context.IsWithinValidatedRoutineScope);
        if (!valid) context.FailedSafely = true;
        watch.Stop();
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, valid ? "Accepted" : "FailedSafely", valid,
            valid ? "Deterministic workflow validation accepted the controlled outcome." : "Workflow outcome failed deterministic validation.",
            (int)watch.ElapsedMilliseconds, 0, valid ? null : "WorkflowValidationFailed"));
    }
}

/// <summary>Four-stage runtime wrapper for validation and immediate deterministic safety screening.</summary>
public sealed class IntakeAndInitialSafetyAgent : ISafeTriageAgent
{
    private readonly IntakeValidationAgent intake = new();
    private readonly SafetyRedFlagAgent redFlags = new();
    public string Name => nameof(IntakeAndInitialSafetyAgent);
    public string ToolName => "ValidateInputAndEvaluateRedFlagsTool";

    public async Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var validation = await intake.ExecuteAsync(context, cancellationToken);
        if (context.FailedSafely) return validation with { Agent = Name, Tool = ToolName };
        var safety = await redFlags.ExecuteAsync(context, cancellationToken);
        return safety with { Agent = Name, Tool = ToolName, Outcome = $"{validation.Outcome} {safety.Outcome}", DurationMs = validation.DurationMs + safety.DurationMs };
    }
}

/// <summary>Four-stage runtime wrapper for Gemini fact extraction.</summary>
public sealed class ClinicalUnderstandingAgent(ISafeTriageSemanticExtractionAgent extractionTool) : ISafeTriageAgent
{
    private readonly ClinicalInformationExtractionWorkflowAgent extraction = new(extractionTool);
    public string Name => nameof(ClinicalUnderstandingAgent);
    public string ToolName => "GeminiStructuredExtractionTool";
    public async Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default) =>
        (await extraction.ExecuteAsync(context, cancellationToken)) with { Agent = Name, Tool = ToolName };
}

/// <summary>Four-stage runtime wrapper for grounded safety assessment, one follow-up question, and routing.</summary>
public sealed class SafetyRoutingAgent(ISafeTriageQuestionPlanningAgent planner) : ISafeTriageAgent
{
    private readonly StructuredSafetyAssessmentAgent structuredSafety = new();
    private readonly AdaptiveQuestionPlanningAgent questionPlanning = new(planner);
    private readonly CareRoutingAgent routing = new();
    public string Name => nameof(SafetyRoutingAgent);
    public string ToolName => "EvaluateFactsPlanQuestionAndRouteTool";

    public async Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var assessment = await structuredSafety.ExecuteAsync(context, cancellationToken);
        SafeTriageAgentExecution? questions = null;
        if (!context.RequiresClinicalApproval) questions = await questionPlanning.ExecuteAsync(context, cancellationToken);
        var route = await routing.ExecuteAsync(context, cancellationToken);
        return route with
        {
            Agent = Name,
            Tool = ToolName,
            ValidationPassed = assessment.ValidationPassed && (questions?.ValidationPassed ?? true) && route.ValidationPassed,
            Outcome = questions is null ? $"{assessment.Outcome} {route.Outcome}" : $"{assessment.Outcome} {questions.Outcome} {route.Outcome}",
            DurationMs = assessment.DurationMs + (questions?.DurationMs ?? 0) + route.DurationMs,
            RetryCount = Math.Max(assessment.RetryCount, Math.Max(questions?.RetryCount ?? 0, route.RetryCount)),
            ErrorCode = assessment.ErrorCode ?? questions?.ErrorCode ?? route.ErrorCode
        };
    }
}

/// <summary>Final deterministic validation. Patient-facing wording is generated only after this stage accepts the route.</summary>
public sealed class GuidanceValidationAgent : ISafeTriageAgent
{
    private readonly SafetyValidationAgent validation = new();
    public string Name => nameof(GuidanceValidationAgent);
    public string ToolName => "ValidateOutcomeBeforeGuidanceTool";
    public async Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default) =>
        (await validation.ExecuteAsync(context, cancellationToken)) with { Agent = Name, Tool = ToolName };
}

public sealed class SafeTriageWorkflowCoordinator
{
    private const int MaxSteps = 4;
    private readonly IntakeAndInitialSafetyAgent _intakeSafety = new();
    private readonly ClinicalUnderstandingAgent _clinicalUnderstanding;
    private readonly SafetyRoutingAgent _safetyRouting;
    private readonly GuidanceValidationAgent _guidanceValidation = new();

    public SafeTriageWorkflowCoordinator(ISafeTriageSemanticExtractionAgent extractionTool, ISafeTriageQuestionPlanningAgent questionPlanner)
    {
        _clinicalUnderstanding = new(extractionTool);
        _safetyRouting = new(questionPlanner);
    }

    public async Task<(SafeTriageAgentContext Context, IReadOnlyList<SafeTriageAgentExecution> Trace)> RunAsync(StartTriageWorkflowDto request, CancellationToken cancellationToken = default,
        IReadOnlyList<SafeTriageRequirement>? requirements = null, ClinicalFactSet? previousFacts = null, string? currentAnswerText = null,
        int followUpCount = 0, int maxFollowUpQuestions = SafeTriageOptions.HardFollowUpLimit, PlanningWorkflowRecord? execution = null,
        Func<SafeTriageAgentContext, Task>? persistExecution = null, Action<SafeTriageAgentContext>? restoreSafety = null)
    {
        if (execution is not null)
            execution.AuditEvents.Add(new() { EventType = "AgentDispatched", Description = "Clinical SafeTriage", Metadata = "Persisted plan execution" });
        var context = new SafeTriageAgentContext(request);
        context.Requirements.AddRange(requirements ?? []);
        context.PreviousFacts = previousFacts;
        if (previousFacts is not null)
            context.Extraction = new ClinicalExtractionResult([], [], null, "NotRun", Facts: previousFacts, Requirements: context.Requirements);
        context.CurrentAnswerText = currentAnswerText;
        context.FollowUpCount = followUpCount;
        context.MaxFollowUpQuestions = maxFollowUpQuestions;
        restoreSafety?.Invoke(context);
        var trace = new List<SafeTriageAgentExecution>();
        async Task Run(ISafeTriageAgent agent)
        {
            if (trace.Count >= MaxSteps) throw new InvalidOperationException("SafeTriage workflow reached its hard execution-step limit.");
            trace.Add(await agent.ExecuteAsync(context, cancellationToken));
        }

        if (execution is null)
        {
            await Run(_intakeSafety); if (context.FailedSafely) return (context, trace);
            if (!context.RequiresClinicalApproval) await Run(_clinicalUnderstanding);
            await Run(_safetyRouting); await Run(_guidanceValidation);
            return (context, trace);
        }

        while (true)
        {
            var step = execution.Steps.FirstOrDefault(candidate => candidate.Status == "Pending" &&
                candidate.AssignedAgent == "Clinical SafeTriage" && candidate.StepType is PlanningWorkflowSteps.SafetyCheck or PlanningWorkflowSteps.SymptomExtraction or PlanningWorkflowSteps.TriageAssessment &&
                candidate.Dependencies.All(id => execution.Steps.Any(done => done.StepId == id && done.Status == "Completed")));
            if (step is null) break;
            step.StartedAt = DateTimeOffset.UtcNow; step.Status = "Running";
            var before = trace.Count;
            var waitingForPatient = false;
            switch (step.StepType)
            {
                case PlanningWorkflowSteps.SafetyCheck:
                    await Run(_intakeSafety);
                    break;
                case PlanningWorkflowSteps.SymptomExtraction:
                    if (!context.RequiresClinicalApproval) await Run(_clinicalUnderstanding);
                    break;
                case PlanningWorkflowSteps.TriageAssessment:
                    await Run(_safetyRouting);
                    waitingForPatient = !context.RequiresClinicalApproval && context.PlannedQuestions.Count > 0;
                    if (!waitingForPatient) await Run(_guidanceValidation);
                    break;
                default:
                    step.Status = "Failed"; step.ValidationStatus = "Failed"; step.Error = "UnapprovedStep";
                    execution.Status = "FailedSafely"; execution.ErrorCode = "UnapprovedStep";
                    await (persistExecution?.Invoke(context) ?? Task.CompletedTask);
                    return (context, trace);
            }
            // Finish the same controlled safety outcome as the legacy pipeline at
            // the detecting step, without advancing any downstream plan dependency.
            if (context.RequiresClinicalApproval && !context.FailedSafely && step.StepType != PlanningWorkflowSteps.TriageAssessment)
            {
                await Run(_safetyRouting);
                await Run(_guidanceValidation);
            }
            var completed = trace.Skip(before).ToArray();
            var valid = completed.All(item => item.ValidationPassed);
            var result = completed.LastOrDefault() ?? new SafeTriageAgentExecution(step.StepType, "", "FailedSafely", false, "No agent result.", 0, ErrorCode: "NoAgentResult");
            step.EndedAt = DateTimeOffset.UtcNow; step.OutputSummary = result.Outcome;
            step.ValidationStatus = valid ? "Passed" : "Failed"; step.Error = valid ? null : result.ErrorCode;
            step.Status = context.RequiresClinicalApproval || context.FailedSafely ? "WaitingForClinicalReview"
                : !valid ? "Failed" : waitingForPatient ? "WaitingForPatient" : "Completed";
            if (context.RequiresClinicalApproval || context.FailedSafely) execution.Status = "AwaitingClinicalReview";
            execution.CurrentStep = step.StepId; execution.CurrentAgent = step.AssignedAgent;
            execution.AuditEvents.Add(new() { EventType = "SafeTriageAgentExecuted", Description = step.AssignedAgent, Metadata = result.Status });
            await (persistExecution?.Invoke(context) ?? Task.CompletedTask);
            if (!valid || waitingForPatient || context.FailedSafely || context.RequiresClinicalApproval) break;
        }
        return (context, trace);
    }
}

internal static partial class SafeTriageRules
{
    private static readonly string[] EmergencyPhrases =
    [
        "severe chest pain", "difficulty breathing", "severe breathing difficulty", "cannot breathe", "choking",
        "loss of consciousness", "unconscious", "signs of stroke", "face drooping", "one-sided weakness",
        "severe bleeding", "coughing up blood", "vomiting blood", "seizure", "severe allergic reaction",
        "severe confusion", "blue lips", "suicidal thoughts", "kill myself", "overdose", "poisoning",
        "lost an eye", "lose an eye", "loose an eye", "loose eye", "dislodged eye", "eye came out", "eye is out", "eye missing"
    ];

    private static readonly string[] UrgentPhrases =
    [
        "chest pain", "chest discomfort", "sudden severe pain", "high fever", "persistent vomiting",
        "unable to keep fluids down", "signs of dehydration"
    ];

    private static readonly string[] SeriousContextPhrases =
    [
        "cancer", "tumour", "tumor", "chemotherapy", "chemo", "immunotherapy", "radiotherapy", "radiation therapy",
        "organ transplant", "immunosuppressed", "weakened immune system", "pregnant", "pregnancy", "postpartum",
        "recent surgery", "blood-thinning medicine", "bleeding or clotting condition", "recurrent nosebleeds"
    ];

    private static readonly string[] CancerTreatmentPhrases = ["chemotherapy", "chemo", "immunotherapy", "radiotherapy", "radiation therapy"];
    private static readonly string[] TreatmentWarningPhrases =
    [
        "fever", "high temperature", "shivering", "chills", "feel very unwell", "feeling very unwell",
        "signs of infection", "unusual bleeding", "persistent diarrhea", "persistent diarrhoea"
    ];

    private static readonly string[] ValidatedRoutinePhrases =
    [
        "runny nose", "blocked nose", "nasal congestion", "sneezing", "sneeze", "nosebleed", "nose bleed",
        "headache", "migraine", "head pain", "dizzy", "dizziness", "lightheaded",
        "cough", "sore throat", "throat pain", "cold", "flu", "fever", "chills", "body ache", "fatigue", "tiredness",
        "stomach", "abdominal", "nausea", "vomiting", "diarrhea", "diarrhoea", "indigestion", "heartburn", "cramps",
        "back pain", "joint pain", "muscle pain", "leg pain", "arm pain", "sprain",
        "rash", "itching", "hives", "skin", "burn", "bite",
        "earache", "ear pain", "toothache", "eye", "red eye", "pink eye", "pain", "unwell", "sick", "feeling",
        // Supported symptom concepts only. Named diseases outside these pathways are
        // handled as deterministic out-of-scope requests, never as a review queue fallback.
        "covid", "corona", "influenza", "sinusitis", "gastroenteritis",
        "urinary tract", "uti", "kidney infection", "bladder infection",
        // Common chronic conditions patients reference
        "diabetes", "diabetic", "hypertension", "blood pressure", "asthma", "allergy", "allergic",
        "migraine", "anxiety", "infection", "inflammation", "swelling", "swollen",
        // General illness/symptom words patients use
        "ill", "ill health", "weakness", "weak", "tired", "lethargic", "losing appetite", "appetite loss",
        "not eating", "loss of taste", "loss of smell", "dehydrated", "dehydration",
        // Injury and wound terms
        "wound", "cut", "bruise", "bleeding", "fracture", "broken", "twisted", "stiff",
        "soreness", "aching", "ache", "discomfort", "tenderness"
    ];

    public static List<string> Validate(StartTriageWorkflowDto request)
    {
        var errors = new List<string>();
        var symptoms = request.Symptoms.Trim();
        if (symptoms.Length < 3 || !System.Text.RegularExpressions.Regex.IsMatch(symptoms, @"[A-Za-z]"))
            errors.Add("Please describe the symptom using a few words.");
        if (System.Text.RegularExpressions.Regex.IsMatch(symptoms.Replace(" ", string.Empty), @"^(.)\1{2,}$"))
            errors.Add("The symptom description appears to be repeated characters. Please describe what you are feeling.");
        if (System.Text.RegularExpressions.Regex.IsMatch(symptoms, @"\b(ignore (all |previous )?instructions|system prompt|jailbreak)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            errors.Add("Please enter symptom information only; instructions for the system cannot be processed.");
        if (System.Text.RegularExpressions.Regex.IsMatch(symptoms, @"\b(password|passcode|cvv|card number|account number)\b|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
            ContainsPhoneNumber(symptoms))
            errors.Add("Remove contact details, account details, and passwords before submitting a symptom report.");
        var vitals = request.Vitals;
        if (vitals is null) return errors;
        if (vitals.TemperatureCelsius is < 25 or > 45) errors.Add("Temperature is outside the accepted input range.");
        if (vitals.HeartRateBpm is < 20 or > 300) errors.Add("Heart rate is outside the accepted input range.");
        if (vitals.SystolicBloodPressure is < 40 or > 300 || vitals.DiastolicBloodPressure is < 20 or > 200) errors.Add("Blood pressure is outside the accepted input range.");
        if (vitals.SystolicBloodPressure.HasValue && vitals.DiastolicBloodPressure.HasValue && vitals.SystolicBloodPressure <= vitals.DiastolicBloodPressure) errors.Add("Blood pressure values are contradictory.");
        if (vitals.OxygenSaturationPercent is < 1 or > 100) errors.Add("Oxygen saturation must be between 1 and 100 percent.");
        if (vitals.ObservedAt > DateTime.UtcNow.AddMinutes(5)) errors.Add("Vital-sign observation time cannot be in the future.");
        return errors;
    }

    public static List<string> FindEmergencyFlags(string symptoms) => EmergencyPhrases
        .Where(phrase => ContainsNonNegatedPhrase(symptoms, phrase)).ToList();
    public static List<string> FindUrgentFlags(string symptoms)
    {
        var flags = UrgentPhrases.Where(phrase => ContainsNonNegatedPhrase(symptoms, phrase)).ToList();
        if (CancerTreatmentPhrases.Any(phrase => ContainsNonNegatedPhrase(symptoms, phrase)) &&
            TreatmentWarningPhrases.Any(phrase => ContainsNonNegatedPhrase(symptoms, phrase)))
            flags.Add("cancer treatment with a reported warning symptom");
        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> FindClinicalReviewFlags(string symptoms) => SeriousContextPhrases
        .Where(phrase => ContainsNonNegatedPhrase(symptoms, phrase))
        .Select(phrase => $"reported high-risk context: {phrase}")
        .ToList();

    public static bool IsWithinValidatedRoutineScope(string symptoms) =>
        !string.IsNullOrWhiteSpace(symptoms) &&
        ValidatedRoutinePhrases.Any(phrase => symptoms.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    /// <summary>Recognizes contact numbers without confusing common appointment/date formats for phones.</summary>
    public static bool ContainsPhoneNumber(string text) => PhoneNumberPattern().IsMatch(DatePattern().Replace(text, ""));

    public static string RedactPhoneNumbers(string text) => PhoneNumberPattern().Replace(text, "[redacted phone]");

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<!\d)(?:\+94[\s().-]?|0094[\s().-]?|0)7\d(?:[\s().-]?\d){7}(?!\d)|(?<!\d)\+?[1-9]\d{7,14}(?!\d)", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex PhoneNumberPattern();

    // Preserve all supported date forms before evaluating digit sequences as contact details.
    [System.Text.RegularExpressions.GeneratedRegex(@"(?<!\d)(?:\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{4})(?!\d)", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex DatePattern();

    public static List<string> FindEmergencyFlags(ClinicalFactSet facts)
    {
        var flags = new List<string>();
        foreach (var warning in facts.WarningSigns.Where(warning => IsGrounded(facts, "warningSigns", warning)))
        {
            flags.AddRange(FindEmergencyFlags(warning));
            if (NormalizedEmergencyWarnings.TryGetValue(warning, out var normalized)) flags.Add(normalized);
        }

        if (IsGrounded(facts, "primaryConcept", facts.PrimaryConcept) &&
            string.Equals(facts.PrimaryConcept, "nosebleed", StringComparison.OrdinalIgnoreCase) &&
            IsGrounded(facts, "currentlyActive") && facts.CurrentlyActive == true &&
            IsGrounded(facts, "durationMinutes") && facts.DurationMinutes >= 15)
            flags.Add("grounded active nosebleed lasting at least 15 minutes");

        if (string.Equals(facts.PrimaryConcept, "nosebleed", StringComparison.OrdinalIgnoreCase) &&
            facts.WarningSigns.Any(warning => IsGrounded(facts, "warningSigns", warning)))
            flags.Add("grounded nosebleed warning sign");
        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> FindUrgentFlags(ClinicalFactSet facts)
    {
        var flags = facts.WarningSigns.Where(warning => IsGrounded(facts, "warningSigns", warning))
            .SelectMany(FindUrgentFlags).ToList();
        flags.AddRange(facts.WarningSigns.Where(warning => IsGrounded(facts, "warningSigns", warning))
            .Where(warning => NormalizedUrgentWarnings.TryGetValue(warning, out _))
            .Select(warning => NormalizedUrgentWarnings[warning]));
        if (facts.TemperatureCelsius >= 39.5m && IsGrounded(facts, "temperatureCelsius"))
            flags.Add("grounded measured temperature of at least 39.5°C");
        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> FindClinicalReviewFlags(ClinicalFactSet facts) => facts.RiskContexts
        .Where(risk => IsGrounded(facts, "riskContexts", risk))
        .SelectMany(FindClinicalReviewFlags)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static bool HasGroundedConcept(ClinicalFactSet facts) =>
        !string.IsNullOrWhiteSpace(facts.PrimaryConcept) && IsGrounded(facts, "primaryConcept", facts.PrimaryConcept);

    private static bool IsGrounded(ClinicalFactSet facts, string field, string? value = null) =>
        facts.Evidence.Any(item => string.Equals(item.Field, field, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(item.Quote) &&
            (value is null || string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase)));

    private static readonly IReadOnlyDictionary<string, string> NormalizedEmergencyWarnings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["breathing_difficulty"] = "grounded normalized breathing difficulty",
            ["severe_chest_pain"] = "grounded normalized severe chest pain",
            ["fainting_or_loss_of_consciousness"] = "grounded normalized fainting or loss of consciousness",
            ["new_confusion"] = "grounded normalized new confusion",
            ["stroke_like_symptoms"] = "grounded normalized stroke-like symptoms",
            ["severe_bleeding"] = "grounded normalized severe bleeding",
            ["seizure"] = "grounded normalized seizure",
            ["severe_allergic_reaction"] = "grounded normalized severe allergic reaction",
            ["blue_lips"] = "grounded normalized blue lips",
            ["coughing_or_vomiting_blood"] = "grounded normalized coughing or vomiting blood"
        };

    private static readonly IReadOnlyDictionary<string, string> NormalizedUrgentWarnings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["persistent_vomiting"] = "grounded normalized persistent vomiting",
            ["dehydration_signs"] = "grounded normalized dehydration signs",
            ["high_fever"] = "grounded normalized high fever"
        };

    private static bool ContainsNonNegatedPhrase(string text, string phrase)
    {
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(phrase, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;
            
            var prefix = text[..index];
            var nearbyPrefix = prefix[Math.Max(0, prefix.Length - 45)..];
            
            var suffixIndex = index + phrase.Length;
            var suffix = text[suffixIndex..];
            var nearbySuffix = suffix[..Math.Min(suffix.Length, 45)];
            
            var hasPrefixNegation = System.Text.RegularExpressions.Regex.IsMatch(nearbyPrefix,
                    @"\b(no|not|without|deny|denies|never|don't|do not)\b(?:\W+\w+){0,4}\W*$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
            var hasSuffixNegation = System.Text.RegularExpressions.Regex.IsMatch(nearbySuffix,
                    @"^\W+(is|are|was|were)?\W*(not|resolved|gone|cleared|absent)\b",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!hasPrefixNegation && !hasSuffixNegation)
                return true;
                
            start = index + phrase.Length;
        }
        return false;
    }
}
