using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class DoctorSearchServiceTests
{
    [Theory]
    [InlineData("nimal")]
    [InlineData("PERERA")]
    [InlineData("cardio")]
    public async Task SearchApprovedDoctorsAsync_FindsByNameOrSpecialization(string query)
    {
        await using var db = CreateContext();
        await SeedDoctors(db);
        var service = CreateService(db);

        var results = await service.SearchApprovedDoctorsAsync(query);

        var doctor = Assert.Single(results);
        Assert.Equal("Nimal Perera", doctor.FullName);
        Assert.Equal("Cardiology", doctor.Specialization);
    }

    [Fact]
    public async Task SearchApprovedDoctorsAsync_HidesPendingAndDeclinedDoctors()
    {
        await using var db = CreateContext();
        await SeedDoctors(db);
        var service = CreateService(db);

        var results = await service.SearchApprovedDoctorsAsync(null);

        var doctor = Assert.Single(results);
        Assert.Equal("Nimal Perera", doctor.FullName);
    }

    [Fact]
    public async Task GetApprovedDoctorProfileAsync_ReturnsOnlyApprovedDoctorDetails()
    {
        await using var db = CreateContext();
        var ids = await SeedDoctors(db);
        var service = CreateService(db);

        var approved = await service.GetApprovedDoctorProfileAsync(ids.ApprovedId);
        var pending = await service.GetApprovedDoctorProfileAsync(ids.PendingId);

        Assert.NotNull(approved);
        Assert.Equal("doctor@hospital.lk", approved!.Email);
        Assert.Equal("SLMC-100", approved.SlmcLicenseNumber);
        Assert.Null(pending);
    }

    [Fact]
    public async Task UpdateProfileAsync_RefreshesMatchingSlotDoctorNamesButNotSpecialty()
    {
        await using var db = CreateContext();
        var ids = await SeedDoctors(db);
        var doctor = await db.Doctors.SingleAsync(value => value.DoctorId == ids.ApprovedId);
        var slot = new DoctorTimeSlot
        {
            DoctorId = doctor.DoctorId, DoctorName = "Dr. Nimal Perera", Specialty = "Cardiology",
            StartAt = DateTime.UtcNow.AddDays(1), EndAt = DateTime.UtcNow.AddDays(1).AddHours(1), Capacity = 1, IsActive = true
        };
        db.DoctorTimeSlots.Add(slot);
        await db.SaveChangesAsync();

        await CreateService(db).UpdateProfileAsync(doctor.UserId, new UpdateDoctorProfileDto
        {
            FirstName = "Nimal", LastName = "Silva", PhoneNumber = "0771234567"
        });

        Assert.Equal("Dr. Nimal Silva", slot.DoctorName);
        Assert.Equal("Cardiology", slot.Specialty);
    }

    [Fact]
    public async Task RequestDeletionAsync_ThrowsWhenDoctorHasUpcomingBookingsOrSlots()
    {
        await using var db = CreateContext();
        var ids = await SeedDoctors(db);
        var doctor = await db.Doctors.SingleAsync(d => d.DoctorId == ids.ApprovedId);

        var slot = new DoctorTimeSlot
        {
            DoctorId = doctor.DoctorId,
            DoctorName = "Dr. Nimal Perera",
            Specialty = "Cardiology",
            StartAt = DateTime.UtcNow.AddDays(2),
            EndAt = DateTime.UtcNow.AddDays(2).AddHours(2),
            Capacity = 5,
            IsActive = true
        };
        db.DoctorTimeSlots.Add(slot);
        await db.SaveChangesAsync();

        var appointment = new Appointment
        {
            DoctorTimeSlotId = slot.DoctorTimeSlotId,
            AppointmentNumber = 1,
            PatientName = "Test Patient",
            PatientPhone = "0770000000",
            AppointmentType = "Consultation",
            Reason = "Checkup",
            Status = "Confirmed"
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestDeletionAsync(doctor.UserId, "Leaving hospital"));

        Assert.Contains("upcoming confirmed patient appointments", ex.Message);
    }

    [Fact]
    public async Task CancelDeletionAsync_RevertsDoctorStatusToApproved()
    {
        await using var db = CreateContext();
        var ids = await SeedDoctors(db);
        var doctor = await db.Doctors.SingleAsync(d => d.DoctorId == ids.ApprovedId);
        doctor.RegistrationStatus = DoctorRegistrationStatuses.DeletionPending;
        doctor.DeclineReason = "Want to leave";
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var updated = await service.CancelDeletionAsync(doctor.DoctorId, 999, "Admin decided to keep doctor");

        Assert.NotNull(updated);
        Assert.Equal("Approved", updated!.RegistrationStatus);
        Assert.Null(doctor.DeclineReason);
        Assert.Equal(999, doctor.ReviewedByUserId);
    }

    private static DoctorService CreateService(ApplicationDbContext db) =>
        new(db, new PasswordHasher<User>());

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(int ApprovedId, int PendingId)> SeedDoctors(ApplicationDbContext db)
    {
        var approvedUser = new User
        {
            FullName = "Nimal Perera",
            Email = "doctor@hospital.lk",
            PasswordHash = "hash",
            Role = "Doctor"
        };
        var pendingUser = new User
        {
            FullName = "Kamal Silva",
            Email = "pending@hospital.lk",
            PasswordHash = "hash",
            Role = "Doctor"
        };
        var declinedUser = new User
        {
            FullName = "Sunil Fernando",
            Email = "declined@hospital.lk",
            PasswordHash = "hash",
            Role = "Doctor"
        };

        var approved = Doctor(approvedUser, "Nimal", "Perera", "Cardiology", "SLMC-100",
            DoctorRegistrationStatuses.Approved);
        var pending = Doctor(pendingUser, "Kamal", "Silva", "Neurology", "SLMC-200",
            DoctorRegistrationStatuses.Pending);
        var declined = Doctor(declinedUser, "Sunil", "Fernando", "Pediatrics", "SLMC-300",
            DoctorRegistrationStatuses.Declined);
        db.Doctors.AddRange(approved, pending, declined);
        await db.SaveChangesAsync();
        return (approved.DoctorId, pending.DoctorId);
    }

    private static Doctor Doctor(User user, string firstName, string lastName,
        string specialization, string license, string status) => new()
    {
        User = user,
        FirstName = firstName,
        LastName = lastName,
        NIC = $"20000000000{license[^1]}",
        Specialization = specialization,
        SlmcLicenseNumber = license,
        PhoneNumber = "0771234567",
        RegistrationStatus = status
    };
}
