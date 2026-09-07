using System.ComponentModel.DataAnnotations;

namespace StudioFlow.API.Authentication;

/// <summary>
/// Strongly-typed JWT settings bound from the <c>Jwt</c> configuration section
/// (spec §19, §44). The signing <see cref="Key"/> is a secret: it is empty in the
/// committed <c>appsettings.json</c> and supplied per-environment
/// (appsettings.Development.json for local Docker, User Secrets / env vars
/// elsewhere).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32)] // HMAC-SHA256 needs a key of at least 256 bits
    public string Key { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int ExpiryMinutes { get; set; } = 120;
}
