using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.AgenticAI.MedicalReports;

public sealed class MedicalReportAssistantAgent : IHospitalAssistantReadAgent
{
    private readonly IMedicalRecordRepository _recordRepository;
    private readonly IMedicalRecordIntelligenceAgent _intelligenceAgent;

    public MedicalReportAssistantAgent(
        IMedicalRecordRepository recordRepository,
        IMedicalRecordIntelligenceAgent intelligenceAgent)
    {
        _recordRepository = recordRepository;
        _intelligenceAgent = intelligenceAgent;
    }

    public AssistantCapability Capability =>
        new("medical-reports", "Medical Reports", true, "Summarize my medical reports");

    public bool CanHandle(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        // "General Medicine" is a doctor specialty, not a request to read a
        // patient's medication/medical-record data. Let the directory route own it.
        if (Regex.IsMatch(message, @"\b(doctors?|specialists?)\b.*\b(work|available|availability|find|show)\b|\b(work|available|availability|find|show)\b.*\b(doctors?|specialists?)\b", RegexOptions.IgnoreCase))
            return false;

        var pattern = @"\b(medical\s*reports?|medical\s*records?|my\s*reports?|my\s*records?|lab\s*(reports?|results?|notes?|findings?)|laboratory|cholesterol|lipid(\s*panel)?|test\s*results?|prescriptions?|prescribed|prescribe|medications?|medicines?|diagnos(is|es|ed)|discharge\s*summary|doctor\s*notes?|clinical\s*notes?|blood\s*tests?|treatment\s*plan|symptoms?|recorded\s+symptoms?|past\s+symptoms?|visit\s+symptoms?|typed\s*(medical\s*)?data|typed\s*record|record\s*details?|medical\s*details?|clinical\s*details?|visit\s*details?|attachments?|uploaded\s*(file|files|document|documents|attachment|attachments)|follow-?up(\s*dates?)?|next\s*visit|return\s*date|record\s*#?\d+|what\s+did\s+(the\s+)?doctor\s+(prescribe|say|write|order|diagnose)|what\s+(was|were|is|are)\s+my\s+(diagnosis|symptoms?|medications?|medicines?|prescriptions?|treatment|care\s+plan))\b";
        return Regex.IsMatch(message, pattern, RegexOptions.IgnoreCase);
    }

    public async Task<string> ReadAsync(string message, PatientDto patient, CancellationToken cancellationToken)
    {
        var records = await _recordRepository.GetByPatientIdAsync(patient.PatientId);
        // Patient-facing AI explains clinician-finalized records (both with and without attachments).
        // Drafts are incomplete and cannot be presented as verified medical guidance.
        var recordList = records == null
            ? []
            : records.Where(record => record.Status == Models.MedicalRecordStatuses.Finalized).ToList();

        if (recordList.Count == 0)
        {
            return $"Hello {patient.FullName}, you currently do not have any finalized medical records available for explanation. " +
                   "Draft or unavailable information cannot be presented as verified medical guidance.";
        }

        var analysis = await _intelligenceAgent.AnalyzeRecordsAsync(
            recordList,
            patient.FullName,
            message,
            cancellationToken);

        return analysis.PlainLanguageSummary;
    }
}
