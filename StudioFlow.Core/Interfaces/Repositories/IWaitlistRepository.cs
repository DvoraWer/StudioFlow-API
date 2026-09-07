using StudioFlow.Core.Entities;

namespace StudioFlow.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="WaitlistEntry"/> (StudioFlow spec §28, §18).
/// </summary>
public interface IWaitlistRepository
{
    /// <summary>True if this member already has a waitlist entry for the class (any status).</summary>
    Task<bool> ExistsAsync(int memberId, int classId, CancellationToken cancellationToken = default);

    /// <summary>Tracked lookup of a member's waitlist entry for a class, for the leave flow.</summary>
    Task<WaitlistEntry?> GetForUpdateAsync(int memberId, int classId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked "first in line": the earliest still-Waiting entry for a class,
    /// ordered by Position then join time — the one promoted when a seat frees up.
    /// </summary>
    Task<WaitlistEntry?> GetNextWaitingAsync(int classId, CancellationToken cancellationToken = default);

    /// <summary>Count of still-Waiting entries for a class (used to assign the next Position).</summary>
    Task<int> CountWaitingByClassAsync(int classId, CancellationToken cancellationToken = default);

    /// <summary>All waitlist entries for a class ordered by Position, with Member loaded.</summary>
    Task<IReadOnlyList<WaitlistEntry>> GetByClassAsync(int classId, CancellationToken cancellationToken = default);

    void Add(WaitlistEntry entry);
}
