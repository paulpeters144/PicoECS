namespace PicoECS;

/// <summary>
/// Mandatory base class for all entities in the PicoECS store.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public uint Id { get; internal set; } = (uint)Random.Shared.Next(1, int.MaxValue);

    internal uint ParentId { get; set; }
    internal uint[] ChildIds { get; set; } = [];
}
