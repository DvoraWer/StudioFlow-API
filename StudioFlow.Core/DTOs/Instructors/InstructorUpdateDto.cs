using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Instructors;

/// <summary>
/// Admin payload to update an instructor's profile fields (spec §6, §21).
/// The linked user account is not reassigned through this DTO.
/// </summary>
public class InstructorUpdateDto
{
    [StringLength(200)]
    public string? Specialization { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }
}
