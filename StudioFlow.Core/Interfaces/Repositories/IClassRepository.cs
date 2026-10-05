using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.DTOs.Common;
using StudioFlow.Core.Entities;

namespace StudioFlow.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="Class"/> (StudioFlow spec §28). Includes the
/// server-side paged/filtered list (§22) and the scheduling-overlap queries the
/// service uses to enforce the no-double-booking rules (§15).
/// </summary>
public interface IClassRepository
{
    /// <summary>
    /// One page of classes matching the given filters, ordered by start time.
    /// Read-only; Instructor (+ its User) and Room are eager-loaded for display.
    /// Skip/Take/Count run in the database.
    /// </summary>
    Task<PagedResult<Class>> GetPagedAsync(ClassQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Read-only lookup by id, with Instructor (+ User) and Room loaded.</summary>
    Task<Class?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked lookup by id with no includes — used by the register / cancel /
    /// admin-edit flows where the concurrency token must travel with the entity.
    /// </summary>
    Task<Class?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a tracked Class (from <see cref="GetForUpdateAsync"/>) part of the next save
    /// without changing any of its values: the row is written back as-is, so the xmin
    /// concurrency token is checked and advanced. Used to make the Class the per-class
    /// concurrency boundary for changes that do not otherwise touch it (waitlist join/leave).
    /// </summary>
    void MarkForConcurrencyCheck(Class @class);

    /// <summary>True if this instructor already has an active class overlapping the given time window.</summary>
    Task<bool> InstructorHasOverlapAsync(int instructorId, DateTime startTime, DateTime endTime, int? excludeClassId, CancellationToken cancellationToken = default);

    /// <summary>True if this room already holds an active class overlapping the given time window.</summary>
    Task<bool> RoomHasOverlapAsync(int roomId, DateTime startTime, DateTime endTime, int? excludeClassId, CancellationToken cancellationToken = default);

    void Add(Class @class);
}
