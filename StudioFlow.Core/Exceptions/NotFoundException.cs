namespace StudioFlow.Core.Exceptions;

/// <summary>A requested entity does not exist → HTTP 404 (spec §35).</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Convenience factory: <c>For("Class", 42)</c> → "Class '42' was not found."</summary>
    public static NotFoundException For(string entity, object key) =>
        new($"{entity} '{key}' was not found.");
}
