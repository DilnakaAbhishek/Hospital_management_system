using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public interface IMedicalRecordRepository
    {
        Task<IEnumerable<MedicalRecord>> GetAllAsync(
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
            int pageSize);

        Task<int> GetTotalCountAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate);

        Task<MedicalRecord?> GetByIdAsync(int id);
        Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId);
        Task<MedicalRecordSummaryDto> GetSummaryAsync(int? doctorId = null);
        Task<MedicalRecord> CreateAsync(MedicalRecord record);
        Task<MedicalRecord> UpdateAsync(MedicalRecord record);
        Task DeleteAsync(MedicalRecord record);
        Task<MedicalRecordAttachment> AddAttachmentAsync(MedicalRecordAttachment attachment);
        Task<MedicalRecordAttachment?> GetAttachmentByIdAsync(int attachmentId);
        Task DeleteAttachmentAsync(MedicalRecordAttachment attachment);
    }
}
