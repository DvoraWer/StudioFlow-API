using System.Text.Json;
using StudioFlow.API.Contracts;
using StudioFlow.Core.Exceptions;

namespace StudioFlow.API.Middleware;

/// <summary>
/// Single place that turns exceptions into HTTP responses (spec §31, §33, §35).
/// Known <see cref="DomainException"/>s map to their status code with a client-safe
/// message; everything else becomes a generic 500. EF Core details, stack traces
/// and <c>DbUpdateConcurrencyException</c> never reach the client — the Data layer
/// has already translated a concurrency clash into
/// <see cref="ConcurrencyConflictException"/> (spec §9, §17).
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away mid-request; there is nobody to send a body to.
            _logger.LogInformation("Request aborted by the client. CorrelationId={CorrelationId}",
                context.GetCorrelationId());
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.GetCorrelationId();
        var (status, code, message) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogWarning(
                "{Code} ({Status}) on {Method} {Path}. CorrelationId={CorrelationId}. {Detail}",
                code, status, context.Request.Method, context.Request.Path, correlationId, exception.Message);
        }

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started; error body not written. CorrelationId={CorrelationId}", correlationId);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        var body = new ApiError(code, message, correlationId);
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }

    // ConcurrencyConflictException must be matched before ConflictException (it derives from it).
    private static (int Status, string Code, string Message) Map(Exception exception) => exception switch
    {
        ConcurrencyConflictException ex => (StatusCodes.Status409Conflict, "CONCURRENCY_CONFLICT", ex.Message),
        ConflictException ex => (StatusCodes.Status409Conflict, "CONFLICT", ex.Message),
        NotFoundException ex => (StatusCodes.Status404NotFound, "NOT_FOUND", ex.Message),
        ValidationException ex => (StatusCodes.Status400BadRequest, "VALIDATION_ERROR", ex.Message),
        AuthenticationException ex => (StatusCodes.Status401Unauthorized, "UNAUTHORIZED", ex.Message),
        ForbiddenActionException ex => (StatusCodes.Status403Forbidden, "FORBIDDEN", ex.Message),
        _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.")
    };
}
