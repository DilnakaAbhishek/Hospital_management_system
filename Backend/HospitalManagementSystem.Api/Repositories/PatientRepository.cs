using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.Repositories
{
    public class PatientRepository : IPatientRepository
    {
        private readonly ApplicationDbContext _context;

        public PatientRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Patient>> GetAllAsync(string? search, string? gender, string? bloodGroup, string? sortBy, string? sortDirection, int page, int pageSize)
        {
            var query = ApplyFilters(_context.Patients.AsQueryable(), search, gender, bloodGroup);
            var descending = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

            query = sortBy?.ToLowerInvariant() switch
            {
                "name" => descending ? query.OrderByDescending(p => p.FirstName).ThenByDescending(p => p.LastName) : query.OrderBy(p => p.FirstName).ThenBy(p => p.LastName),
                "dob" => descending ? query.OrderByDescending(p => p.DateOfBirth) : query.OrderBy(p => p.DateOfBirth),
                _ => descending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
            };

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public Task<int> GetTotalCountAsync(string? search, string? gender, string? bloodGroup) =>
            ApplyFilters(_context.Patients.AsQueryable(), search, gender, bloodGroup).CountAsync();

        public async Task<PatientSummaryDto> GetSummaryAsync()
        {
            var patients = _context.Patients.AsNoTracking();
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return new PatientSummaryDto
            {
                TotalPatients = await patients.CountAsync(),
                RegisteredThisMonth = await patients.CountAsync(patient => patient.CreatedAt >= startOfMonth),
                MaleCount = await patients.CountAsync(patient => patient.Gender == "Male"),
                FemaleCount = await patients.CountAsync(patient => patient.Gender == "Female"),
                OtherCount = await patients.CountAsync(patient => patient.Gender == "Other"),
            };
        }

        public async Task<Patient?> GetByIdAsync(int id) =>
            await _context.Patients.FindAsync(id);

        public async Task<IEnumerable<Appointment>> GetAppointmentsByPatientIdAsync(int patientId) =>
            await _context.Appointments
                .AsNoTracking()
                .Include(appointment => appointment.DoctorTimeSlot)
                .Where(appointment => appointment.PatientId == patientId)
                .OrderByDescending(appointment => appointment.DoctorTimeSlot!.StartAt)
                .ToListAsync();

        public async Task<Patient?> GetByEmailAsync(string email) =>
            await _context.Patients.FirstOrDefaultAsync(p => p.Email == email);

        public async Task<Patient> CreateAsync(Patient patient)
        {
            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();
            return patient;
        }

        public async Task<Patient> UpdateAsync(Patient patient)
        {
            patient.UpdatedAt = DateTime.UtcNow;
            _context.Patients.Update(patient);
            await _context.SaveChangesAsync();
            return patient;
        }

        public async Task DeleteAsync(Patient patient)
        {
            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();
        }

        public async Task<string?> GetDeletionBlockReasonAsync(int patientId)
        {
            var hasAppointments = await _context.Appointments.AnyAsync(a => a.PatientId == patientId);
            var hasMedicalRecords = await _context.MedicalRecords.AnyAsync(m => m.PatientId == patientId);
            var hasTriageWorkflows = await _context.TriageWorkflows.AnyAsync(t => t.PatientId == patientId);
            var hasProposals = await _context.AppointmentProposals.AnyAsync(p => p.PatientId == patientId);
            var hasAssessments = await _context.PatientCareAssessments.AnyAsync(a => a.PatientId == patientId);

            var dependencies = new List<string>();
            if (hasAppointments) dependencies.Add("appointments");
            if (hasMedicalRecords) dependencies.Add("medical records");
            if (hasTriageWorkflows) dependencies.Add("triage workflows");
            if (hasProposals) dependencies.Add("appointment proposals");
            if (hasAssessments) dependencies.Add("care assessments");

            if (dependencies.Count > 0)
            {
                return $"Cannot delete patient because they have existing {string.Join(", ", dependencies)}.";
            }

            return null;
        }

        public async Task<bool> ExistsByEmailAsync(string email, int? excludeId = null) =>
            await _context.Patients.AnyAsync(p => p.Email == email && p.PatientId != excludeId);

        public async Task<bool> ExistsByNICAsync(string nic, int? excludeId = null) =>
            await _context.Patients.AnyAsync(p => p.NIC == nic && p.PatientId != excludeId);

        private static IQueryable<Patient> ApplyFilters(IQueryable<Patient> query, string? search, string? gender, string? bloodGroup)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchPattern = $"%{search.Trim()}%";
                query = query.Where(p =>
                    EF.Functions.ILike(p.FirstName, searchPattern) ||
                    EF.Functions.ILike(p.LastName, searchPattern) ||
                    (p.Email != null && EF.Functions.ILike(p.Email, searchPattern)) ||
                    EF.Functions.ILike(p.NIC, searchPattern) ||
                    EF.Functions.ILike(p.PhoneNumber, searchPattern));
            }

            if (!string.IsNullOrWhiteSpace(gender))
                query = query.Where(p => p.Gender == gender.Trim());

            if (!string.IsNullOrWhiteSpace(bloodGroup))
                query = query.Where(p => p.BloodGroup == bloodGroup.Trim());

            return query;
        }
    }
}
