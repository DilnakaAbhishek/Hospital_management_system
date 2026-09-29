using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.DTOs
{
    public class MedicalRecordAttachmentDto
    {
        public int AttachmentId { get; set; }
        public int MedicalRecordId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class CreateAttachmentDto
    {
        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string FileType { get; set; } = string.Empty;

        [Required, MaxLength(1000)]
        public string FileUrl { get; set; } = string.Empty;

        [Range(1, 50 * 1024 * 1024, ErrorMessage = "File size must be between 1 byte and 50 MB.")]
        public long FileSize { get; set; }
    }

    public class MedicalRecordDto
    {
        public int MedicalRecordId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientEmail { get; set; } = string.Empty;
        public int? DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public string? DoctorSpecialization { get; set; }
        public int? AppointmentId { get; set; }
        public DateTime RecordDate { get; set; }
        public string RecordType { get; set; } = MedicalRecordTypes.Consultation;
        public string Diagnosis { get; set; } = string.Empty;
        public string Symptoms { get; set; } = string.Empty;
        public string TreatmentPlan { get; set; } = string.Empty;
        public string? PrescriptionNotes { get; set; }
        public string? LabNotes { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string Status { get; set; } = MedicalRecordStatuses.Finalized;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<MedicalRecordAttachmentDto> Attachments { get; set; } = new();
    }

    public class CreateMedicalRecordDto
    {
        [Required]
        public int PatientId { get; set; }

        public int? DoctorId { get; set; }

        public int? AppointmentId { get; set; }

        public DateTime? RecordDate { get; set; }

        [Required, MaxLength(50)]
        public string RecordType { get; set; } = MedicalRecordTypes.Consultation;

        [Required, MaxLength(500)]
        public string Diagnosis { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Symptoms { get; set; }

        [MaxLength(2000)]
        public string? TreatmentPlan { get; set; }

        [MaxLength(2000)]
        public string? PrescriptionNotes { get; set; }

        [MaxLength(4000)]
        public string? LabNotes { get; set; }

        public DateTime? FollowUpDate { get; set; }

        [MaxLength(30)]
        public string Status { get; set; } = MedicalRecordStatuses.Finalized;

        public List<CreateAttachmentDto>? Attachments { get; set; }
    }

    public class UpdateMedicalRecordDto
    {
        [Required, MaxLength(50)]
        public string RecordType { get; set; } = MedicalRecordTypes.Consultation;

        [Required, MaxLength(500)]
        public string Diagnosis { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Symptoms { get; set; }

        [MaxLength(2000)]
        public string? TreatmentPlan { get; set; }

        [MaxLength(2000)]
        public string? PrescriptionNotes { get; set; }

        [MaxLength(4000)]
        public string? LabNotes { get; set; }

        public DateTime? FollowUpDate { get; set; }
        public int? DoctorId { get; set; }

        [Required, MaxLength(30)]
        public string Status { get; set; } = MedicalRecordStatuses.Finalized;
    }

    public class MedicalRecordSummaryDto
    {
        public int TotalRecords { get; set; }
        public int ConsultationCount { get; set; }
        public int LabReportCount { get; set; }
        public int DischargeSummaryCount { get; set; }
        public int PrescriptionCount { get; set; }
        public int GeneralNoteCount { get; set; }
        public int DraftCount { get; set; }
        public int FinalizedCount { get; set; }
        public int AddedThisMonth { get; set; }
    }
}
