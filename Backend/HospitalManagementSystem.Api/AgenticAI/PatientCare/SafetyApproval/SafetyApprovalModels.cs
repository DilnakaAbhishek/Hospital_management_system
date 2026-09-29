namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;

public sealed record ProposalConfirmationRequest(int ProposalId, int SelectedDoctorTimeSlotId);
public sealed record SafetyApprovalResult(string Status, bool RequiresClinicalApproval, string Message, int? AppointmentId = null);
