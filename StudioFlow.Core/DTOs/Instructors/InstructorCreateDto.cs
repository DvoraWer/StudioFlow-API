using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Instructors;

/// <summary>
/// Admin payload to create an instructor (spec §6, §21). A single operation
/// provisions both the User account (from Name/Email/Password) and the linked
/// Instructor entity. Phase 6 (InstructorService) checks the email is unused,
/// assigns the Instructor role, links the Instructor to the new User, and
/// persists atomically. Specialization and Bio are optional (spec §6).
/// </summary>
public class InstructorCreateDto
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Specialization { get; set; }

    [StringLength(2000)]
    public string? Bio { get; set; }
}
