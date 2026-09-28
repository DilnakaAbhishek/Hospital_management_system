using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.AgenticAI.MedicalReports;

public static class DeterministicClinicalSafetyEngine
{
    public static IReadOnlyList<MedicationAlert> EvaluateSafety(IEnumerable<MedicalRecord> records)
    {
        var alerts = new List<MedicationAlert>();

        foreach (var record in records)
        {
            // Only dates explicitly recorded by a clinician are safe to surface
            // deterministically. Medication advice must never be inferred from a
            // drug name or supplied from a hardcoded rule.
            if (record.FollowUpDate.HasValue)
            {
                var daysUntil = (record.FollowUpDate.Value.Date - DateTime.UtcNow.Date).TotalDays;
                if (daysUntil >= 0 && daysUntil <= 7)
                {
                    alerts.Add(new MedicationAlert(
                        "Reminder",
                        "Clinical Follow-up",
                        $"You have a recommended follow-up review on {record.FollowUpDate.Value:MMM dd, yyyy} (in {(int)daysUntil} days)."
                    ));
                }
                else if (daysUntil < 0)
                {
                    alerts.Add(new MedicationAlert(
                        "Advisory",
                        "Overdue Follow-up",
                        $"The follow-up date recorded by your clinician was {record.FollowUpDate.Value:MMM dd, yyyy}, and that date has passed."
                    ));
                }
            }
        }

        return alerts
            .GroupBy(a => $"{a.Severity}:{a.DrugName}:{a.Message}")
            .Select(g => g.First())
            .ToList();
    }

    public static MedicalReportAnalysisResult BuildHeuristicSummary(
        IEnumerable<MedicalRecord> records,
        string patientName,
        string? userQuery)
    {
        var recordList = records
            .OrderByDescending(r => r.RecordDate.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.MedicalRecordId)
            .ToList();
        if (recordList.Count == 0)
        {
            return new MedicalReportAnalysisResult(
                Overview: "No records found",
                KeyDiagnoses: [],
                PrescribedMedications: [],
                LabFindings: [],
                SafetyAlerts: [],
                FollowUpInstructions: null,
                PlainLanguageSummary: $"Hello {patientName}, you do not have any recorded medical consultations, prescriptions, or lab reports on file yet.",
                AgentTrajectoryDescription: "Retrieved 0 records; returned patient onboarding guidance.",
                UsedGemini: false
            );
        }

        var latest = recordList.First();
        var diagnoses = recordList.Select(r => r.Diagnosis).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().Take(5).ToList();
        var meds = recordList.Select(r => r.PrescriptionNotes).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).Distinct().Take(5).ToList();
        var labNotes = recordList.Select(r => r.LabNotes).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!).Distinct().Take(5).ToList();
        var safetyAlerts = EvaluateSafety(recordList);

        var focused = FocusedAnswer(recordList, userQuery);
        if (focused is not null)
            return new MedicalReportAnalysisResult(
                Overview: $"Verified medical-record answer ({latest.RecordDate:MMM dd, yyyy})",
                KeyDiagnoses: diagnoses,
                PrescribedMedications: meds,
                LabFindings: labNotes,
                SafetyAlerts: safetyAlerts,
                FollowUpInstructions: latest.FollowUpDate?.ToString("MMMM dd, yyyy"),
                PlainLanguageSummary: focused,
                AgentTrajectoryDescription: "Answered from finalized structured medical-record fields using the deterministic grounded fallback.",
                UsedGemini: false);

        var doctorText = latest.Doctor != null ? $"Dr. {latest.Doctor.FirstName} {latest.Doctor.LastName}" : "Hospital Clinician";
        var followUpText = latest.FollowUpDate.HasValue
            ? $"Scheduled for {latest.FollowUpDate.Value:MMMM dd, yyyy}"
            : "No specific follow-up date recorded.";

        var nonMedicalAttachments = recordList
            .Where(r => r.Attachments != null)
            .SelectMany(r => r.Attachments!)
            .Select(a => (Name: a.FileName ?? Path.GetFileName(a.FileUrl ?? "attachment"),
                          Cat: MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category))
            .Where(x => x.Cat == DocumentCategory.NonMedical)
            .Select(x => x.Name)
            .Distinct()
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Here is a clear summary of your medical reports, {patientName}:");
        if (nonMedicalAttachments.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"• Note: The attached file ({string.Join(", ", nonMedicalAttachments)}) does not appear to be a relevant medical document. The clinical details below are provided directly from your clinician's typed medical records.");
        }
        var isDiagGarbage = MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Diagnosis);
        var isPlanGarbage = MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.TreatmentPlan);
        var medAtts = records.Where(r => r.Attachments != null)
            .SelectMany(r => r.Attachments!)
            .Where(a => MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category == DocumentCategory.Medical)
            .Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "document"))
            .Distinct()
            .ToList();

        sb.AppendLine();
        sb.AppendLine($"📋 Latest Clinical Visit ({latest.RecordDate:MMM dd, yyyy})");
        sb.AppendLine($"• Attending Doctor: {doctorText}");
        if (isDiagGarbage && medAtts.Count > 0)
        {
            sb.AppendLine($"• Attached Clinical Document(s): {string.Join(", ", medAtts)}");
            sb.AppendLine("• Note: The typed record fields contained placeholder text; this summary is generated directly from your attached medical document.");
        }
        else
        {
            if (!isDiagGarbage)
                sb.AppendLine($"• Primary Diagnosis: {latest.Diagnosis}");
            if (!string.IsNullOrWhiteSpace(latest.Symptoms) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Symptoms))
            {
                sb.AppendLine($"• Symptoms Addressed: {latest.Symptoms}");
            }
            if (!isPlanGarbage)
                sb.AppendLine($"• Care Plan: {latest.TreatmentPlan}");
            if (medAtts.Count > 0)
            {
                sb.AppendLine($"• Integrated Clinical Document(s): {string.Join(", ", medAtts)}");
            }
        }

        if (meds.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Prescriptions recorded by your clinician");
            foreach (var med in meds)
            {
                sb.AppendLine($"• {med}");
            }
        }

        if (labNotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Lab and diagnostic notes");
            foreach (var lab in labNotes)
            {
                sb.AppendLine($"• {lab}");
            }
        }

        if (safetyAlerts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Recorded follow-up reminders");
            foreach (var alert in safetyAlerts)
            {
                sb.AppendLine($"• [{alert.DrugName}] {alert.Message}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Follow-up status: {followUpText}");
        sb.AppendLine();
        sb.AppendLine("Guidance disclaimer: This explains finalized information already in your record. It is not a diagnosis or a new treatment plan. Follow the instructions recorded by your doctor or pharmacist.");

        return new MedicalReportAnalysisResult(
            Overview: $"Visit on {latest.RecordDate:MMM dd, yyyy} - {latest.Diagnosis}",
            KeyDiagnoses: diagnoses,
            PrescribedMedications: meds,
            LabFindings: labNotes,
            SafetyAlerts: safetyAlerts,
            FollowUpInstructions: followUpText,
            PlainLanguageSummary: sb.ToString().Trim(),
            AgentTrajectoryDescription: "Generated a finalized-record summary with deterministic follow-up-date checks.",
            UsedGemini: false
        );
    }

    private static string? FocusedAnswer(IReadOnlyList<MedicalRecord> records, string? userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery)) return null;
        var query = userQuery.ToLowerInvariant();
        var latest = records[0];
        const string disclaimer = " This is an explanation of the finalized record, not a diagnosis or a new prescription.";

        var isLatestRecordQuery = Regex.IsMatch(query,
            @"\b(last|latest|newest|most\s*recent)\s*(uploaded\s*)?(medical\s*)?(record|report|visit|document|file|upload|attachment|consultation|encounter|summary|summerization)?\b");

        if (isLatestRecordQuery && Regex.IsMatch(query, @"\b(summary|summarize|summerization|detail|details|explain|tell me|overview|about)\b"))
        {
            var doc = latest.Doctor != null ? $"Dr. {latest.Doctor.FirstName} {latest.Doctor.LastName}".Trim() : "Hospital Clinician";
            var nonMed = (latest.Attachments ?? [])
                .Where(a => MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category == DocumentCategory.NonMedical)
                .Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "attachment"))
                .ToList();

            var medAtts = (latest.Attachments ?? [])
                .Where(a => MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category == DocumentCategory.Medical)
                .Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "attachment"))
                .ToList();

            var isTypedGarbage = MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Diagnosis) &&
                                 MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.TreatmentPlan);

            var sbLatest = new System.Text.StringBuilder();

            if (isTypedGarbage && medAtts.Count > 0)
            {
                sbLatest.AppendLine($"Here is the clinical summary for your latest medical record (Record #{latest.MedicalRecordId}, dated {latest.RecordDate:yyyy-MM-dd}):");
                sbLatest.AppendLine($"• Clinician: {doc}");
                sbLatest.AppendLine($"• Encounter Type: {latest.RecordType}");
                sbLatest.AppendLine($"• Attached Clinical Document(s): {string.Join(", ", medAtts)}");
                if (!string.IsNullOrWhiteSpace(latest.LabNotes) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.LabNotes))
                    sbLatest.AppendLine($"• Lab Notes: {latest.LabNotes}");
                if (latest.FollowUpDate.HasValue)
                    sbLatest.AppendLine($"• Follow-up Date: {latest.FollowUpDate.Value:MMMM dd, yyyy}");
                sbLatest.AppendLine();
                sbLatest.AppendLine($"Note: The doctor's typed record fields contained uninformative placeholder text; this summary is generated directly from your uploaded clinical document ({string.Join(", ", medAtts)}).");
            }
            else
            {
                sbLatest.AppendLine($"Here is the summary of your latest medical record (Record #{latest.MedicalRecordId}, dated {latest.RecordDate:yyyy-MM-dd}):");
                sbLatest.AppendLine($"• Clinician: {doc}");
                sbLatest.AppendLine($"• Encounter Type: {latest.RecordType}");
                if (!MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Diagnosis))
                    sbLatest.AppendLine($"• Diagnosis: {latest.Diagnosis}");
                if (!string.IsNullOrWhiteSpace(latest.Symptoms) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Symptoms))
                    sbLatest.AppendLine($"• Symptoms: {latest.Symptoms}");
                if (!string.IsNullOrWhiteSpace(latest.TreatmentPlan) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.TreatmentPlan))
                    sbLatest.AppendLine($"• Care Plan: {latest.TreatmentPlan}");
                if (!string.IsNullOrWhiteSpace(latest.PrescriptionNotes) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.PrescriptionNotes))
                    sbLatest.AppendLine($"• Prescriptions: {latest.PrescriptionNotes}");
                if (!string.IsNullOrWhiteSpace(latest.LabNotes) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.LabNotes))
                    sbLatest.AppendLine($"• Lab Notes: {latest.LabNotes}");
                if (latest.FollowUpDate.HasValue)
                    sbLatest.AppendLine($"• Follow-up Date: {latest.FollowUpDate.Value:MMMM dd, yyyy}");

                if (nonMed.Count > 0)
                {
                    sbLatest.AppendLine();
                    sbLatest.AppendLine($"Note: The attached file ({string.Join(", ", nonMed)}) does not appear to be a relevant medical document. The details above are from your doctor's typed clinical record.");
                }

                if (medAtts.Count > 0)
                {
                    sbLatest.AppendLine();
                    sbLatest.AppendLine($"• Integrated Clinical Document(s): {string.Join(", ", medAtts)}");
                }
            }

            sbLatest.Append(disclaimer);
            return sbLatest.ToString();
        }

        var hasMeds = Regex.IsMatch(query, @"\b(medicine|medicines|medication|medications|prescription|prescriptions|prescribed|prescribe)\b");
        var hasDiagnosis = Regex.IsMatch(query, @"\b(diagnosis|diagnoses|diagnosed)\b");
        var hasSymptoms = Regex.IsMatch(query, @"\b(symptoms?|feeling|complaints?)\b");

        // Compound query: Diagnosis + Medications
        if (hasDiagnosis && hasMeds)
        {
            var isDiagGarbage = MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Diagnosis);
            var diagText = isDiagGarbage
                ? $"No specific diagnosis was recorded for the visit on {latest.RecordDate:yyyy-MM-dd}."
                : $"The finalized record dated {latest.RecordDate:yyyy-MM-dd} lists your diagnosis as: {latest.Diagnosis}.";

            var medEntries = records.Where(record => !string.IsNullOrWhiteSpace(record.PrescriptionNotes) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(record.PrescriptionNotes))
                .Select(record => $"{record.RecordDate:yyyy-MM-dd}: {record.PrescriptionNotes}").Take(5).ToArray();
            var medText = medEntries.Length == 0
                ? "No prescription details are recorded in your finalized medical records."
                : "The following prescription information is recorded:\n- " + string.Join("\n- ", medEntries);

            return $"{diagText}\n\n{medText}{disclaimer}";
        }

        // Compound query: Diagnosis + Symptoms
        if (hasDiagnosis && hasSymptoms)
        {
            var isDiagGarbage = MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Diagnosis);
            var diagText = isDiagGarbage
                ? $"No specific diagnosis was recorded for the visit on {latest.RecordDate:yyyy-MM-dd}."
                : $"The finalized record dated {latest.RecordDate:yyyy-MM-dd} lists your diagnosis as: {latest.Diagnosis}.";

            var symEntries = records.Where(record => !string.IsNullOrWhiteSpace(record.Symptoms) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(record.Symptoms))
                .Select(record => $"{record.RecordDate:yyyy-MM-dd}: {record.Symptoms}").Take(5).ToArray();
            var symText = symEntries.Length == 0
                ? "No specific symptoms were recorded for your visits."
                : "The following symptoms are recorded in your clinical notes:\n- " + string.Join("\n- ", symEntries);

            return $"{diagText}\n\n{symText}{disclaimer}";
        }

        if (hasMeds)
        {
            var entries = records.Where(record => !string.IsNullOrWhiteSpace(record.PrescriptionNotes) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(record.PrescriptionNotes))
                .Select(record => $"{record.RecordDate:yyyy-MM-dd}: {record.PrescriptionNotes}").Take(5).ToArray();
            return entries.Length == 0
                ? "No prescription details are recorded in your finalized medical records. Please confirm medication instructions with your doctor or pharmacist."
                : "The following prescription information is recorded:\n- " + string.Join("\n- ", entries) + disclaimer;
        }
        if (Regex.IsMatch(query, @"\b(lab|laboratory|blood test|test result|results|cholesterol|lipid|panel|findings?|creatinine|glucose|hemoglobin|platelet|wbc|rbc)\b"))
        {
            var entries = records.Where(record => !string.IsNullOrWhiteSpace(record.LabNotes) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(record.LabNotes))
                .Select(record => $"{record.RecordDate:yyyy-MM-dd}: {record.LabNotes}").Take(5).ToArray();

            var medAtts = records.Where(r => r.Attachments != null)
                .SelectMany(r => r.Attachments!)
                .Where(a => MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category == DocumentCategory.Medical)
                .Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "document"))
                .Distinct()
                .ToList();

            var attNote = medAtts.Count > 0
                ? $"\n\nAttached clinical report(s): {string.Join(", ", medAtts)}. Specific laboratory values and biological reference intervals are documented in the attached clinical report."
                : "";

            return entries.Length == 0
                ? $"No lab result details or reference ranges are recorded in your typed notes.{attNote}{disclaimer}"
                : "The following lab information is recorded:\n- " + string.Join("\n- ", entries) + $"{attNote}{disclaimer}";
        }
        if (hasDiagnosis)
        {
            var isDiagGarbage = MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Diagnosis);
            var medAtts = (latest.Attachments ?? [])
                .Where(a => MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category == DocumentCategory.Medical)
                .Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "attachment"))
                .ToList();

            if (isDiagGarbage)
            {
                return medAtts.Count > 0
                    ? $"No specific diagnosis was typed for the visit on {latest.RecordDate:yyyy-MM-dd}. Please refer to the attached clinical document: {string.Join(", ", medAtts)}.{disclaimer}"
                    : $"No specific diagnosis was recorded for the finalized visit on {latest.RecordDate:yyyy-MM-dd}.{disclaimer}";
            }

            return $"The finalized record dated {latest.RecordDate:yyyy-MM-dd} lists the diagnosis as: {latest.Diagnosis}.{disclaimer}";
        }
        if (Regex.IsMatch(query, @"\b(follow-up|follow up|return|next visit)\b"))
        {
            var targetRec = latest;
            var idMatch = Regex.Match(query, @"\brecord\s*#?\s*(\d+)\b");
            if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out var recId))
            {
                var found = records.FirstOrDefault(r => r.MedicalRecordId == recId);
                if (found != null) targetRec = found;
            }
            else
            {
                var condRec = records.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Diagnosis) && query.Contains(r.Diagnosis.ToLowerInvariant()));
                if (condRec != null) targetRec = condRec;
            }

            return targetRec.FollowUpDate.HasValue
                ? $"The finalized record for {targetRec.Diagnosis} (Record #{targetRec.MedicalRecordId}, dated {targetRec.RecordDate:yyyy-MM-dd}) lists a follow-up date of {targetRec.FollowUpDate.Value:MMMM dd, yyyy}.{disclaimer}"
                : $"No follow-up date is recorded in the finalized medical record for {targetRec.Diagnosis} (Record #{targetRec.MedicalRecordId}).{disclaimer}";
        }

        if (hasSymptoms)
        {
            var entries = records.Where(record => !string.IsNullOrWhiteSpace(record.Symptoms))
                .Select(record => $"{record.RecordDate:yyyy-MM-dd}: {record.Symptoms}").Take(5).ToArray();
            return entries.Length == 0
                ? "No specific symptoms were recorded for your visits."
                : "The following symptoms are recorded in your clinical notes:\n- " + string.Join("\n- ", entries) + disclaimer;
        }

        if (Regex.IsMatch(query, @"\b(treatment|treatment plan|care plan|therapy|management|advice)\b"))
        {
            var entries = records.Where(record => !string.IsNullOrWhiteSpace(record.TreatmentPlan))
                .Select(record => $"{record.RecordDate:yyyy-MM-dd}: {record.TreatmentPlan}").Take(5).ToArray();
            return entries.Length == 0
                ? "No specific treatment plan was recorded for your visits."
                : "The following treatment plan is recorded in your clinical notes:\n- " + string.Join("\n- ", entries) + disclaimer;
        }

        if (Regex.IsMatch(query, @"\b(doctor|physician|clinician|who treated|who saw)\b"))
        {
            var doc = latest.Doctor != null ? $"Dr. {latest.Doctor.FirstName} {latest.Doctor.LastName}".Trim() : "Hospital Clinician";
            return $"Your attending clinician for the visit on {latest.RecordDate:yyyy-MM-dd} was {doc}.{disclaimer}";
        }

        if (Regex.IsMatch(query, @"\b(attachment|attachments|uploaded|upload|file|files|document|documents|photo|image|receipt|invoice|png|jpg|pdf)\b"))
        {
            var allAtts = records.Where(r => r.Attachments != null).SelectMany(r => r.Attachments!).ToList();
            if (allAtts.Count == 0)
            {
                return $"Your medical record does not have any attached files. It consists of verified typed clinical data:\n" +
                       $"- Visit Date: {latest.RecordDate:yyyy-MM-dd}\n" +
                       $"- Diagnosis: {latest.Diagnosis}\n" +
                       $"- Care Plan: {latest.TreatmentPlan}" +
                       (!string.IsNullOrWhiteSpace(latest.PrescriptionNotes) ? $"\n- Prescriptions: {latest.PrescriptionNotes}" : "") +
                       disclaimer;
            }

            var nonMed = allAtts.Where(a => MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category == DocumentCategory.NonMedical).ToList();
            if (nonMed.Count > 0)
            {
                var names = string.Join(", ", nonMed.Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "attachment")));
                var isPlural = nonMed.Count > 1;
                var fileLabel = isPlural ? "file(s)" : "file";
                var desc = isPlural ? "do not appear to be relevant medical documents" : "does not appear to be a relevant medical document";
                return $"The attached {fileLabel} ({names}) {desc}. However, your medical record contains the following verified typed clinical data:\n" +
                       $"- Visit Date: {latest.RecordDate:yyyy-MM-dd}\n" +
                       $"- Diagnosis: {latest.Diagnosis}\n" +
                       $"- Care Plan: {latest.TreatmentPlan}" +
                       (!string.IsNullOrWhiteSpace(latest.PrescriptionNotes) ? $"\n- Prescriptions: {latest.PrescriptionNotes}" : "") +
                       disclaimer;
            }

            var medNames = string.Join(", ", allAtts.Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "attachment")));
            var isTypedGarbage = MedicalDocumentValidator.IsGarbageOrPlaceholder(latest.Diagnosis);
            if (isTypedGarbage)
            {
                return $"Your medical record includes the following clinical attachment(s): {medNames}.\n" +
                       $"Note: The doctor's typed fields contained uninformative placeholder text; clinical details are retrieved directly from your attached medical document." +
                       disclaimer;
            }
            return $"Your medical record includes the following clinical attachment(s): {medNames}. The typed clinical data recorded is:\n" +
                   $"- Visit Date: {latest.RecordDate:yyyy-MM-dd}\n" +
                   $"- Diagnosis: {latest.Diagnosis}\n" +
                   $"- Care Plan: {latest.TreatmentPlan}" +
                   disclaimer;
        }

        if (Regex.IsMatch(query, @"\b(typed|detail|details|record|records|report|reports|summary|summarize|history|visit)\b"))
        {
            var targetRec = latest;
            var idMatch = Regex.Match(query, @"\brecord\s*#?\s*(\d+)\b");
            if (idMatch.Success && int.TryParse(idMatch.Groups[1].Value, out var recId))
            {
                var found = records.FirstOrDefault(r => r.MedicalRecordId == recId);
                if (found != null) targetRec = found;
            }
            else
            {
                var condRec = records.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Diagnosis) && query.Contains(r.Diagnosis.ToLowerInvariant()));
                if (condRec != null) targetRec = condRec;
            }

            var doc = targetRec.Doctor != null ? $"Dr. {targetRec.Doctor.FirstName} {targetRec.Doctor.LastName}".Trim() : "Hospital Clinician";
            var isGarbage = MedicalDocumentValidator.IsGarbageOrPlaceholder(targetRec.Diagnosis);
            var headerDesc = isGarbage ? "your visit" : targetRec.Diagnosis;

            var sbTyped = new System.Text.StringBuilder();
            sbTyped.AppendLine($"Here are the details from your clinical record for {headerDesc} (Record #{targetRec.MedicalRecordId}, dated {targetRec.RecordDate:yyyy-MM-dd}):");
            sbTyped.AppendLine($"• Clinician: {doc}");
            if (!isGarbage)
                sbTyped.AppendLine($"• Diagnosis: {targetRec.Diagnosis}");
            if (!string.IsNullOrWhiteSpace(targetRec.Symptoms) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(targetRec.Symptoms))
                sbTyped.AppendLine($"• Symptoms: {targetRec.Symptoms}");
            if (!string.IsNullOrWhiteSpace(targetRec.TreatmentPlan) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(targetRec.TreatmentPlan))
                sbTyped.AppendLine($"• Care Plan: {targetRec.TreatmentPlan}");
            if (!string.IsNullOrWhiteSpace(targetRec.PrescriptionNotes) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(targetRec.PrescriptionNotes))
                sbTyped.AppendLine($"• Prescriptions: {targetRec.PrescriptionNotes}");
            if (!string.IsNullOrWhiteSpace(targetRec.LabNotes) && !MedicalDocumentValidator.IsGarbageOrPlaceholder(targetRec.LabNotes))
                sbTyped.AppendLine($"• Lab Notes: {targetRec.LabNotes}");
            if (targetRec.FollowUpDate.HasValue)
                sbTyped.AppendLine($"• Follow-up Date: {targetRec.FollowUpDate.Value:MMMM dd, yyyy}");
            else
                sbTyped.AppendLine("• Follow-up Date: None recorded");

            var targetMedAtts = (targetRec.Attachments ?? [])
                .Where(a => MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category == DocumentCategory.Medical)
                .Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "attachment"))
                .ToList();
            if (targetMedAtts.Count > 0)
            {
                sbTyped.AppendLine();
                sbTyped.AppendLine($"• Attached Clinical Document(s): {string.Join(", ", targetMedAtts)}");
            }

            var nonMed = (targetRec.Attachments ?? [])
                .Where(a => MedicalDocumentValidator.Classify(a.FileName ?? a.FileUrl ?? "", a.FileType ?? "").Category == DocumentCategory.NonMedical)
                .Select(a => a.FileName ?? Path.GetFileName(a.FileUrl ?? "attachment"))
                .ToList();
            if (nonMed.Count > 0)
            {
                sbTyped.AppendLine();
                sbTyped.AppendLine($"Note: The attached file ({string.Join(", ", nonMed)}) does not appear to be a relevant medical document. The details above are from your doctor's typed clinical record.");
            }

            sbTyped.Append(disclaimer);
            return sbTyped.ToString();
        }

        return null;
    }
}
