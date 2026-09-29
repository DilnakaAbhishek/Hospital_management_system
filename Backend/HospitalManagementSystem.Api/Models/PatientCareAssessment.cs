namespace HospitalManagementSystem.Api.Models;

/// <summary>Persistent audit record for the four-agent Patient Care workflow.</summary>
public class PatientCareAssessment
{
    public int PatientCareAssessmentId { get; set; }
    public int PatientId { get; set; }
    public string Symptoms { get; set; } = string.Empty;
    public string? RequestedSpecialty { get; set; }
    public bool RequestedAppointmentProposal { get; set; }
    public string TriageLevel { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public string ClinicalJson { get; set; } = "{}";
    public string? ProposalJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
