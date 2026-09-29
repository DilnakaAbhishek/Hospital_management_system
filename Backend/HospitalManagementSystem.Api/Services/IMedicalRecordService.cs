using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services
{
    public interface IMedicalRecordService
    {
        Task<PagedResult<MedicalRecordDto>> GetAllRecordsAsync(
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

        Task<MedicalRecordDto?> GetRecordByIdAsync(int id);
        Task<IEnumerable<MedicalRecordDto>?> GetPatientMedicalHistoryAsync(int patientId, string? userEmail, string? userRole);
        Task<IEnumerable<MedicalRecordDto>?> GetMyMedicalRecordsAsync(string patientEmail);
        Task<int?> GetPatientIdByEmailAsync(string email);
        Task<int?> GetDoctorIdByEmailAsync(string email);
        Task<MedicalRecordSummaryDto> GetSummaryAsync(int? doctorId = null);
        Task<MedicalRecordDto> CreateRecordAsync(CreateMedicalRecordDto dto, string? userEmail, string? userRole);
        Task<MedicalRecordDto?> UpdateRecordAsync(int id, UpdateMedicalRecordDto dto);
        Task<bool> DeleteRecordAsync(int id);
        Task<MedicalRecordAttachmentDto?> AddAttachmentAsync(int recordId, CreateAttachmentDto dto);
        Task<bool> DeleteAttachmentAsync(int recordId, int attachmentId);
    }
}
