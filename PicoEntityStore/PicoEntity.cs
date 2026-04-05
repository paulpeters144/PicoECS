namespace PicoEntityStoreCore;

/// <summary>
/// Mandatory base class for all entities in the PicoEntityStore store.
/// </summary>
using System.Security.Cryptography;

public abstract class PicoEntity
{
    public uint Id { get; }
    internal uint ParentId { get; set; }
    internal uint[] ChildIds { get; set; } = [];
    internal int TypeListIndex { get; set; } = -1;
    protected PicoEntity()
    {
        Id = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
    }
}