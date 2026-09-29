using System.Net.Http.Headers;
using System.Text.Json;

namespace HospitalManagementSystem.Api.Services;

public sealed class InfobipSmsService(HttpClient http, IConfiguration configuration,
    ILogger<InfobipSmsService> logger) : ISmsService
{
    public async Task<bool> SendAsync(string recipient, string message, CancellationToken cancellationToken = default)
    {
        var number = SmsPhoneNumber.Normalize(recipient);
        if (number is null)
        {
            logger.LogWarning("Infobip SMS skipped: invalid Sri Lankan phone number.");
            return false;
        }
        var key = configuration["Infobip:ApiKey"];
        var sender = configuration["Infobip:Sender"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(sender) ||
            !Uri.TryCreate(configuration["Infobip:BaseUrl"], UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(baseUri.UserInfo) ||
            baseUri.AbsolutePath != "/" || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            logger.LogWarning("Infobip SMS skipped: configure HTTPS Infobip:BaseUrl (origin only), Infobip:Sender and secret Infobip:ApiKey.");
            return false;
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/sms/3/messages"));
            request.Headers.Authorization = new AuthenticationHeaderValue("App", key);
            request.Content = JsonContent.Create(new
            {
                messages = new[] { new { sender, destinations = new[] { new { to = number } }, content = new { text = message } } }
            });
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Infobip SMS failed with HTTP {StatusCode}.", (int)response.StatusCode);
                return false;
            }
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var messages = body.RootElement.GetProperty("messages");
            if (messages.GetArrayLength() != 1) throw new JsonException();
            var result = messages[0];
            var group = result.GetProperty("status").GetProperty("groupId").GetInt32();
            if (string.IsNullOrWhiteSpace(result.GetProperty("messageId").GetString()) || group is not (1 or 3))
            {
                logger.LogWarning("Infobip SMS was not accepted; status group {GroupId}.", group);
                return false;
            }
            logger.LogInformation("Infobip accepted SMS submission; handset delivery is not yet verified.");
            return true;
        }
        catch (Exception exception)
        {
            // Do not log provider bodies, headers, or exception messages: they may contain secrets.
            logger.LogWarning("Infobip SMS failed ({ErrorType}); no automatic retry.", exception.GetType().Name);
            return false;
        }
    }
}
