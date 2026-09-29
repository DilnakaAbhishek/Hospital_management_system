using System.Reflection;
using System.Security.Claims;
using HospitalManagementSystem.Api.Controllers;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class AppointmentServiceTests
{
    [Fact]
    public async Task CreateAppointment_RechecksAvailabilityAfterAnotherPatientTakesPreviewedNumber()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup, capacity: 2);
        var service = CreateService(db);
        var preview = Assert.Single(await service.GetSlotsAsync(null, null, true));
        Assert.Equal(1, preview.NextAppointmentNumber);

        var other = await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId, patientName: "Other Patient"));
        var confirmed = await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId));
        Assert.Equal(1, other.AppointmentNumber);
        Assert.Equal(2, confirmed.AppointmentNumber);
        var full = Assert.Single(await service.GetSlotsAsync(null, null, false));
        Assert.Equal(0, full.NextAppointmentNumber);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId)));

        await service.CancelAppointmentAsync(other.AppointmentId, "Patient unavailable");
        var available = Assert.Single(await service.GetSlotsAsync(null, null, true));
        Assert.Equal(1, available.NextAppointmentNumber);
    }

    [Fact]
    public async Task AppointmentTime_UsesSessionStartRegardlessOfQueueNumberOrCapacity()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup, capacity: 4);
        var service = CreateService(db);
        var first = await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId));
        var last = await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId, patientName: "Last Patient"));

        Assert.Equal(slot.StartAt, first.StartAt);
        Assert.Equal(slot.StartAt, last.StartAt);
        slot.Capacity = 8;
        ScheduleAppointmentUpdates.Apply(slot, notify: true);
        await db.SaveChangesAsync();
        var updated = await service.GetAllAppointmentsAsync(null, null, null, null, null, null, 1, 50);
        Assert.All(updated.Data, appointment => Assert.Equal(slot.StartAt, appointment.StartAt));
        Assert.All(await db.AppointmentNotifications.ToListAsync(), notification =>
        {
            Assert.Contains("New appointment time:", notification.Message);
            Assert.DoesNotContain("estimated", notification.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Completion_UpdatesExpiredConfirmedAppointmentsBeforeFiltering()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var past = await AddSlotAsync(db, setup, start: DateTime.UtcNow.AddHours(-2));
        var future = await AddSlotAsync(db, setup);
        var expired = await AddAppointmentAsync(db, past);
        var cancelled = await AddAppointmentAsync(db, past, appointmentNumber: 2, status: "Cancelled");
        var upcoming = await AddAppointmentAsync(db, future);
        var service = new AppointmentService(new AppointmentRepository(db), SmsTestSupport.Create(db));
        var result = await service.GetAllAppointmentsAsync(null, "Completed", null, null, null, null, 1, 50);
        Assert.Equal(expired.AppointmentId, Assert.Single(result.Data).AppointmentId);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Confirmed", upcoming.Status);
        await AppointmentCompletionService.CompleteDueAsync(db);
        Assert.Equal("Completed", expired.Status);
        Assert.Empty(await db.AppointmentNotifications.ToListAsync());
    }

    [Fact]
    public async Task CreateAppointment_AssignsSequentialNumbersAndUniqueIds()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup, capacity: 2);
        var service = CreateService(db);
        var first = await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId));
        var second = await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId, patientName: "Second Patient"));
        Assert.Equal(1, first.AppointmentNumber);
        Assert.Equal(2, second.AppointmentNumber);
        Assert.True(first.AppointmentId > 0);
        Assert.NotEqual(first.AppointmentId, second.AppointmentId);
    }
    [Fact]
    public async Task CreateAppointment_RejectsFullyBookedSlot()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup, capacity: 1);
        var service = CreateService(db);
        await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId, patientName: "Second Patient")));

        Assert.Equal("Selected doctor time slot is fully booked.", exception.Message);
    }

    [Fact]
    public async Task CancelAppointment_ReleasesAppointmentNumberForCapacityChecks()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup, capacity: 1);
        var service = CreateService(db);
        var first = await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId));

        var cancelled = await service.CancelAppointmentAsync(first.AppointmentId, "Patient unavailable");
        var replacement = await service.CreateAppointmentAsync(Appointment(slot.DoctorTimeSlotId, patientName: "Replacement"));

        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled!.Status);
        Assert.Equal("Patient unavailable", cancelled.CancellationReason);
        Assert.Equal(1, replacement.AppointmentNumber);
    }

    [Fact]
    public async Task RescheduleAppointment_MovesToNextAvailableSlotNumberAndKeepsConfirmation()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var source = await AddSlotAsync(db, setup, start: DateTime.UtcNow.AddDays(2), capacity: 2);
        var destination = await AddSlotAsync(db, setup, start: DateTime.UtcNow.AddDays(3), capacity: 2);
        var service = CreateService(db);
        await service.CreateAppointmentAsync(Appointment(destination.DoctorTimeSlotId, patientName: "Existing Patient"));
        var appointment = await service.CreateAppointmentAsync(Appointment(source.DoctorTimeSlotId, patientName: "Moving Patient"));

        var rescheduled = await service.RescheduleAppointmentAsync(appointment.AppointmentId, destination.DoctorTimeSlotId);

        Assert.NotNull(rescheduled);
        Assert.Equal(destination.DoctorTimeSlotId, rescheduled!.DoctorTimeSlotId);
        Assert.Equal(2, rescheduled.AppointmentNumber);
        Assert.Equal("Confirmed", rescheduled.Status);
        Assert.Equal(destination.StartAt, rescheduled.StartAt);
    }

    [Fact]
    public async Task RescheduleAppointment_RejectsTerminalAppointment()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var source = await AddSlotAsync(db, setup, start: DateTime.UtcNow.AddDays(2), capacity: 1);
        var destination = await AddSlotAsync(db, setup, start: DateTime.UtcNow.AddDays(3), capacity: 1);
        var appointment = await AddAppointmentAsync(db, source, status: "Completed");
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RescheduleAppointmentAsync(appointment.AppointmentId, destination.DoctorTimeSlotId));

        Assert.Equal("Terminal appointments cannot be rescheduled.", exception.Message);
    }

    [Fact]
    public async Task UpdateSlot_RejectsCapacityBelowActiveBookings()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup, capacity: 3);
        await AddAppointmentAsync(db, slot, appointmentNumber: 1);
        await AddAppointmentAsync(db, slot, appointmentNumber: 2, patientName: "Second Patient");
        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateSlotAsync(slot.DoctorTimeSlotId, new UpdateDoctorTimeSlotDto
            {
                DoctorId = setup.FirstDoctorId,
                StartAt = slot.StartAt,
                EndAt = slot.EndAt,
                Capacity = 1,
                ConsultationFee = 2500m,
                IsActive = true
            }));

        Assert.Equal("Slot capacity cannot be less than the number of booked appointments.", exception.Message);
    }

    [Fact]
    public async Task CreateSlot_RejectsDoctorAndRoomOverlaps()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var service = CreateService(db);
        var start = DateTime.UtcNow.AddDays(4);
        var end = start.AddHours(1);
        await service.CreateSlotAsync(SlotDto(setup.FirstDoctorId, setup.RoomId, start, end));

        var doctorOverlap = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSlotAsync(SlotDto(setup.FirstDoctorId, null, start.AddMinutes(15), end.AddMinutes(15))));
        var roomOverlap = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSlotAsync(SlotDto(setup.SecondDoctorId, setup.RoomId, start.AddMinutes(15), end.AddMinutes(15))));

        Assert.Equal("This doctor already has an overlapping time slot.", doctorOverlap.Message);
        Assert.Equal("This room is already booked for the selected time period.", roomOverlap.Message);
    }

    [Fact]
    public async Task CreateSlot_PersistsConsultationFee()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var service = CreateService(db);
        var start = DateTime.UtcNow.AddDays(6);

        var slot = await service.CreateSlotAsync(SlotDto(setup.FirstDoctorId, setup.RoomId, start, start.AddHours(1), 3750.50m));

        Assert.Equal(3750.50m, slot.ConsultationFee);
        Assert.Equal(3750.50m, await db.DoctorTimeSlots.Select(value => value.ConsultationFee).SingleAsync());
    }

    [Fact]
    public async Task UpdateSlot_UpdatesConsultationFee()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup);
        var service = CreateService(db);

        var updated = await service.UpdateSlotAsync(slot.DoctorTimeSlotId, new UpdateDoctorTimeSlotDto
        {
            DoctorId = setup.FirstDoctorId,
            RoomId = setup.RoomId,
            StartAt = slot.StartAt,
            EndAt = slot.EndAt,
            Capacity = slot.Capacity,
            ConsultationFee = 4200.75m,
            IsActive = true
        });

        Assert.NotNull(updated);
        Assert.Equal(4200.75m, updated!.ConsultationFee);
        Assert.Equal(4200.75m, await db.DoctorTimeSlots.Where(value => value.DoctorTimeSlotId == slot.DoctorTimeSlotId).Select(value => value.ConsultationFee).SingleAsync());
    }

    [Fact]
    public async Task CreateSlot_RejectsInvalidConsultationFee()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var service = CreateService(db);
        var start = DateTime.UtcNow.AddDays(7);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSlotAsync(SlotDto(setup.FirstDoctorId, setup.RoomId, start, start.AddHours(1), 2500.555m)));

        Assert.Equal("Consultation fee can contain a maximum of two decimal places.", exception.Message);
    }

    [Fact]
    public async Task CreateSlot_RequiresRoomTurnoverAfterPreviousSlot()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var service = CreateService(db);
        var start = DateTime.UtcNow.AddDays(8).Date.AddHours(8);
        var end = start.AddHours(2);
        await service.CreateSlotAsync(SlotDto(setup.FirstDoctorId, setup.RoomId, start, end));

        var insideTurnover = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSlotAsync(SlotDto(setup.SecondDoctorId, setup.RoomId, end.AddMinutes(15), end.AddHours(2))));
        var afterTurnover = await service.CreateSlotAsync(SlotDto(setup.SecondDoctorId, setup.RoomId, end.AddMinutes(30), end.AddHours(2)));

        Assert.Equal("This room is already booked for the selected time period.", insideTurnover.Message);
        Assert.Equal(end.AddMinutes(30), afterTurnover.StartAt);
    }

    [Fact]
    public async Task CreateSlot_RejectsCapacityExceeding50()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var service = CreateService(db);
        var start = DateTime.UtcNow.AddDays(5);
        var dto = SlotDto(setup.FirstDoctorId, setup.RoomId, start, start.AddHours(1));
        dto.Capacity = 51;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSlotAsync(dto));

        Assert.Equal("Slot capacity must be between 1 and 50.", exception.Message);
    }

    [Fact]
    public async Task CreateSlot_RejectsWhenActiveSlotsLimitReached()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var service = CreateService(db);
        var doctor = await db.Doctors.FindAsync(setup.FirstDoctorId);

        var baseDate = DateTime.UtcNow.AddDays(10);
        for (int i = 0; i < 50; i++)
        {
            var dummyRoom = new Room { RoomNumber = $"A-{i + 100}", RoomName = $"Room {i}", Floor = "1", IsConfirmed = true };
            db.Rooms.Add(dummyRoom);
            db.DoctorTimeSlots.Add(new DoctorTimeSlot
            {
                DoctorId = doctor!.DoctorId,
                Room = dummyRoom,
                DoctorName = $"Dr. {doctor.FirstName} {doctor.LastName}",
                Specialty = doctor.Specialization,
                StartAt = baseDate.AddDays(i),
                EndAt = baseDate.AddDays(i).AddHours(1),
                Capacity = 10,
                ConsultationFee = 2000m,
                IsActive = true
            });
        }
        await db.SaveChangesAsync();

        var newSlot = SlotDto(setup.FirstDoctorId, setup.RoomId, baseDate.AddDays(60), baseDate.AddDays(60).AddHours(1));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSlotAsync(newSlot));

        Assert.Contains("maximum limit of 50 active appointment schedules", exception.Message);
    }

    [Fact]
    public async Task CreateEndpoint_UsesPatientClaimsAndPersistsAppointment()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup, capacity: 2);
        var controller = CreateController(db, "Patient", setup.PatientUserId, "PATIENT@EXAMPLE.COM");

        var result = await controller.Create(Appointment(slot.DoctorTimeSlotId, patientEmail: "spoof@example.com"));

        var createdAt = Assert.IsType<CreatedAtActionResult>(result);
        var appointment = Assert.IsType<AppointmentDto>(createdAt.Value);
        Assert.Equal(setup.PatientId, appointment.PatientId);
        Assert.Equal("patient@example.com", appointment.PatientEmail);
        Assert.Equal(1, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task PatientCannotCancelAnotherPatientsAppointment()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var slot = await AddSlotAsync(db, setup);
        var appointment = await AddAppointmentAsync(db, slot, patientId: setup.PatientId, patientEmail: "patient@example.com");
        var controller = CreateController(db, "Patient", setup.OtherPatientUserId, "other.patient@example.com");

        var result = await controller.Cancel(appointment.AppointmentId, new CancelAppointmentDto { Reason = "Not mine" });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DoctorCreateSlotEndpoint_UsesCurrentDoctorProfile()
    {
        await using var db = CreateContext();
        var setup = await SeedAppointmentDataAsync(db);
        var controller = CreateController(db, "Doctor", setup.FirstDoctorUserId, "first.doctor@example.com");
        var dto = SlotDto(setup.SecondDoctorId, setup.RoomId, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(5).AddHours(1));

        var result = await controller.CreateSlot(dto);

        var createdAt = Assert.IsType<CreatedAtActionResult>(result);
        var slot = Assert.IsType<DoctorTimeSlotDto>(createdAt.Value);
        Assert.Equal(setup.FirstDoctorId, slot.DoctorId);
        Assert.Equal("Dr. Ada Lovelace", slot.DoctorName);
    }

    [Theory]
    [InlineData(nameof(AppointmentController.Create), "Admin,Patient")]
    [InlineData(nameof(AppointmentController.Update), "Admin")]
    [InlineData(nameof(AppointmentController.CreateSlot), "Admin,Doctor")]
    public void AppointmentEndpoints_DeclareExpectedRolePermissions(string methodName, string expectedRoles)
    {
        var method = typeof(AppointmentController).GetMethods()
            .Single(info => info.Name == methodName && info.DeclaringType == typeof(AppointmentController));

        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(expectedRoles, authorize!.Roles);
    }

    private static AppointmentService CreateService(ApplicationDbContext db) => new(new AppointmentRepository(db), SmsTestSupport.Create(db));

    private static AppointmentController CreateController(ApplicationDbContext db, string role, int userId, string email)
    {
        var controller = new AppointmentController(CreateService(db), db, NullLogger<AppointmentController>.Instance);
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Email, email)
        ], "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }

    private static CreateAppointmentDto Appointment(
        int doctorTimeSlotId,
        string patientName = "Test Patient",
        string patientEmail = "patient@example.com") => new()
        {
            DoctorTimeSlotId = doctorTimeSlotId,
            PatientName = patientName,
            PatientPhone = "0770000000",
            PatientEmail = patientEmail,
            AppointmentType = "Consultation",
        };

    private static CreateDoctorTimeSlotDto SlotDto(int? doctorId, int? roomId, DateTime start, DateTime end, decimal consultationFee = 2500m) => new()
    {
        DoctorId = doctorId,
        RoomId = roomId,
        StartAt = start,
        EndAt = end,
        Capacity = 2,
        ConsultationFee = consultationFee
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"appointment-tests-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<AppointmentTestData> SeedAppointmentDataAsync(ApplicationDbContext db)
    {
        var admin = new User { FullName = "Admin", Email = "admin@example.com", PasswordHash = "hash", Role = "Admin" };
        var patientUser = new User { FullName = "Patient User", Email = "patient@example.com", PasswordHash = "hash", Role = "Patient" };
        var otherPatientUser = new User { FullName = "Other Patient", Email = "other.patient@example.com", PasswordHash = "hash", Role = "Patient" };
        var firstDoctorUser = new User { FullName = "Ada Lovelace", Email = "first.doctor@example.com", PasswordHash = "hash", Role = "Doctor" };
        var secondDoctorUser = new User { FullName = "Grace Hopper", Email = "second.doctor@example.com", PasswordHash = "hash", Role = "Doctor" };
        db.Users.AddRange(admin, patientUser, otherPatientUser, firstDoctorUser, secondDoctorUser);

        var patient = Patient("Test", "Patient", "patient@example.com", "900101001V");
        var otherPatient = Patient("Other", "Patient", "other.patient@example.com", "900101002V");
        db.Patients.AddRange(patient, otherPatient);

        var firstDoctor = Doctor(firstDoctorUser, "Ada", "Lovelace", "Cardiology", "SLMC-1001");
        var secondDoctor = Doctor(secondDoctorUser, "Grace", "Hopper", "Neurology", "SLMC-1002");
        db.Doctors.AddRange(firstDoctor, secondDoctor);

        var room = new Room
        {
            RoomNumber = "C-100",
            RoomName = "Consultation Room",
            Floor = "First",
            IsConfirmed = true,
            CreatedByUser = admin
        };
        db.Rooms.Add(room);

        await db.SaveChangesAsync();
        return new AppointmentTestData(
            admin.UserId,
            patientUser.UserId,
            otherPatientUser.UserId,
            firstDoctorUser.UserId,
            secondDoctorUser.UserId,
            patient.PatientId,
            otherPatient.PatientId,
            firstDoctor.DoctorId,
            secondDoctor.DoctorId,
            room.RoomId);
    }

    private static async Task<DoctorTimeSlot> AddSlotAsync(
        ApplicationDbContext db,
        AppointmentTestData setup,
        int? doctorId = null,
        DateTime? start = null,
        int capacity = 2)
    {
        var resolvedDoctorId = doctorId ?? setup.FirstDoctorId;
        var doctor = await db.Doctors.AsNoTracking().SingleAsync(value => value.DoctorId == resolvedDoctorId);
        var startAt = start ?? DateTime.UtcNow.AddDays(1);
        var slot = new DoctorTimeSlot
        {
            DoctorId = doctor.DoctorId,
            RoomId = setup.RoomId,
            DoctorName = $"Dr. {doctor.FirstName} {doctor.LastName}",
            Specialty = doctor.Specialization,
            StartAt = startAt,
            EndAt = startAt.AddHours(1),
            Capacity = capacity,
            ConsultationFee = 2500m,
            IsActive = true
        };

        db.DoctorTimeSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot;
    }

    private static async Task<Appointment> AddAppointmentAsync(
        ApplicationDbContext db,
        DoctorTimeSlot slot,
        int appointmentNumber = 1,
        string patientName = "Test Patient",
        string status = "Confirmed",
        int? patientId = null,
        string? patientEmail = "patient@example.com")
    {
        var appointment = new Appointment
        {
            DoctorTimeSlotId = slot.DoctorTimeSlotId,
            PatientId = patientId,
            AppointmentNumber = appointmentNumber,

            PatientName = patientName,
            PatientPhone = "0770000000",
            PatientEmail = patientEmail,
            AppointmentType = "Consultation",
            Reason = "Checkup",
            Status = status
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }

    private static Patient Patient(string firstName, string lastName, string email, string nic) => new()
    {
        FirstName = firstName,
        LastName = lastName,
        DateOfBirth = new DateTime(1990, 1, 1),
        Gender = "Female",
        NIC = nic,
        PhoneNumber = "0771111111",
        Email = email,
        Address = "Colombo"
    };

    private static Doctor Doctor(User user, string firstName, string lastName, string specialty, string license) => new()
    {
        User = user,
        FirstName = firstName,
        LastName = lastName,
        NIC = license.Replace("SLMC-", "DOC"),
        Specialization = specialty,
        SlmcLicenseNumber = license,
        PhoneNumber = "0772222222",
        RegistrationStatus = DoctorRegistrationStatuses.Approved
    };

    private sealed record AppointmentTestData(
        int AdminUserId,
        int PatientUserId,
        int OtherPatientUserId,
        int FirstDoctorUserId,
        int SecondDoctorUserId,
        int PatientId,
        int OtherPatientId,
        int FirstDoctorId,
        int SecondDoctorId,
        int RoomId);
}
