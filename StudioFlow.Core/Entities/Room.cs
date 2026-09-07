namespace StudioFlow.Core.Entities;

public class Room
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int MaximumCapacity { get; set; }

    public bool IsActive { get; set; }

    // Room 1 ---- N Class.
    public ICollection<Class> Classes { get; set; } = new List<Class>();
}
