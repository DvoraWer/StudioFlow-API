using StudioFlow.Core.Enums;

namespace StudioFlow.Core.Entities;

public class Registration
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int ClassId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public RegistrationStatus Status { get; set; }

    // User 1 ---- N Registration (a member is a User whose role is Member).
    public User Member { get; set; } = null!;

    // Class 1 ---- N Registration.
    public Class Class { get; set; } = null!;
}
