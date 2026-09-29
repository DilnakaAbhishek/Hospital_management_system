using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
namespace HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
/// Compatibility facade; the coordinator owns all routing and execution.
public sealed class HospitalAssistantService(
    ApplicationDbContext db, AssistantAgentRegistry registry, ITriageWorkflowService workflows,
    IHospitalAppointmentProposalAgent proposals, ISafetyValidationApprovalAgent approval,
    IAppointmentSearchTools tools, IAppointmentService appointments, AppointmentSmsNotifier sms,
    IAssessmentIntentClient? intentClient = null, IPlanningModelClient? modelClient = null, ILogger<PlanningCoordinatorAgent>? logger = null)
{
    private readonly PlanningCoordinatorAgent coordinator = new(db, registry, workflows, proposals, approval,
        tools, appointments, sms, intentClient, modelClient, logger);
    public IReadOnlyList<AssistantCapability> Capabilities => coordinator.Capabilities;
    public Task<object> HistoryAsync(int patientId, CancellationToken token) => coordinator.HistoryAsync(patientId, token);
    public Task<AssistantConversationResponse> GetAsync(int patientId, Guid id, CancellationToken token) => coordinator.GetAsync(patientId, id, token);
    public Task<AssistantConversationResponse> MessageAsync(PatientDto patient, AssistantMessageRequest request, CancellationToken token) => coordinator.MessageAsync(patient, request, token);
    public Task<AssistantConversationResponse> DecideAsync(PatientDto patient, Guid id, AssistantActionRequest request, CancellationToken token) => coordinator.DecideAsync(patient, id, request, token);
}
