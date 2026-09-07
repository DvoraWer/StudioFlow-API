using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Rooms;

/// <summary>Admin payload to update a room (spec §7, §21). Reducing
/// MaximumCapacity below what existing classes already need is a service-level
/// business check, not DTO validation.</summary>
public class RoomUpdateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int MaximumCapacity { get; set; }

    public bool IsActive { get; set; } = true;
}
