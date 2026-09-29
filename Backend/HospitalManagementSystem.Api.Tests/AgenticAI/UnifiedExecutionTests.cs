using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public class UnifiedExecutionTests
{
    [Fact]
    public async Task PlanSurvivesNewContextAndStoreWithOwnershipIntact()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        string id;
        await using (var db = new ApplicationDbContext(options))
        {
            var planner = new PlanningCoordinatorAgent(new Model(), new PlanningCoordinatorStore(db), NullLogger<PlanningCoordinatorAgent>.Instance);
            id = (await planner.PlanAsync(new() { PatientId = 42, Objective = "Book a doctor" })).WorkflowId;
        }
        await using var restarted = new ApplicationDbContext(options);
        var store = new PlanningCoordinatorStore(restarted);
        var record = await store.GetAsync(id);
        Assert.Equal(42, record!.PatientId);
        Assert.Equal(4, record.Steps.Count);
        Assert.Equal(record.Steps[0].StepId, record.Steps[1].Dependencies.Single());
        Assert.Empty(await store.GetByPatientIdAsync(99));
        record.PatientId = 99;
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(record));
    }

    [Fact]
    public async Task InvalidModelStepsCannotBecomeExecutableAndOrderIsCanonical()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var store = new PlanningCoordinatorStore(db);
        var planner = new PlanningCoordinatorAgent(new Model(), store, NullLogger<PlanningCoordinatorAgent>.Instance);
        var response = await planner.PlanAsync(new() { PatientId = 1, Objective = "Book a doctor" });
        Assert.Equal(PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.AppointmentProposal], response.Plan.RequiredSteps);
        Assert.DoesNotContain("BookImmediately", response.Plan.RequiredSteps);
        Assert.DoesNotContain("private reasoning", (await store.GetAsync(response.WorkflowId))!.Plan.Rationale);
    }

    [Fact]
    public void ProposalAgentReceivesNoBookingCapability()
    {
        var parameter = typeof(HospitalAppointmentProposalAgent).GetConstructors().Single().GetParameters()[0];
        Assert.Equal(typeof(IAppointmentSearchTools), parameter.ParameterType);
        Assert.DoesNotContain(typeof(IAppointmentSearchTools).GetMethods(), m => m.Name.Contains("Book"));
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Rejected")]
    [InlineData("Booked")]
    public async Task TerminalProposalCannotBeConfirmedAgain(string status)
    {
        var tools = new ApprovalTools { Proposal = new() { PatientId = 1, Status = status } };
        var result = await new SafetyValidationApprovalAgent(tools).ConfirmAsync(new(1, 1), new() { PatientId = 1 });
        Assert.Equal("Rejected", result.Status);
        Assert.Equal(0, tools.Bookings);
    }

    [Fact]
    public async Task PatientConfirmationRechecksAvailabilityBeforeBooking()
    {
        var tools = new ApprovalTools { Available = false, Proposal = new() { PatientId = 1, Status = "PendingPatientConfirmation" } };
        var agent = new SafetyValidationApprovalAgent(tools);
        Assert.Equal("Rejected", (await agent.ConfirmAsync(new(1, 1), new() { PatientId = 1 })).Status);
        Assert.Equal(0, tools.Bookings);
        Assert.Equal(1, tools.Rechecks);
    }

    private sealed class Model : IPlanningModelClient
    {
        public Task<GeminiPlanningDecision> PlanObjectiveAsync(string objective, CancellationToken cancellationToken = default) => Task.FromResult(new GeminiPlanningDecision {
            WorkflowType = "AppointmentProposal", RequiredSteps = ["PatientConfirmation", "BookImmediately"], Rationale = "private reasoning"
        });
    }

    private sealed class ApprovalTools : ISafetyApprovalTools
    {
        public AppointmentProposal Proposal { get; set; } = new() { PatientId = 1, Status = "PendingPatientConfirmation" };
        public bool Available { get; set; } = true;
        public int Bookings { get; private set; }
        public int Rechecks { get; private set; }
        public Task<AppointmentProposal?> GetOwnedProposalAsync(int proposalId, int patientId, CancellationToken token) => Task.FromResult<AppointmentProposal?>(Proposal.PatientId == patientId ? Proposal : null);
        public Task<AgentSlot?> ValidateSelectedSlotAsync(AppointmentProposal proposal, int slotId, CancellationToken token)
        {
            Rechecks++;
            return Task.FromResult<AgentSlot?>(Available ? new("S1", 1, 1, "Doctor", "General", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1), 1, 5, 100, "Room") : null);
        }
        public Task<AgentBooking> FinalizeBookingAsync(AgentSlot slot, PatientDto patient, CancellationToken token)
        {
            Bookings++;
            return Task.FromResult(new AgentBooking(1, 1, 1, "Doctor", slot.StartAt, "Confirmed"));
        }
        public Task SaveAsync(CancellationToken token) => Task.CompletedTask;
    }
}
