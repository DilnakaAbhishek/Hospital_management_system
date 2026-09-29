namespace HospitalManagementSystem.Api.Services;

public sealed class MockSmsService(ILogger<MockSmsService> logger) : ISmsService
{
    public Task<bool> SendAsync(string recipient, string message, CancellationToken cancellationToken = default)
    {
        var normalized = SmsPhoneNumber.Normalize(recipient);
        if (normalized is null)
        {
            logger.LogWarning("Mock SMS skipped: invalid Sri Lankan phone number.");
            return Task.FromResult(false);
        }
        logger.LogInformation("Mock SMS to {Recipient}: {Message}", normalized, message);
        return Task.FromResult(true);
    }
}
