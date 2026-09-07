using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Auth;

/// <summary>
/// Public self-registration payload (spec §19). The created account is always a
/// Member — the role is decided by the service, never accepted from the client
/// (spec §5, §43). The plaintext password is inbound only; it is hashed by the
/// service and never stored or returned (spec §9, §43).
/// </summary>
public class RegisterRequestDto
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
}
