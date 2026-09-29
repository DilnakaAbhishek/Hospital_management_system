using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public class MedicalRecordRepository : IMedicalRecordRepository
    {
        private readonly ApplicationDbContext _db;

        public MedicalRecordRepository(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IEnumerable<MedicalRecord>> GetAllAsync(
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
            var query = ApplyFilters(_db.MedicalRecords.AsNoTracking()
                .Include(m => m.Patient)
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Attachments)
                .AsSplitQuery(),
                patientId, doctorId, recordType, status, search, fromDate, toDate);

            // Sorting
            bool desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            query = (sortBy?.ToLower()) switch
            {
                "patient" => desc ? query.OrderByDescending(m => m.Patient != null ? m.Patient.FirstName : "") : query.OrderBy(m => m.Patient != null ? m.Patient.FirstName : ""),
                "type" => desc ? query.OrderByDescending(m => m.RecordType) : query.OrderBy(m => m.RecordType),
                "status" => desc ? query.OrderByDescending(m => m.Status) : query.OrderBy(m => m.Status),
                "id" or "record" => desc ? query.OrderByDescending(m => m.MedicalRecordId) : query.OrderBy(m => m.MedicalRecordId),
                "date" => desc ? query.OrderByDescending(m => m.RecordDate.Date).ThenByDescending(m => m.CreatedAt).ThenByDescending(m => m.MedicalRecordId)
                               : query.OrderBy(m => m.RecordDate.Date).ThenBy(m => m.CreatedAt).ThenBy(m => m.MedicalRecordId),
                _ => desc ? query.OrderByDescending(m => m.RecordDate.Date).ThenByDescending(m => m.CreatedAt).ThenByDescending(m => m.MedicalRecordId)
                          : query.OrderBy(m => m.RecordDate.Date).ThenBy(m => m.CreatedAt).ThenBy(m => m.MedicalRecordId)
            };

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = ApplyFilters(_db.MedicalRecords.AsNoTracking(),
                patientId, doctorId, recordType, status, search, fromDate, toDate);
            return await query.CountAsync();
        }

        public async Task<MedicalRecord?> GetByIdAsync(int id)
        {
            return await _db.MedicalRecords
                .Include(m => m.Patient)
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.MedicalRecordId == id);
        }

        public async Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId)
        {
            return await _db.MedicalRecords.AsNoTracking()
                .Include(m => m.Doctor)
                .Include(m => m.Appointment)
                .Include(m => m.Attachments)
                .Where(m => m.PatientId == patientId)
                .OrderByDescending(m => m.RecordDate.Date)
                .ThenByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.MedicalRecordId)
                .ToListAsync();
        }

        public async Task<MedicalRecordSummaryDto> GetSummaryAsync(int? doctorId = null)
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var query = _db.MedicalRecords.AsNoTracking();
            if (doctorId.HasValue)
            {
                query = query.Where(m => m.DoctorId == doctorId.Value);
            }

            var summary = await query
                .GroupBy(_ => 1)
                .Select(g => new MedicalRecordSummaryDto
                {
                    TotalRecords = g.Count(),
                    ConsultationCount = g.Count(m => m.RecordType == MedicalRecordTypes.Consultation),
                    LabReportCount = g.Count(m => m.RecordType == MedicalRecordTypes.LabReport),
                    DischargeSummaryCount = g.Count(m => m.RecordType == MedicalRecordTypes.DischargeSummary),
                    PrescriptionCount = g.Count(m => m.RecordType == MedicalRecordTypes.Prescription),
                    GeneralNoteCount = g.Count(m => m.RecordType == MedicalRecordTypes.GeneralNote),
                    DraftCount = g.Count(m => m.Status == MedicalRecordStatuses.Draft),
                    FinalizedCount = g.Count(m => m.Status == MedicalRecordStatuses.Finalized),
                    AddedThisMonth = g.Count(m => m.CreatedAt >= startOfMonth)
                })
                .FirstOrDefaultAsync();

            return summary ?? new MedicalRecordSummaryDto();
        }

        public async Task<MedicalRecord> CreateAsync(MedicalRecord record)
        {
            _db.MedicalRecords.Add(record);
            await _db.SaveChangesAsync();
            return record;
        }

        public async Task<MedicalRecord> UpdateAsync(MedicalRecord record)
        {
            record.UpdatedAt = DateTime.UtcNow;
            _db.MedicalRecords.Update(record);
            await _db.SaveChangesAsync();
            return record;
        }

        public async Task DeleteAsync(MedicalRecord record)
        {
            _db.MedicalRecords.Remove(record);
            await _db.SaveChangesAsync();
        }

        public async Task<MedicalRecordAttachment> AddAttachmentAsync(MedicalRecordAttachment attachment)
        {
            _db.MedicalRecordAttachments.Add(attachment);
            await _db.SaveChangesAsync();
            return attachment;
        }

        public async Task<MedicalRecordAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return await _db.MedicalRecordAttachments.FindAsync(attachmentId);
        }

        public async Task DeleteAttachmentAsync(MedicalRecordAttachment attachment)
        {
            _db.MedicalRecordAttachments.Remove(attachment);
            await _db.SaveChangesAsync();
        }

        private static IQueryable<MedicalRecord> ApplyFilters(
            IQueryable<MedicalRecord> query,
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate)
        {
            if (patientId.HasValue)
                query = query.Where(m => m.PatientId == patientId.Value);

            if (doctorId.HasValue)
                query = query.Where(m => m.DoctorId == doctorId.Value);

            if (!string.IsNullOrWhiteSpace(recordType))
            {
                var rt = recordType.Trim().ToLower();
                query = query.Where(m => m.RecordType.ToLower() == rt);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status.Trim().ToLower();
                query = query.Where(m => m.Status.ToLower() == st);
            }

            if (fromDate.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc);
                query = query.Where(m => m.RecordDate >= fromUtc);
            }

            if (toDate.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
                query = query.Where(m => m.RecordDate <= toUtc);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                var numStr = s.StartsWith("#") ? s[1..] : s;
                bool isId = int.TryParse(numStr, out int targetId);

                query = query.Where(m =>
                    (isId && m.MedicalRecordId == targetId) ||
                    m.Diagnosis.ToLower().Contains(s) ||
                    m.Symptoms.ToLower().Contains(s) ||
                    m.TreatmentPlan.ToLower().Contains(s) ||
                    m.RecordType.ToLower().Contains(s) ||
                    m.Status.ToLower().Contains(s) ||
                    (m.PrescriptionNotes != null && m.PrescriptionNotes.ToLower().Contains(s)) ||
                    (m.LabNotes != null && m.LabNotes.ToLower().Contains(s)) ||
                    (m.Patient != null && (
                        (m.Patient.FirstName.ToLower() + " " + m.Patient.LastName.ToLower()).Contains(s) ||
                        m.Patient.FirstName.ToLower().Contains(s) ||
                        m.Patient.LastName.ToLower().Contains(s) ||
                        (m.Patient.Email != null && m.Patient.Email.ToLower().Contains(s)) ||
                        (m.Patient.NIC != null && m.Patient.NIC.ToLower().Contains(s)) ||
                        (m.Patient.PhoneNumber != null && m.Patient.PhoneNumber.ToLower().Contains(s))
                    )) ||
                    (m.Doctor != null && (
                        (m.Doctor.FirstName.ToLower() + " " + m.Doctor.LastName.ToLower()).Contains(s) ||
                        m.Doctor.FirstName.ToLower().Contains(s) ||
                        m.Doctor.LastName.ToLower().Contains(s) ||
                        (m.Doctor.Specialization != null && m.Doctor.Specialization.ToLower().Contains(s))
                    ))
                );
            }

            return query;
        }
    }
}
