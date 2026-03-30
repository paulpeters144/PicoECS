namespace PicoECS;

/// <summary>
/// Mandatory base class for all entities in the PicoECS store.
/// </summary>
public abstract class Entity : IEntity, IInternalEntity
{
    public uint Id { get; private set; }

    // Use private fields to store the internal state
    private uint _parentId;
    private uint[] _childIds = [];

    // Explicit implementation to keep the public API clean while allowing EcStore access
    uint IInternalEntity.Id
    {
        get => Id;
        set => Id = value;
    }

    uint IInternalEntity.ParentId
    {
        get => _parentId;
        set => _parentId = value;
    }

    uint[] IInternalEntity.ChildIds
    {
        get => _childIds;
        set => _childIds = value;
    }
}
