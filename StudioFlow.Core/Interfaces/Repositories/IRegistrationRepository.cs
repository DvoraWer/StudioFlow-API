using StudioFlow.Core.Entities;

namespace StudioFlow.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="Registration"/> (StudioFlow spec §28).
/// </summary>
public interface IRegistrationRepository
{
    /// <summary>Read-only lookup by id, with Class and Member loaded.</summary>
    Task<Registration?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked lookup of a member's registration for a class (any status), for the
    /// cancel flow. Returns null if the member never registered for that class.
    /// </summary>
    Task<Registration?> GetForUpdateAsync(int memberId, int classId, CancellationToken cancellationToken = default);

    /// <summary>True if a registration row already exists for this member + class (any status).</summary>
    Task<bool> ExistsAsync(int memberId, int classId, CancellationToken cancellationToken = default);

    /// <summary>Count of active registrations for a class (used for the capacity rules).</summary>
    Task<int> CountActiveByClassAsync(int classId, CancellationToken cancellationToken = default);

    /// <summary>A member's registrations, newest first, with Class (+ Room, Instructor→User) loaded.</summary>
    Task<IReadOnlyList<Registration>> GetByMemberAsync(int memberId, CancellationToken cancellationToken = default);

    /// <summary>Active registrations for a class, with Member loaded (participants list).</summary>
    Task<IReadOnlyList<Registration>> GetActiveByClassAsync(int classId, CancellationToken cancellationToken = default);

    void Add(Registration registration);
}
