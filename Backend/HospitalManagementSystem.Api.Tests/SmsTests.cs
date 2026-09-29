using System.Net;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

internal static class SmsTestSupport
{
    public static AppointmentSmsNotifier Create(ApplicationDbContext db, ISmsService? sms = null) =>
        new(db, sms ?? new MockSmsService(NullLogger<MockSmsService>.Instance), NullLogger<AppointmentSmsNotifier>.Instance);
}

public class SmsTests
{
    [Theory]
    [InlineData("0771234567", "94771234567")]
    [InlineData("+94 77 123 4567", "94771234567")]
    [InlineData("0094771234567", "94771234567")]
    [InlineData("94771234567", "94771234567")]
    [InlineData("077abc1234567", null)]
    [InlineData("077123", null)]
    [InlineData("", null)]
    public void NormalizePhone(string input, string? expected) => Assert.Equal(expected, SmsPhoneNumber.Normalize(input));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BookingSavesBeforeSms_UsesProfilePhone_AndSurvivesProviderFailure(bool fail)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var slot = new DoctorTimeSlot { DoctorName = "Dr. Silva", StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(3).AddHours(1), Capacity = 3, IsActive = true };
        var patient = new Patient { PhoneNumber = "0771234567", Email = "sms-test@example.com" };
        db.AddRange(slot, patient);
        await db.SaveChangesAsync();
        var persistedBeforeSms = false;
        var sms = new RecordingSms(async () =>
        {
            await using var check = new ApplicationDbContext(options);
            persistedBeforeSms = await check.Appointments.AnyAsync();
            if (fail) throw new HttpRequestException("secret must not be logged");
        });
        var service = new AppointmentService(new AppointmentRepository(db), SmsTestSupport.Create(db, sms));
        var result = await service.CreateAppointmentAsync(new CreateAppointmentDto { DoctorTimeSlotId = slot.DoctorTimeSlotId,
            PatientId = patient.PatientId, PatientName = "Patient", PatientPhone = "0779999999" });
        Assert.Equal("Confirmed", result.Status);
        Assert.True(persistedBeforeSms);
        Assert.Equal("94771234567", Assert.Single(sms.Messages).Phone);
        Assert.Contains("Dr. Silva", sms.Messages[0].Message);
        Assert.Contains("Sri Lanka", sms.Messages[0].Message);
        var destination = new DoctorTimeSlot { DoctorName = "Dr. Perera", StartAt = slot.StartAt.AddDays(1),
            EndAt = slot.EndAt.AddDays(1), Capacity = 3, IsActive = true };
        db.Add(destination);
        await db.SaveChangesAsync();
        await service.RescheduleAppointmentAsync(result.AppointmentId, destination.DoctorTimeSlotId);
        Assert.Contains("rescheduled", sms.Messages[1].Message);
        Assert.Contains("Dr. Perera", sms.Messages[1].Message);
        await service.RescheduleAppointmentAsync(result.AppointmentId, destination.DoctorTimeSlotId);
        Assert.Equal(2, sms.Messages.Count);
        await service.CancelAppointmentAsync(result.AppointmentId, "Unavailable");
        await service.CancelAppointmentAsync(result.AppointmentId, "Unavailable");
        Assert.Equal(3, sms.Messages.Count);
        Assert.Equal("Cancelled", (await db.Appointments.SingleAsync()).Status);
        slot.Capacity = 1;
        await db.SaveChangesAsync();
        await service.CreateAppointmentAsync(new() { DoctorTimeSlotId = slot.DoctorTimeSlotId,
            PatientId = patient.PatientId, PatientName = "Patient", PatientPhone = "0779999999" });
        var sent = sms.Messages.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAppointmentAsync(new()
            { DoctorTimeSlotId = slot.DoctorTimeSlotId, PatientId = patient.PatientId, PatientName = "Patient", PatientPhone = "0779999999" }));
        Assert.Equal(sent, sms.Messages.Count);
        await service.CancelSlotAsync(slot.DoctorTimeSlotId, "Doctor unavailable");
        Assert.Equal(sent + 1, sms.Messages.Count);
    }

    [Theory]
    [InlineData("Mock", typeof(MockSmsService))]
    [InlineData("Infobip", typeof(InfobipSmsService))]
    public void DiResolvesBothProviders(string provider, Type expected)
    {
        var configuration = Config(provider);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IDoctorScheduleService, DoctorScheduleService>();
        services.AddSmsNotifications(configuration);
        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = root.CreateScope();
        Assert.IsType(expected, scope.ServiceProvider.GetRequiredService<ISmsService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAppointmentService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDoctorScheduleService>());
    }

    [Theory]
    [InlineData(200, "{\"messages\":[{\"messageId\":\"test\",\"status\":{\"groupId\":1}}]}", true)]
    [InlineData(200, "{\"messages\":[{\"messageId\":\"test\",\"status\":{\"groupId\":5}}]}", false)]
    [InlineData(200, "{}", false)]
    [InlineData(200, "invalid JSON", false)]
    [InlineData(401, "unauthorized", false)]
    [InlineData(500, "error", false)]
    public async Task InfobipUsesV3AndChecksResponse(int status, string body, bool expected)
    {
        var handler = new StubHandler(async request =>
        {
            Assert.Equal("https://example.api.infobip.com/sms/3/messages", request.RequestUri!.ToString());
            Assert.Equal("App", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization.Parameter);
            var payload = await request.Content!.ReadAsStringAsync();
            Assert.Contains("\"sender\":\"ServiceSMS\"", payload);
            Assert.Contains("\"content\":{\"text\":\"Hello\"}", payload);
            Assert.Contains("94771234567", payload);
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body) };
        });
        using var http = new HttpClient(handler);
        var service = new InfobipSmsService(http, Config("Infobip"), NullLogger<InfobipSmsService>.Instance);
        Assert.Equal(expected, await service.SendAsync("0771234567", "Hello"));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InfobipHandlesNetworkErrorsAndTimeouts(bool timeout)
    {
        using var http = new HttpClient(new StubHandler(_ => timeout
            ? throw new TaskCanceledException() : throw new HttpRequestException()));
        var service = new InfobipSmsService(http, Config("Infobip"), NullLogger<InfobipSmsService>.Instance);
        Assert.False(await service.SendAsync("0771234567", "Hello"));
    }

    [Fact]
    public async Task InvalidPhoneOrMissingConfigurationDoesNotMakeHttpRequest()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Must not call HTTP"));
        using var http = new HttpClient(handler);
        var config = Config("Infobip");
        var service = new InfobipSmsService(http, config, NullLogger<InfobipSmsService>.Instance);
        Assert.False(await service.SendAsync("bad", "Hello"));
        config["Infobip:ApiKey"] = "";
        Assert.False(await service.SendAsync("0771234567", "Hello"));
        Assert.Equal(0, handler.Calls);
        Assert.True(await new MockSmsService(NullLogger<MockSmsService>.Instance).SendAsync("0771234567", "Hello"));
    }

    private static IConfigurationRoot Config(string provider) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Sms:Provider"] = provider, ["Infobip:BaseUrl"] = "https://example.api.infobip.com",
            ["Infobip:Sender"] = "ServiceSMS", ["Infobip:ApiKey"] = "test-key" }).Build();

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; return respond(request); }
    }

    private sealed class RecordingSms(Func<Task> onSend) : ISmsService
    {
        public List<(string Phone, string Message)> Messages { get; } = [];
        public async Task<bool> SendAsync(string recipient, string message, CancellationToken cancellationToken = default)
        { Messages.Add((recipient, message)); await onSend(); return true; }
    }
}
