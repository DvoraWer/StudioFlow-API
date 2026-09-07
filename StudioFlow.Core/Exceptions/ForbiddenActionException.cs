namespace StudioFlow.Core.Exceptions;

/// <summary>
/// The caller is authenticated but not allowed to perform this action on this
/// resource — e.g. an instructor requesting the participants of a class that is
/// not theirs, or a non-member trying to register → HTTP 403 (spec §20, §35).
/// </summary>
public sealed class ForbiddenActionException : DomainException
{
    public ForbiddenActionException(string message)
        : base(message)
    {
    }
}
