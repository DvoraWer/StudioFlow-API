using StudioFlow.Core.Enums;

namespace StudioFlow.Core.Entities;

public class Class
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int InstructorId { get; set; }

    public int RoomId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Capacity { get; set; }

    public int RegisteredCount { get; set; }

    public ClassStatus Status { get; set; }

    // Optimistic-concurrency token (§9). Mapped read-only to PostgreSQL's system
    // `xmin` column in ClassConfiguration; it is not a stored column of its own.
    // Prevents two simultaneous requests from taking the same last seat.
    public uint Version { get; set; }

    // Instructor 1 ---- N Class.
    public Instructor Instructor { get; set; } = null!;

    // Room 1 ---- N Class.
    public Room Room { get; set; } = null!;

    // Class 1 ---- N Registration.
    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    // Class 1 ---- N WaitlistEntry.
    public ICollection<WaitlistEntry> WaitlistEntries { get; set; } = new List<WaitlistEntry>();

    // Class N ---- N Tag (join table "ClassTags", no payload).
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
