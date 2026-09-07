namespace StudioFlow.Core.DTOs.Instructors;

/// <summary>
/// Instructor view for GET /api/instructors and /api/instructors/{id}
/// (spec §21, §23). Name and Email are flattened from the linked user account;
/// PasswordHash is never included (spec §43).
/// </summary>
public class InstructorResponseDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Specialization { get; set; }

    public string? Bio { get; set; }
}
