namespace StudioFlow.Core.DTOs.Classes;

/// <summary>
/// Full class detail for GET /api/classes/{id} (spec §21, §39 screen 3).
/// AvailableSeats and IsFull are derived on read, never stored (spec §8).
/// The PostgreSQL xmin concurrency token is intentionally not exposed (spec §9).
/// Tags are shown as a flat list of <see cref="TagDto"/> — never the EF
/// navigation collection.
/// </summary>
public class ClassResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int InstructorId { get; set; }

    public string InstructorName { get; set; } = string.Empty;

    public int RoomId { get; set; }

    public string RoomName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Capacity { get; set; }

    public int RegisteredCount { get; set; }

    public int AvailableSeats => Capacity - RegisteredCount;

    public bool IsFull => RegisteredCount >= Capacity;

    /// <summary>Status name: "Active" or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<TagDto> Tags { get; set; } = new List<TagDto>();
}
