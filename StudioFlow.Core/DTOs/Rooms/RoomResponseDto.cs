namespace StudioFlow.Core.DTOs.Rooms;

/// <summary>Room view for GET /api/rooms and /api/rooms/{id} (spec §21, §23).</summary>
public class RoomResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MaximumCapacity { get; set; }

    public bool IsActive { get; set; }
}
