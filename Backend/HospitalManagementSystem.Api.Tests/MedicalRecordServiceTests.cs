using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class MedicalRecordServiceTests
{
    private class FakeMedicalRecordRepo : IMedicalRecordRepository
    {
        public List<MedicalRecord> Records { get; set; } = new();

        public Task<IEnumerable<MedicalRecord>> GetAllAsync(
            int? patientId, int? doctorId, string? recordType, string? status, string? search,
            DateTime? fromDate, DateTime? toDate, string? sortBy, string? sortDirection, int page, int pageSize)
        {
            return Task.FromResult<IEnumerable<MedicalRecord>>(Records);
        }

        public Task<int> GetTotalCountAsync(
            int? patientId, int? doctorId, string? recordType, string? status, string? search,
            DateTime? fromDate, DateTime? toDate)
        {
            return Task.FromResult(Records.Count);
        }

        public Task<MedicalRecord?> GetByIdAsync(int id)
        {
            return Task.FromResult(Records.FirstOrDefault(r => r.MedicalRecordId == id));
        }

        public Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId)
        {
            return Task.FromResult<IEnumerable<MedicalRecord>>(Records.Where(r => r.PatientId == patientId));
        }

        public Task<MedicalRecordSummaryDto> GetSummaryAsync(int? doctorId = null)
        {
            return Task.FromResult(new MedicalRecordSummaryDto());
        }

        public Task<MedicalRecord> CreateAsync(MedicalRecord record)
        {
            record.MedicalRecordId = Records.Count + 1;
            Records.Add(record);
            return Task.FromResult(record);
        }

        public Task<MedicalRecord> UpdateAsync(MedicalRecord record)
        {
            var idx = Records.FindIndex(r => r.MedicalRecordId == record.MedicalRecordId);
            if (idx >= 0) Records[idx] = record;
            return Task.FromResult(record);
        }

        public Task DeleteAsync(MedicalRecord record)
        {
            Records.Remove(record);
            return Task.CompletedTask;
        }

        public Task<MedicalRecordAttachment> AddAttachmentAsync(MedicalRecordAttachment attachment)
        {
            return Task.FromResult(attachment);
        }

        public Task<MedicalRecordAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return Task.FromResult<MedicalRecordAttachment?>(null);
        }

        public Task DeleteAttachmentAsync(MedicalRecordAttachment attachment)
        {
            return Task.CompletedTask;
        }
    }

    private static ApplicationDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var db = new ApplicationDbContext(options);

        // Seed a sample patient
        if (!db.Patients.Any(p => p.PatientId == 1))
        {
            db.Patients.Add(new Patient
            {
                PatientId = 1,
                FirstName = "Kamal",
                LastName = "Silva",
                Email = "kamal@example.com",
                DateOfBirth = new DateTime(1985, 5, 20),
                Gender = "Male",
                NIC = "198512345678",
                PhoneNumber = "+94771234567"
            });
        }

        // Seed a sample doctor
        if (!db.Doctors.Any(d => d.DoctorId == 1))
        {
            var user = new User
            {
                UserId = 1,
                Email = "doctor@example.com",
                FullName = "Dr. Perera",
                Role = "Doctor",
                PasswordHash = "hash"
            };
            db.Users.Add(user);
            db.Doctors.Add(new Doctor
            {
                DoctorId = 1,
                UserId = 1,
                User = user,
                FirstName = "Dr.",
                LastName = "Perera",
                Specialization = "General Medicine",
                NIC = "197512345678",
                SlmcLicenseNumber = "SLMC-1234"
            });
        }

        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task CreateRecordAsync_RejectsFutureRecordDate()
    {
        var db = CreateInMemoryDb(nameof(CreateRecordAsync_RejectsFutureRecordDate));
        var repo = new FakeMedicalRecordRepo();
        var service = new MedicalRecordService(repo, db);

        var dto = new CreateMedicalRecordDto
        {
            PatientId = 1,
            Diagnosis = "Hypertension Check",
            TreatmentPlan = "Follow diet recommendations",
            RecordDate = DateTime.UtcNow.AddDays(2), // In future!
            RecordType = MedicalRecordTypes.Consultation,
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateRecordAsync(dto, "doctor@example.com", "Doctor"));

        Assert.Contains("Record date cannot be in the future", ex.Message);
    }

    [Fact]
    public async Task CreateRecordAsync_RejectsPastFollowUpDate()
    {
        var db = CreateInMemoryDb(nameof(CreateRecordAsync_RejectsPastFollowUpDate));
        var repo = new FakeMedicalRecordRepo();
        var service = new MedicalRecordService(repo, db);

        var dto = new CreateMedicalRecordDto
        {
            PatientId = 1,
            Diagnosis = "Hypertension Check",
            TreatmentPlan = "Follow diet recommendations",
            RecordDate = DateTime.UtcNow.Date,
            FollowUpDate = DateTime.UtcNow.AddDays(-2), // In past!
            RecordType = MedicalRecordTypes.Consultation,
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateRecordAsync(dto, "doctor@example.com", "Doctor"));

        Assert.Contains("Follow-up date must be a future date", ex.Message);
    }

    [Fact]
    public async Task CreateRecordAsync_RejectsFollowUpDateBeforeOrOnRecordDate()
    {
        var db = CreateInMemoryDb(nameof(CreateRecordAsync_RejectsFollowUpDateBeforeOrOnRecordDate));
        var repo = new FakeMedicalRecordRepo();
        var service = new MedicalRecordService(repo, db);

        var recordDate = DateTime.UtcNow.Date;
        var dto = new CreateMedicalRecordDto
        {
            PatientId = 1,
            Diagnosis = "Routine Examination",
            TreatmentPlan = "Maintain healthy hydration",
            RecordDate = recordDate,
            FollowUpDate = recordDate, // Same date as record!
            RecordType = MedicalRecordTypes.Consultation,
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateRecordAsync(dto, "doctor@example.com", "Doctor"));

        Assert.True(
            ex.Message.Contains("Follow-up date must be a future date") ||
            ex.Message.Contains("Follow-up date must be strictly after the record date"));
    }

    [Fact]
    public async Task CreateRecordAsync_RejectsEmptyOrShortDiagnosis()
    {
        var db = CreateInMemoryDb(nameof(CreateRecordAsync_RejectsEmptyOrShortDiagnosis));
        var repo = new FakeMedicalRecordRepo();
        var service = new MedicalRecordService(repo, db);

        var dto = new CreateMedicalRecordDto
        {
            PatientId = 1,
            Diagnosis = "A", // too short!
            TreatmentPlan = "Valid treatment plan",
            RecordDate = DateTime.UtcNow.Date,
            RecordType = MedicalRecordTypes.Consultation,
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateRecordAsync(dto, "doctor@example.com", "Doctor"));

        Assert.Contains("Diagnosis is required and must be at least 3 characters long", ex.Message);
    }

    [Fact]
    public async Task CreateRecordAsync_AcceptsValidDatesAndCreatesRecord()
    {
        var db = CreateInMemoryDb(nameof(CreateRecordAsync_AcceptsValidDatesAndCreatesRecord));
        var repo = new FakeMedicalRecordRepo();
        var service = new MedicalRecordService(repo, db);

        var today = DateTime.UtcNow.Date;
        var futureFollowUp = today.AddDays(14);

        var dto = new CreateMedicalRecordDto
        {
            PatientId = 1,
            Diagnosis = "Bronchial Asthma Consultation",
            TreatmentPlan = "Use salbutamol inhaler PRN and maintain diary",
            RecordDate = today,
            FollowUpDate = futureFollowUp,
            RecordType = MedicalRecordTypes.Consultation,
            Status = MedicalRecordStatuses.Finalized
        };

        var created = await service.CreateRecordAsync(dto, "admin@example.com", "Admin");

        Assert.NotNull(created);
        Assert.Equal("Bronchial Asthma Consultation", created.Diagnosis);
        Assert.Equal(today, created.RecordDate.Date);
        Assert.Equal(futureFollowUp, created.FollowUpDate?.Date);
    }

    [Fact]
    public async Task UpdateRecordAsync_RejectsPastFollowUpDate()
    {
        var db = CreateInMemoryDb(nameof(UpdateRecordAsync_RejectsPastFollowUpDate));
        var repo = new FakeMedicalRecordRepo
        {
            Records = new List<MedicalRecord>
            {
                new MedicalRecord
                {
                    MedicalRecordId = 10,
                    PatientId = 1,
                    RecordDate = DateTime.UtcNow.Date,
                    Diagnosis = "Existing Diagnosis",
                    TreatmentPlan = "Existing Treatment",
                    Status = MedicalRecordStatuses.Finalized,
                }
            }
        };
        var service = new MedicalRecordService(repo, db);

        var updateDto = new UpdateMedicalRecordDto
        {
            RecordType = MedicalRecordTypes.Consultation,
            Diagnosis = "Updated Diagnosis",
            TreatmentPlan = "Updated Plan",
            FollowUpDate = DateTime.UtcNow.AddDays(-1), // Past date!
            Status = MedicalRecordStatuses.Finalized,
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateRecordAsync(10, updateDto));

        Assert.Contains("Follow-up date must be a future date", ex.Message);
    }
}
