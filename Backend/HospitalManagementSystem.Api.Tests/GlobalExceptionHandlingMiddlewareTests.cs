using System.Text.Json;
using HospitalManagementSystem.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Npgsql;

namespace HospitalManagementSystem.Api.Tests;

public class GlobalExceptionHandlingMiddlewareTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvokeAsync_WhenDatabaseConnectionTimesOut_ReturnsServiceUnavailable(bool wrapped)
    {
        var context = CreateHttpContext("/api/appointment");
        Exception failure = new NpgsqlException("Connection failed.", new TimeoutException("Connection timed out."));
        if (wrapped)
            failure = new InvalidOperationException("A transient failure occurred.", failure);
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw failure,
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance,
            new TestHostEnvironment { EnvironmentName = Environments.Production });

        await middleware.InvokeAsync(context);

        using var json = JsonDocument.Parse(await ReadResponseBodyAsync(context));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("Database temporarily unavailable. Please try again shortly.", json.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Connection failed", json.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledInvalidOperationException_ReturnsConflictProblemDetails()
    {
        var context = CreateHttpContext("/api/appointment");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Selected doctor time slot is fully booked."),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance,
            new TestHostEnvironment { EnvironmentName = Environments.Development });

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        using var json = JsonDocument.Parse(body);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Contains("application/problem+json", context.Response.ContentType);
        Assert.Equal("Request could not be completed.", json.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "Selected doctor time slot is fully booked.",
            json.RootElement.GetProperty("detail").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_ReturnsGenericServerErrorProblemDetails()
    {
        var context = CreateHttpContext("/api/appointment");
        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new Exception("Database connection failed."),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance,
            new TestHostEnvironment { EnvironmentName = Environments.Production });

        await middleware.InvokeAsync(context);

        var body = await ReadResponseBodyAsync(context);
        using var json = JsonDocument.Parse(body);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("application/problem+json", context.Response.ContentType);
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("title").GetString());
        Assert.Equal("The request could not be processed.", json.RootElement.GetProperty("detail").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HospitalManagementSystem.Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
