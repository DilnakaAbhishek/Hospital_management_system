using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;

public interface ISafetyValidationApprovalAgent
{
    Task<SafetyApprovalResult> ConfirmAsync(ProposalConfirmationRequest request, PatientDto patient, CancellationToken cancellationToken = default);
}

public sealed class SafetyValidationApprovalAgent(ISafetyApprovalTools tools) : ISafetyValidationApprovalAgent
{
    public async Task<SafetyApprovalResult> ConfirmAsync(ProposalConfirmationRequest request, PatientDto patient, CancellationToken cancellationToken = default)
    {
        var proposal = await tools.GetOwnedProposalAsync(request.ProposalId, patient.PatientId, cancellationToken);
        if (proposal is null) return new("Rejected", false, "The appointment proposal was not found or does not belong to you.");
        if (proposal.ExpiresAt <= DateTime.UtcNow) return new("Expired", false, "This appointment proposal has expired. Please search again.");
        if (proposal.Status != "PendingPatientConfirmation") return new("Rejected", false, "This proposal cannot be confirmed in its current state.");
        if (!await tools.CanBookPatientAsync(patient.PatientId, cancellationToken)) return new("Rejected", true, "The safety assessment must be resolved before booking.");
        var slot = await tools.ValidateSelectedSlotAsync(proposal, request.SelectedDoctorTimeSlotId, cancellationToken);
        if (slot is null) return new("Rejected", false, "The selected appointment slot is no longer available. Please search again.");
        proposal.SelectedDoctorTimeSlotId = slot.DoctorTimeSlotId;
        proposal.ConfirmedAt = DateTime.UtcNow;
        proposal.RequiresClinicalApproval = false;
        var booking = await tools.FinalizeBookingAsync(slot, patient, cancellationToken);
        proposal.Status = "Booked";
        proposal.AppointmentId = booking.AppointmentId;
        await tools.SaveAsync(cancellationToken);
        return new("Booked", false, "Your confirmed appointment has been created.", booking.AppointmentId);
    }

}
