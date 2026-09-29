using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.AgenticAI.MedicalReports;

public sealed record ClinicalEntity(string Category, string Name, string Detail);

public sealed record MedicationAlert(string Severity, string DrugName, string Message);

public sealed record MedicalReportAnalysisResult(
    string Overview,
    IReadOnlyList<string> KeyDiagnoses,
    IReadOnlyList<string> PrescribedMedications,
    IReadOnlyList<string> LabFindings,
    IReadOnlyList<MedicationAlert> SafetyAlerts,
    string? FollowUpInstructions,
    string PlainLanguageSummary,
    string AgentTrajectoryDescription,
    bool UsedGemini
);

public interface IMedicalRecordIntelligenceAgent
{
    Task<MedicalReportAnalysisResult> AnalyzeRecordsAsync(
        IEnumerable<MedicalRecord> records,
        string patientName,
        string? specificUserQuery,
        CancellationToken cancellationToken = default);
}
