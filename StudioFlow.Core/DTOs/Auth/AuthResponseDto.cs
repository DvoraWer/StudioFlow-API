namespace StudioFlow.Core.DTOs.Auth;

/// <summary>
/// Result of a successful register/login (spec §19, §23). Carries the signed JWT
/// plus the minimum identity a client needs to render its UI. Never contains the
/// password or the password hash (spec §9, §43).
/// </summary>
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Role name: "Admin", "Instructor" or "Member".</summary>
    public string Role { get; set; } = string.Empty;
}
