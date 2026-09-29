using System.ComponentModel.DataAnnotations;
using HospitalManagementSystem.Api.AgenticAI.SafeTriage;

namespace HospitalManagementSystem.Api.DTOs;

public class StartTriageWorkflowDto
{
    [Required, MinLength(3), MaxLength(4000)] public string Symptoms { get; set; } = string.Empty;
    public TriageVitalsDto? Vitals { get; set; }
    public bool IsFollowUp { get; set; }
}

public class TriageVitalsDto
{
    public decimal? TemperatureCelsius { get; set; }
    public int? HeartRateBpm { get; set; }
    public int? SystolicBloodPressure { get; set; }
    public int? DiastolicBloodPressure { get; set; }
    public int? OxygenSaturationPercent { get; set; }
    public DateTime? ObservedAt { get; set; }
    public string? Source { get; set; }
}

public class ReviewTriageWorkflowDto
{
    [Required] public string Decision { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Note { get; set; }
    [MaxLength(4000)] public string? FinalResponse { get; set; }
}

public class ContinueTriageWorkflowDto
{
    [Required, MinLength(1), MaxLength(12)] public List<TriageAnswerDto> Answers { get; set; } = [];
}

public class TriageAnswerDto
{
    [Required, MaxLength(80)] public string QuestionId { get; set; } = string.Empty;
    [MaxLength(500)] public string Value { get; set; } = string.Empty;
    public SafeTriageRequirementState? State { get; set; }
    [MaxLength(30)] public string? Unit { get; set; }
}

public class TriageWorkflowDto
{
    public int WorkflowId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string TriageLevel { get; set; } = string.Empty;
    public string UncertaintyState { get; set; } = string.Empty;
    public bool RequiresHumanReview { get; set; }
    public string PatientMessage { get; set; } = string.Empty;
    public string OriginalComplaint { get; set; } = string.Empty;
    public IReadOnlyList<SafeTriageRequirement> Requirements { get; set; } = [];
    public int FollowUpCount { get; set; }
    public string? SafeTriageSuggestion { get; set; }
    public string? ReviewedResponse { get; set; }
    /// <summary>The patient's original report and any recorded follow-up responses.</summary>
    public string PatientReportedSymptoms { get; set; } = string.Empty;
    public TriageGuidanceDto? Guidance { get; set; }
    public IReadOnlyList<string> RiskFactors { get; set; } = [];
    public IReadOnlyList<string> RedFlags { get; set; } = [];
    public IReadOnlyList<string> UrgentFlags { get; set; } = [];
    public IReadOnlyList<string> ClinicalReviewFlags { get; set; } = [];
    public IReadOnlyList<string> MissingInformation { get; set; } = [];
    public TriageClinicalFactsDto? ClinicalFacts { get; set; }
    public IReadOnlyList<string> DecisionBasis { get; set; } = [];
    public IReadOnlyList<TriagePlanStepDto> Plan { get; set; } = [];
    public string RuleSetVersion { get; set; } = string.Empty;
    public string WorkflowVersion { get; set; } = string.Empty;
    public int? AssignedDoctorId { get; set; }
    public string? AssignedDoctorName { get; set; }
    public string? TargetSpecialty { get; set; }
    public string PriorityLevel { get; set; } = "Normal";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TriageClinicalFactsDto
{
    public string? PrimaryConcept { get; set; }
    public bool? CurrentlyActive { get; set; }
    public decimal? DurationMinutes { get; set; }
    public decimal? DurationDays { get; set; }
    public decimal? SeverityScore { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public string? Progression { get; set; }
    public IReadOnlyList<string> WarningSigns { get; set; } = [];
    public IReadOnlyList<string> NegatedWarningSigns { get; set; } = [];
    public IReadOnlyList<string> RiskContexts { get; set; } = [];
    public IReadOnlyList<TriageFactEvidenceDto> Evidence { get; set; } = [];
}

public class TriageFactEvidenceDto
{
    public string Field { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string Quote { get; set; } = string.Empty;
}

public class TriageGuidanceDto
{
    public string Heading { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> Actions { get; set; } = [];
    public IReadOnlyList<string> SeekHelpIf { get; set; } = [];
    public IReadOnlyList<string> FollowUpQuestions { get; set; } = [];
    public IReadOnlyList<TriageFollowUpQuestionDto> FollowUpItems { get; set; } = [];
    public string EvidenceSource { get; set; } = string.Empty;
}

public class TriageFollowUpQuestionDto
{
    public string Id { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public IReadOnlyList<string> Options { get; set; } = [];
    public string? Unit { get; set; }
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    /// <summary>Groups questions visually in the UI (e.g. "🚨 Safety Check", "⏱ Onset & Duration", "📊 Severity", "🔄 Progression", "⚕ Associated Symptoms", "🧬 Medical Context")</summary>
    public string? Category { get; set; }
    /// <summary>Short helper text shown below the question in the UI</summary>
    public string? Hint { get; set; }
}

public class TriagePlanStepDto { public string Agent { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public string Purpose { get; set; } = string.Empty; }

public class TriageWorkflowEventDto
{
    public string Stage { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Tool { get; set; }
    public bool? ValidationPassed { get; set; }
    public string? Outcome { get; set; }
    public int? DurationMs { get; set; }
    public int? RetryCount { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; }
}
