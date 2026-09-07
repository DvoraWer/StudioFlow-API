namespace StudioFlow.Core.Entities;

public class Instructor
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? Specialization { get; set; }

    public string? Bio { get; set; }

    // User 1 ---- 1 Instructor.
    public User User { get; set; } = null!;

    // Instructor 1 ---- N Class.
    public ICollection<Class> Classes { get; set; } = new List<Class>();
}
