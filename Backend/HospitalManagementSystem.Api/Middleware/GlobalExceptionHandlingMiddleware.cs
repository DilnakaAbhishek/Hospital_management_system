using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace HospitalManagementSystem.Api.Middleware;

public sealed class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(
                    exception,
                    "An unhandled exception occurred after the response started. TraceId: {TraceId}",
                    context.TraceIdentifier);
                throw;
            }

            await WriteProblemDetailsAsync(context, exception);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            _ when IsTransientDatabaseFailure(exception) => StatusCodes.Status503ServiceUnavailable,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        var title = statusCode switch
        {
            StatusCodes.Status503ServiceUnavailable => "Database temporarily unavailable. Please try again shortly.",
            StatusCodes.Status400BadRequest => "Invalid request.",
            StatusCodes.Status403Forbidden => "Access denied.",
            StatusCodes.Status409Conflict => "Request could not be completed.",
            _ => "An unexpected error occurred."
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Request failed for {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = _environment.IsDevelopment()
                ? GetDevelopmentErrorDetail(exception)
                : "The request could not be processed.",
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.ContentType = "application/problem+json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problemDetails,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            context.RequestAborted);
    }

    private static string GetDevelopmentErrorDetail(Exception exception)
    {
        var innermost = exception;
        while (innermost.InnerException is not null)
            innermost = innermost.InnerException;

        return innermost == exception
            ? exception.Message
            : $"{exception.Message} Inner database error: {innermost.Message}";
    }

    private static bool IsTransientDatabaseFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException { IsTransient: true })
                return true;
        }

        return false;
    }
}
