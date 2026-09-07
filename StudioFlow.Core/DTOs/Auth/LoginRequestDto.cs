using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Auth;

/// <summary>Credentials for JWT login (spec §19). Validated for shape only;
/// the service decides whether the user exists, the password matches and the
/// account is active.</summary>
public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}
