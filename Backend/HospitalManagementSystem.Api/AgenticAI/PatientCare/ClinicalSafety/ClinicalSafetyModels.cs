using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;

public sealed record ClinicalSafetyAssessment(string TriageLevel, string ProposedRoute, bool RequiresClinicalReview, bool FailedSafely,
    IReadOnlyList<string> ValidationProblems, IReadOnlyList<string> EmergencyFlags, IReadOnlyList<string> UrgentFlags,
    IReadOnlyList<string> ClinicalReviewFlags, ClinicalExtractionResult? ExtractedFacts, IReadOnlyList<string> MissingInformation,
    IReadOnlyList<ClinicalSafetyToolExecution> ToolTrace);
public sealed record ClinicalSafetyToolExecution(string Tool, string Status, bool ValidationPassed, string Outcome, int DurationMs, int RetryCount = 0, string? ErrorCode = null);
public sealed record ClinicalFlagSet(IReadOnlyList<string> Emergency, IReadOnlyList<string> Urgent, IReadOnlyList<string> ClinicalReview)
{ public bool HasEscalation => Emergency.Count > 0 || Urgent.Count > 0 || ClinicalReview.Count > 0; public string Status => Emergency.Count > 0 ? "EmergencyEscalation" : Urgent.Count > 0 ? "UrgentAssessment" : ClinicalReview.Count > 0 ? "ClinicalReviewRequired" : "Completed"; }
