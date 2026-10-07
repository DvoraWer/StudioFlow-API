using StudioFlow.Core.DTOs.Instructors;

namespace StudioFlow.Core.Interfaces.Services;

/// <summary>
/// Instructor administration (spec §6, §21) — Admin only, enforced at the API — plus
/// the instructor's own profile (the <c>*MyProfile*</c> methods, resolved from the
/// caller's user id, Instructor only).
/// </summary>
public interface IInstructorService
{
    /// <summary>The profile linked to <paramref name="userId"/>. Throws <c>NotFoundException</c> if there is none.</summary>
    Task<InstructorResponseDto> GetMyProfileAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates Specialization/Bio of the profile linked to <paramref name="userId"/>
    /// only. Throws <c>NotFoundException</c> if there is none (it is never created here).
    /// </summary>
    Task<InstructorResponseDto> UpdateMyProfileAsync(int userId, InstructorUpdateDto request, CancellationToken cancellationToken = default);

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
