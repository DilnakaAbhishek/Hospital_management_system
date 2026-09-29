using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class PatientServiceTests
{
    [Fact]
    public async Task CreatePatientAsync_CreatesPatientAndNormalizesEmail()
    {
        var repository = new FakePatientRepository();
        var service = new PatientService(repository);

        var result = await service.CreatePatientAsync(CreateDto(email: "  AMAL@EXAMPLE.COM "));

        Assert.Equal(1, result.PatientId);
        Assert.Equal("amal@example.com", result.Email);
        Assert.Single(repository.Patients);
    }

    [Fact]
    public async Task CreatePatientAsync_RejectsDuplicateNic()
    {
        var repository = new FakePatientRepository { Patients = [Patient(id: 1, nic: "850314123V")] };
        var service = new PatientService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePatientAsync(CreateDto()));

        Assert.Equal("A patient with this NIC already exists.", exception.Message);
    }

    [Fact]
    public async Task UpdatePatientAsync_UpdatesAllEditablePatientFields()
    {
        var repository = new FakePatientRepository { Patients = [Patient(id: 1)] };
        var service = new PatientService(repository);
        var update = new UpdatePatientDto
        {
            FirstName = "Nimal", LastName = "Silva", DateOfBirth = new DateTime(1995, 2, 3),
            Gender = "Male", NIC = "950203456V", PhoneNumber = "+94 77 222 3333",
            Email = "nimal@example.com", Address = "Kandy", BloodGroup = "O+",
            EmergencyContactName = "Saman Silva", EmergencyContactPhone = "+94 71 222 3333"
        };

        var result = await service.UpdatePatientAsync(1, update);

        Assert.NotNull(result);
        Assert.Equal("Nimal", result.FirstName);
        Assert.Equal("950203456V", result.NIC);
        Assert.Equal("nimal@example.com", result.Email);
        Assert.Equal("O+", result.BloodGroup);
    }

    [Fact]
    public async Task GetAllPatientsAsync_ReturnsPagingMetadata()
    {
        var repository = new FakePatientRepository { Patients = [Patient(id: 1), Patient(id: 2)] };
        var service = new PatientService(repository);

        var result = await service.GetAllPatientsAsync("amal", "Male", "B+", "name", "asc", 2, 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal("amal", repository.LastSearch);
        Assert.Equal("Male", repository.LastGender);
        Assert.Equal("B+", repository.LastBloodGroup);
        Assert.Equal("name", repository.LastSortBy);
    }

    [Fact]
    public async Task GetAppointmentHistoryAsync_ReturnsAppointmentsForExistingPatient()
    {
        var repository = new FakePatientRepository { Patients = [Patient(id: 1)] };
        repository.Appointments =
        [
            new Appointment
            {
                AppointmentId = 9, PatientId = 1,
                AppointmentType = "Consultation", Reason = "Checkup", Status = "Confirmed",
                DoctorTimeSlot = new DoctorTimeSlot { DoctorName = "Dr. Perera", Specialty = "Cardiology" }
            }
        ];
        var service = new PatientService(repository);

        var result = (await service.GetAppointmentHistoryAsync(1))!.Single();

        Assert.Equal(9, result.AppointmentId);
        Assert.Equal("Dr. Perera", result.DoctorName);
        Assert.Equal("Confirmed", result.Status);
    }

    private static CreatePatientDto CreateDto(string email = "amal@example.com") => new()
    {
        FirstName = "Amal", LastName = "Perera", DateOfBirth = new DateTime(1985, 3, 14),
        Gender = "Male", NIC = "850314123V", PhoneNumber = "+94 77 234 5678", Email = email,
        Address = "Colombo", BloodGroup = "B+", EmergencyContactName = "Kamala", EmergencyContactPhone = "+94 71 234 5678"
    };

    private static Patient Patient(int id, string nic = "850314123V") => new()
    {
        PatientId = id, FirstName = "Amal", LastName = "Perera", DateOfBirth = new DateTime(1985, 3, 14),
        Gender = "Male", NIC = nic, PhoneNumber = "+94 77 234 5678", Email = $"amal{id}@example.com",
        Address = "Colombo", BloodGroup = "B+", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };
}

internal sealed class FakePatientRepository : IPatientRepository
{
    public List<Patient> Patients { get; set; } = [];
    public List<Appointment> Appointments { get; set; } = [];
    public string? LastSearch { get; private set; }
    public string? LastGender { get; private set; }
    public string? LastBloodGroup { get; private set; }
    public string? LastSortBy { get; private set; }

    public Task<IEnumerable<Patient>> GetAllAsync(string? search, string? gender, string? bloodGroup, string? sortBy, string? sortDirection, int page, int pageSize)
    {
        LastSearch = search; LastGender = gender; LastBloodGroup = bloodGroup; LastSortBy = sortBy;
        return Task.FromResult<IEnumerable<Patient>>(Patients);
    }
    public Task<int> GetTotalCountAsync(string? search, string? gender, string? bloodGroup) => Task.FromResult(Patients.Count);
    public Task<PatientSummaryDto> GetSummaryAsync() => Task.FromResult(new PatientSummaryDto
    {
        TotalPatients = Patients.Count,
        MaleCount = Patients.Count(patient => patient.Gender == "Male"),
        FemaleCount = Patients.Count(patient => patient.Gender == "Female"),
        OtherCount = Patients.Count(patient => patient.Gender == "Other"),
    });
    public Task<Patient?> GetByIdAsync(int id) => Task.FromResult(Patients.SingleOrDefault(patient => patient.PatientId == id));
    public Task<IEnumerable<Appointment>> GetAppointmentsByPatientIdAsync(int patientId) => Task.FromResult<IEnumerable<Appointment>>(Appointments.Where(appointment => appointment.PatientId == patientId));
    public Task<Patient?> GetByEmailAsync(string email) => Task.FromResult(Patients.SingleOrDefault(patient => patient.Email == email));
    public Task<Patient> CreateAsync(Patient patient) { patient.PatientId = Patients.Count + 1; Patients.Add(patient); return Task.FromResult(patient); }
    public Task<Patient> UpdateAsync(Patient patient) { patient.UpdatedAt = DateTime.UtcNow; return Task.FromResult(patient); }
    public Task DeleteAsync(Patient patient) { Patients.Remove(patient); return Task.CompletedTask; }
    public Task<bool> ExistsByEmailAsync(string email, int? excludeId = null) => Task.FromResult(Patients.Any(patient => patient.Email == email && patient.PatientId != excludeId));
    public Task<bool> ExistsByNICAsync(string nic, int? excludeId = null) => Task.FromResult(Patients.Any(patient => patient.NIC == nic && patient.PatientId != excludeId));
}
