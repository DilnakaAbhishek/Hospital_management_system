namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.Shared;


public sealed record AgentToolExecution(
    string Tool, string Status, bool ValidationPassed, string Outcome,
    int? DurationMs = null, int RetryCount = 0, string? ErrorCode = null);
