using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.Services
{
    public class PatientService : IPatientService
    {
        private readonly IPatientRepository _repository;

        public PatientService(IPatientRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<PatientDto>> GetAllPatientsAsync(string? search, string? gender, string? bloodGroup, string? sortBy, string? sortDirection, int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Max(1, page);

            var patients = await _repository.GetAllAsync(search, gender, bloodGroup, sortBy, sortDirection, page, pageSize);
            var totalCount = await _repository.GetTotalCountAsync(search, gender, bloodGroup);

            return new PagedResult<PatientDto>
            {
                Data = patients.Select(MapToDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public Task<PatientSummaryDto> GetSummaryAsync() => _repository.GetSummaryAsync();

        public async Task<PatientDto?> GetPatientByIdAsync(int id)
        {
            var patient = await _repository.GetByIdAsync(id);
            return patient is null ? null : MapToDto(patient);
        }

        public async Task<PatientDto?> GetPatientByEmailAsync(string email)
        {
            var patient = await _repository.GetByEmailAsync(email.Trim().ToLowerInvariant());
            return patient is null ? null : MapToDto(patient);
        }

        public async Task<IEnumerable<PatientAppointmentHistoryDto>?> GetAppointmentHistoryAsync(int patientId)
        {
            if (await _repository.GetByIdAsync(patientId) is null) return null;

            var appointments = await _repository.GetAppointmentsByPatientIdAsync(patientId);
            return appointments.Select(appointment => new PatientAppointmentHistoryDto
            {
                AppointmentId = appointment.AppointmentId,
                ScheduledAt = appointment.DoctorTimeSlot!.StartAt,
                DoctorName = appointment.DoctorTimeSlot?.DoctorName ?? "Not assigned",
                Specialty = appointment.DoctorTimeSlot?.Specialty ?? "—",
                AppointmentType = appointment.AppointmentType,
                Status = appointment.Status,
                Reason = appointment.Reason,
            });
        }

        public async Task<PatientDto> CreatePatientAsync(CreatePatientDto dto)
        {
            if (!string.IsNullOrEmpty(dto.Email) && await _repository.ExistsByEmailAsync(dto.Email))
                throw new InvalidOperationException("A patient with this email already exists.");

            if (await _repository.ExistsByNICAsync(dto.NIC))
                throw new InvalidOperationException("A patient with this NIC already exists.");

            var patient = new Patient
            {
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth, DateTimeKind.Utc),
                Gender = dto.Gender,
                NIC = dto.NIC.Trim(),
                PhoneNumber = dto.PhoneNumber.Trim(),
                Email = dto.Email?.Trim().ToLower(),
                Address = dto.Address?.Trim(),
                BloodGroup = dto.BloodGroup,
                EmergencyContactName = dto.EmergencyContactName?.Trim() ?? string.Empty,
                EmergencyContactPhone = dto.EmergencyContactPhone?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(patient);
            return MapToDto(created);
        }

        public async Task<PatientDto?> UpdatePatientAsync(int id, UpdatePatientDto dto)
        {
            var patient = await _repository.GetByIdAsync(id);
            if (patient is null) return null;

            var normalizedEmail = dto.Email?.Trim().ToLower();
            var normalizedNic = dto.NIC.Trim();
            if (!string.IsNullOrEmpty(normalizedEmail) && await _repository.ExistsByEmailAsync(normalizedEmail, id))
                throw new InvalidOperationException("A patient with this email already exists.");

            if (await _repository.ExistsByNICAsync(normalizedNic, id))
                throw new InvalidOperationException("A patient with this NIC already exists.");

            patient.FirstName = dto.FirstName.Trim();
            patient.LastName = dto.LastName.Trim();
            patient.DateOfBirth = DateTime.SpecifyKind(dto.DateOfBirth, DateTimeKind.Utc);
            patient.Gender = dto.Gender.Trim();
            patient.NIC = normalizedNic;
            patient.PhoneNumber = dto.PhoneNumber.Trim();
            patient.Email = normalizedEmail;
            patient.Address = dto.Address?.Trim();
            patient.BloodGroup = dto.BloodGroup;
            patient.EmergencyContactName = dto.EmergencyContactName?.Trim();
            patient.EmergencyContactPhone = dto.EmergencyContactPhone?.Trim();
            patient.ProfileImageUrl = dto.ProfileImageUrl;

            var updated = await _repository.UpdateAsync(patient);
            return MapToDto(updated);
        }

        public async Task<bool> DeletePatientAsync(int id)
        {
            var patient = await _repository.GetByIdAsync(id);
            if (patient is null) return false;

            await _repository.DeleteAsync(patient);
            return true;
        }

        // Map Entity -> DTO helper
        private static PatientDto MapToDto(Patient p) => new()
        {
            PatientId = p.PatientId,
            FirstName = p.FirstName,
            LastName = p.LastName,
            DateOfBirth = p.DateOfBirth,
            Gender = p.Gender,
            NIC = p.NIC,
            PhoneNumber = p.PhoneNumber,
            Email = p.Email,
            Address = p.Address,
            BloodGroup = p.BloodGroup,
            EmergencyContactName = p.EmergencyContactName,
            EmergencyContactPhone = p.EmergencyContactPhone,
            ProfileImageUrl = p.ProfileImageUrl,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
