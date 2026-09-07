using StudioFlow.Core.Enums;

namespace StudioFlow.Core.Entities;

public class User
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; }

    // User 1 ---- 1 Instructor (optional: not every user is an instructor).
    public Instructor? Instructor { get; set; }

    // User 1 ---- N Registration.
    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    // User 1 ---- N WaitlistEntry.
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();
}
