using System.Threading;

namespace PicoECS;

/// <summary>
/// Mandatory base class for all entities in the PicoECS store.
/// </summary>
public abstract class Entity
{
    private static uint _nextId = 0;

    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    public uint Id { get; }

    internal uint ParentId { get; set; }
    internal uint[] ChildIds { get; set; } = [];
    internal int TypeListIndex { get; set; } = -1;

    protected Entity()
    {
        Id = Interlocked.Increment(ref _nextId);
    }
}
