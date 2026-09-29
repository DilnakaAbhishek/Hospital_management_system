using System.ComponentModel.DataAnnotations;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;
using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;

public sealed class AssistantConversation
{
    public Guid AssistantConversationId { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public Guid InitialRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StateJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class AssistantMessageRequest
{
    [System.Text.Json.Serialization.JsonIgnore] public TriageVitalsDto? Vitals { get; set; }
    public Guid? ConversationId { get; set; }
    [StringLength(4000)] public string Message { get; set; } = string.Empty;
    [MaxLength(80)] public string? RequirementId { get; set; }
    public HospitalManagementSystem.Api.AgenticAI.SafeTriage.SafeTriageRequirementState? RequirementState { get; set; }
    public Guid RequestId { get; set; }
}
public sealed class AssistantActionRequest
{
    public Guid ActionId { get; set; }
    [Required] public string Decision { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public int? DoctorTimeSlotId { get; set; }
    public int? AppointmentId { get; set; }
}
public sealed record AssistantCapability(string Id, string Label, bool Enabled, string Prompt);
public sealed record AssistantMessage(string Id, string Role, string Text, DateTime CreatedAt, IReadOnlyList<string> Progress)
{
    public IReadOnlyList<AppointmentDto> Appointments { get; init; } = [];
    public IReadOnlyList<AgentSlot> Slots { get; init; } = [];
    public IReadOnlyList<AgentDoctor> Doctors { get; init; } = [];
    public AssistantPendingAction? ProposedAction { get; init; }
    public bool AvailabilityChecked { get; init; }
    // Snapshot on an accepted patient answer, separate from the next active question.
    public AssistantQuestion? FollowUpQuestion { get; init; }
    public SafeTriage.SafeTriageRequirementState? FollowUpState { get; init; }
}
public sealed record AssistantClinicalReview(int WorkflowId, string Status, string ApprovalStatus, string Message);
public sealed record AssistantQuestion(string Id, string Prompt, bool Required)
{
    public string Type { get; init; } = string.Empty;
    public IReadOnlyList<string> Options { get; init; } = [];
    public string? Hint { get; init; }
    public string? Unit { get; init; }
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
}
public sealed class AssistantPendingAction
{
    public Guid ActionId { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "book";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<AgentSlot> Slots { get; set; } = [];
    public IReadOnlyList<AppointmentDto> Appointments { get; set; } = [];
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
    public int? ProposalId { get; set; }
    // Historical proposals remain visible, but only Pending can be confirmed.
    public string Status { get; set; } = "Pending";
}
public sealed class AssistantState
{
    public string? ExecutionWorkflowId { get; set; }
    public string State { get; set; } = "COMPLETED";
    public List<AssistantMessage> Messages { get; set; } = [];
    public AssistantPendingAction? PendingAction { get; set; }
    public List<AssistantQuestion> Questions { get; set; } = [];
    public IReadOnlyList<AppointmentDto> Appointments { get; set; } = [];
    public IReadOnlyList<AgentSlot> Slots { get; set; } = [];
    public IReadOnlyList<AgentDoctor> Doctors { get; set; } = [];
    public Dictionary<Guid, string> Requests { get; set; } = [];
    public ClinicalSafetyAssessment? Clinical { get; set; }
    public int? WorkflowId { get; set; }
    public List<TriageAnswerDto> Answers { get; set; } = [];
    public bool SafetyBlocked { get; set; }
    public string? SearchQuery { get; set; }
    public DateOnly? PreferredDate { get; set; }
    public DateOnly? ThroughDate { get; set; }
    public string? Period { get; set; }
    public bool WantsAppointment { get; set; }
    public string? Awaiting { get; set; }
    public string? CancellationReason { get; set; }
    public int? RescheduleAppointmentId { get; set; }
    public IReadOnlyList<AssistantClinicalReview> ClinicalReviews { get; set; } = [];
    public string? ReadSearchMode { get; set; }
    public string? ActiveTask { get; set; }
    public bool AvailabilityChecked { get; set; }
    public IReadOnlyList<int> ExcludedDoctorTimeSlotIds { get; set; } = [];
}
public sealed record AssistantConversationResponse(Guid ConversationId, string Title, string State,
    DateTime UpdatedAt, IReadOnlyList<AssistantMessage> Messages, AssistantPendingAction? PendingAction,
    IReadOnlyList<AssistantQuestion> Questions, IReadOnlyList<AppointmentDto> Appointments,
    IReadOnlyList<AgentSlot> Slots, IReadOnlyList<AgentDoctor> Doctors, IReadOnlyList<AssistantCapability> Capabilities)
{
    public IReadOnlyList<AssistantClinicalReview> ClinicalReviews { get; init; } = [];
    public bool AvailabilityChecked { get; init; }
    public string? ExecutionWorkflowId { get; init; }
    public bool AssessmentInputActive { get; init; }
}

// Register future capability handlers here and in the coordinator; clients consume this list.
public interface IHospitalAssistantReadAgent
{
    AssistantCapability Capability { get; }
    bool CanHandle(string message);
    Task<string> ReadAsync(string message, PatientDto patient, CancellationToken cancellationToken);
}

public sealed class AssistantAgentRegistry(IEnumerable<IHospitalAssistantReadAgent> additionalAgents)
{
    public IReadOnlyList<IHospitalAssistantReadAgent> AdditionalAgents { get; } = additionalAgents.ToArray();
    public IReadOnlyList<AssistantCapability> Capabilities => new AssistantCapability[] {
        new("patient-help", "Patient Help", true, "I need help with my symptoms"),
        new("find-doctor", "Find Doctor", true, "Find a doctor"),
        new("appointments", "Appointments", true, "Show my appointments"),
        AdditionalAgents.FirstOrDefault(a => a.Capability.Id == "medical-reports")?.Capability ?? new("medical-reports", "Medical Reports", false, ""),
    }.Concat(AdditionalAgents.Where(a => a.Capability.Id != "medical-reports" && a.Capability.Id != "doctor-schedules").Select(a => a.Capability)).ToArray();
}
