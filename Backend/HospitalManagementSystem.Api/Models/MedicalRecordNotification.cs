namespace HospitalManagementSystem.Api.Models;

/// <summary>
/// Notification created when a medical record or attachment is added.
/// - When a Doctor/Admin creates/finalizes a record → notify the Patient.
/// - When a Patient uploads an attachment → notify the assigned Doctor and all Admins.
/// </summary>
public class MedicalRecordNotification
{
    public int MedicalRecordNotificationId { get; set; }

    /// <summary>The record that triggered this notification.</summary>
    public int MedicalRecordId { get; set; }
    public MedicalRecord MedicalRecord { get; set; } = null!;

    /// <summary>
    /// Who should receive this notification.
    /// Use "Patient" for patient-facing alerts and "Staff" for doctor/admin alerts.
    /// </summary>
    public string RecipientRole { get; set; } = string.Empty;

    /// <summary>
    /// Optional: if the notification targets a specific patient or doctor by user ID.
    /// Null means the notification is for all staff of the given RecipientRole.
    /// </summary>
    public int? RecipientUserId { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Type: "RecordAdded" | "AttachmentUploaded"</summary>
    public string EventType { get; set; } = "RecordAdded";

    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
