using StudioFlow.Core.DTOs.Instructors;

namespace StudioFlow.Core.Interfaces.Services;

/// <summary>Instructor administration (spec §6, §21). Admin only (enforced at the API).</summary>
public interface IInstructorService
{
    Task<IReadOnlyList<InstructorResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Throws <c>NotFoundException</c> if missing.</summary>
    Task<InstructorResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Provisions a new <c>User</c> (Role = Instructor, active, hashed password) and
    /// a linked <c>Instructor</c> in one atomic operation (Phase 5 decision). Throws
    /// <c>ConflictException</c> if the email is already in use.
    /// </summary>
    Task<InstructorResponseDto> CreateAsync(InstructorCreateDto request, CancellationToken cancellationToken = default);

    /// <summary>Updates Specialization/Bio only; the linked user is never changed.</summary>
    Task<InstructorResponseDto> UpdateAsync(int id, InstructorUpdateDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the <c>Instructor</c> (the linked <c>User</c> is left intact). Throws
    /// <c>ConflictException</c> if any class references it (spec §25).
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
