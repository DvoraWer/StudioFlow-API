using System.ComponentModel.DataAnnotations;

namespace StudioFlow.Core.DTOs.Classes;

/// <summary>
/// Admin payload to update a class (spec §21, PUT /api/classes/{id}). Carries the
/// same editable fields as <see cref="ClassCreateDto"/>. Status is not here —
/// cancellation is its own endpoint (POST /api/classes/{id}/cancel). The xmin
/// concurrency token is not exposed; the service handles conflict detection
/// (spec §9, §17). Class business rules — including EndTime later than StartTime —
/// and which fields an Instructor (vs Admin) may change (spec §13) are enforced
/// by ClassService, not this DTO.
/// </summary>
public class ClassUpdateDto
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
