using System.Net;
using System.Text.Json;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.API.Middleware;

/// <summary>
/// Catches domain and application exceptions and maps them to consistent HTTP responses.
/// Keeps controllers clean — no try/catch blocks needed there.
///
/// Error response shape:
/// { "type": "ShipmentNotFoundException", "message": "...", "traceId": "..." }
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            ShipmentNotFoundException e =>
                (HttpStatusCode.NotFound, e.Message),

            InvalidShipmentStatusTransitionException e =>
                (HttpStatusCode.UnprocessableEntity, e.Message),

            ShipmentAlreadyTerminatedException e =>
                (HttpStatusCode.UnprocessableEntity, e.Message),

            MaxDeliveryAttemptsExceededException e =>
                (HttpStatusCode.UnprocessableEntity, e.Message),

            UnauthorizedAccessException e =>
                (HttpStatusCode.Forbidden, e.Message),

            ArgumentException e =>
                (HttpStatusCode.BadRequest, e.Message),

            // Unexpected — log the full exception, return generic message
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning(exception, "Domain exception {Type} for {Method} {Path}",
                exception.GetType().Name, context.Request.Method, context.Request.Path);

        var response = new
        {
            type = exception.GetType().Name,
            message,
            traceId = context.TraceIdentifier
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}