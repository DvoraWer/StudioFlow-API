namespace StudioFlow.Core.DTOs.Waitlist;

/// <summary>
/// Waitlist entry view (spec §11, §18, §21). Returned when a member joins a
/// full class's waiting list and when listing their position.
/// </summary>
public class WaitlistResponseDto
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int ClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public int Position { get; set; }

    public DateTime JoinedAt { get; set; }

    /// <summary>Status name: "Waiting", "Promoted" or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;
}
