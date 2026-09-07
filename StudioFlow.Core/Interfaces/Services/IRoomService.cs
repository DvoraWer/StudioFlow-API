using StudioFlow.Core.DTOs.Rooms;

namespace StudioFlow.Core.Interfaces.Services;

/// <summary>Room administration (spec §7, §21). Admin only (enforced at the API).</summary>
public interface IRoomService
{
    Task<IReadOnlyList<RoomResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Throws <c>NotFoundException</c> if missing.</summary>
    Task<RoomResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<RoomResponseDto> CreateAsync(RoomCreateDto request, CancellationToken cancellationToken = default);

    /// <summary>Throws <c>NotFoundException</c> if missing.</summary>
    Task<RoomResponseDto> UpdateAsync(int id, RoomUpdateDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a room. Throws <c>ConflictException</c> if any class references it
    /// (spec §7, §25 — delete is restricted); deactivate instead.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
