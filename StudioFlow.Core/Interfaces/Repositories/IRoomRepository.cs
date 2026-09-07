using StudioFlow.Core.Entities;

namespace StudioFlow.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="Room"/> (StudioFlow spec §28).
/// </summary>
public interface IRoomRepository
{
    /// <summary>Read-only lookup by id (no change tracking).</summary>
    Task<Room?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tracked lookup by id, for load-modify-save flows.</summary>
    Task<Room?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Room>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>True if any class references this room (delete is restricted).</summary>
    Task<bool> HasClassesAsync(int roomId, CancellationToken cancellationToken = default);

    void Add(Room room);

    void Remove(Room room);
}
