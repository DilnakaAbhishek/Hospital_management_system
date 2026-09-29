namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public interface IPlanningCoordinatorStore
{
    Task SaveAsync(PlanningWorkflowRecord record, CancellationToken cancellationToken = default);
    Task<PlanningWorkflowRecord?> GetAsync(string workflowId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlanningWorkflowRecord>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default);
    Task AddAuditEventAsync(string workflowId, string eventType, string description, string? metadata = null, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(string workflowId, string status, string? completedStage = null, string? error = null, CancellationToken cancellationToken = default);
}
