using StudioFlow.Core.Entities;

namespace StudioFlow.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="Instructor"/> (StudioFlow spec §28).
/// </summary>
public interface IInstructorRepository
{
    /// <summary>Read-only lookup by id, including the linked <see cref="User"/>.</summary>
    Task<Instructor?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tracked lookup by id, for load-modify-save flows.</summary>
    Task<Instructor?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Read-only lookup by the owning user id (resolves "my" instructor profile from a token).</summary>
    Task<Instructor?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Instructor>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>True if any class references this instructor (delete is restricted).</summary>
    Task<bool> HasClassesAsync(int instructorId, CancellationToken cancellationToken = default);

    void Add(Instructor instructor);

    void Remove(Instructor instructor);
}
