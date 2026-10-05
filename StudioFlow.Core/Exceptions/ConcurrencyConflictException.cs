namespace StudioFlow.Core.Exceptions;

/// <summary>
/// An optimistic-concurrency clash was detected on save (spec §9, §17): two
/// writers raced for the last seat and this one lost. The Data layer's UnitOfWork
/// raises this in place of EF Core's <c>DbUpdateConcurrencyException</c> so the
/// Service and API never take a dependency on EF Core. Maps to HTTP 409.
/// </summary>
public sealed class ConcurrencyConflictException : ConflictException
{
    public const string DefaultMessage = "The last available seat was taken by another user.";

    /// <summary>Used by the waitlist join/leave flows, which share the Class concurrency token.</summary>
    public const string WaitlistMessage =
        "The waiting list for this class was changed by another request. Please try again.";

    public ConcurrencyConflictException()
        : base(DefaultMessage)
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
