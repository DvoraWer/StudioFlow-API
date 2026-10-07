using StudioFlow.Core.DTOs.Users;

namespace StudioFlow.Core.Interfaces.Services;

/// <summary>
/// Minimal user lookup (spec §5). §21 defines no user-management endpoints; this
/// backs the "current user" lookup (GET /api/me/account).
/// </summary>
public interface IUserService
{
    /// <summary>Read-only view of a user. Throws <c>NotFoundException</c> if missing.</summary>
    Task<UserResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
