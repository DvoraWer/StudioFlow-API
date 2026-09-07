namespace StudioFlow.Core.Interfaces.Security;

/// <summary>
/// Hashes and verifies user passwords (spec §5, §43 — plaintext is never stored).
/// The concrete algorithm lives in the Service layer; callers depend only on this
/// abstraction.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Produces a self-describing hash string (algorithm, parameters, salt and
    /// derived key) suitable for storing in <c>User.PasswordHash</c>.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// True if <paramref name="password"/> matches a hash previously produced by
    /// <see cref="Hash"/>. Returns false (never throws) for malformed input.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
