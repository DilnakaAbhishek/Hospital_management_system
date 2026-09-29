using System.Text.Json;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

public interface IAppointmentProposalStore
{
    Task<int> CreateAsync(int patientId, string triageLevel, IReadOnlyList<AgentSlot> slots, CancellationToken cancellationToken);
}

public sealed class AppointmentProposalStore(ApplicationDbContext db) : IAppointmentProposalStore
{
    public async Task<int> CreateAsync(int patientId, string triageLevel, IReadOnlyList<AgentSlot> slots, CancellationToken cancellationToken)
    {
        var proposal = new HospitalManagementSystem.Api.Models.AppointmentProposal { PatientId = patientId, TriageLevel = triageLevel, RequiresClinicalApproval = false,
            CandidateSlotsJson = JsonSerializer.Serialize(slots) };
        db.AppointmentProposals.Add(proposal);
        await db.SaveChangesAsync(cancellationToken);
        return proposal.AppointmentProposalId;
    }
}
