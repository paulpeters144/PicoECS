namespace PicoECS;

/// <summary>
/// Internal interface for managing entity state without exposing it to the public API.
/// </summary>
internal interface IInternalEntity : IEntity
{
    new uint Id { get; set; }
    uint ParentId { get; set; }
    uint[] ChildIds { get; set; }
}
