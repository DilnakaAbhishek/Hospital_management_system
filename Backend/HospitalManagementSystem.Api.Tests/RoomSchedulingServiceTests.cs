using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class RoomSchedulingServiceTests
{
    [Fact]
    public async Task CreateAndConfirmRoom_MakesRoomAvailableToDoctors()
    {
        await using var db = CreateContext();
        var admin = new User { FullName = "Admin", Email = "admin@test.lk", PasswordHash = "hash", Role = "Admin" };
        db.Users.Add(admin); await db.SaveChangesAsync();
        var service = new RoomService(db);

        var created = await service.CreateAsync(new CreateRoomDto
        {
            RoomNumber = "c-204", RoomName = "Consultation Room", Floor = "Second Floor"
        }, admin.UserId);
        var confirmed = await service.ConfirmAsync(created.RoomId);

        Assert.False(created.IsConfirmed);
        Assert.NotNull(confirmed);
        Assert.True(confirmed!.IsConfirmed);
        Assert.Equal("Available", confirmed.Status);
    }

    [Fact]
    public async Task CreateSchedule_RejectsOverlappingRoomBookingByAnotherDoctor()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(2).AddHours(9);
        var end = start.AddHours(2);
        await service.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, end));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(setup.SecondUserId, Schedule(setup.RoomId, start.AddMinutes(30), end.AddHours(1))));

        Assert.Equal("This room is already booked for the selected time period.", exception.Message);
    }

    [Fact]
    public async Task CreateSchedule_RejectsRoomBookingsInsideTurnoverWindow()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(9);
        await service.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, start.AddHours(1)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(setup.SecondUserId, Schedule(setup.RoomId, start.AddHours(1), start.AddHours(2))));

        Assert.Equal("This room is already booked for the selected time period.", exception.Message);
    }

    [Fact]
    public async Task CreateSchedule_AllowsRoomBookingAfterTurnoverWindow()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(9);
        var first = await service.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, start.AddHours(1)));
        var second = await service.CreateAsync(setup.SecondUserId, Schedule(setup.RoomId, start.AddHours(1).AddMinutes(30), start.AddHours(2)));

        Assert.True(first.IsActive);
        Assert.True(second.IsActive);
    }

    [Fact]
    public async Task GetAvailableRooms_UsesTimeRangeWithTurnoverBuffer()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var scheduleService = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var roomService = new RoomService(db);
        var start = DateTime.UtcNow.AddDays(4).Date.AddHours(8);
        var end = start.AddHours(2);
        await scheduleService.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, end));

        var insideTurnover = await roomService.GetAllAsync(end.AddMinutes(15), end.AddHours(1), confirmedOnly: true);
        var afterTurnover = await roomService.GetAllAsync(end.AddMinutes(30), end.AddHours(2).AddMinutes(30), confirmedOnly: true);

        Assert.Equal("Booked", Assert.Single(insideTurnover).Status);
        Assert.Equal("Available", Assert.Single(afterTurnover).Status);
    }

    [Fact]
    public async Task CancelSchedule_ReleasesRoomForSamePeriod()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var scheduleService = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var roomService = new RoomService(db);
        var start = DateTime.UtcNow.Date.AddDays(4).AddHours(9);
        var end = start.AddHours(1);
        var schedule = await scheduleService.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, end));
        await scheduleService.CancelAsync(setup.FirstUserId, schedule.DoctorTimeSlotId);

        var rooms = await roomService.GetAllAsync(start, end, confirmedOnly: true);

        Assert.Equal("Available", Assert.Single(rooms).Status);
    }

    [Fact]
    public async Task GetMine_DoesNotReturnAnotherDoctorsSchedules()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(5).AddHours(9);
        await service.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, start.AddHours(1)));

        var secondDoctorsSchedules = await service.GetMineAsync(setup.SecondUserId);

        Assert.Empty(secondDoctorsSchedules);
    }

    [Fact]
    public async Task CreateSchedule_RejectsPastStartTime()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.AddHours(-2);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, start.AddHours(1))));

        Assert.Equal("A schedule cannot start in the past.", exception.Message);
    }

    [Fact]
    public async Task CreateSchedule_RejectsInvalidConsultationFee()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(6).AddHours(9);
        var dto = Schedule(setup.RoomId, start, start.AddHours(1));
        dto.ConsultationFee = 0;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(setup.FirstUserId, dto));

        Assert.Contains("Consultation fee", exception.Message);
    }

    [Fact]
    public async Task BookedSchedule_CanBeEditedAndNotifiesPatient_ButCannotBeCancelledOrDeleted()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(7).AddHours(9);
        var dto = Schedule(setup.RoomId, start, start.AddHours(1));
        var schedule = await service.CreateAsync(setup.FirstUserId, dto);
        db.Appointments.Add(new Appointment
        {
            DoctorTimeSlotId = schedule.DoctorTimeSlotId, AppointmentNumber = 1,
            PatientName = "Booked Patient", PatientPhone = "0770000000",
            AppointmentType = "Consultation", Reason = "Checkup", Status = "Confirmed"
        });
        await db.SaveChangesAsync();

        var updated = await service.UpdateAsync(setup.FirstUserId, schedule.DoctorTimeSlotId, new UpdateDoctorScheduleDto
        {
            RoomId = dto.RoomId, StartAt = dto.StartAt.AddHours(2), EndAt = dto.EndAt.AddHours(2), Capacity = dto.Capacity, ConsultationFee = dto.ConsultationFee
        });
        var cancelError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(setup.FirstUserId, schedule.DoctorTimeSlotId));
        var deleteError = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(setup.FirstUserId, schedule.DoctorTimeSlotId));

        Assert.NotNull(updated);
        Assert.Equal(start.AddHours(2), (await db.Appointments.Include(a => a.DoctorTimeSlot).SingleAsync()).DoctorTimeSlot!.StartAt);
        Assert.Equal(1, (await db.Appointments.SingleAsync()).AppointmentNumber);
        Assert.Contains("has been updated", (await db.AppointmentNotifications.SingleAsync()).Message);
        Assert.Contains("cannot be cancelled", cancelError.Message);
        Assert.Contains("cannot be deleted", deleteError.Message);
    }

    [Fact]
    public async Task EditBookedSchedule_ProtectsHighAppointmentNumbersAndDoesNotNotifyOnNoOp()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(7).AddHours(9);
        var schedule = await service.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, start.AddHours(1)));
        db.Appointments.Add(new Appointment { DoctorTimeSlotId = schedule.DoctorTimeSlotId,
            AppointmentNumber = 5, Status = "Confirmed" });
        await db.SaveChangesAsync();
        var dto = new UpdateDoctorScheduleDto { RoomId = setup.RoomId, StartAt = start,
            EndAt = start.AddHours(1), Capacity = 4, ConsultationFee = 2500m };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(setup.FirstUserId, schedule.DoctorTimeSlotId, dto));
        Assert.Empty(await db.AppointmentNotifications.ToListAsync());
        dto.Capacity = 5;
        await service.UpdateAsync(setup.FirstUserId, schedule.DoctorTimeSlotId, dto);
        Assert.Empty(await db.AppointmentNotifications.ToListAsync());
        dto.StartAt = start.AddHours(2);
        dto.EndAt = start.AddHours(3);
        await service.UpdateAsync(setup.FirstUserId, schedule.DoctorTimeSlotId, dto);
        Assert.Equal(start.AddHours(2), (await db.Appointments.Include(a => a.DoctorTimeSlot).SingleAsync()).DoctorTimeSlot!.StartAt);
        Assert.Single(await db.AppointmentNotifications.ToListAsync());
    }

    [Fact]
    public async Task DeleteSchedule_RemovesUnbookedOwnedSchedule()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(8).AddHours(9);
        var schedule = await service.CreateAsync(setup.FirstUserId, Schedule(setup.RoomId, start, start.AddHours(1)));

        var deleted = await service.DeleteAsync(setup.FirstUserId, schedule.DoctorTimeSlotId);

        Assert.True(deleted);
        Assert.Empty(await service.GetMineAsync(setup.FirstUserId));
    }

    [Fact]
    public async Task CreateSchedule_RejectsCapacityExceeding50()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var start = DateTime.UtcNow.Date.AddDays(5).AddHours(9);
        var dto = Schedule(setup.RoomId, start, start.AddHours(1));
        dto.Capacity = 51;

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(setup.FirstUserId, dto));

        Assert.Equal("Slot capacity must be between 1 and 50.", exception.Message);
    }

    [Fact]
    public async Task CreateSchedule_RejectsWhenActiveSchedulesLimitReached()
    {
        await using var db = CreateContext();
        var setup = await SeedSchedulingDataAsync(db);
        var service = new DoctorScheduleService(db, SmsTestSupport.Create(db));
        var doctor = await db.Doctors.SingleAsync(d => d.UserId == setup.FirstUserId);

        // Seed 50 active future slots for this doctor
        var baseDate = DateTime.UtcNow.Date.AddDays(10).AddHours(9);
        for (int i = 0; i < 50; i++)
        {
            var dummyRoom = new Room { RoomNumber = $"R-{i + 100}", RoomName = $"Room {i}", Floor = "1", IsConfirmed = true };
            db.Rooms.Add(dummyRoom);
            db.DoctorTimeSlots.Add(new DoctorTimeSlot
            {
                DoctorId = doctor.DoctorId,
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

        var newSchedule = Schedule(setup.RoomId, baseDate.AddDays(60), baseDate.AddDays(60).AddHours(1));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(setup.FirstUserId, newSchedule));

        Assert.Contains("maximum limit of 50 active appointment schedules", exception.Message);
    }

    private static CreateDoctorScheduleDto Schedule(int roomId, DateTime start, DateTime end) =>
        new() { RoomId = roomId, StartAt = start, EndAt = end, Capacity = 5, ConsultationFee = 2500m };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"room-tests-{Guid.NewGuid()}").Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(int FirstUserId, int SecondUserId, int RoomId)> SeedSchedulingDataAsync(ApplicationDbContext db)
    {
        var admin = new User { FullName = "Admin", Email = "admin@test.lk", PasswordHash = "hash", Role = "Admin" };
        var firstUser = new User { FullName = "First Doctor", Email = "first@test.lk", PasswordHash = "hash", Role = "Doctor" };
        var secondUser = new User { FullName = "Second Doctor", Email = "second@test.lk", PasswordHash = "hash", Role = "Doctor" };
        db.Users.AddRange(admin, firstUser, secondUser);
        db.Doctors.AddRange(
            new Doctor { User = firstUser, FirstName = "First", LastName = "Doctor", NIC = "200000000001", Specialization = "Medicine", SlmcLicenseNumber = "SLMC-1", PhoneNumber = "0771111111", RegistrationStatus = DoctorRegistrationStatuses.Approved },
            new Doctor { User = secondUser, FirstName = "Second", LastName = "Doctor", NIC = "200000000002", Specialization = "Medicine", SlmcLicenseNumber = "SLMC-2", PhoneNumber = "0772222222", RegistrationStatus = DoctorRegistrationStatuses.Approved });
        var room = new Room { RoomNumber = "C-204", RoomName = "Consultation Room", Floor = "Second", IsConfirmed = true, CreatedByUser = admin };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return (firstUser.UserId, secondUser.UserId, room.RoomId);
    }
}
