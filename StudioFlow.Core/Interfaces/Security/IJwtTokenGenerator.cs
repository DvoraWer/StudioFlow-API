using StudioFlow.Core.Entities;

namespace StudioFlow.Core.Interfaces.Security;

/// <summary>
/// Issues signed JWTs for authenticated users (spec §19). The concrete
/// implementation, signing key and issuer/audience configuration are wired in the
/// API host during the authentication phase; the Service layer depends only on
/// this abstraction.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Creates a signed JWT carrying at least the user id and role claims
    /// (spec §19).
    /// </summary>
    string GenerateToken(User user);
}
