using HospitalManagementSystem.Api.AgenticAI.SafeTriage;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;

public static class ClinicalSafetyTools
{
    public static IReadOnlyList<string> ValidateVitals(StartTriageWorkflowDto request) => SafeTriageRules.Validate(request);
    public static ClinicalFlagSet EvaluateRedFlags(string text) => new(SafeTriageRules.FindEmergencyFlags(text), SafeTriageRules.FindUrgentFlags(text), SafeTriageRules.FindClinicalReviewFlags(text));
    public static ClinicalFlagSet EvaluateGroundedFacts(ClinicalFactSet? facts) => new(facts is null ? [] : SafeTriageRules.FindEmergencyFlags(facts), facts is null ? [] : SafeTriageRules.FindUrgentFlags(facts), facts is null ? [] : SafeTriageRules.FindClinicalReviewFlags(facts));
    public static bool IsRoutine(string text, ClinicalFactSet? facts) => SafeTriageRules.IsWithinValidatedRoutineScope(text) || (facts is not null && SafeTriageRules.HasGroundedConcept(facts));
}
