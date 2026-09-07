namespace StudioFlow.Core.Exceptions;

/// <summary>
/// The request is well-formed but violates a business-state rule that is not a
/// conflict — capacity outside the allowed range, EndTime not after StartTime, a
/// referenced instructor/room that does not exist → HTTP 400 (spec §35, §36).
/// </summary>
public sealed class ValidationException : DomainException
{
    public ValidationException(string message)
        : base(message)
    {
    }
}
