namespace PicoECS;

/// <summary>
/// Mandatory base class for all entities in the PicoECS store.
/// </summary>
public abstract class Entity : IEntity, IInternalEntity
{
    public uint Id { get; private set; }
    public uint ParentId { get; private set; }
    public uint[] ChildIds { get; private set; } = [];

    // Explicit implementation to keep the public API clean while allowing EcStore access
    uint IInternalEntity.Id
    {
        get => Id;
        set => Id = value;
    }

    uint IInternalEntity.ParentId
    {
        get => ParentId;
        set => ParentId = value;
    }

    uint[] IInternalEntity.ChildIds
    {
        get => ChildIds;
        set => ChildIds = value;
    }
}
