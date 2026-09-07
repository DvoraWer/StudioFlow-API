namespace StudioFlow.Core.Exceptions;

/// <summary>
/// The operation conflicts with the current state of the system — class full,
/// duplicate registration, instructor/room schedule overlap, capacity below the
/// active registration count, deleting an entity other rows still reference, or
/// registering for a cancelled/started class → HTTP 409 (spec §35).
/// Not sealed: <see cref="ConcurrencyConflictException"/> extends it.
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
