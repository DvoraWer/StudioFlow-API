namespace StudioFlow.Core.Exceptions;

/// <summary>
/// Login failed — no such user, wrong password, or the account is inactive →
/// HTTP 401 (spec §19, §35). The message is deliberately generic so it does not
/// reveal which check failed.
/// </summary>
public sealed class AuthenticationException : DomainException
{
    public AuthenticationException(string message = "Invalid email or password.")
        : base(message)
    {
    }
}
