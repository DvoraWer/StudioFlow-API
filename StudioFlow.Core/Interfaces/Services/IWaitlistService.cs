using StudioFlow.Core.DTOs.Waitlist;

namespace StudioFlow.Core.Interfaces.Services;

/// <summary>
/// Waiting-list membership (spec §18). This is the only service that creates
/// <c>WaitlistEntry</c> rows — <c>RegistrationService</c> never does.
/// </summary>
public interface IWaitlistService
{
    /// <summary>
    /// Adds the member to the class's waiting list at the next position. Allowed
    /// only when the class exists, is active, has not started, and is full, and the
    /// member is neither actively registered nor already waiting (spec §18);
    /// otherwise throws <c>ConflictException</c>. A previously cancelled entry for
    /// the same member/class is re-activated rather than duplicated.
    /// </summary>
    Task<WaitlistResponseDto> JoinAsync(int classId, int memberId, CancellationToken cancellationToken = default);

    /// <summary>Removes the member from the waiting list (marks their entry Cancelled).</summary>
    Task LeaveAsync(int classId, int memberId, CancellationToken cancellationToken = default);
}
