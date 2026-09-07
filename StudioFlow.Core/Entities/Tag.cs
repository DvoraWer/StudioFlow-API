namespace StudioFlow.Core.Entities;

public class Tag
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    // Tag N ---- N Class (join table "ClassTags", no payload).
    public ICollection<Class> Classes { get; set; } = new List<Class>();
}
