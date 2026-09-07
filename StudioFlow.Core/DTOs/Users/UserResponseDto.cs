namespace StudioFlow.Core.DTOs.Users;

/// <summary>
/// User view (spec §23). Never includes PasswordHash or any credential
/// material (spec §43).
/// </summary>
public class UserResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Role name: "Admin", "Instructor" or "Member".</summary>
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
