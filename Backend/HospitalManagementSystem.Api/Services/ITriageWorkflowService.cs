using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services;

public interface ITriageWorkflowService
{
    Task<TriageWorkflowDto> StartForPatientAsync(int patientId, StartTriageWorkflowDto request, string? executionWorkflowId = null);
    Task<TriageWorkflowDto?> ContinueForPatientAsync(int workflowId, int patientId, ContinueTriageWorkflowDto request, string? executionWorkflowId = null);
    Task<TriageWorkflowDto?> GetForPatientAsync(int workflowId, int patientId);
    Task<IReadOnlyList<TriageWorkflowDto>> GetHistoryForPatientAsync(int patientId);
    Task<TriageWorkflowDto?> GetForClinicalReviewerAsync(int workflowId);
    Task<IReadOnlyList<TriageWorkflowDto>> GetPendingClinicalReviewsAsync(int? doctorUserId = null);
    Task<IReadOnlyList<TriageWorkflowEventDto>?> GetAuditEventsAsync(int workflowId);
    Task<TriageWorkflowDto?> ReviewAsync(int workflowId, int reviewerUserId, ReviewTriageWorkflowDto request);
    Task<TriageWorkflowDto?> SetPatientClinicalReviewChoiceAsync(int workflowId, int patientId, bool requested, int? preferredDoctorId = null, string? targetSpecialty = null);
}
