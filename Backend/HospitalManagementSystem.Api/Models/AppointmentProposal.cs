namespace HospitalManagementSystem.Api.Models;

public class AppointmentProposal
{
    public int AppointmentProposalId { get; set; }
    public string? ExecutionWorkflowId { get; set; }
    public int PatientId { get; set; }
    public string CandidateSlotsJson { get; set; } = "[]";
    public string TriageLevel { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingPatientConfirmation";
    public bool RequiresClinicalApproval { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
    public int? SelectedDoctorTimeSlotId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public int? AppointmentId { get; set; }
}
