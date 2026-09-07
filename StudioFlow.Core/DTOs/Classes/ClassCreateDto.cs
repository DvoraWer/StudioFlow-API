using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Classes;

/// <summary>
/// Admin payload to create a class (spec §21, POST /api/classes).
/// Server-controlled fields — Id, RegisteredCount, Status and the xmin
/// concurrency token (spec §9) — are never accepted here. All Class business
/// rules are enforced by ClassService, not this DTO: capacity vs room capacity,
/// instructor/room schedule overlaps, and EndTime later than StartTime
/// (spec §14, §15).
/// </summary>
public class ClassCreateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int InstructorId { get; set; }

    [Range(1, int.MaxValue)]
    public int RoomId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than zero.")]
    public int Capacity { get; set; }
}
