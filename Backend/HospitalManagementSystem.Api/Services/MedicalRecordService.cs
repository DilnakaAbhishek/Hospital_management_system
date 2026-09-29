using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.Services
{
    public class MedicalRecordService : IMedicalRecordService
    {
        private readonly IMedicalRecordRepository _repo;
        private readonly ApplicationDbContext _db;

        public MedicalRecordService(IMedicalRecordRepository repo, ApplicationDbContext db)
        {
            _repo = repo;
            _db = db;
        }

        public async Task<PagedResult<MedicalRecordDto>> GetAllRecordsAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var total = await _repo.GetTotalCountAsync(patientId, doctorId, recordType, status, search, fromDate, toDate);
            var records = await _repo.GetAllAsync(patientId, doctorId, recordType, status, search, fromDate, toDate, sortBy, sortDirection, page, pageSize);

            return new PagedResult<MedicalRecordDto>
            {
                Data = records.Select(MapToDto),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<MedicalRecordDto?> GetRecordByIdAsync(int id)
        {
            var record = await _repo.GetByIdAsync(id);
            return record == null ? null : MapToDto(record);
        }

        public async Task<IEnumerable<MedicalRecordDto>?> GetPatientMedicalHistoryAsync(int patientId, string? userEmail, string? userRole)
        {
            var patient = await _db.Patients.FindAsync(patientId);
            if (patient == null)
                return null;

            // Role scope verification: Non-Admin, non-Doctor users can only access their own history
            if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(userRole, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(userEmail) ||
                    string.IsNullOrWhiteSpace(patient.Email) ||
                    !string.Equals(patient.Email.Trim(), userEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("You are not authorized to view another patient's medical history.");
                }
            }

            var records = await _repo.GetByPatientIdAsync(patientId);
            return records.Select(MapToDto);
        }

        public async Task<IEnumerable<MedicalRecordDto>?> GetMyMedicalRecordsAsync(string patientEmail)
        {
            var normalized = patientEmail?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalized)) return null;

            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == normalized);
            if (patient == null)
                return null;

            var records = await _repo.GetByPatientIdAsync(patient.PatientId);
            return records.Select(MapToDto);
        }

        public async Task<int?> GetPatientIdByEmailAsync(string email)
        {
            var normalized = email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalized)) return null;

            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == normalized);
            return patient?.PatientId;
        }

        public async Task<int?> GetDoctorIdByEmailAsync(string email)
        {
            var normalized = email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(normalized)) return null;

            var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.User.Email != null && d.User.Email.ToLower() == normalized);
            return doctor?.DoctorId;
        }

        public async Task<MedicalRecordSummaryDto> GetSummaryAsync(int? doctorId = null)
        {
            return await _repo.GetSummaryAsync(doctorId);
        }

        public async Task<MedicalRecordDto> CreateRecordAsync(CreateMedicalRecordDto dto, string? userEmail, string? userRole)
        {
            Patient? patient;

            // Role scope: If user is not Admin or Doctor, resolve their own patient record from their logged-in email
            if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(userRole, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(userEmail))
                    throw new UnauthorizedAccessException("Token missing user email claim.");

                var normalized = userEmail.Trim().ToLowerInvariant();
                patient = await _db.Patients.FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == normalized);
                if (patient == null)
                    throw new KeyNotFoundException($"No patient profile was found associated with your account ({userEmail}).");
            }
            else
            {
                patient = await _db.Patients.FindAsync(dto.PatientId);
                if (patient == null)
                    throw new KeyNotFoundException($"Patient with ID {dto.PatientId} was not found.");
            }

            int? doctorId = dto.DoctorId;

            // A doctor can only author records under their own profile. Never trust a
            // client-supplied DoctorId, since it would allow assigning a record to a
            // different doctor.
            if (string.Equals(userRole, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(userEmail))
                    throw new UnauthorizedAccessException("Token missing user email claim.");

                var docEmail = userEmail.Trim().ToLowerInvariant();
                var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.User.Email != null && d.User.Email.ToLower() == docEmail);
                if (doctor == null)
                    throw new UnauthorizedAccessException("No doctor profile was found for this account.");

                doctorId = doctor.DoctorId;
            }

            if (string.IsNullOrWhiteSpace(dto.Diagnosis) || dto.Diagnosis.Trim().Length < 3)
                throw new ArgumentException("Diagnosis is required and must be at least 3 characters long.");

            var recordDate = DateTime.SpecifyKind((dto.RecordDate ?? DateTime.UtcNow).Date, DateTimeKind.Utc);
            if (dto.RecordDate.HasValue && dto.RecordDate.Value.Date > DateTime.UtcNow.Date)
                throw new ArgumentException("Record date cannot be in the future. It should be the creation date or past consultation date.");

            if (dto.FollowUpDate.HasValue)
            {
                if (dto.FollowUpDate.Value.Date <= DateTime.UtcNow.Date)
                    throw new ArgumentException("Follow-up date must be a future date.");
                if (dto.FollowUpDate.Value.Date <= recordDate.Date)
                    throw new ArgumentException("Follow-up date must be strictly after the record date.");
            }

            var symptoms = !string.IsNullOrWhiteSpace(dto.Symptoms)
                ? dto.Symptoms.Trim()
                : (!string.IsNullOrWhiteSpace(dto.LabNotes) ? dto.LabNotes.Trim() : (!string.IsNullOrWhiteSpace(dto.PrescriptionNotes) ? dto.PrescriptionNotes.Trim() : "Clinical record"));

            var treatmentPlan = !string.IsNullOrWhiteSpace(dto.TreatmentPlan)
                ? dto.TreatmentPlan.Trim()
                : (!string.IsNullOrWhiteSpace(dto.PrescriptionNotes) ? dto.PrescriptionNotes.Trim() : "Follow general medical advice.");

            if (string.IsNullOrWhiteSpace(treatmentPlan) || treatmentPlan.Trim().Length < 3)
                throw new ArgumentException("Treatment plan / recommendations is required and must be at least 3 characters long.");

            // Only a Doctor or Admin can finalize a record. Patients are restricted to Draft.
            var isPrivilegedRole = string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(userRole, "Doctor", StringComparison.OrdinalIgnoreCase);
            var requestedStatus = string.IsNullOrWhiteSpace(dto.Status) ? MedicalRecordStatuses.Finalized : dto.Status;
            // Archived status is removed — treat it as Draft if somehow supplied
            var resolvedStatus = string.Equals(requestedStatus, "Archived", StringComparison.OrdinalIgnoreCase)
                ? MedicalRecordStatuses.Draft
                : requestedStatus;
            var finalStatus = isPrivilegedRole ? resolvedStatus : MedicalRecordStatuses.Draft;

            var record = new MedicalRecord
            {
                PatientId = patient.PatientId,
                DoctorId = doctorId,
                AppointmentId = dto.AppointmentId,
                RecordDate = recordDate,
                RecordType = string.IsNullOrWhiteSpace(dto.RecordType) ? MedicalRecordTypes.Consultation : dto.RecordType,
                Diagnosis = dto.Diagnosis.Trim(),
                Symptoms = symptoms,
                TreatmentPlan = treatmentPlan,
                PrescriptionNotes = dto.PrescriptionNotes?.Trim(),
                LabNotes = dto.LabNotes?.Trim(),
                FollowUpDate = dto.FollowUpDate,
                Status = finalStatus,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };


            if (dto.Attachments != null && dto.Attachments.Any())
            {
                foreach (var att in dto.Attachments)
                {
                    record.Attachments.Add(new MedicalRecordAttachment
                    {
                        FileName = att.FileName.Trim(),
                        FileType = att.FileType.Trim(),
                        FileUrl = att.FileUrl.Trim(),
                        FileSize = att.FileSize,
                        UploadedAt = DateTime.UtcNow
                    });
                }
            }

            var created = await _repo.CreateAsync(record);
            var loaded = await _repo.GetByIdAsync(created.MedicalRecordId);
            return MapToDto(loaded ?? created);
        }

        public async Task<MedicalRecordDto?> UpdateRecordAsync(int id, UpdateMedicalRecordDto dto)
        {
            var record = await _repo.GetByIdAsync(id);
            if (record == null)
                return null;

            if (string.IsNullOrWhiteSpace(dto.Diagnosis) || dto.Diagnosis.Trim().Length < 3)
                throw new ArgumentException("Diagnosis is required and must be at least 3 characters long.");

            if (dto.FollowUpDate.HasValue)
            {
                if (dto.FollowUpDate.Value.Date <= DateTime.UtcNow.Date)
                    throw new ArgumentException("Follow-up date must be a future date.");
                if (dto.FollowUpDate.Value.Date <= record.RecordDate.Date)
                    throw new ArgumentException("Follow-up date must be strictly after the record date.");
            }

            record.RecordType = dto.RecordType;
            record.Diagnosis = dto.Diagnosis.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Symptoms))
                record.Symptoms = dto.Symptoms.Trim();
            if (!string.IsNullOrWhiteSpace(dto.TreatmentPlan))
                record.TreatmentPlan = dto.TreatmentPlan.Trim();
            record.PrescriptionNotes = dto.PrescriptionNotes?.Trim();
            record.LabNotes = dto.LabNotes?.Trim();
            record.FollowUpDate = dto.FollowUpDate;
            if (dto.DoctorId.HasValue)
                record.DoctorId = dto.DoctorId;
            // Validate the requested status — Archived is removed, only Draft and Finalized are valid.
            var validStatuses = new[] { MedicalRecordStatuses.Draft, MedicalRecordStatuses.Finalized };
            if (!validStatuses.Contains(dto.Status, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid record status '{dto.Status}'. Only 'Draft' and 'Finalized' are accepted.");

            record.Status = dto.Status;

            var updated = await _repo.UpdateAsync(record);
            return MapToDto(updated);
        }

        public async Task<bool> DeleteRecordAsync(int id)
        {
            var record = await _repo.GetByIdAsync(id);
            if (record == null)
                return false;

            await _repo.DeleteAsync(record);
            return true;
        }

        public async Task<MedicalRecordAttachmentDto?> AddAttachmentAsync(int recordId, CreateAttachmentDto dto)
        {
            var record = await _repo.GetByIdAsync(recordId);
            if (record == null)
                return null;

            var attachment = new MedicalRecordAttachment
            {
                MedicalRecordId = recordId,
                FileName = dto.FileName.Trim(),
                FileType = dto.FileType.Trim(),
                FileUrl = dto.FileUrl.Trim(),
                FileSize = dto.FileSize,
                UploadedAt = DateTime.UtcNow
            };

            var created = await _repo.AddAttachmentAsync(attachment);
            return new MedicalRecordAttachmentDto
            {
                AttachmentId = created.AttachmentId,
                MedicalRecordId = created.MedicalRecordId,
                FileName = created.FileName,
                FileType = created.FileType,
                FileUrl = created.FileUrl,
                FileSize = created.FileSize,
                UploadedAt = created.UploadedAt
            };
        }

        public async Task<bool> DeleteAttachmentAsync(int recordId, int attachmentId)
        {
            var attachment = await _repo.GetAttachmentByIdAsync(attachmentId);
            if (attachment == null || attachment.MedicalRecordId != recordId)
                return false;

            await _repo.DeleteAttachmentAsync(attachment);
            return true;
        }

        private static MedicalRecordDto MapToDto(MedicalRecord m)
        {
            return new MedicalRecordDto
            {
                MedicalRecordId = m.MedicalRecordId,
                PatientId = m.PatientId,
                PatientName = m.Patient != null ? $"{m.Patient.FirstName} {m.Patient.LastName}".Trim() : string.Empty,
                PatientEmail = m.Patient?.Email ?? string.Empty,
                DoctorId = m.DoctorId,
                DoctorName = m.Doctor != null ? $"Dr. {m.Doctor.FirstName} {m.Doctor.LastName}".Trim() : null,
                DoctorSpecialization = m.Doctor?.Specialization,
                AppointmentId = m.AppointmentId,
                RecordDate = m.RecordDate,
                RecordType = m.RecordType,
                Diagnosis = m.Diagnosis,
                Symptoms = m.Symptoms,
                TreatmentPlan = m.TreatmentPlan,
                PrescriptionNotes = m.PrescriptionNotes,
                LabNotes = m.LabNotes,
                FollowUpDate = m.FollowUpDate,
                Status = m.Status,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                Attachments = m.Attachments?.Select(a => new MedicalRecordAttachmentDto
                {
                    AttachmentId = a.AttachmentId,
                    MedicalRecordId = a.MedicalRecordId,
                    FileName = a.FileName,
                    FileType = a.FileType,
                    FileUrl = a.FileUrl,
                    FileSize = a.FileSize,
                    UploadedAt = a.UploadedAt
                }).ToList() ?? new List<MedicalRecordAttachmentDto>()
            };
        }
    }
}
