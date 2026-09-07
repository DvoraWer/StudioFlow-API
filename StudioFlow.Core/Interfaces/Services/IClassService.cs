using StudioFlow.Core.DTOs.Classes;
using StudioFlow.Core.DTOs.Common;
using StudioFlow.Core.DTOs.Participants;
using StudioFlow.Core.Enums;

namespace StudioFlow.Core.Interfaces.Services;

/// <summary>Class catalogue and scheduling (spec §8, §14, §15, §21, §22).</summary>
public interface IClassService
{
    /// <summary>Server-side paged and filtered list (spec §22).</summary>
    Task<PagedResult<ClassListItemDto>> GetPagedAsync(ClassQueryParameters query, CancellationToken cancellationToken = default);

    /// <summary>Full detail including tags (spec §21). Throws <c>NotFoundException</c> if missing.</summary>
    Task<ClassResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a class after enforcing every §14/§15 rule. Admin only (enforced at the API).</summary>
    Task<ClassResponseDto> CreateAsync(ClassCreateDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the editable fields and re-validates every affected §14/§15 rule
    /// (schedule overlaps, room capacity, capacity vs. active registrations).
    /// Admin only. Server-managed state (RegisteredCount, Status, concurrency
    /// token) is never taken from the request.
    /// </summary>
    Task<ClassResponseDto> UpdateAsync(int id, ClassUpdateDto request, CancellationToken cancellationToken = default);

    /// <summary>Marks the class Cancelled (spec §21). Existing registrations are left intact.</summary>
    Task CancelAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active participants of a class (spec §21). An Admin may view any class; an
    /// instructor only their own class, otherwise <c>ForbiddenActionException</c>.
    /// </summary>
    Task<IReadOnlyList<ParticipantDto>> GetParticipantsAsync(
        int classId, int callerUserId, UserRole callerRole, CancellationToken cancellationToken = default);
}
