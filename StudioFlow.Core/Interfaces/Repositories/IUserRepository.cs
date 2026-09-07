using StudioFlow.Core.Entities;

namespace StudioFlow.Core.Interfaces.Repositories;

/// <summary>
/// Data access for <see cref="User"/>. No business rules — callers in the service
/// layer decide what to do with what is returned (StudioFlow spec §28).
/// </summary>
public interface IUserRepository
{
    /// <summary>Read-only lookup by id (no change tracking).</summary>
    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Tracked lookup by id, for load-modify-save flows.</summary>
    Task<User?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Read-only lookup by email (used by login and registration).</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    void Add(User user);
}
