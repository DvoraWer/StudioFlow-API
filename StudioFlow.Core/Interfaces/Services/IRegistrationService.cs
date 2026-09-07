using StudioFlow.Core.DTOs.Registrations;

namespace StudioFlow.Core.Interfaces.Services;

/// <summary>The registration algorithm and its cancellation / promotion side-effects (spec §16, §17, §18).</summary>
public interface IRegistrationService
{
    /// <summary>
    /// Registers the member for the class (spec §16). Throws <c>ConflictException</c>
    /// if the class is cancelled, already started, already registered, or full, and
    /// <c>ConcurrencyConflictException</c> if another request took the last seat
    /// during the operation (spec §17). Does NOT fall back to the waiting list.
    /// </summary>
    Task<RegistrationResponseDto> RegisterAsync(int classId, int memberId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the member's active registration and, if that frees a seat on an
    /// active class, promotes the first waiting member — creating their registration
    /// and marking their waiting-list entry Promoted — all in one transaction
    /// (spec §18).
    /// </summary>
    Task CancelAsync(int classId, int memberId, CancellationToken cancellationToken = default);

    /// <summary>The member's own registrations, newest first (spec §21, /api/me/registrations).</summary>
    Task<IReadOnlyList<RegistrationResponseDto>> GetMyRegistrationsAsync(int memberId, CancellationToken cancellationToken = default);
}
