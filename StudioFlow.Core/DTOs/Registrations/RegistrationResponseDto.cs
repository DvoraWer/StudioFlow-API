namespace StudioFlow.Core.DTOs.Registrations;

/// <summary>
/// A member's registration, primarily for GET /api/me/registrations
/// (spec §21, §39 screen 4). Class time / room / instructor are flattened in so
/// the client can render the list without a second call.
/// </summary>
public class RegistrationResponseDto
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int ClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string RoomName { get; set; } = string.Empty;

    public string InstructorName { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; }

    /// <summary>Status name: "Active" or "Cancelled".</summary>
    public string Status { get; set; } = string.Empty;
}
