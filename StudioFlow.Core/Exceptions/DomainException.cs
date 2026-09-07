namespace StudioFlow.Core.Exceptions;

/// <summary>
/// Base type for expected, business-level failures raised by the Service layer.
/// The API's global exception middleware (spec §31, §35) maps each concrete
/// subtype to an HTTP status code; anything that is not a <see cref="DomainException"/>
/// becomes 500. Services throw these — they never deal in HTTP or EF Core types.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
