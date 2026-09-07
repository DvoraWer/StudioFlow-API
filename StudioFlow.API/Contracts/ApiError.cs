namespace StudioFlow.API.Contracts;

/// <summary>
/// The single error response shape for the whole API (spec §17, §31, §35).
/// No stack traces, no EF or database internals — <see cref="Message"/> is safe
/// for clients.
/// </summary>
public sealed class ApiError
{
    public ApiError(string code, string message, string correlationId)
    {
        Code = code;
        Message = message;
        CorrelationId = correlationId;
    }

    /// <summary>Stable machine-readable code, e.g. <c>NOT_FOUND</c>, <c>CONCURRENCY_CONFLICT</c>.</summary>
    public string Code { get; }

    /// <summary>Human-readable, client-safe explanation.</summary>
    public string Message { get; }

    /// <summary>The request's correlation id (also returned in the <c>X-Correlation-Id</c> header).</summary>
    public string CorrelationId { get; }
}
