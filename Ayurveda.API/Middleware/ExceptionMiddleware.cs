using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Ayurveda.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title, detail, type) = exception switch
        {
            ArgumentException argEx => (
                (int)HttpStatusCode.BadRequest,
                "Bad Request",
                argEx.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            InvalidOperationException opEx => (
                (int)HttpStatusCode.BadRequest,
                "Bad Request",
                opEx.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            KeyNotFoundException notFoundEx => (
                (int)HttpStatusCode.NotFound,
                "Resource Not Found",
                notFoundEx.Message,
                "https://tools.ietf.org/html/rfc7231#section-6.5.4"
            ),
            UnauthorizedAccessException authEx => (
                (int)HttpStatusCode.Unauthorized,
                "Unauthorized",
                authEx.Message,
                "https://tools.ietf.org/html/rfc7235#section-3.1"
            ),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing your request. Please try again later.",
                "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            )
        };

        context.Response.StatusCode = statusCode;

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = traceId;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }
}
