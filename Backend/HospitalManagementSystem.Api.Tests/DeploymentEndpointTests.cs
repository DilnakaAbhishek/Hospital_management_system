using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class DeploymentEndpointTests
{
    [Fact]
    public async Task RootEndpoint_WithHtmlAcceptHeader_ReturnsDeploymentSuccessHtml()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Backend Service Deployed Successfully", html);
        Assert.Contains("ONLINE & DEPLOYED", html);
        Assert.Contains("Swagger API Documentation", html);
        Assert.Contains("/health", html);
    }

    [Fact]
    public async Task RootEndpoint_WithJsonAcceptHeader_ReturnsDeploymentSuccessJson()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Success", json.GetProperty("status").GetString());
        Assert.Contains("deployed and running successfully", json.GetProperty("message").GetString());
        Assert.Equal("/swagger", json.GetProperty("documentation").GetString());
        Assert.Equal("/health", json.GetProperty("health").GetString());
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", json.GetProperty("status").GetString());
        Assert.True(json.TryGetProperty("timestamp", out _));
    }
}
