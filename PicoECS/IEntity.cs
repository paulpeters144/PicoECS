namespace PicoECS;

/// <summary>
/// Public interface for an entity.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    uint Id { get; }
}
