namespace StudioFlow.Core.DTOs.Classes;

/// <summary>
/// One row of the paged classes list (spec §22, §39 screen 2): name, instructor,
/// room, time, available seats, status. Deliberately slim — no description, no
/// tags, no nested collections — so a page of rows never drags large navigation
/// graphs through AutoMapper.
/// </summary>
public class ClassListItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string InstructorName { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Capacity { get; set; }

    public int RegisteredCount { get; set; }

    public int AvailableSeats => Capacity - RegisteredCount;

    public bool IsFull => RegisteredCount >= Capacity;

    /// <summary>Status name: "Active" or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;
}
