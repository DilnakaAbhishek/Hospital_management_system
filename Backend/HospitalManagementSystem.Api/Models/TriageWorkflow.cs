namespace HospitalManagementSystem.Api.Models;

/// <summary>
/// An auditable decision-support workflow. This is not a diagnosis or treatment record.
/// </summary>
public class TriageWorkflow
{
    public int TriageWorkflowId { get; set; }
    public string? ExecutionWorkflowId { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string Status { get; set; } = TriageWorkflowStatuses.InProgress;
    public string ApprovalStatus { get; set; } = TriageApprovalStatuses.NotRequired;
    public string TriageLevel { get; set; } = TriageLevels.InsufficientInformation;
    public string UncertaintyState { get; set; } = TriageUncertaintyStates.LimitedInformation;
    public bool RequiresHumanReview { get; set; }
    public string Symptoms { get; set; } = string.Empty;
    public string? VitalsJson { get; set; }
    public string PlanJson { get; set; } = "[]";
    [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
    public string ResultJson { get; set; } = "{}";
    public string? ErrorCode { get; set; }
    public string? FinalOutcome { get; set; }
    public string RuleSetVersion { get; set; } = "safetriage-rules-v3";
    public string WorkflowVersion { get; set; } = "safetriage-workflow-v2";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public int? AssignedDoctorId { get; set; }
    public Doctor? AssignedDoctor { get; set; }
    public string? TargetSpecialty { get; set; }
    public string PriorityLevel { get; set; } = "Normal";
}

public class TriageWorkflowEvent
{
    public int TriageWorkflowEventId { get; set; }
    public int TriageWorkflowId { get; set; }
    public TriageWorkflow? TriageWorkflow { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class TriageWorkflowStatuses { public const string InProgress = "InProgress"; public const string PendingPatientInput = "PendingPatientInput"; public const string Completed = "Completed"; public const string PendingClinicalReview = "PendingClinicalReview"; public const string FailedSafely = "FailedSafely"; }
public static class TriageApprovalStatuses { public const string NotRequired = "NotRequired"; public const string Pending = "Pending"; public const string Approved = "Approved"; public const string Rejected = "Rejected"; public const string RevisionRequested = "RevisionRequested"; }
public static class TriageLevels { public const string Emergency = "Emergency"; public const string Urgent = "Urgent"; public const string ClinicalReview = "ClinicalReview"; public const string NonUrgent = "NonUrgent"; public const string InsufficientInformation = "InsufficientInformation"; }
public static class TriageUncertaintyStates { public const string SufficientInformation = "SufficientInformation"; public const string LimitedInformation = "LimitedInformation"; public const string ConflictingInformation = "ConflictingInformation"; public const string OutsideValidatedScope = "OutsideValidatedScope"; public const string HumanReviewRequired = "HumanReviewRequired"; }
