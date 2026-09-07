using StudioFlow.Core.Enums;

namespace StudioFlow.Core.DTOs.Classes;

/// <summary>
/// Filter + paging inputs for the classes list endpoint (StudioFlow spec §22).
/// Bound from the query string by the controller; the repository turns these into
/// a single database query (Where → OrderBy → Skip → Take).
/// </summary>
public class ClassQueryParameters
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    /// <summary>Free-text match against class name / description.</summary>
    public string? Search { get; set; }

    public int? InstructorId { get; set; }

    public int? RoomId { get; set; }

    public ClassStatus? Status { get; set; }

    /// <summary>Restrict to classes whose start time falls on this calendar day (UTC).</summary>
    public DateTime? Date { get; set; }
}
