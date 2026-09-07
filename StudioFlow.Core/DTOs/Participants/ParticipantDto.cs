namespace StudioFlow.Core.DTOs.Participants;

/// <summary>
/// One participant in a class roster, for GET /api/classes/{id}/participants
/// (spec §21). Exposed only to the class's own instructor and to admins.
/// MemberId is the participant's user id; PasswordHash is never included.
/// </summary>
public class ParticipantDto
{
    public int MemberId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; }

    /// <summary>Registration status name: "Active" or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;
}
