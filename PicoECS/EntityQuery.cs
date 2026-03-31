using System;

namespace PicoECS;

/// <summary>
/// A lightweight wrapper for deferred entity queries.
/// </summary>
public readonly struct EntityQuery
{
    private readonly EcStore _store;
    private readonly Type[] _types;

    internal EntityQuery(EcStore store, Type[] types)
    {
        _store = store;
        _types = types;
    }

    /// <summary>
    /// Executes the provided action on every entity matching the query types.
    /// </summary>
    public void ForEach(Action<Entity> action)
    {
        _store.ForEachInternal(_types, action);
    }
}
