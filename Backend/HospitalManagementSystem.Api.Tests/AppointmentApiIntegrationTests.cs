using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class AppointmentApiIntegrationTests
{
    [Theory]
    [InlineData("DELETE", "", HttpStatusCode.MethodNotAllowed)]
    [InlineData("PATCH", "/status", HttpStatusCode.NotFound)]
    public async Task RemovedAppointmentEndpoints_AreNotRoutable(string method, string suffix, HttpStatusCode expected)
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedScenarioAsync(factory);
        await AuthorizeAsync(client, "admin@medicore.lk", "Admin1234");
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/appointment/{seed.AdminManagedAppointmentId}{suffix}");
        var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        var appointment = await client.GetFromJsonAsync<AppointmentDto>($"/api/appointment/{seed.AdminManagedAppointmentId}");
        Assert.Equal("Confirmed", appointment!.Status);
    }

    [Fact]
    public async Task PostAppointment_WithPatientToken_CreatesAppointmentForCurrentPatient()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedScenarioAsync(factory);
        await AuthorizeAsync(client, "amal.perera@email.com", "Patient123!");

        var response = await client.PostAsJsonAsync("/api/appointment", new CreateAppointmentDto
        {
            DoctorTimeSlotId = seed.PatientBookingSlotId,
            PatientName = "Spoofed Name",
            PatientPhone = "0771112222",
            PatientEmail = "spoof@example.com",
            AppointmentType = "Consultation",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var appointment = await response.Content.ReadFromJsonAsync<AppointmentDto>();
        Assert.NotNull(appointment);
        Assert.Equal(seed.AmalPatientId, appointment!.PatientId);
        Assert.Equal("amal.perera@email.com", appointment.PatientEmail);
        Assert.True(appointment.AppointmentId > 0);
        Assert.Equal(1, appointment.AppointmentNumber);

        // Legacy clients cannot override backend-generated identifiers.
        var secondResponse = await client.PostAsJsonAsync("/api/appointment", new
        {
            DoctorTimeSlotId = seed.PatientBookingSlotId,
            AppointmentNumber = 99,
            AppointmentId = 99999,
            PatientName = "Amal Perera",
            PatientPhone = "0771112222",
            AppointmentType = "Consultation",
        });
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var second = await secondResponse.Content.ReadFromJsonAsync<AppointmentDto>();
        Assert.Equal(2, second!.AppointmentNumber);
        Assert.NotEqual(appointment.AppointmentId, second.AppointmentId);
        Assert.NotEqual(99999, second.AppointmentId);
    }

    [Fact]
    public async Task Reschedule_WithPatientToken_MovesOwnedAppointmentToAvailableSlot()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedScenarioAsync(factory);
        await AuthorizeAsync(client, "amal.perera@email.com", "Patient123!");

        var response = await client.PostAsJsonAsync($"/api/appointment/{seed.AmalAppointmentId}/reschedule",
            new RescheduleAppointmentDto { DoctorTimeSlotId = seed.RescheduleDestinationSlotId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var appointment = await response.Content.ReadFromJsonAsync<AppointmentDto>();
        Assert.Equal(seed.RescheduleDestinationSlotId, appointment!.DoctorTimeSlotId);
        Assert.Equal("Confirmed", appointment.Status);
    }

    [Fact]
    public async Task GetAppointment_WithDifferentPatientToken_ReturnsForbidden()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedScenarioAsync(factory);
        await AuthorizeAsync(client, "amal.perera@email.com", "Patient123!");

        var response = await client.GetAsync($"/api/appointment/{seed.NimeshaAppointmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Notifications_ReturnOnlyTheAuthenticatedPatientsUpdates()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedScenarioAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AppointmentNotifications.AddRange(
                new AppointmentNotification { AppointmentId = seed.AmalAppointmentId, Message = "Your time changed" },
                new AppointmentNotification { AppointmentId = seed.NimeshaAppointmentId, Message = "Private other patient update" });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/appointment/notifications")).StatusCode);
        await AuthorizeAsync(client, "amal.perera@email.com", "Patient123!");
        var response = await client.GetAsync("/api/appointment/notifications");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Your time changed", body);
        Assert.DoesNotContain("Private other patient update", body);
        await AuthorizeAsync(client, "admin@medicore.lk", "Admin1234");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/appointment/notifications")).StatusCode);
    }

    private static async Task AuthorizeAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    private static async Task<AppointmentApiSeed> SeedScenarioAsync(AppointmentApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var admin = await db.Users.SingleAsync(user => user.Email == "admin@medicore.lk");
        var amal = await db.Patients.SingleAsync(patient => patient.Email == "amal.perera@email.com");
        var nimesha = await db.Patients.SingleAsync(patient => patient.Email == "nimesha.silva@email.com");

        var firstDoctorUser = new User
        {
            FullName = "First Integration Doctor",
            Email = $"first.integration.{suffix}@example.com",
            Role = "Doctor"
        };
        firstDoctorUser.PasswordHash = hasher.HashPassword(firstDoctorUser, "Doctor123!");

        var secondDoctorUser = new User
        {
            FullName = "Second Integration Doctor",
            Email = $"second.integration.{suffix}@example.com",
            Role = "Doctor"
        };
        secondDoctorUser.PasswordHash = hasher.HashPassword(secondDoctorUser, "Doctor123!");

        db.Users.AddRange(firstDoctorUser, secondDoctorUser);
        await db.SaveChangesAsync();

        var firstDoctor = ApprovedDoctor(firstDoctorUser, "First", "Doctor", "Cardiology", $"SLMC-{suffix}-1");
        var secondDoctor = ApprovedDoctor(secondDoctorUser, "Second", "Doctor", "Neurology", $"SLMC-{suffix}-2");
        db.Doctors.AddRange(firstDoctor, secondDoctor);

        var room = new Room
        {
            RoomNumber = $"INT-{suffix}",
            RoomName = "Integration Room",
            Floor = "Second",
            IsConfirmed = true,
            CreatedByUserId = admin.UserId
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var baseDate = DateTime.UtcNow.AddDays(14).Date.AddHours(8);
        var patientBookingSlot = Slot(firstDoctor, room, baseDate, 2);
        var rescheduleSourceSlot = Slot(firstDoctor, room, baseDate.AddDays(1), 2);
        var rescheduleDestinationSlot = Slot(firstDoctor, room, baseDate.AddDays(2), 2);
        var adminManagedSlot = Slot(firstDoctor, room, baseDate.AddDays(3), 2);
        var firstDoctorSlot = Slot(firstDoctor, room, baseDate.AddDays(4), 2);
        db.DoctorTimeSlots.AddRange(patientBookingSlot, rescheduleSourceSlot, rescheduleDestinationSlot, adminManagedSlot, firstDoctorSlot);
        await db.SaveChangesAsync();

        var amalAppointment = Appointment(rescheduleSourceSlot, amal, "Confirmed", 1);
        var nimeshaAppointment = Appointment(rescheduleDestinationSlot, nimesha, "Confirmed", 1);
        var adminManagedAppointment = Appointment(adminManagedSlot, amal, "Confirmed", 1);
        var firstDoctorAppointment = Appointment(firstDoctorSlot, amal, "Confirmed", 1);
        db.Appointments.AddRange(amalAppointment, nimeshaAppointment, adminManagedAppointment, firstDoctorAppointment);
        await db.SaveChangesAsync();

        return new AppointmentApiSeed(
            amal.PatientId,
            patientBookingSlot.DoctorTimeSlotId,
            rescheduleDestinationSlot.DoctorTimeSlotId,
            amalAppointment.AppointmentId,
            nimeshaAppointment.AppointmentId,
            adminManagedAppointment.AppointmentId,
            firstDoctorAppointment.AppointmentId,
            secondDoctorUser.Email);
    }

    private static Doctor ApprovedDoctor(User user, string firstName, string lastName, string specialty, string license) => new()
    {
        UserId = user.UserId,
        FirstName = firstName,
        LastName = lastName,
        NIC = license.Replace("SLMC-", "NIC"),
        Specialization = specialty,
        SlmcLicenseNumber = license,
        PhoneNumber = "0772223333",
        RegistrationStatus = DoctorRegistrationStatuses.Approved
    };

    private static DoctorTimeSlot Slot(Doctor doctor, Room room, DateTime startAt, int capacity) => new()
    {
        DoctorId = doctor.DoctorId,
        RoomId = room.RoomId,
        DoctorName = $"Dr. {doctor.FirstName} {doctor.LastName}",
        Specialty = doctor.Specialization,
        StartAt = startAt,
        EndAt = startAt.AddHours(1),
        Capacity = capacity,
        ConsultationFee = 2500m,
        IsActive = true
    };

    private static Appointment Appointment(DoctorTimeSlot slot, Patient patient, string status, int number) => new()
    {
        DoctorTimeSlotId = slot.DoctorTimeSlotId,
        PatientId = patient.PatientId,
        AppointmentNumber = number,

        PatientName = $"{patient.FirstName} {patient.LastName}",
        PatientPhone = patient.PhoneNumber,
        PatientEmail = patient.Email,
        AppointmentType = "Consultation",
        Reason = "Checkup",
        Status = status
    };

    private sealed record LoginResponse(string Token);

    private sealed record AppointmentApiSeed(
        int AmalPatientId,
        int PatientBookingSlotId,
        int RescheduleDestinationSlotId,
        int AmalAppointmentId,
        int NimeshaAppointmentId,
        int AdminManagedAppointmentId,
        int FirstDoctorAppointmentId,
        string SecondDoctorEmail);
}

public sealed class AppointmentApiFactory : WebApplicationFactory<Program>
{
    private const string TestJwtSecret = "integration-test-secret-key-1234567890";
    private readonly string _databaseName = $"appointment-api-tests-{Guid.NewGuid()}";

    public AppointmentApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Secret", TestJwtSecret);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "MediCore.Api");
        Environment.SetEnvironmentVariable("Jwt__Audience", "MediCore.Client");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = TestJwtSecret,
                ["Jwt:Issuer"] = "MediCore.Api",
                ["Jwt:Audience"] = "MediCore.Client",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=integration_tests"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
