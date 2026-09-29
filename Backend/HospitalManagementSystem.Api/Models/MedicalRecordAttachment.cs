using System;

namespace HospitalManagementSystem.Api.Models
{
    public class MedicalRecordAttachment
    {
        public int AttachmentId { get; set; }
        public int MedicalRecordId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public MedicalRecord? MedicalRecord { get; set; }
    }
}
