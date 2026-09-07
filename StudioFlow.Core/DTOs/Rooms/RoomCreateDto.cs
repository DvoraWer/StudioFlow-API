using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Rooms;

/// <summary>Admin payload to create a room (spec §7, §21).</summary>
public class RoomCreateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int MaximumCapacity { get; set; }

    public bool IsActive { get; set; } = true;
}
