using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
using HospitalManagementSystem.Api.AgenticAI.MedicalReports;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public sealed class MedicalReportAssistantAgentTests
{
    [Fact]
    public void Capability_ExposesCorrectIdAndPrompt()
    {
        var agent = new MedicalReportAssistantAgent(new FakeMedicalRecordRepository([]), new FakeIntelligenceAgent());
        
        Assert.Equal("medical-reports", agent.Capability.Id);
        Assert.Equal("Medical Reports", agent.Capability.Label);
        Assert.True(agent.Capability.Enabled);
        Assert.Equal("Summarize my medical reports", agent.Capability.Prompt);
    }

    [Theory]
    [InlineData("Summarize my medical reports", true)]
    [InlineData("Can you summarize my medical report?", true)]
    [InlineData("Show my medical records", true)]
    [InlineData("Explain my lab reports", true)]
    [InlineData("What are my lab results?", true)]
    [InlineData("What medications did the doctor prescribe?", true)]
    [InlineData("Tell me about my prescriptions", true)]
    [InlineData("Explain my medicines", true)]
    [InlineData("What is my diagnosis?", true)]
    [InlineData("Show my discharge summary", true)]
    [InlineData("What do the doctor notes say?", true)]
    [InlineData("My blood tests summary", true)]
    [InlineData("Book an appointment tomorrow", false)]
    [InlineData("What doctors work in Cardiology?", false)]
    [InlineData("What doctors work in General Medicine?", false)]
    [InlineData("Show me the General Medicine doctors", false)]
    [InlineData("Show my appointments", false)]
    [InlineData("Hello", false)]
    [InlineData("", false)]
    public void CanHandle_MatchesRelevantClinicalInquiries(string input, bool expected)
    {
        var agent = new MedicalReportAssistantAgent(new FakeMedicalRecordRepository([]), new FakeIntelligenceAgent());
        Assert.Equal(expected, agent.CanHandle(input));
    }

    [Fact]
    public async Task ReadAsync_WhenNoRecordsExist_ReturnsPatientGuidance()
    {
        var repo = new FakeMedicalRecordRepository([]);
        var agent = new MedicalReportAssistantAgent(repo, new FakeIntelligenceAgent());
        var patient = new PatientDto { PatientId = 1, FirstName = "Kasun", LastName = "Perera" };

        var reply = await agent.ReadAsync("Summarize my medical reports", patient, CancellationToken.None);

        Assert.Contains("Kasun Perera", reply);
        Assert.Contains("currently do not have any finalized medical records", reply);
    }

    [Fact]
    public async Task ReadAsync_WhenRecordsExist_InvokesIntelligenceAgent()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 10,
            PatientId = 1,
            RecordDate = DateTime.UtcNow.AddDays(-2),
            Diagnosis = "Acute Bronchitis",
            Symptoms = "Cough, fever",
            TreatmentPlan = "Rest and hydration",
            PrescriptionNotes = "Amoxicillin 500mg TDS for 5 days"
        };
        var repo = new FakeMedicalRecordRepository([record]);
        var fakeAi = new FakeIntelligenceAgent();
        var agent = new MedicalReportAssistantAgent(repo, fakeAi);
        var patient = new PatientDto { PatientId = 1, FirstName = "Kasun", LastName = "Perera" };

        var reply = await agent.ReadAsync("Summarize my medical reports", patient, CancellationToken.None);

        Assert.Equal("Analysis for Kasun Perera: 1 records", reply);
        Assert.Single(fakeAi.ObservedRecords);
        Assert.Equal("Kasun Perera", fakeAi.ObservedPatientName);
    }

    [Fact]
    public async Task ReadAsync_ExcludesDraftRecords()
    {
        var records = new[]
        {
            new MedicalRecord { MedicalRecordId = 1, PatientId = 1, Status = MedicalRecordStatuses.Draft },
            new MedicalRecord { MedicalRecordId = 3, PatientId = 1, Status = MedicalRecordStatuses.Finalized }
        };
        var fakeAi = new FakeIntelligenceAgent();
        var agent = new MedicalReportAssistantAgent(new FakeMedicalRecordRepository(records), fakeAi);
        var patient = new PatientDto { PatientId = 1, FirstName = "Kasun", LastName = "Perera" };

        await agent.ReadAsync("Summarize my medical records", patient, CancellationToken.None);

        var visible = Assert.Single(fakeAi.ObservedRecords);
        Assert.Equal(3, visible.MedicalRecordId);
    }

    [Fact]
    public void DeterministicSafetyEngine_DoesNotInventMedicationDirections()
    {
        var records = new[]
        {
            new MedicalRecord
            {
                MedicalRecordId = 1,
                PrescriptionNotes = "Amoxicillin 500mg, Ibuprofen and Aspirin",
                TreatmentPlan = "Pain management"
            }
        };

        var alerts = DeterministicClinicalSafetyEngine.EvaluateSafety(records);

        Assert.Empty(alerts);
    }

    [Fact]
    public void DeterministicSafetyEngine_AnswersPrescriptionQuestionFromRecordedTextOnly()
    {
        var records = new[]
        {
            new MedicalRecord
            {
                MedicalRecordId = 1,
                RecordDate = new DateTime(2026, 9, 10),
                Diagnosis = "Viral infection",
                PrescriptionNotes = "Paracetamol 500mg 2 tabs PRN for fever",
                TreatmentPlan = "Rest"
            }
        };

        var result = DeterministicClinicalSafetyEngine.BuildHeuristicSummary(
            records, "Nimal", "What medicine did my doctor prescribe?");

        Assert.Contains("Paracetamol 500mg 2 tabs PRN for fever", result.PlainLanguageSummary);
        Assert.DoesNotContain("4,000", result.PlainLanguageSummary);
        Assert.Contains("not a diagnosis or a new prescription", result.PlainLanguageSummary);
    }

    [Fact]
    public void DeterministicSafetyEngine_EvaluatesFollowUpDates()
    {
        var upcoming = new MedicalRecord
        {
            MedicalRecordId = 1,
            FollowUpDate = DateTime.UtcNow.AddDays(3),
            TreatmentPlan = "Review wound healing"
        };
        var alertsUpcoming = DeterministicClinicalSafetyEngine.EvaluateSafety([upcoming]);
        Assert.Contains(alertsUpcoming, a => a.Severity == "Reminder" && a.Message.Contains("in 3 days"));

        var overdue = new MedicalRecord
        {
            MedicalRecordId = 2,
            FollowUpDate = DateTime.UtcNow.AddDays(-5),
            TreatmentPlan = "Routine follow up"
        };
        var alertsOverdue = DeterministicClinicalSafetyEngine.EvaluateSafety([overdue]);
        Assert.Contains(alertsOverdue, a => a.Severity == "Advisory" && a.Message.Contains("recorded by your clinician"));
    }

    [Fact]
    public void DeterministicSafetyEngine_BuildHeuristicSummary_FormatsFullClinicalReport()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 10),
            RecordType = MedicalRecordTypes.Consultation,
            Doctor = new Doctor { FirstName = "Saman", LastName = "Jayasinghe" },
            Diagnosis = "Streptococcal Pharyngitis",
            Symptoms = "Sore throat, fever",
            TreatmentPlan = "Oral antibiotic therapy and salt water gargle",
            PrescriptionNotes = "Amoxicillin 500mg TDS for 7 days; Paracetamol 500mg PRN",
            LabNotes = "Throat swab culture: Streptococcus pyogenes confirmed positive",
            FollowUpDate = DateTime.UtcNow.AddDays(4)
        };

        var summary = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Nimal Bandara", null);

        Assert.False(summary.UsedGemini);
        Assert.Contains("Dr. Saman Jayasinghe", summary.PlainLanguageSummary);
        Assert.Contains("Streptococcal Pharyngitis", summary.PlainLanguageSummary);
        Assert.Contains("Amoxicillin 500mg", summary.PlainLanguageSummary);
        Assert.Contains("Streptococcus pyogenes", summary.PlainLanguageSummary);
        Assert.Contains("Recorded follow-up reminders", summary.PlainLanguageSummary);
        Assert.Contains("Guidance disclaimer", summary.PlainLanguageSummary);
    }

    [Fact]
    public void AssistantAgentRegistry_ExposesMedicalReportsCapabilityWhenAgentPresent()
    {
        var readAgent = new MedicalReportAssistantAgent(new FakeMedicalRecordRepository([]), new FakeIntelligenceAgent());
        var registry = new AssistantAgentRegistry([readAgent]);

        var capabilities = registry.Capabilities;
        var medReportsCap = Assert.Single(capabilities, c => c.Id == "medical-reports");
        
        Assert.True(medReportsCap.Enabled);
        Assert.Equal("Medical Reports", medReportsCap.Label);
        Assert.Equal("Summarize my medical reports", medReportsCap.Prompt);
    }

    [Fact]
    public async Task GeminiMedicalRecordClient_FallsBackToDeterministic_WhenApiKeyEmpty()
    {
        var inMemoryConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = ""
            })
            .Build();

        using var http = new System.Net.Http.HttpClient();
        var client = new GeminiMedicalRecordClient(http, inMemoryConfig, NullLogger<GeminiMedicalRecordClient>.Instance);

        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            Diagnosis = "Essential Hypertension",
            TreatmentPlan = "Lifestyle modification",
            PrescriptionNotes = "Amlodipine 5mg OD"
        };

        var result = await client.AnalyzeRecordsAsync([record], "Sunil", null);

        Assert.False(result.UsedGemini);
        Assert.Contains("Essential Hypertension", result.PlainLanguageSummary);
        Assert.Contains("Amlodipine 5mg OD", result.PlainLanguageSummary);
    }

    [Fact]
    public void DeterministicSafetyEngine_AnswersSymptomsFromTypedMedicalData_WithoutFileAttachments()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 15),
            Diagnosis = "Acute Bronchitis",
            Symptoms = "Persistent dry cough, mild wheezing and chest tightness",
            TreatmentPlan = "Hydration, bronchodilator inhaler as needed",
            PrescriptionNotes = "Salbutamol 100mcg inhaler",
            Attachments = []
        };

        var result = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Kamal", "What are my symptoms?");

        Assert.Contains("Persistent dry cough, mild wheezing and chest tightness", result.PlainLanguageSummary);
    }

    [Fact]
    public void DeterministicSafetyEngine_AnswersTreatmentPlanFromTypedMedicalData_WithoutFileAttachments()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 15),
            Diagnosis = "Gastritis",
            TreatmentPlan = "Avoid spicy foods, eat smaller frequent meals, take PPI before breakfast",
            PrescriptionNotes = "Omeprazole 20mg daily",
            Attachments = []
        };

        var result = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Kamal", "What is my treatment plan?");

        Assert.Contains("Avoid spicy foods, eat smaller frequent meals", result.PlainLanguageSummary);
    }

    [Fact]
    public void DeterministicSafetyEngine_AnswersTypedRecordDetails_WhenQueried()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 15),
            Diagnosis = "Type 2 Diabetes Mellitus",
            Symptoms = "Polydipsia, increased fatigue",
            TreatmentPlan = "Diet control and regular exercise",
            PrescriptionNotes = "Metformin 500mg BD",
            LabNotes = "HbA1c: 7.2%",
            Attachments = []
        };

        var result = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Kamal", "Tell me my medical record details from typed data");

        Assert.Contains("Type 2 Diabetes Mellitus", result.PlainLanguageSummary);
        Assert.Contains("Polydipsia, increased fatigue", result.PlainLanguageSummary);
        Assert.Contains("Diet control and regular exercise", result.PlainLanguageSummary);
        Assert.Contains("Metformin 500mg BD", result.PlainLanguageSummary);
        Assert.Contains("HbA1c: 7.2%", result.PlainLanguageSummary);
    }

    [Fact]
    public void DeterministicSafetyEngine_NonMedicalAttachment_MentionsNonRelevantFile_AndSuppliesTypedMedicalData()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 15),
            Diagnosis = "Migraine with Aura",
            Symptoms = "Throbbing unilateral headache, photophobia",
            TreatmentPlan = "Rest in a dark room, maintain sleep schedule",
            PrescriptionNotes = "Sumatriptan 50mg PRN",
            Attachments =
            [
                new MedicalRecordAttachment
                {
                    AttachmentId = 10,
                    FileName = "hotel_receipt.pdf",
                    FileType = "application/pdf"
                }
            ]
        };

        // When user asks for a general summary
        var summaryResult = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Kamal", null);
        Assert.Contains("hotel_receipt.pdf", summaryResult.PlainLanguageSummary);
        Assert.Contains("does not appear to be a relevant medical document", summaryResult.PlainLanguageSummary);
        Assert.Contains("Migraine with Aura", summaryResult.PlainLanguageSummary);
        Assert.Contains("Sumatriptan 50mg", summaryResult.PlainLanguageSummary);

        // When user asks specifically about the attachment / uploaded file
        var attachResult = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Kamal", "What did I upload in my attachment?");
        Assert.Contains("hotel_receipt.pdf", attachResult.PlainLanguageSummary);
        Assert.Contains("does not appear to be a relevant medical document", attachResult.PlainLanguageSummary);
        Assert.Contains("Migraine with Aura", attachResult.PlainLanguageSummary);
        Assert.Contains("Rest in a dark room", attachResult.PlainLanguageSummary);
    }

    [Fact]
    public void DeterministicSafetyEngine_UploadQueryOnRecordWithoutAttachments_ExplainsPureTypedData()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 15),
            Diagnosis = "Hypertension",
            TreatmentPlan = "Low sodium diet",
            PrescriptionNotes = "Losartan 50mg daily",
            Attachments = []
        };

        var result = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Kamal", "What is my attached file?");

        Assert.Contains("does not have any attached files", result.PlainLanguageSummary);
        Assert.Contains("Hypertension", result.PlainLanguageSummary);
        Assert.Contains("Low sodium diet", result.PlainLanguageSummary);
    }

    [Theory]
    [InlineData("What are the details of my typed medical data?")]
    [InlineData("Show me my medical record details")]
    [InlineData("Can you tell me about my treatment plan?")]
    [InlineData("What are my recorded symptoms?")]
    [InlineData("What was my diagnosis and what medications did the doctor prescribe?")]
    [InlineData("What did the doctor prescribe?")]
    [InlineData("Explain my latest lab results and cholesterol findings")]
    [InlineData("What did I upload for my medical record?")]
    [InlineData("Summarize my medical records")]
    public void MedicalReportAssistantAgent_CanHandle_RecognizesTypedDataAndAttachmentQueries(string query)
    {
        var agent = new MedicalReportAssistantAgent(new FakeMedicalRecordRepository([]), new FakeIntelligenceAgent());
        Assert.True(agent.CanHandle(query));
    }

    [Fact]
    public void DeterministicSafetyEngine_CompoundDiagnosisAndMedicationsQuery_AnswersBoth()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 15),
            Diagnosis = "Essential Hypertension",
            Symptoms = "Occasional morning dizziness",
            TreatmentPlan = "DASH diet, 30 min daily walking",
            PrescriptionNotes = "Amlodipine 5mg OD, Telmisartan 40mg OD"
        };

        var result = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Kamal", "What was my diagnosis and what medications did the doctor prescribe?");

        Assert.Contains("Essential Hypertension", result.PlainLanguageSummary);
        Assert.Contains("Amlodipine 5mg OD", result.PlainLanguageSummary);
        Assert.Contains("Telmisartan 40mg OD", result.PlainLanguageSummary);
    }

    [Fact]
    public void DeterministicSafetyEngine_RecordedSymptomsQuery_AnswersSymptoms()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 15),
            Diagnosis = "Acute Bronchitis",
            Symptoms = "Dry cough, mild fever, sore throat",
            TreatmentPlan = "Hydration, warm fluids, steam inhalation",
            PrescriptionNotes = "Paracetamol 500mg SOS"
        };

        var result = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Kamal", "What are my recorded symptoms?");

        Assert.Contains("Dry cough, mild fever, sore throat", result.PlainLanguageSummary);
    }

    [Fact]
    public async Task DeterministicEngine_WhenQueryAsksAboutLabAttachment_AcknowledgesDocument()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 2,
            PatientId = 1,
            RecordDate = DateTime.UtcNow,
            Diagnosis = "Hyperlipidemia",
            TreatmentPlan = "Diet and exercise",
            LabNotes = "Cholesterol: 218 mg/dL; LDL: 138 mg/dL",
            Status = MedicalRecordStatuses.Finalized,
            Attachments =
            [
                new MedicalRecordAttachment
                {
                    AttachmentId = 10,
                    MedicalRecordId = 2,
                    FileName = "blood_test_lab_report.pdf",
                    FileType = "application/pdf",
                    FileUrl = "https://example.com/blood_test_lab_report.pdf"
                }
            ]
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = ""
        }).Build();

        using var http = new HttpClient();
        var client = new GeminiMedicalRecordClient(http, config, NullLogger<GeminiMedicalRecordClient>.Instance);
        var result = await client.AnalyzeRecordsAsync(
            [record],
            "Test Patient",
            "what is Serum Creatinine finding in my last medical record attachment");

        Assert.NotNull(result);
        Assert.Contains("blood_test_lab_report.pdf", result.PlainLanguageSummary);
    }

    [Fact]
    public async Task DeterministicEngine_WhenQueryAsksSummaryOfLastRecord_FocusesOnlyOnLastRecord()
    {
        var record1 = new MedicalRecord
        {
            MedicalRecordId = 31,
            PatientId = 1,
            RecordDate = new DateTime(2026, 9, 28),
            Diagnosis = "Essential Hypertension",
            TreatmentPlan = "Low sodium diet",
            PrescriptionNotes = "Amlodipine 5mg",
            Status = MedicalRecordStatuses.Finalized
        };

        var record2 = new MedicalRecord
        {
            MedicalRecordId = 32,
            PatientId = 1,
            RecordDate = new DateTime(2026, 9, 28),
            Diagnosis = "Clinical Lab Investigation",
            TreatmentPlan = "Exercise",
            LabNotes = "Cholesterol: 218 mg/dL",
            Status = MedicalRecordStatuses.Finalized
        };

        var record3 = new MedicalRecord
        {
            MedicalRecordId = 33,
            PatientId = 1,
            RecordDate = new DateTime(2026, 9, 28),
            Diagnosis = "Gastroesophageal Reflux Disease (GERD)",
            TreatmentPlan = "Elevate head of bed",
            PrescriptionNotes = "Pantoprazole 40mg",
            Status = MedicalRecordStatuses.Finalized
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = ""
        }).Build();

        using var http = new HttpClient();
        var client = new GeminiMedicalRecordClient(http, config, NullLogger<GeminiMedicalRecordClient>.Instance);
        var result = await client.AnalyzeRecordsAsync(
            [record3, record2, record1],
            "Kasun Perera",
            "give summerization regarding last uploaded medical record");

        Assert.NotNull(result);
        Assert.Contains("Record #33", result.PlainLanguageSummary);
        Assert.Contains("Gastroesophageal Reflux Disease", result.PlainLanguageSummary);
        Assert.DoesNotContain("Essential Hypertension", result.PlainLanguageSummary);
        Assert.DoesNotContain("Amlodipine", result.PlainLanguageSummary);
    }

    [Fact]
    public async Task DeterministicEngine_WhenTypedDataIsGarbage_AndMedicalAttachmentPresent_IgnoresGarbageAndSummarizesDocument()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 88,
            PatientId = 1,
            RecordDate = new DateTime(2026, 9, 28),
            Diagnosis = "asdfghjkl12345",
            TreatmentPlan = "qwertyuiop",
            Symptoms = "xyz123 random nonsense",
            PrescriptionNotes = "none",
            Status = MedicalRecordStatuses.Finalized,
            Attachments =
            [
                new MedicalRecordAttachment
                {
                    AttachmentId = 20,
                    MedicalRecordId = 88,
                    FileName = "blood_test_lab_report.pdf",
                    FileType = "application/pdf"
                }
            ]
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = ""
        }).Build();

        using var http = new HttpClient();
        var client = new GeminiMedicalRecordClient(http, config, NullLogger<GeminiMedicalRecordClient>.Instance);
        var result = await client.AnalyzeRecordsAsync(
            [record],
            "Sunil",
            "give summerization regarding last uploaded medical record");

        Assert.NotNull(result);
        Assert.DoesNotContain("asdfghjkl12345", result.PlainLanguageSummary);
        Assert.DoesNotContain("qwertyuiop", result.PlainLanguageSummary);
        Assert.Contains("blood_test_lab_report.pdf", result.PlainLanguageSummary);
        Assert.Contains("placeholder text", result.PlainLanguageSummary);
    }

    [Fact]
    public async Task DeterministicEngine_WhenBothTypedDataAndMedicalAttachmentPresent_SynthesizesBothOptimally()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 89,
            PatientId = 1,
            RecordDate = new DateTime(2026, 9, 28),
            Diagnosis = "Essential Hypertension",
            TreatmentPlan = "DASH diet and walking",
            PrescriptionNotes = "Amlodipine 5mg OD",
            Status = MedicalRecordStatuses.Finalized,
            Attachments =
            [
                new MedicalRecordAttachment
                {
                    AttachmentId = 21,
                    MedicalRecordId = 89,
                    FileName = "fasting_lipid_profile_report.pdf",
                    FileType = "application/pdf"
                }
            ]
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = ""
        }).Build();

        using var http = new HttpClient();
        var client = new GeminiMedicalRecordClient(http, config, NullLogger<GeminiMedicalRecordClient>.Instance);
        var result = await client.AnalyzeRecordsAsync(
            [record],
            "Sunil",
            "give summerization regarding last uploaded medical record");

        Assert.NotNull(result);
        Assert.Contains("Essential Hypertension", result.PlainLanguageSummary);
        Assert.Contains("Amlodipine 5mg OD", result.PlainLanguageSummary);
        Assert.Contains("fasting_lipid_profile_report.pdf", result.PlainLanguageSummary);
    }

    private sealed class FakeMedicalRecordRepository : IMedicalRecordRepository
    {
        private readonly List<MedicalRecord> _records;

        public FakeMedicalRecordRepository(IEnumerable<MedicalRecord> records)
        {
            _records = records.ToList();
        }

        public Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId) =>
            Task.FromResult<IEnumerable<MedicalRecord>>(_records.Where(r => r.PatientId == patientId).ToList());

        public Task<MedicalRecord?> GetByIdAsync(int id) =>
            Task.FromResult(_records.FirstOrDefault(r => r.MedicalRecordId == id));

        public Task<IEnumerable<MedicalRecord>> GetAllAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize) =>
            Task.FromResult<IEnumerable<MedicalRecord>>(_records);

        public Task<int> GetTotalCountAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate) =>
            Task.FromResult(_records.Count);

        public Task<MedicalRecordSummaryDto> GetSummaryAsync(int? doctorId = null) =>
            Task.FromResult(new MedicalRecordSummaryDto());

        public Task<MedicalRecord> CreateAsync(MedicalRecord record)
        {
            _records.Add(record);
            return Task.FromResult(record);
        }

        public Task<MedicalRecord> UpdateAsync(MedicalRecord record) => Task.FromResult(record);

        public Task DeleteAsync(MedicalRecord record)
        {
            _records.Remove(record);
            return Task.CompletedTask;
        }

        public Task<MedicalRecordAttachment> AddAttachmentAsync(MedicalRecordAttachment attachment) => Task.FromResult(attachment);

        public Task<MedicalRecordAttachment?> GetAttachmentByIdAsync(int attachmentId) => Task.FromResult<MedicalRecordAttachment?>(null);

        public Task DeleteAttachmentAsync(MedicalRecordAttachment attachment) => Task.CompletedTask;
    }

    private sealed class FakeIntelligenceAgent : IMedicalRecordIntelligenceAgent
    {
        public List<MedicalRecord> ObservedRecords { get; } = [];
        public string? ObservedPatientName { get; private set; }

        public Task<MedicalReportAnalysisResult> AnalyzeRecordsAsync(
            IEnumerable<MedicalRecord> records,
            string patientName,
            string? specificUserQuery,
            CancellationToken cancellationToken = default)
        {
            ObservedRecords.AddRange(records);
            ObservedPatientName = patientName;
            return Task.FromResult(new MedicalReportAnalysisResult(
                Overview: "Mock Overview",
                KeyDiagnoses: ["Mock Diagnosis"],
                PrescribedMedications: ["Mock Med"],
                LabFindings: [],
                SafetyAlerts: [],
                FollowUpInstructions: null,
                PlainLanguageSummary: $"Analysis for {patientName}: {ObservedRecords.Count} records",
                AgentTrajectoryDescription: "Mock Agent",
                UsedGemini: false
            ));
        }
    }
}
