using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Auth;

/// <summary>
/// Authenticated self-service password change. The user is always the caller
/// (from the JWT), never a value in this payload. Both passwords are inbound only:
/// the current one is verified against the stored hash and the new one is hashed;
/// neither is stored, returned or logged (spec §9, §43). The "confirm new password"
/// check is client-only and deliberately not part of this contract.
/// </summary>
public class ChangePasswordRequestDto
{
    [Required]
    [StringLength(100)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}
