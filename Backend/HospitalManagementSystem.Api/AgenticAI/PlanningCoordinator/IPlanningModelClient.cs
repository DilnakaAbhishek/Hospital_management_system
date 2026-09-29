namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public interface IPlanningModelClient
{
    Task<GeminiPlanningDecision> PlanObjectiveAsync(string objective, CancellationToken cancellationToken = default);
    Task<string?> AnswerHealthInformationAsync(string question, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
