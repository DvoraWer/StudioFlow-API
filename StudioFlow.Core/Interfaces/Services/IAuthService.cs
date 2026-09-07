using StudioFlow.Core.DTOs.Auth;
using StudioFlow.Core.DTOs.Users;

namespace StudioFlow.Core.Interfaces.Services;

/// <summary>Registration and login (spec §19).</summary>
public interface IAuthService
{
    /// <summary>
    /// Creates a new account. The new user is always <c>Role = Member</c> and
    /// active (spec §5, §19). Returns the created user — no token is issued here;
    /// §19 lists JWT creation under Login only. Throws <c>ConflictException</c> if
    /// the email is already in use.
    /// </summary>
    Task<UserResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the credentials and the account's active state, then returns a
    /// signed JWT plus identity (spec §19). Throws <c>AuthenticationException</c>
    /// on any failure, without revealing which check failed.
    /// </summary>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
