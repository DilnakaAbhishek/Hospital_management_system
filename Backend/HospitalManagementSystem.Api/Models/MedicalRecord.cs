using System;
using System.Collections.Generic;

namespace HospitalManagementSystem.Api.Models
{
    public static class MedicalRecordStatuses
    {
        public const string Draft = "Draft";
        public const string Finalized = "Finalized";
        // Note: Archived status has been removed. Records are either Draft or Finalized.
    }

    public static class MedicalRecordTypes
    {
        public const string Consultation = "Consultation";
        public const string LabReport = "LabReport";
        public const string DischargeSummary = "DischargeSummary";
        public const string Prescription = "Prescription";
        public const string GeneralNote = "GeneralNote";
    }

    public class MedicalRecord
    {
        public int MedicalRecordId { get; set; }
        public int PatientId { get; set; }
        public int? DoctorId { get; set; }
        public int? AppointmentId { get; set; }
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;
        public string RecordType { get; set; } = MedicalRecordTypes.Consultation;
        public string Diagnosis { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string TreatmentPlan { get; set; } = string.Empty;
        public string? PrescriptionNotes { get; set; }
        public string? LabNotes { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string Status { get; set; } = MedicalRecordStatuses.Finalized;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Patient? Patient { get; set; }
        public Doctor? Doctor { get; set; }
        public Appointment? Appointment { get; set; }
        public ICollection<MedicalRecordAttachment> Attachments { get; set; } = new List<MedicalRecordAttachment>();
    }
}
