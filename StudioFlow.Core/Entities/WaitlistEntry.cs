using StudioFlow.Core.Enums;

namespace StudioFlow.Core.Entities;

public class WaitlistEntry
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int ClassId { get; set; }

    public DateTime JoinedAt { get; set; }

    public int Position { get; set; }

    public WaitlistStatus Status { get; set; }

    // User 1 ---- N WaitlistEntry (a member is a User whose role is Member).
    public User Member { get; set; } = null!;

    // Class 1 ---- N WaitlistEntry.
    public Class Class { get; set; } = null!;
}
