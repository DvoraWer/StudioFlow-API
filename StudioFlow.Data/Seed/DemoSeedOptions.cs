namespace StudioFlow.Data.Seed;

/// <summary>
/// Settings for <see cref="ProductionDemoSeed"/>, bound from the <c>DemoSeed</c>
/// configuration section (env vars <c>DemoSeed__Enabled</c>, <c>DemoSeed__AdminEmail</c>, …).
/// No values live in tracked files: credentials come from environment variables
/// (Render) or User Secrets. Passwords are only ever passed to <c>IPasswordHasher</c>.
/// </summary>
public sealed class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    /// <summary>The seed runs only when this is true (default false).</summary>
    public bool Enabled { get; set; }

    /// <summary>Display name of the Demo Admin. Needed only while the admin does not exist yet.</summary>
    public string? AdminName { get; set; }

    /// <summary>Natural key of the Demo Admin. Always required when the seed is enabled.</summary>
    public string? AdminEmail { get; set; }

    /// <summary>Demo Admin password. Needed only while the admin does not exist yet.</summary>
    public string? AdminPassword { get; set; }

    /// <summary>Demo Instructor password. Needed only while the instructor does not exist yet.</summary>
    public string? DemoUserPassword { get; set; }
}
