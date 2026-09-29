namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public interface IPlanningCoordinatorAgent
{
    Task<PlanningResponseDto> PlanAsync(PlanningRequestDto request, CancellationToken cancellationToken = default);
    Task<PlanningResponseDto?> GetWorkflowStatusAsync(string workflowId, CancellationToken cancellationToken = default);
}
