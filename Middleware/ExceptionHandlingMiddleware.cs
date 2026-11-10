using System.Text.Json;
using System.Text.Json.Serialization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Exceptions;

namespace HospitalManagement.Api.Middleware;

/// <summary>
/// Converts exceptions into the standard ApiResponse envelope.
/// Stack traces and internal messages are logged, never returned to the client.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to send back.
            logger.LogDebug("Request was cancelled by the client: {Path}", context.Request.Path);
        }
        catch (AppException ex)
        {
            logger.LogWarning("Handled application error {StatusCode} on {Path}: {Message}",
                ex.StatusCode, context.Request.Path, ex.Message);
            await WriteResponseAsync(context, ex.StatusCode, ex.Message, ex.Errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteResponseAsync(context, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred. Please try again later.", Array.Empty<string>());
        }
    }

    private static async Task WriteResponseAsync(
        HttpContext context, int statusCode, string message, IEnumerable<string> errors)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = ApiResponse.Failure(message, errors, context.TraceIdentifier);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
