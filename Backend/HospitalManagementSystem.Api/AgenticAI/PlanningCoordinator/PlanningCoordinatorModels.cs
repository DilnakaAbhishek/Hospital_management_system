using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

/// <summary>
/// Predefined, allow-listed workflow types.
/// </summary>
public enum PlanningWorkflowType
{
    TriageThenAppointmentProposal,
    AppointmentProposal,
    AppointmentStatus,
    AppointmentCancellation,
    AppointmentReschedule,
    MedicalRecords,
    Unsupported,
    SafeTriage
}

/// <summary>
/// Predefined, allow-listed workflow step names.
/// </summary>
public static class PlanningWorkflowSteps
{
    public const string IntakeAndInitialSafetyAgent = "IntakeAndInitialSafetyAgent";
    public const string ClinicalUnderstandingAgent = "ClinicalUnderstandingAgent";
    public const string SafetyRoutingAgent = "SafetyRoutingAgent";
    public const string GuidanceValidationAgent = "GuidanceValidationAgent";
    public const string SafetyCheck = "SafetyCheck";
    public const string SymptomExtraction = "SymptomExtraction";
    public const string TriageAssessment = "TriageAssessment";
    public const string DoctorLookup = "DoctorLookup";
    public const string SlotSearch = "SlotSearch";
    public const string AppointmentProposal = "AppointmentProposal";
    public const string PatientConfirmation = "PatientConfirmation";
    public const string AppointmentLookup = "AppointmentLookup";
    public const string StatusNotification = "StatusNotification";
    public const string MedicalRecordLookup = "MedicalRecordLookup";
    public const string MedicalRecordExplanation = "MedicalRecordExplanation";
    public const string SafeControlledResponse = "SafeControlledResponse";

    public static readonly IReadOnlyDictionary<PlanningWorkflowType, IReadOnlyList<string>> DefaultStepsByWorkflow =
        new Dictionary<PlanningWorkflowType, IReadOnlyList<string>>
        {
            [PlanningWorkflowType.TriageThenAppointmentProposal] =
            [
                SafetyCheck,
                SymptomExtraction,
                TriageAssessment,
                AppointmentProposal,
                PatientConfirmation
            ],
            [PlanningWorkflowType.AppointmentProposal] =
            [
                DoctorLookup,
                SlotSearch,
                AppointmentProposal,
                PatientConfirmation
            ],
            [PlanningWorkflowType.AppointmentStatus] =
            [
                AppointmentLookup,
                StatusNotification
            ],
            [PlanningWorkflowType.AppointmentCancellation] = [AppointmentLookup, PatientConfirmation],
            [PlanningWorkflowType.AppointmentReschedule] =
            [AppointmentLookup, DoctorLookup, SlotSearch, AppointmentProposal, PatientConfirmation],
            [PlanningWorkflowType.MedicalRecords] = [MedicalRecordLookup, MedicalRecordExplanation],
            [PlanningWorkflowType.SafeTriage] = [SafetyCheck, SymptomExtraction, TriageAssessment],
            [PlanningWorkflowType.Unsupported] =
            [
                SafeControlledResponse
            ]
        };

    public static readonly HashSet<string> AllAllowedSteps =
    [
        SafetyCheck,
        SymptomExtraction,
        TriageAssessment,
        DoctorLookup,
        SlotSearch,
        AppointmentProposal,
        PatientConfirmation,
        AppointmentLookup,
        StatusNotification,
        MedicalRecordLookup,
        MedicalRecordExplanation,
        SafeControlledResponse
        , IntakeAndInitialSafetyAgent, ClinicalUnderstandingAgent, SafetyRoutingAgent,
        GuidanceValidationAgent
    ];

    public static bool IsSafeTriageAgent(string name) => name is
        IntakeAndInitialSafetyAgent or ClinicalUnderstandingAgent or SafetyRoutingAgent or GuidanceValidationAgent;
}

/// <summary>
/// Request DTO representing a patient's objective.
/// </summary>
public sealed class PlanningRequestDto
{
    [JsonIgnore] public string? ExistingWorkflowId { get; set; }
    [Required, StringLength(4000, MinimumLength = 2)]
    public string Objective { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? PatientId { get; set; }

    [StringLength(100)]
    public string? PreferredSpecialty { get; set; }

    [StringLength(50)]
    public string? PreferredDate { get; set; }

    [StringLength(50)]
    public string? PreferredTime { get; set; }
}

/// <summary>
/// Plan output produced by the Coordinator.
/// </summary>
public sealed class PlanningPlanDto
{
    public string WorkflowType { get; set; } = PlanningWorkflowType.Unsupported.ToString();
    public bool AppointmentRequested { get; set; }
    public bool PatientConfirmationRequired { get; set; }
    public IReadOnlyList<string> RequiredSteps { get; set; } = [];
    public string? PreferredDate { get; set; }
    public string? PreferredTime { get; set; }
    public IReadOnlyList<string> FollowUpQuestions { get; set; } = [];
    public string Rationale { get; set; } = string.Empty;
    public string SafeResponse { get; set; } = string.Empty;
}

/// <summary>
/// Response returned to the client.
/// </summary>
public sealed record PlanningResponseDto(
    string WorkflowId,
    string Status,
    string Objective,
    PlanningPlanDto Plan,
    IReadOnlyList<string> CompletedStages,
    IReadOnlyList<string> Errors,
    IReadOnlyList<PlanningAuditEventDto> AuditEvents,
    DateTimeOffset CreatedAt);

/// <summary>
/// Audit event for coordinator actions and state transitions.
/// </summary>
public sealed class PlanningAuditEvent
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Metadata { get; set; }
}

public sealed record PlanningAuditEventDto(
    DateTimeOffset Timestamp,
    string EventType,
    string Description,
    string? Metadata);

/// <summary>
/// Durable / persisted record of a planning workflow.
/// </summary>
public sealed class PlanningWorkflowRecord
{
    public string WorkflowId { get; set; } = Guid.NewGuid().ToString("N");
    public int? PatientId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public PlanningPlanDto Plan { get; set; } = new();
    public string WorkflowType => Plan.WorkflowType;
    public IReadOnlyList<string> CompletedSteps => CompletedStages;
    public List<PlanningPlanDto> PreviousPlans { get; set; } = [];
    public List<ExecutionPlanStep> Steps { get; set; } = [];
    public string? CurrentStep { get; set; }
    public string? CurrentAgent { get; set; }
    public List<string> ToolResultsSummary { get; set; } = [];
    public List<string> ValidationResults { get; set; } = [];
    public int RetryCount { get; set; }
    public string ApprovalStatus { get; set; } = "NotRequired";
    public string? FinalOutcome { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorSummary { get; set; }
    public string? FailedStep { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public int Revision { get; set; }
    public string Status { get; set; } = "Created"; // Created, Planned, InProgress, Completed, Failed, Unsupported
    public List<string> CompletedStages { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public List<PlanningAuditEvent> AuditEvents { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Configuration options for the Planning Agent.
/// </summary>
public sealed class PlanningAgentOptions
{
    public const string SectionName = "PlanningAgent";
    public string GeminiApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public int TimeoutSeconds { get; set; } = 45;
}

/// <summary>
/// Raw structured JSON decision payload returned by Gemini.
/// </summary>
public sealed class GeminiPlanningDecision
{
    [JsonPropertyName("workflowType")]
    public string WorkflowType { get; set; } = string.Empty;

    [JsonPropertyName("appointmentRequested")]
    public bool AppointmentRequested { get; set; }

    [JsonPropertyName("patientConfirmationRequired")]
    public bool PatientConfirmationRequired { get; set; }

    [JsonPropertyName("requiredSteps")]
    public string[] RequiredSteps { get; set; } = [];

    [JsonPropertyName("preferredDate")]
    public string? PreferredDate { get; set; }

    [JsonPropertyName("preferredTime")]
    public string? PreferredTime { get; set; }

    [JsonPropertyName("followUpQuestions")]
    public string[] FollowUpQuestions { get; set; } = [];

    [JsonPropertyName("rationale")]
    public string Rationale { get; set; } = string.Empty;

    [JsonPropertyName("safeResponse")]
    public string SafeResponse { get; set; } = string.Empty;
}

public sealed class ExecutionPlanStep
{
    public string StepId { get; set; } = Guid.NewGuid().ToString("N");
    public string StepType { get; set; } = "";
    public string AssignedAgent { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public List<string> Dependencies { get; set; } = [];
    public string Input { get; set; } = "";
    public string? OutputSummary { get; set; }
    public string ValidationStatus { get; set; } = "Pending";
    public string? Error { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

// Indexed ownership envelope; the versioned document contains only explicit inputs and execution summaries.
public sealed class AgenticExecution
{
    public string WorkflowId { get; set; } = "";
    public int? PatientId { get; set; }
    public string RecordJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; }
}
