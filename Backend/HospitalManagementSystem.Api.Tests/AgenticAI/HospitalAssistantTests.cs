using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public sealed class HospitalAssistantTests
{
    [Theory]
    [InlineData("button")]
    [InlineData("cancel")]
    [InlineData("never mind")]
    public async Task DismissalClearsBookingStateAndCannotCaptureLaterMessages(string dismissal)
    {
        await using var h = await Harness.Create();
        var first = await h.Send("Book a cardiologist tomorrow afternoon");
        var action = first.PendingAction!;
        var dismissed = dismissal == "button"
            ? await h.Service.DecideAsync(h.Patient, first.ConversationId, new() {
                ActionId = action.ActionId, Decision = "cancel", RequestId = Guid.NewGuid() }, default)
            : await h.Send(dismissal, first.ConversationId);
        Assert.Null(dismissed.PendingAction);
        Assert.Empty(dismissed.Slots);
        Assert.Empty(dismissed.Doctors);
        var saved = SavedState(await h.Db.AssistantConversations.SingleAsync());
        Assert.False(saved.WantsAppointment);
        Assert.False(saved.AvailabilityChecked);
        Assert.Null(saved.Awaiting);
        Assert.Null(saved.ActiveTask);
        Assert.Null(saved.SearchQuery);
        Assert.Null(saved.PreferredDate);
        Assert.Equal("Dismissed", dismissed.Messages.Single(m => m.ProposedAction != null).ProposedAction!.Status);
        Assert.Equal("Cancelled", (await h.Db.AppointmentProposals.SingleAsync()).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.DecideAsync(h.Patient, first.ConversationId, h.Confirm(action), default));
        var unrelated = await h.Send("Tell me something else", first.ConversationId);
        Assert.Null(unrelated.PendingAction);
        Assert.Empty(unrelated.Slots);
        var symptom = await h.Send("I have a mild headache", first.ConversationId);
        Assert.True(symptom.AssessmentInputActive);
        Assert.Null(symptom.PendingAction);
        Assert.Empty(symptom.Slots);
        Assert.Single(await h.Db.AppointmentProposals.ToListAsync());
    }

    [Theory]
    [InlineData("I have a mild headache")]
    [InlineData("I have a cough")]
    [InlineData("I feel sick and nauseated")]
    public async Task SymptomsSupersedeAnActiveBooking(string symptom)
    {
        await using var h = await Harness.Create();
        var booking = await h.Send("Book a cardiologist");
        var reply = await h.Send(symptom, booking.ConversationId);
        Assert.Null(reply.PendingAction);
        Assert.Empty(reply.Slots);
        Assert.Single(await h.Db.TriageWorkflows.ToListAsync());
        Assert.Single(await h.Db.AppointmentProposals.ToListAsync());
        Assert.False(SavedState(await h.Db.AssistantConversations.SingleAsync()).WantsAppointment);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("confirm this appointment")]
    [InlineData("select this")]
    public async Task AppointmentContinuationKeepsTheProposalUntilExplicitConfirmation(string text)
    {
        await using var h = await Harness.Create();
        var booking = await h.Send("Book a cardiologist");
        var reply = await h.Send(text, booking.ConversationId);
        Assert.Equal(booking.PendingAction!.ActionId, reply.PendingAction!.ActionId);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
        var confirmed = await h.Service.DecideAsync(h.Patient, booking.ConversationId, h.Confirm(reply.PendingAction), default);
        Assert.Single(confirmed.Appointments);
        Assert.Null(SavedState(await h.Db.AssistantConversations.SingleAsync()).SearchQuery);
    }

    [Fact]
    public async Task AnsweredQuestionIsPersistedBesideTheAnswerAndNextQuestionIsSeparate()
    {
        await using var h = await Harness.Create();
        var start = await h.Send("I have a mild headache");
        var next = await h.Send("Gradually", start.ConversationId);
        var answer = next.Messages.Last(m => m.Role == "user");
        Assert.Equal(start.Questions.Single().Prompt, answer.FollowUpQuestion!.Prompt);
        Assert.Equal("Answered", answer.FollowUpState.ToString());
        Assert.Equal("Gradually", answer.Text);
        Assert.NotEqual(answer.FollowUpQuestion.Id, Assert.Single(next.Questions).Id);
        var loaded = await h.Service.GetAsync(1, start.ConversationId, default);
        Assert.Equal(answer.FollowUpQuestion.Prompt, loaded.Messages.Last(m => m.Role == "user").FollowUpQuestion!.Prompt);
    }

    [Fact]
    public async Task ShortNumericFreeTextAnswerIsNotRejectedByAnArbitraryLengthRule()
    {
        await using var h = await Harness.Create();
        var current = await h.Send("I have a mild headache");
        current = await h.Send("Gradually", current.ConversationId);
        current = await h.Send("stable", current.ConversationId);
        Assert.Equal("severity_score", Assert.Single(current.Questions).Id);
        current = await h.Send("2", current.ConversationId);
        Assert.Empty(current.Questions);
        Assert.Equal("2", (await h.Workflows.GetHistoryForPatientAsync(1)).Single().Requirements.Single(r => r.Key == "severity_score").Value);
        Assert.NotNull(current.Messages.Last(m => m.Role == "user").FollowUpQuestion);
    }

    [Fact]
    public async Task DeclineActionIsPersistedAndRetryDoesNotAdvanceAgain()
    {
        await using var h = await Harness.Create();
        var start = await h.Send("I have a mild headache");
        var questionId = Assert.Single(start.Questions).Id;
        var request = new AssistantMessageRequest { ConversationId = start.ConversationId, RequestId = Guid.NewGuid(),
            RequirementId = questionId, RequirementState = HospitalManagementSystem.Api.AgenticAI.SafeTriage.SafeTriageRequirementState.Declined };
        var first = await h.Service.MessageAsync(h.Patient, request, default);
        var retry = await h.Service.MessageAsync(h.Patient, request, default);
        Assert.Equal(first.Messages.Count, retry.Messages.Count);
        Assert.NotEqual(questionId, Assert.Single(first.Questions).Id);
        var workflow = (await h.Workflows.GetHistoryForPatientAsync(1)).Single();
        Assert.Equal(2, workflow.FollowUpCount);
        var declined = workflow.Requirements.Single(r => r.Key == questionId);
        Assert.Equal(HospitalManagementSystem.Api.AgenticAI.SafeTriage.SafeTriageRequirementState.Declined, declined.State);
        Assert.Null(declined.Value);
        Assert.Equal("Declined", first.Messages.Last(m => m.Role == "user").FollowUpState.ToString());
        Assert.Equal(start.Questions.Single().Prompt, first.Messages.Last(m => m.Role == "user").FollowUpQuestion!.Prompt);
        Assert.DoesNotContain("Patient response", workflow.PatientReportedSymptoms);
    }

    [Theory]
    [InlineData("I'd rather not answer")]
    [InlineData("I don't know")]
    [InlineData("That doesn't apply to me")]
    public async Task NaturalUnavailableAnswerBypassesIntentModelAndAdvances(string text)
    {
        await using var h = await Harness.Create();
        var start = await h.Send("I have a mild headache");
        h.Intent.UseNext = true;
        h.Intent.Next = null;
        var updated = await h.Send(text, start.ConversationId);
        Assert.NotEqual(start.Questions.Single().Id, updated.Questions.Single().Id);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("ClinicianResponse")]
    [InlineData("Rejected")]
    public async Task ReviewedFinalResponseIsDeliveredOnceOnRefresh(string decision)
    {
        await using var h = await Harness.Create();
        var start = await h.Send("I have cancer");
        start = await h.Send("yes", start.ConversationId);
        var workflow = (await h.Workflows.GetHistoryForPatientAsync(1)).Single();
        await AssertReviewParity(h, start, workflow, "I have cancer", TriageLevels.ClinicalReview, requestedReview: true);
        const string response = "Here is the clinician's final care suggestion.";
        var reviewed = await h.Workflows.ReviewAsync(workflow.WorkflowId, 42, new() { Decision = decision, FinalResponse = response });
        var updated = await h.Service.GetAsync(1, start.ConversationId, default);
        Assert.Equal("Clinical review completed\n\n" + reviewed!.PatientMessage, updated.Messages.Last().Text);
        var refreshed = await h.Service.GetAsync(1, start.ConversationId, default);
        Assert.Equal(updated.Messages.Count, refreshed.Messages.Count);
        Assert.Empty(refreshed.Questions);
    }

    [Fact]
    public async Task ClarificationsAndGeneralQuestionsKeepTheOriginalQuestionUntilAValidAnswer()
    {
        await using var h = await Harness.Create();
        var start = await h.Send("I have a mild headache");
        h.Intent.UseNext = true;
        var firstId = Assert.Single(start.Questions).Id;
        foreach (var (message, intent) in new[] {
            ("What does peak mean?", "QUESTION_HELP"), ("wdym?", "CLARIFICATION"),
            ("Can you explain that?", "QUESTION_HELP"), ("What is this assessment for?", "GENERAL_QUERY") })
        {
            h.Intent.Next = new(intent, false, null, "Here is the explanation from the saved question guidance.");
            var reply = await h.Send(message, start.ConversationId);
            Assert.Equal(firstId, Assert.Single(reply.Questions).Id);
            Assert.Equal("Here is the explanation from the saved question guidance.", reply.Messages.Last().Text);
            Assert.Empty(SavedState(await h.Db.AssistantConversations.SingleAsync()).Answers);
        }
        h.Intent.Next = new("ANSWER", true, "not a valid option", null);
        var invalid = await h.Send("Something else", start.ConversationId);
        Assert.Equal(firstId, Assert.Single(invalid.Questions).Id);
        var workflow = await h.Workflows.GetForPatientAsync((await h.Db.TriageWorkflows.SingleAsync()).TriageWorkflowId, 1);
        var firstOption = workflow!.Guidance!.FollowUpItems.Single(q => q.Id == firstId).Options.First();
        h.Intent.Next = new("ANSWER", true, firstOption, null);
        var accepted = await h.Send(firstOption, start.ConversationId);
        Assert.NotEqual(firstId, Assert.Single(accepted.Questions).Id);
        Assert.Empty(SavedState(await h.Db.AssistantConversations.SingleAsync()).Answers);
        var persisted = await h.Workflows.GetForPatientAsync(workflow.WorkflowId, 1);
        Assert.Equal(firstOption, persisted!.Requirements.Single(r => r.Key == firstId).Value);
    }

    [Fact]
    public async Task InvalidModelResultNeverConsumesAssessmentQuestion()
    {
        await using var h = await Harness.Create();
        var start = await h.Send("I have a mild headache");
        h.Intent.UseNext = true;
        h.Intent.Next = null;
        var reply = await h.Send("What does that mean?", start.ConversationId);
        Assert.Equal(Assert.Single(start.Questions).Id, Assert.Single(reply.Questions).Id);
        Assert.Contains("cannot interpret your message right now", reply.Messages.Last().Text);
        Assert.DoesNotContain(start.Questions[0].Prompt, reply.Messages.Last().Text);
        Assert.Empty(SavedState(await h.Db.AssistantConversations.SingleAsync()).Answers);
    }

    [Fact]
    public async Task UnclearModelResponseIsShownWithoutAdvancing()
    {
        await using var h = await Harness.Create();
        var start = await h.Send("I have a mild headache");
        h.Intent.UseNext = true;
        h.Intent.Next = new("UNCLEAR", false, null, "Could you tell me more about what you mean?");
        var reply = await h.Send("Maybe", start.ConversationId);
        Assert.Equal("Could you tell me more about what you mean?", reply.Messages.Last().Text);
        Assert.Equal(Assert.Single(start.Questions).Id, Assert.Single(reply.Questions).Id);
        Assert.Empty(SavedState(await h.Db.AssistantConversations.SingleAsync()).Answers);
    }

    private static AssistantState SavedState(AssistantConversation conversation) =>
        JsonSerializer.Deserialize<AssistantState>(conversation.StateJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public async Task MalformedGeminiJsonIsRejected()
    {
        using var http = new HttpClient(new InvalidIntentHandler()) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/") };
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Gemini:ApiKey"] = "test-key"
        }).Build();
        var client = new GeminiAssessmentIntentClient(http, settings,
            NullLogger<GeminiAssessmentIntentClient>.Instance);
        var result = await client.InterpretAsync("wdym?", new() { Id = "q", Prompt = "When?", Type = "shortText" },
            new(), [], null, default);
        Assert.Null(result);
    }

    [Fact]
    public async Task GeminiClassifiesFollowUpIntentWithoutCallingOllama()
    {
        using var http = new HttpClient(new GeminiIntentHandler()) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/") };
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Gemini:ApiKey"] = "test-key", ["Gemini:Model"] = "test-model"
        }).Build();
        var client = new GeminiAssessmentIntentClient(http, settings, NullLogger<GeminiAssessmentIntentClient>.Instance);
        var result = await client.InterpretAsync("like suddenly", new() { Id = "q", Prompt = "How quickly?", Type = "singleChoice",
            Options = ["Suddenly reached maximum intensity within seconds ('thunderclap')", "Built up gradually over minutes to hours"] }, new(), [], null, default);
        Assert.Equal("ANSWER", result?.Intent);
        Assert.Equal("Suddenly reached maximum intensity within seconds ('thunderclap')", result?.NormalizedAnswer);
    }

    private sealed class GeminiIntentHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("generativelanguage.googleapis.com", request.RequestUri!.Host);
            Assert.EndsWith("/models/gemini-3.1-flash-lite:generateContent", request.RequestUri.AbsolutePath);
            Assert.Contains("test-key", request.Headers.GetValues("x-goog-api-key"));
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.True(payload.RootElement.GetProperty("generationConfig").TryGetProperty("responseJsonSchema", out _));
            var decision = "{\"intent\":\"ANSWER\",\"isAnswer\":true,\"normalizedAnswer\":\"Suddenly reached maximum intensity within seconds ('thunderclap')\",\"response\":null}";
            var envelope = JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = decision } } } } } });
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(envelope, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class InvalidIntentHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"not JSON\"}]}}]}", Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task DoctorInformationAndFollowupAvailabilityRetainHistoryWithoutProposals()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("What doctors work in cardiology?");
        Assert.Single(first.Doctors);
        Assert.Empty(first.Slots);
        Assert.Null(first.PendingAction);
        var second = await h.Send("Which one is available tomorrow?", first.ConversationId);
        Assert.Single(second.Slots);
        Assert.Null(second.PendingAction);
        Assert.Empty(await h.Db.AppointmentProposals.ToListAsync());
        Assert.Empty(await h.Db.Appointments.ToListAsync());
        var restored = await h.Service.GetAsync(1, first.ConversationId, default);
        Assert.Equal(4, restored.Messages.Count);
        Assert.Single(restored.Messages[1].Doctors);
        Assert.Single(restored.Messages[3].Slots);
        var booking = await h.Send("Book that doctor tomorrow", first.ConversationId);
        Assert.NotNull(booking.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task AvailabilityFollowupRetainsRequestedDateAndAddsDaypart()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("Which General Medicine doctors are available tomorrow?");
        var followup = await h.Send("Which one is available in the evening?", first.ConversationId);

        var row = await h.Db.AssistantConversations.SingleAsync();
        var state = SavedState(row);
        Assert.Equal("availability", state.ActiveTask);
        Assert.Equal("evening", state.Period);
        Assert.NotNull(state.PreferredDate);
        Assert.Null(state.ThroughDate);
        Assert.All(followup.Slots, slot => Assert.Equal(state.PreferredDate, DateOnly.FromDateTime(slot.StartAt.DateTime)));
        Assert.Null(followup.PendingAction);
    }

    [Theory]
    [InlineData("All speciality")]
    [InlineData("All specialities")]
    public async Task SpecialtyListAcceptsBritishSpelling(string message)
    {
        await using var h = await Harness.Create();
        var result = await h.Send(message);
        Assert.Contains("Available specialties:", result.Messages.Last().Text);
    }

    [Fact]
    public async Task DoctorListAndAcknowledgementUseReadOnlyRoutes()
    {
        await using var h = await Harness.Create();
        var doctors = await h.Send("Show me a list of doctors working in this hospital.");
        Assert.NotEmpty(doctors.Doctors);
        var thanks = await h.Send("Thank you for the help.", doctors.ConversationId);
        Assert.Equal("You're welcome.", thanks.Messages.Last().Text);
    }

    [Fact]
    public async Task NewBookingWithoutDateShowsRealUpcomingSlotsWithoutReusingOldFilters()
    {
        await using var h = await Harness.Create();
        var availability = await h.Send("Which cardiologists are available tomorrow evening?");
        var booking = await h.Send("Book me with Dr.Silva.", availability.ConversationId);

        Assert.NotNull(booking.PendingAction);
        Assert.Single(booking.PendingAction!.Slots);
    }

    [Fact]
    public async Task AcceptingAnotherDateAfterNoAvailabilityBroadensTheExistingSearch()
    {
        await using var h = await Harness.Create();
        var unavailable = await h.Send("Book a cardiologist tomorrow evening");
        Assert.Null(unavailable.PendingAction);
        Assert.Contains("no matching sessions", unavailable.Messages.Last().Text);

        var broadened = await h.Send("Yes", unavailable.ConversationId);
        Assert.NotNull(broadened.PendingAction);
        Assert.Single(broadened.PendingAction!.Slots);
        var state = SavedState(await h.Db.AssistantConversations.SingleAsync());
        Assert.Null(state.PreferredDate);
        Assert.Null(state.Period);
    }

    [Theory]
    [InlineData("Dr. Silva")]
    [InlineData("Dr Silva")]
    [InlineData("Dr.Silva")]
    public async Task BookingRecognizesDoctorTitlesWithOrWithoutSpaces(string doctorName)
    {
        await using var h = await Harness.Create();
        var result = await h.Send($"Book an appointment with {doctorName} tomorrow");

        Assert.NotNull(result.PendingAction);
        Assert.Single(result.PendingAction!.Slots);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task RoutinePendingAssessmentAllowsIndependentBookingAndCanBeExplicitlyResumed()
    {
        await using var h = await Harness.Create();
        var clinical = await h.Send("I have a mild headache");
        var workflowId = SavedState(await h.Db.AssistantConversations.SingleAsync()).WorkflowId;
        Assert.NotNull(workflowId);

        var booking = await h.Send("Book me with Dr. Neranjani Silva.", clinical.ConversationId);
        Assert.NotNull(booking.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
        Assert.Equal(TriageWorkflowStatuses.PendingPatientInput,
            (await h.Workflows.GetForPatientAsync(workflowId!.Value, 1))!.Status);

        var resumed = await h.Send("Resume my assessment", clinical.ConversationId);
        Assert.NotEmpty(resumed.Questions);
        var answered = await h.Send("No", clinical.ConversationId);
        var state = SavedState(await h.Db.AssistantConversations.SingleAsync());
        Assert.Empty(state.Answers);
        Assert.Contains((await h.Workflows.GetForPatientAsync(workflowId.Value, 1))!.Requirements,
            r => r.State == HospitalManagementSystem.Api.AgenticAI.SafeTriage.SafeTriageRequirementState.Answered);
        Assert.DoesNotContain("cannot verify the current assessment question", answered.Messages.Last().Text);
    }

    [Theory]
    [InlineData("What doctors work in General Medicine?")]
    [InlineData("Which cardiologists are available tomorrow?")]
    [InlineData("Find me a cardiologist Friday afternoon")]
    public async Task InformationalSearchNeverCreatesProposal(string text)
    {
        await using var h = await Harness.Create();
        var result = await h.Send(text);
        Assert.Null(result.PendingAction);
        Assert.Empty(await h.Db.AppointmentProposals.ToListAsync());
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task AppointmentReadsFilterStatusAndFindNearestFutureRecord()
    {
        await using var h = await Harness.Create();
        var slot = await h.Db.DoctorTimeSlots.SingleAsync();
        h.Db.Appointments.AddRange(
            new Appointment { DoctorTimeSlotId = slot.DoctorTimeSlotId, PatientId = 1, AppointmentNumber = 1, Status = "Confirmed" },
            new Appointment { DoctorTimeSlotId = slot.DoctorTimeSlotId, PatientId = 1, AppointmentNumber = 2, Status = "Cancelled" },
            new Appointment { DoctorTimeSlotId = slot.DoctorTimeSlotId, PatientId = 999, AppointmentNumber = 3, Status = "Confirmed" });
        var later = new DoctorTimeSlot { DoctorId = 1, DoctorName = "Later doctor", StartAt = slot.StartAt.AddDays(1), EndAt = slot.EndAt.AddDays(1), Capacity = 5, IsActive = true };
        h.Db.DoctorTimeSlots.Add(later);
        h.Db.Appointments.Add(new Appointment { DoctorTimeSlot = later, PatientId = 1, AppointmentNumber = 1, Status = "Confirmed" });
        await h.Db.SaveChangesAsync();
        var next = await h.Send("What time is my next appointment?");
        Assert.Equal(slot.DoctorTimeSlotId, Assert.Single(next.Appointments).DoctorTimeSlotId);
        Assert.Contains("Your next appointment is with", next.Messages.Last().Text);
        var active = await h.Send("Show my existing appointments", next.ConversationId);
        Assert.Equal(2, active.Appointments.Count);
        Assert.All(active.Appointments, a => Assert.Equal("Confirmed", a.Status));
        var cancelled = await h.Send("Show my cancelled appointments", next.ConversationId);
        Assert.Equal("Cancelled", Assert.Single(cancelled.Appointments).Status);
        var all = await h.Send("Show all my appointments", next.ConversationId);
        Assert.Equal(3, all.Appointments.Count);
        var history = await h.Send("Show my appointment history", next.ConversationId);
        Assert.Equal("Cancelled", Assert.Single(history.Appointments).Status);
    }

    [Theory]
    [InlineData(TriageWorkflowStatuses.FailedSafely, TriageLevels.InsufficientInformation)]
    [InlineData(TriageWorkflowStatuses.PendingPatientInput, TriageLevels.InsufficientInformation)]
    [InlineData(TriageWorkflowStatuses.PendingClinicalReview, TriageLevels.Urgent)]
    public async Task UnresolvedSafetyAllowsIndependentReads_AndOnlyRoutinePendingInputCanStartBooking(string status, string level)
    {
        await using var h = await Harness.Create();
        h.Db.TriageWorkflows.Add(new TriageWorkflow { PatientId = 1, Status = status,
            TriageLevel = level, ApprovalStatus = TriageApprovalStatuses.Pending, RequiresHumanReview = true });
        await h.Db.SaveChangesAsync();
        var first = await h.Send("Book a cardiologist tomorrow");
        if (status == TriageWorkflowStatuses.PendingPatientInput)
            Assert.NotNull(first.PendingAction);
        else
            Assert.Null(first.PendingAction);
        var read = await h.Send("Which cardiologists are available tomorrow?", first.ConversationId);
        Assert.NotEmpty(read.Slots);
        Assert.Single(read.ClinicalReviews);
        if (status == TriageWorkflowStatuses.PendingPatientInput)
            Assert.NotNull(read.PendingAction);
        else
            Assert.Null(read.PendingAction);
        var list = await h.Send("Show my appointments", first.ConversationId);
        Assert.Contains("No appointments", list.Messages.Last().Text);
        var laterBooking = await h.Send("Book a cardiologist tomorrow", first.ConversationId);
        if (status == TriageWorkflowStatuses.PendingPatientInput)
            Assert.NotNull(laterBooking.PendingAction);
        else
            Assert.Null(laterBooking.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task ReadDuringPatientApprovalPreservesActionAndProposalHistory()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("Book a cardiologist tomorrow");
        var action = first.PendingAction!;
        var read = await h.Send("Show my appointments", first.ConversationId);
        Assert.Equal(action.ActionId, read.PendingAction!.ActionId);
        Assert.Contains(read.Messages, m => m.ProposedAction?.ActionId == action.ActionId);
        var confirmed = await h.Service.DecideAsync(h.Patient, first.ConversationId, h.Confirm(action), default);
        Assert.Single(confirmed.Appointments);
        Assert.Contains(confirmed.Messages, m => m.ProposedAction?.ActionId == action.ActionId);
    }

    [Fact]
    public async Task ReadDuringMissingAnswersDoesNotConsumeClinicalInput()
    {
        await using var h = await Harness.Create();
        var clinical = await h.Send("I have a mild headache");
        Assert.NotEmpty(clinical.Questions);
        Assert.True(clinical.AssessmentInputActive);
        var read = await h.Send("What doctors work in cardiology?", clinical.ConversationId);
        Assert.NotEmpty(read.Doctors);
        Assert.Equal(clinical.Questions[0].Id, read.Questions[0].Id);
        Assert.False(read.AssessmentInputActive);
        Assert.DoesNotContain("urgent safety", read.Messages.Last().Text);
        var booking = await h.Send("Book a cardiologist tomorrow", clinical.ConversationId);
        Assert.Equal(clinical.Questions[0].Id, booking.Questions[0].Id);
        Assert.NotNull(booking.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task NewRoutineComplaintIsNotReplacedByAnOlderPendingClinicalReview()
    {
        await using var h = await Harness.Create();
        h.Db.TriageWorkflows.Add(new TriageWorkflow {
            PatientId = 1,
            Symptoms = "I have cancer and need advice",
            Status = TriageWorkflowStatuses.PendingClinicalReview,
            TriageLevel = TriageLevels.ClinicalReview,
            ApprovalStatus = TriageApprovalStatuses.Pending,
            RequiresHumanReview = true
        });
        await h.Db.SaveChangesAsync();

        var response = await h.Send("I have a headache");

        Assert.NotEqual("WAITING_FOR_HUMAN_APPROVAL", response.State);
        Assert.NotEmpty(response.Questions);
        Assert.Equal(2, await h.Db.TriageWorkflows.CountAsync());
        Assert.Contains(response.ClinicalReviews, review => review.Status == TriageWorkflowStatuses.PendingClinicalReview);
    }

    [Fact]
    public async Task PatientCanDeferAssessmentWithoutItRemainingInTheActiveUiFlow()
    {
        await using var h = await Harness.Create();
        var clinical = await h.Send("I have a mild headache");
        var deferred = await h.Send("I don't want to answer this now", clinical.ConversationId);

        Assert.False(deferred.AssessmentInputActive);
        Assert.Contains("kept the assessment for later", deferred.Messages.Last().Text);
        var doctorRead = await h.Send("What doctors work in cardiology?", clinical.ConversationId);
        Assert.False(doctorRead.AssessmentInputActive);
        Assert.NotEmpty(doctorRead.Doctors);
        Assert.Single(doctorRead.ClinicalReviews);
    }

    [Theory]
    [InlineData(false, TriageApprovalStatuses.Approved)]
    [InlineData(false, TriageApprovalStatuses.Rejected)]
    [InlineData(true, TriageApprovalStatuses.Approved)]
    [InlineData(true, TriageApprovalStatuses.Rejected)]
    public async Task FailedInputCanBeReviewedAndResumeBookingWithoutAutomaticMutation(bool legacy, string decision)
    {
        await using var h = await Harness.Create();
        var failed = await h.Workflows.StartForPatientAsync(1, new() {
            Symptoms = "I feel dizzy", Vitals = new TriageVitalsDto { TemperatureCelsius = 98 }
        });
        Assert.Equal(TriageWorkflowStatuses.FailedSafely, failed.Status);
        Assert.Equal(TriageApprovalStatuses.Pending, failed.ApprovalStatus);
        var record = await h.Db.TriageWorkflows.SingleAsync();
        if (legacy) { record.ApprovalStatus = TriageApprovalStatuses.NotRequired; await h.Db.SaveChangesAsync(); }
        Assert.Contains(await h.Workflows.GetPendingClinicalReviewsAsync(), w => w.WorkflowId == failed.WorkflowId);
        Assert.Equal(legacy ? TriageApprovalStatuses.NotRequired : TriageApprovalStatuses.Pending, record.ApprovalStatus);
        var blocked = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(blocked.PendingAction);
        var revised = await h.Workflows.ReviewAsync(failed.WorkflowId, 42, new() { Decision = TriageApprovalStatuses.RevisionRequested });
        Assert.NotNull(revised);
        var stillBlocked = await h.Send("Book a cardiologist tomorrow", blocked.ConversationId);
        Assert.Null(stillBlocked.PendingAction);
        var reviewed = await h.Workflows.ReviewAsync(failed.WorkflowId, 42, new() { Decision = decision, Note = "Reviewed test assessment" });
        Assert.Equal(TriageWorkflowStatuses.Completed, reviewed!.Status);
        Assert.Equal(42, record.ReviewedByUserId);
        Assert.Equal("InvalidOrSuspiciousInput", record.ErrorCode);
        Assert.Equal(legacy ? 1 : 0, await h.Db.TriageWorkflowEvents.CountAsync(e => e.EventType == "LegacyFailedAssessmentRecovered"));
        var resumed = await h.Send("Book a cardiologist tomorrow", blocked.ConversationId);
        Assert.NotNull(resumed.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task FinalizedEmergencyReviewDoesNotClearUrgentConversationSafety()
    {
        await using var h = await Harness.Create();
        var blocked = await h.Send("I have severe chest pain and difficulty breathing. Book a cardiologist tomorrow.");
        var record = await h.Db.TriageWorkflows.SingleAsync();
        await h.Workflows.ReviewAsync(record.TriageWorkflowId, 42, new() { Decision = TriageApprovalStatuses.Approved });
        var later = await h.Send("Book a cardiologist tomorrow", blocked.ConversationId);
        Assert.Null(later.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Theory]
    [InlineData(TriageWorkflowStatuses.FailedSafely, "could not be completed safely")]
    public async Task BookingBlockExplainsActualAssessmentAndIncludesSavedGuidance(string status, string expected)
    {
        await using var h = await Harness.Create();
        h.Db.TriageWorkflows.Add(new TriageWorkflow {
            PatientId = 1, Status = status, FinalOutcome = "Saved assessment instructions."
        });
        await h.Db.SaveChangesAsync();
        var response = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(response.PendingAction);
        var reply = response.Messages.Last().Text;
        Assert.Contains(expected, reply);
        Assert.Contains("Saved assessment instructions.", reply);
        Assert.DoesNotContain("urgent safety", reply);
        Assert.DoesNotContain("shown above", reply);
    }

    [Fact]
    public async Task ResolvedNonUrgentAssessmentClearsCachedBlockInExistingConversation()
    {
        await using var h = await Harness.Create();
        var workflow = new TriageWorkflow {
            PatientId = 1, Status = TriageWorkflowStatuses.PendingPatientInput
        };
        h.Db.TriageWorkflows.Add(workflow);
        await h.Db.SaveChangesAsync();
        var blocked = await h.Send("Book a cardiologist tomorrow");
        Assert.NotNull(blocked.PendingAction);
        workflow.Status = TriageWorkflowStatuses.Completed;
        workflow.TriageLevel = TriageLevels.NonUrgent;
        workflow.ApprovalStatus = TriageApprovalStatuses.Approved;
        await h.Db.SaveChangesAsync();
        var resumed = await h.Send("Book a cardiologist tomorrow", blocked.ConversationId);
        Assert.NotNull(resumed.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task NewConversationShowsExistingUrgentGuidanceWithBookingBlock()
    {
        await using var h = await Harness.Create();
        h.Db.TriageWorkflows.Add(new TriageWorkflow {
            PatientId = 1, Status = TriageWorkflowStatuses.PendingClinicalReview,
            ApprovalStatus = TriageApprovalStatuses.Pending, TriageLevel = TriageLevels.Urgent,
            RequiresHumanReview = true, FinalOutcome = "Contact the care team for your saved urgent assessment."
        });
        await h.Db.SaveChangesAsync();
        var response = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(response.PendingAction);
        Assert.Contains("Contact the care team for your saved urgent assessment.", response.Messages.Last().Text);
        Assert.Contains("urgent safety concerns", response.Messages.Last().Text);
    }

    [Fact]
    public async Task SearchAndConversationalAssentNeverBook_ExplicitConfirmationUsesBackendNumber_AndIsIdempotent()
    {
        await using var h = await Harness.Create();
        var result = await h.Send("Book a cardiologist tomorrow afternoon");
        Assert.NotNull(result.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
        var assent = await h.Send("that looks good", result.ConversationId);
        Assert.Equal(result.PendingAction.ActionId, assent.PendingAction!.ActionId);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
        var request = h.Confirm(result.PendingAction);
        var booked = await h.Service.DecideAsync(h.Patient, result.ConversationId, request, default);
        Assert.Equal("COMPLETED", booked.State);
        Assert.Equal(1, Assert.Single(booked.Appointments).AppointmentNumber);
        Assert.Null(booked.PendingAction);
        var historicalProposal = Assert.Single(booked.Messages, m => m.ProposedAction?.ActionId == result.PendingAction.ActionId).ProposedAction!;
        Assert.Equal("Confirmed", historicalProposal.Status);
        Assert.Empty(historicalProposal.Slots);
        Assert.Empty(booked.Slots);
        var retry = await h.Service.DecideAsync(h.Patient, result.ConversationId, request, default);
        Assert.Equal(booked.Appointments[0].AppointmentId, retry.Appointments[0].AppointmentId);
        Assert.Single(await h.Db.Appointments.ToListAsync());
        request.RequestId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.DecideAsync(h.Patient, result.ConversationId, request, default));
    }

    [Fact]
    public async Task ChooseAnotherFindsDifferentAvailableSlotsWhileDismissOnlyCancelsTheProposal()
    {
        await using var h = await Harness.Create();
        var firstSlot = await h.Db.DoctorTimeSlots.SingleAsync();
        for (var day = 1; day <= 5; day++)
        {
            h.Db.DoctorTimeSlots.Add(new DoctorTimeSlot {
                DoctorName = firstSlot.DoctorName, DoctorId = firstSlot.DoctorId, Specialty = firstSlot.Specialty,
                StartAt = firstSlot.StartAt.AddDays(day), EndAt = firstSlot.EndAt.AddDays(day), Capacity = firstSlot.Capacity, IsActive = true
            });
        }
        await h.Db.SaveChangesAsync();

        var first = await h.Send("Book a cardiologist");
        var shownIds = first.PendingAction!.Slots.Select(slot => slot.DoctorTimeSlotId).ToHashSet();
        var alternative = await h.Service.DecideAsync(h.Patient, first.ConversationId, new() {
            ActionId = first.PendingAction.ActionId, Decision = "chooseAnother", RequestId = Guid.NewGuid()
        }, default);

        Assert.NotNull(alternative.PendingAction);
        Assert.DoesNotContain(alternative.PendingAction!.Slots, slot => shownIds.Contains(slot.DoctorTimeSlotId));
        var dismissed = await h.Service.DecideAsync(h.Patient, first.ConversationId, new() {
            ActionId = alternative.PendingAction.ActionId, Decision = "cancel", RequestId = Guid.NewGuid()
        }, default);
        Assert.Null(dismissed.PendingAction);
        Assert.Contains("cancelled this appointment request", dismissed.Messages.Last().Text);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task EmergencyBlocksBookingAcrossTurnsAndNewConversations_AndCreatesClinicalReview()
    {
        await using var h = await Harness.Create();
        var emergency = await h.Send("I have severe chest pain and difficulty breathing. Book a cardiologist tomorrow.");
        Assert.Null(emergency.PendingAction);
        Assert.Equal("WAITING_FOR_HUMAN_APPROVAL", emergency.State);
        Assert.Contains("emergency", string.Join(" ", emergency.Messages.Select(m => m.Text)), StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(await h.Workflows.GetPendingClinicalReviewsAsync());
        var later = await h.Send("Book a cardiologist tomorrow", emergency.ConversationId);
        Assert.Null(later.PendingAction);
        var fresh = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(fresh.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task RoutineSymptomsCanRunSafetyThenAppointmentProposal_WithoutBooking()
    {
        await using var h = await Harness.Create();
        var result = await h.Send("I have a mild headache. Book a cardiologist tomorrow afternoon.");
        Assert.NotEmpty(await h.Db.TriageWorkflows.ToListAsync());
        Assert.Null(result.PendingAction);
        for (var round = 0; result.Questions.Count > 0 && round < 12; round++)
            result = await h.Send("Mild symptoms, started yesterday, getting better. None of the warning signs.", result.ConversationId);
        Assert.NotNull(result.PendingAction);
        Assert.NotEmpty(await h.Db.TriageWorkflowEvents.ToListAsync());
        Assert.Contains(result.Messages, m => m.Progress.Contains("Available hospital sessions were checked."));
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task PersistedPlan_PausesAtPatientFollowUp_ThenResumes()
    {
        await using var h = await Harness.Create();
        var result = await h.Send("I have a mild headache. Book a cardiologist tomorrow afternoon.");
        var store = new HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.PlanningCoordinatorStore(h.Db);
        var execution = await store.GetAsync(result.ExecutionWorkflowId!);
        var triage = execution!.Steps.Single(step => step.StepType == "TriageAssessment");
        var proposal = execution.Steps.Single(step => step.StepType == "AppointmentProposal");

        Assert.NotEmpty(result.Questions);
        Assert.Equal("WaitingForPatient", triage.Status);
        Assert.Equal("Pending", proposal.Status);
        Assert.Equal("AwaitingPatientInput", execution.Status);

        for (var round = 0; result.Questions.Count > 0 && round < 12; round++)
            result = await h.Send("Mild symptoms, started yesterday, getting better. None of the warning signs.", result.ConversationId);

        execution = await store.GetAsync(result.ExecutionWorkflowId!);
        triage = execution!.Steps.Single(step => step.StepType == "TriageAssessment");
        proposal = execution.Steps.Single(step => step.StepType == "AppointmentProposal");
        Assert.Equal("Completed", triage.Status);
        Assert.Equal("Completed", proposal.Status);
        Assert.NotNull(result.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task NewPreferencesInvalidateOldApproval_UnknownSlotCannotBeConfirmed()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("Book a cardiologist tomorrow afternoon");
        var oldAction = first.PendingAction!;
        var second = await h.Send("Try tomorrow morning", first.ConversationId);
        Assert.Null(second.PendingAction);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.DecideAsync(h.Patient, first.ConversationId, h.Confirm(oldAction), default));
        Assert.Equal("Superseded", (await h.Db.AppointmentProposals.SingleAsync()).Status);
        var third = await h.Send("Try tomorrow afternoon", first.ConversationId);
        var invalid = h.Confirm(third.PendingAction!);
        invalid.DoctorTimeSlotId = 99999;
        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.DecideAsync(h.Patient, first.ConversationId, invalid, default));
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task ConversationOwnershipAndRequestFingerprintsAreEnforced()
    {
        await using var h = await Harness.Create();
        var request = new AssistantMessageRequest { Message = "Find a doctor", RequestId = Guid.NewGuid() };
        var result = await h.Service.MessageAsync(h.Patient, request, default);
        var repeated = await h.Service.MessageAsync(h.Patient, request, default);
        Assert.Equal(result.ConversationId, repeated.ConversationId);
        Assert.Equal(2, repeated.Messages.Count);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.GetAsync(999, result.ConversationId, default));
        request.Message = "Book a cardiologist";
        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.MessageAsync(h.Patient, request, default));
    }

    [Theory]
    [InlineData("I cannot attend because of work")]
    [InlineData("hospitalized")]
    [InlineData("I have a cough")]
    public async Task CancellationRequiresReasonSelectionAndApproval(string reason)
    {
        await using var h = await Harness.Create();
        var search = await h.Send("Book a cardiologist tomorrow");
        var booked = await h.Service.DecideAsync(h.Patient, search.ConversationId, h.Confirm(search.PendingAction!), default);
        var request = await h.Send("Cancel my appointment", search.ConversationId);
        Assert.Null(request.PendingAction);
        var proposed = await h.Send(reason, search.ConversationId);
        Assert.Equal("cancel", proposed.PendingAction!.Type);
        Assert.Equal("Confirmed", (await h.Db.Appointments.SingleAsync()).Status);
        var cancelled = await h.Service.DecideAsync(h.Patient, search.ConversationId, new() {
            ActionId = proposed.PendingAction.ActionId, Decision = "confirm", RequestId = Guid.NewGuid(),
            AppointmentId = booked.Appointments[0].AppointmentId
        }, default);
        Assert.Equal("Cancelled", Assert.Single(cancelled.Appointments).Status);
        Assert.Equal(reason, cancelled.Appointments[0].CancellationReason);
    }

    [Fact]
    public async Task CancellationWithIncompleteAppointmentWordStillRequestsReason()
    {
        await using var h = await Harness.Create();
        var search = await h.Send("Book a cardiologist tomorrow");
        await h.Service.DecideAsync(h.Patient, search.ConversationId, h.Confirm(search.PendingAction!), default);

        var request = await h.Send("Cancel my next appointmen", search.ConversationId);

        Assert.Contains("reason", request.Messages.Last().Text, StringComparison.OrdinalIgnoreCase);
        Assert.Null(request.PendingAction);
    }

    [Fact]
    public async Task ExpiredAndDismissedRequestsCannotWrite()
    {
        await using var h = await Harness.Create();
        var result = await h.Send("Book a cardiologist tomorrow");
        var row = await h.Db.AssistantConversations.SingleAsync();
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var state = System.Text.Json.JsonSerializer.Deserialize<AssistantState>(row.StateJson, options)!;
        state.PendingAction!.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        row.StateJson = System.Text.Json.JsonSerializer.Serialize(state, options);
        await h.Db.SaveChangesAsync();
        var expired = await h.Service.DecideAsync(h.Patient, result.ConversationId, h.Confirm(result.PendingAction!), default);
        Assert.Null(expired.PendingAction);
        Assert.Equal("CANCELLED", expired.State);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task ChangedSessionDetailsNeedNewApproval_ButQueueNumberChangesAreAllowed()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("Book a cardiologist tomorrow");
        var slot = await h.Db.DoctorTimeSlots.SingleAsync();
        slot.StartAt = slot.StartAt.AddMinutes(30);
        await h.Db.SaveChangesAsync();
        var rejected = await h.Service.DecideAsync(h.Patient, first.ConversationId, h.Confirm(first.PendingAction!), default);
        Assert.Equal("FAILED", rejected.State);
        Assert.Empty(await h.Db.Appointments.ToListAsync());

        var second = await h.Send("Book a cardiologist tomorrow", first.ConversationId);
        h.Db.Appointments.Add(new Appointment { DoctorTimeSlotId = slot.DoctorTimeSlotId, PatientId = 999,
            AppointmentNumber = 1, PatientName = "Another patient", Status = "Confirmed" });
        await h.Db.SaveChangesAsync();
        var confirmed = await h.Service.DecideAsync(h.Patient, first.ConversationId, h.Confirm(second.PendingAction!), default);
        Assert.Equal(2, Assert.Single(confirmed.Appointments).AppointmentNumber);
    }

    [Fact]
    public async Task EmergencyDuringFollowupInterruptsQuestionsAndPreventsProposal()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("I have a mild headache and want a cardiologist tomorrow");
        Assert.NotEmpty(first.Questions);
        var emergency = await h.Send("Now I have severe chest pain and difficulty breathing", first.ConversationId);
        Assert.Empty(emergency.Questions);
        Assert.Null(emergency.PendingAction);
        Assert.Equal("WAITING_FOR_HUMAN_APPROVAL", emergency.State);
        var workflow = (await h.Workflows.GetHistoryForPatientAsync(1)).First();
        await AssertReviewParity(h, emergency, workflow, "Now I have severe chest pain and difficulty breathing", TriageLevels.Emergency);
    }

    private static async Task AssertReviewParity(Harness h, AssistantConversationResponse response, TriageWorkflowDto workflow, string symptoms, string level, bool requestedReview = false)
    {
        Assert.Equal(TriageWorkflowStatuses.PendingClinicalReview, workflow.Status);
        Assert.Equal(level, workflow.TriageLevel);
        Assert.True(workflow.RequiresHumanReview);
        Assert.Equal(TriageApprovalStatuses.Pending, workflow.ApprovalStatus);
        Assert.Contains(await h.Workflows.GetPendingClinicalReviewsAsync(), w => w.WorkflowId == workflow.WorkflowId);
        Assert.NotNull(await h.Workflows.GetForClinicalReviewerAsync(workflow.WorkflowId));
        var store = new HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.PlanningCoordinatorStore(h.Db);
        var execution = (await store.GetAsync(response.ExecutionWorkflowId!))!;
        Assert.Equal("AwaitingClinicalReview", execution.Status);
        var step = Assert.Single(execution.Steps, s => s.StepId == execution.CurrentStep);
        Assert.Equal("WaitingForClinicalReview", step.Status);
        Assert.All(execution.Steps.SkipWhile(s => s.StepId != step.StepId).Skip(1), s => Assert.Equal("Pending", s.Status));
        Assert.Null(execution.FinalOutcome);
        Assert.Empty(await h.Db.AppointmentProposals.ToListAsync());
        var legacy = await h.Workflows.StartForPatientAsync(1, new() { Symptoms = symptoms });
        if (requestedReview)
            legacy = (await h.Workflows.SetPatientClinicalReviewChoiceAsync(legacy.WorkflowId, 1, requested: true))!;
        Assert.Equal(legacy.Status, workflow.Status);
        Assert.Equal(legacy.TriageLevel, workflow.TriageLevel);
        Assert.Equal(legacy.RequiresHumanReview, workflow.RequiresHumanReview);
        Assert.Equal(legacy.ApprovalStatus, workflow.ApprovalStatus);
        Assert.Equal(legacy.PatientMessage, workflow.PatientMessage);
        Assert.Equal(legacy.RedFlags, workflow.RedFlags);
        Assert.Equal(legacy.UrgentFlags, workflow.UrgentFlags);
        Assert.Equal(legacy.ClinicalReviewFlags, workflow.ClinicalReviewFlags);
    }



    [Fact]
    public async Task ClinicalReviewChoice_PresentsOnlyTwoOptions_AndSupportsDoctorSelection()
    {
        await using var h = await Harness.Create();
        var doctor = new Doctor { DoctorId = 10, UserId = 100, FirstName = "Sunil", LastName = "Perera", Specialization = "Cardiologist", RegistrationStatus = DoctorRegistrationStatuses.Approved };
        h.Db.Doctors.Add(doctor);
        await h.Db.SaveChangesAsync();

        // Step 1: Start with condition requiring optional clinical review
        var first = await h.Send("I have cancer");
        Assert.Equal("GATHERING_INFORMATION", first.State);
        var lastMessage = first.Messages.Last().Text;

        // Verify Option 1 ("Next Available Doctor") was removed and only 2 options are presented
        Assert.Contains("1. 🩺 Select a Specific Doctor", lastMessage);
        Assert.Contains("2. ✕ Not right now", lastMessage);
        Assert.DoesNotContain("Next Available Doctor", lastMessage);
        Assert.DoesNotContain("Fastest", lastMessage);

        // Step 2: Patient sends "1" to request doctor selection
        var docPrompt = await h.Send("1", first.ConversationId);
        Assert.Contains("Please reply with the name of your preferred doctor or specialty", docPrompt.Messages.Last().Text);
        Assert.Contains("Dr. Sunil Perera", docPrompt.Messages.Last().Text);

        // Step 3: Patient names the doctor
        var assigned = await h.Send("Dr. Sunil Perera", first.ConversationId);
        Assert.Equal("WAITING_FOR_HUMAN_APPROVAL", assigned.State);
        var workflow = (await h.Workflows.GetHistoryForPatientAsync(1)).First();
        Assert.Equal(doctor.DoctorId, workflow.AssignedDoctorId);
    }

    [Fact]
    public async Task ClinicalReviewChoice_ReplyingWithSpecialty_AssignsReviewAndDoesNotTriggerMedicalRecordAgent()
    {
        await using var h = await Harness.Create();
        var doctor = new Doctor { DoctorId = 11, UserId = 101, FirstName = "Dilnaka", LastName = "Perera", Specialization = "General Medicine", RegistrationStatus = DoctorRegistrationStatuses.Approved };
        h.Db.Doctors.Add(doctor);
        await h.Db.SaveChangesAsync();

        var first = await h.Send("I have cancer");
        Assert.Equal("GATHERING_INFORMATION", first.State);

        // Patient types "general medicine" directly when choosing review
        var assigned = await h.Send("general medicine", first.ConversationId);
        Assert.Equal("WAITING_FOR_HUMAN_APPROVAL", assigned.State);
        var message = assigned.Messages.Last().Text;
        Assert.Contains("Dilnaka Perera", message);
        Assert.DoesNotContain("recorded diagnosis", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("At your latest visit", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dr. Dr.", message);
    }

    [Fact]
    public async Task EmergencyAlert_ProvidesCriticalEmergencyGuidance()
    {
        await using var h = await Harness.Create();
        var result = await h.Send("I have severe chest pain and difficulty breathing");
        Assert.Equal("WAITING_FOR_HUMAN_APPROVAL", result.State);
        var message = result.Messages.Last().Text;
        Assert.Contains("CRITICAL EMERGENCY ALERT", message);
        Assert.Contains("1990", message);
        Assert.Contains("911", message);
        Assert.Contains("Emergency Room", message);
        // Ensure internal diagnostics did not leak
        Assert.DoesNotContain("vital signs", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Missing.", message);
    }

    [Fact]
    public async Task AlternativeRequestSearchesWithoutCancellingCurrentAppointment()
    {
        await using var h = await Harness.Create();
        var search = await h.Send("Book a cardiologist tomorrow");
        await h.Service.DecideAsync(h.Patient, search.ConversationId, h.Confirm(search.PendingAction!), default);
        var alternative = await h.Send("I cannot attend my current appointment. Book another one tomorrow.", search.ConversationId);
        Assert.NotNull(alternative.PendingAction);
        Assert.Equal("book", alternative.PendingAction.Type);
        Assert.Equal("Confirmed", (await h.Db.Appointments.SingleAsync()).Status);
        Assert.Contains(alternative.Messages, m => m.Text.Contains("current appointment will remain active"));
    }

    [Fact]
    public async Task RescheduleRequiresSourceAndReplacementConfirmationsBeforeUpdatingAppointment()
    {
        await using var h = await Harness.Create();
        var sourceSlot = await h.Db.DoctorTimeSlots.SingleAsync();
        var destination = new DoctorTimeSlot {
            DoctorName = sourceSlot.DoctorName, DoctorId = sourceSlot.DoctorId, Specialty = sourceSlot.Specialty,
            StartAt = sourceSlot.StartAt.AddDays(1), EndAt = sourceSlot.EndAt.AddDays(1), Capacity = 5, IsActive = true
        };
        h.Db.DoctorTimeSlots.Add(destination);
        await h.Db.SaveChangesAsync();
        var appointmentService = new AppointmentService(new AppointmentRepository(h.Db), SmsTestSupport.Create(h.Db));
        var existing = await appointmentService.CreateAppointmentAsync(new() {
            DoctorTimeSlotId = sourceSlot.DoctorTimeSlotId, PatientId = h.Patient.PatientId,
            PatientName = h.Patient.FullName, PatientEmail = h.Patient.Email, PatientPhone = h.Patient.PhoneNumber,
            AppointmentType = "Consultation"
        });

        var chooseSource = await h.Send("Reschedule my appointment");
        Assert.Equal("reschedule-source", chooseSource.PendingAction?.Type);
        Assert.Equal(sourceSlot.DoctorTimeSlotId, (await h.Db.Appointments.SingleAsync()).DoctorTimeSlotId);

        var chooseReplacement = await h.Service.DecideAsync(h.Patient, chooseSource.ConversationId, new() {
            ActionId = chooseSource.PendingAction!.ActionId, Decision = "confirm", RequestId = Guid.NewGuid(),
            AppointmentId = existing.AppointmentId
        }, default);
        Assert.Equal("reschedule", chooseReplacement.PendingAction?.Type);
        Assert.Equal(sourceSlot.DoctorTimeSlotId, (await h.Db.Appointments.SingleAsync()).DoctorTimeSlotId);

        var replacementSlot = Assert.Single(chooseReplacement.PendingAction!.Slots,
            slot => slot.DoctorTimeSlotId == destination.DoctorTimeSlotId);
        var completed = await h.Service.DecideAsync(h.Patient, chooseSource.ConversationId, new() {
            ActionId = chooseReplacement.PendingAction.ActionId, Decision = "confirm", RequestId = Guid.NewGuid(),
            DoctorTimeSlotId = replacementSlot.DoctorTimeSlotId
        }, default);

        Assert.Equal("COMPLETED", completed.State);
        Assert.Equal(destination.DoctorTimeSlotId, (await h.Db.Appointments.SingleAsync()).DoctorTimeSlotId);
        Assert.Contains(completed.Messages, message => message.Text.Contains("was rescheduled"));
    }

    [Fact]
    public async Task AssistantEndpointsRequirePatientRoleAndOwnedConversation()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/hospital-assistant/capabilities")).StatusCode);
        await Login("admin@medicore.lk", "Admin1234");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/hospital-assistant/capabilities")).StatusCode);
        await Login("amal.perera@email.com", "Patient123!");
        var response = await client.PostAsJsonAsync("/api/hospital-assistant/messages", new { message = "hello", requestId = Guid.NewGuid() });
        response.EnsureSuccessStatusCode();
        var conversation = await response.Content.ReadFromJsonAsync<AssistantConversationResponse>();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/appointment-proposals/clinical-review/pending")).StatusCode);
        var planned = await client.PostAsJsonAsync("/api/planning-coordinator/plan", new { objective = "Book a doctor", patientId = 999999 });
        planned.EnsureSuccessStatusCode();
        var planJson = await planned.Content.ReadFromJsonAsync<JsonElement>();
        var planningId = planJson.GetProperty("workflowId").GetString();
        var execution = await client.GetFromJsonAsync<JsonElement>($"/api/planning-coordinator/workflows/{planningId}/execution");
        Assert.NotEqual(999999, execution.GetProperty("patientId").GetInt32());
        await Login("nimesha.silva@email.com", "Patient123!");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/hospital-assistant/conversations/{conversation!.ConversationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/planning-coordinator/workflows/{planningId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/planning-coordinator/workflows/{planningId}/execution")).StatusCode);


        async Task Login(string email, string password)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            login.EnsureSuccessStatusCode();
            var json = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        }
    }

    [Theory]
    [InlineData("next Friday afternoon", "2026-09-18", null, "afternoon")]
    [InlineData("tomorrow morning", "2026-09-12", null, "morning")]
    [InlineData("next week", "2026-09-14", "2026-09-20", null)]
    public void DatePreferencesAreDeterministic(string input, string start, string? end, string? period)
    {
        var state = new AssistantState();
        Assert.Null(AssistantPreferences.ApplyDates(input, state, new DateOnly(2026, 9, 11)));
        Assert.Equal(DateOnly.Parse(start), state.PreferredDate);
        Assert.Equal(end == null ? null : (DateOnly?)DateOnly.Parse(end), state.ThroughDate);
        Assert.Equal(period, state.Period);
    }


    [Fact]
    public async Task UnifiedExecutionPersistsPlanAndCorrelatesProposalAndConfirmation()
    {
        await using var h = await Harness.Create();
        var conversation = await h.Send("Book a cardiologist tomorrow afternoon");
        Assert.NotNull(conversation.ExecutionWorkflowId);
        var store = new HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.PlanningCoordinatorStore(h.Db);
        var execution = await store.GetAsync(conversation.ExecutionWorkflowId!);
        Assert.Equal("AwaitingPatientConfirmation", execution!.Status);
        Assert.Equal("Appointment Proposal", execution.CurrentAgent);
        Assert.All(execution.Steps, step => Assert.False(string.IsNullOrWhiteSpace(step.AssignedAgent)));
        var proposal = await h.Db.AppointmentProposals.SingleAsync();
        Assert.Equal(execution.WorkflowId, proposal.ExecutionWorkflowId);
        var booked = await h.Service.DecideAsync(h.Patient, conversation.ConversationId, h.Confirm(conversation.PendingAction!), default);
        Assert.Equal(conversation.ExecutionWorkflowId, booked.ExecutionWorkflowId);
        execution = await store.GetAsync(conversation.ExecutionWorkflowId!);
        Assert.Equal("Completed", execution!.Status);
        Assert.Contains(execution.AuditEvents, e => e.EventType == "AgentDispatched" && e.Description == "Safety Validation & Approval");
    }

    [Fact]
    public async Task PatientConfirmationBooksEvenIfAnOlderProposalHasAClinicalFlag()
    {
        await using var h = await Harness.Create();
        var conversation = await h.Send("Book a cardiologist tomorrow afternoon");
        var proposal = await h.Db.AppointmentProposals.SingleAsync();
        proposal.RequiresClinicalApproval = true;
        await h.Db.SaveChangesAsync();
        var confirmed = await h.Service.DecideAsync(h.Patient, conversation.ConversationId, h.Confirm(conversation.PendingAction!), default);
        Assert.Equal("COMPLETED", confirmed.State);
        Assert.False(proposal.RequiresClinicalApproval);
        Assert.Equal("Booked", proposal.Status);
        Assert.Single(h.Db.Appointments);
    }


    [Theory]
    [InlineData("My stomach is churning", "SafeTriage", "Clinical SafeTriage")]
    [InlineData("Arrange a visit with Dr. Silva tomorrow", "AppointmentProposal", "Appointment Proposal")]
    public async Task StructuredPlannerDispatchesObjectivesOutsideRoutingKeywords(string objective, string type, string expectedAgent)
    {
        await using var h = await Harness.Create(new SemanticPlanner(type));
        var conversation = await h.Send(objective);
        var store = new HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.PlanningCoordinatorStore(h.Db);
        var execution = await store.GetAsync(conversation.ExecutionWorkflowId!);
        Assert.Contains(execution!.AuditEvents, e => e.EventType == "AgentDispatched" && e.Description == expectedAgent);
        Assert.Empty(h.Db.Appointments);
    }

    [Fact]
    public async Task FullEndToEndFlow_PatientSymptomTriage_DoctorReview_PatientConfirmationAndBooking()
    {
        await using var h = await Harness.Create();

        // Step 1: Patient starts triage with a condition requiring clinical review
        var turn1 = await h.Send("I have cancer");
        Assert.NotEqual(Guid.Empty, turn1.ConversationId);
        Assert.Null(turn1.PendingAction); // Consent safety: zero assumption booking

        // Step 2: Patient opts in for clinical review
        var optIn = await h.Send("yes", turn1.ConversationId);
        var workflow = (await h.Workflows.GetHistoryForPatientAsync(1)).Single();
        await AssertReviewParity(h, optIn, workflow, "I have cancer", TriageLevels.ClinicalReview, requestedReview: true);

        // Step 3: Doctor reviews and approves the SafeTriage assessment via Web Review Queue
        const string clinicianAdvice = "Clinical assessment approved; patient is cleared for routine consultation.";
        var doctorReviewResult = await h.Workflows.ReviewAsync(
            workflow.WorkflowId,
            42,
            new() { Decision = TriageApprovalStatuses.Approved, FinalResponse = clinicianAdvice }
        );
        Assert.NotNull(doctorReviewResult);
        Assert.Equal(TriageWorkflowStatuses.Completed, doctorReviewResult.Status);

        // Step 4: Patient mobile app refreshes/retrieves conversation -> receives clinical review banner
        var refreshed = await h.Service.GetAsync(h.Patient.PatientId, turn1.ConversationId, default);
        Assert.Contains(refreshed.Messages, m => m.Role == "assistant" && m.Text.Contains("Clinical review completed"));

        // Step 5: Patient requests appointment proposal
        var bookingProposal = await h.Send("Book a cardiologist tomorrow afternoon", turn1.ConversationId);
        Assert.NotNull(bookingProposal.PendingAction);
        Assert.Equal("book", bookingProposal.PendingAction.Type);
        Assert.NotEmpty(bookingProposal.PendingAction.Slots);

        // Step 6: Patient explicitly confirms appointment booking
        var confirmed = await h.Service.DecideAsync(
            h.Patient,
            turn1.ConversationId,
            h.Confirm(bookingProposal.PendingAction),
            default
        );
        Assert.Equal("COMPLETED", confirmed.State);
        Assert.Single(confirmed.Appointments);
        Assert.Equal("Dr. Silva", confirmed.Appointments.Single().DoctorName);

        // Step 7: Verify database state integrity
        var dbAppointment = await h.Db.Appointments.SingleAsync();
        Assert.Equal(h.Patient.PatientId, dbAppointment.PatientId);
        Assert.Equal("Confirmed", dbAppointment.Status);
        var dbProposal = await h.Db.AppointmentProposals.SingleAsync();
        Assert.Equal("Booked", dbProposal.Status);
    }

    private sealed class SemanticPlanner(string type) : HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.IPlanningModelClient
    {
        public Task<HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.GeminiPlanningDecision> PlanObjectiveAsync(string objective, CancellationToken cancellationToken = default)
            => Task.FromResult(new HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.GeminiPlanningDecision {
                WorkflowType = type, AppointmentRequested = type == "AppointmentProposal"
            });
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required HospitalAssistantService Service { get; init; }
        public required TriageWorkflowService Workflows { get; init; }
        public required FakeIntentClient Intent { get; init; }
        public PatientDto Patient { get; } = new() { PatientId = 1, FirstName = "Test", LastName = "Patient", Email = "test@example.com", PhoneNumber = "0771234567" };
        public Task<AssistantConversationResponse> Send(string message, Guid? id = null) => Service.MessageAsync(Patient,
            new() { ConversationId = id, Message = message, RequestId = Guid.NewGuid() }, default);
        public AssistantActionRequest Confirm(AssistantPendingAction action) => new() {
            ActionId = action.ActionId, Decision = "confirm", RequestId = Guid.NewGuid(), DoctorTimeSlotId = action.Slots[0].DoctorTimeSlotId
        };
        public static async Task<Harness> Create(HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator.IPlanningModelClient? model = null)
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var tomorrow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AppointmentAgentTools.HospitalTimeZone).Date.AddDays(1).AddHours(13.5);
            var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(tomorrow, DateTimeKind.Unspecified), AppointmentAgentTools.HospitalTimeZone);
            var slot = new DoctorTimeSlot { DoctorName = "Dr. Silva", DoctorId = 1, Specialty = "Cardiology",
                StartAt = start, EndAt = start.AddHours(2), Capacity = 5, IsActive = true };
            db.DoctorTimeSlots.Add(slot);
            await db.SaveChangesAsync();
            var appointments = new AppointmentService(new AppointmentRepository(db), SmsTestSupport.Create(db));
            var tools = new FakeTools(appointments, slot);
            var workflows = TriageWorkflowServiceTests.CreateService(db, new TestSafeTriageAgents { UseChoiceQuestion = true });
            var intent = new FakeIntentClient();
            return new() { Db = db, Workflows = workflows, Intent = intent, Service = new(db, new AssistantAgentRegistry([]), workflows,
                new HospitalAppointmentProposalAgent(tools, new AppointmentProposalStore(db)),
                new SafetyValidationApprovalAgent(new SafetyApprovalTools(db, tools)), tools, appointments, SmsTestSupport.Create(db), intent, model) };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public void AssistantPreferences_StopWordsAreIgnoredAndSpecialtiesPrioritized()
    {
        IReadOnlyList<AgentDoctor> doctors = [
            new("D1", 1, "Dr. Dilnaka Perera", "General Medicine"),
            new("D2", 2, "Dr. Kasun Silva", "Cardiology")
        ];

        // "please suggest a doctor for me" -> candidate "for me" should be ignored (stop words)
        var queryForMe = AssistantPreferences.Query("please suggest a doctor for me", doctors);
        Assert.Null(queryForMe);

        // "like a general doctor for that" -> resolves to "General Medicine" alias
        var queryGeneral = AssistantPreferences.Query("like a general doctor for that", doctors);
        Assert.Equal("General Medicine", queryGeneral);

        // "like a general medicine doctor" -> resolves to "General Medicine"
        var queryGeneralMed = AssistantPreferences.Query("like a general medicine doctor", doctors);
        Assert.Equal("General Medicine", queryGeneralMed);
    }

    private sealed class FakeIntentClient : IAssessmentIntentClient
    {
        public AssessmentIntent? Next { get; set; }
        public bool UseNext { get; set; }
        public Task<AssessmentIntent?> InterpretAsync(string message, TriageFollowUpQuestionDto question,
            TriageGuidanceDto guidance, IReadOnlyList<TriageAnswerDto> answers, string? previousReply, CancellationToken token)
        {
            if (UseNext) return Task.FromResult(Next);
            var answer = question.Type switch {
                "number" or "severityScale" => "2",
                "yesNo" => "No",
                "multipleChoice" => question.Options.FirstOrDefault(o => o == "None of these") ?? question.Options.FirstOrDefault(),
                "singleChoice" => question.Options.FirstOrDefault(o => o.Contains("gradually", StringComparison.OrdinalIgnoreCase)) ?? question.Options.FirstOrDefault(),
                _ => message
            };
            return Task.FromResult<AssessmentIntent?>(new("ANSWER", true, answer, null));
        }
    }

    private sealed class FakeTools(IAppointmentService appointments, DoctorTimeSlot slot) : IAppointmentAgentTools
    {
        public Task<IReadOnlyList<AgentDoctor>> FindDoctorsAsync(string query) => Task.FromResult<IReadOnlyList<AgentDoctor>>(
            string.IsNullOrEmpty(query) || "Cardiology Dr. Silva".Contains(query, StringComparison.OrdinalIgnoreCase)
                ? [new("D1", 1, "Dr. Silva", "Cardiology")] : []);
        public async Task<IReadOnlyList<AgentSlot>> FindSlotsAsync(AgentDoctor doctor, DateOnly? date)
        {
            var available = await appointments.GetSlotsAsync(null, null, true, 1);
            return available.Where(s => !date.HasValue || DateOnly.FromDateTime(AppointmentAgentTools.Local(s.StartAt).DateTime) == date)
                .Select(s => new AgentSlot("S" + s.DoctorTimeSlotId, s.DoctorTimeSlotId, 1, s.DoctorName, s.Specialty,
                    AppointmentAgentTools.Local(s.StartAt), AppointmentAgentTools.Local(s.EndAt), s.NextAppointmentNumber, s.AvailableCount, s.ConsultationFee, "Room 1")).ToArray();
        }
        public async Task<AgentBooking> BookAsync(AgentSlot observedSlot, PatientDto patient)
        {
            var created = await appointments.CreateAppointmentAsync(new() { DoctorTimeSlotId = slot.DoctorTimeSlotId, PatientId = patient.PatientId,
                PatientName = patient.FullName, PatientEmail = patient.Email, PatientPhone = patient.PhoneNumber, AppointmentType = "Consultation" });
            return new(created.AppointmentId, created.DoctorTimeSlotId, created.AppointmentNumber, created.DoctorName,
                AppointmentAgentTools.Local(created.StartAt), created.Status);
        }
    }
}
