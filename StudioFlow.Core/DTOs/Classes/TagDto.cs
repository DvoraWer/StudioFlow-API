namespace StudioFlow.Core.DTOs.Classes;

/// <summary>
/// Lightweight read-only view of a <see cref="Entities.Tag"/> as it appears
/// inside a class response. Tags are reference data (added in Phase 4.5): there
/// are deliberately no Tag CRUD DTOs and no Tag endpoints. Lives with the class
/// DTOs because that is the only place the API contract exposes it.
/// </summary>
public class TagDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
