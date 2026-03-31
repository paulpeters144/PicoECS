using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PicoECS.Tests")]

namespace PicoECS;

/// <summary>
/// A fast, thread-safe store for entities and their relationships.
/// </summary>
public sealed class PicoStore
{
    private readonly Dictionary<Type, List<PicoEntity>> _typeLists = [];
    private readonly Dictionary<uint, PicoEntity> _idIndex = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public int Count 
    { 
        get 
        {
            _lock.EnterReadLock();
            try
            {
                return _idIndex.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Retrieves all entities in the store.
    /// </summary>
    public List<PicoEntity> All()
    {
        _lock.EnterReadLock();
        try
        {
            var result = new List<PicoEntity>(_idIndex.Count);
            foreach (var list in _typeLists.Values)
            {
                result.AddRange(list);
            }
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Retrieves all entities of the specified type.
    /// </summary>
    public List<T> All<T>() where T : PicoEntity
    {
        _lock.EnterReadLock();
        try
        {
            if (_typeLists.TryGetValue(typeof(T), out var list))
            {
                var result = new List<T>(list.Count);
                foreach (var entity in list)
                {
                    result.Add((T)entity);
                }
                return result;
            }
            return [];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Executes the provided action on every entity in the store.
    /// </summary>
    public void ForEach(Action<PicoEntity> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _lock.EnterReadLock();
        try
        {
            foreach (var list in _typeLists.Values)
            {
                foreach (var entity in list)
                {
                    action(entity);
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Executes the provided action on every entity of the specified type.
    /// </summary>
    public void ForEach<T>(Action<T> action) where T : PicoEntity
    {
        ArgumentNullException.ThrowIfNull(action);
        _lock.EnterReadLock();
        try
        {
            if (_typeLists.TryGetValue(typeof(T), out var list))
            {
                foreach (var entity in list)
                {
                    action((T)entity);
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Adds a parent entity and its children to the store, establishing relationships.
    /// </summary>
    /// <param name="parent">The parent entity.</param>
    /// <param name="children">Optional child entities.</param>
    public void Add(PicoEntity parent, params PicoEntity[] children)
    {
        ArgumentNullException.ThrowIfNull(parent);

        _lock.EnterWriteLock();
        try
        {
            ensurePicoEntityIndexed(parent);

            if (children != null && children.Length > 0)
            {
                uint[]? newChildIds = null;
                int addedCount = 0;

                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (child is null) continue;

                    validateChildRelationship(child, parent.Id);

                    bool alreadyChild = false;
                    var existingChildIds = parent.ChildIds;
                    for (int j = 0; j < existingChildIds.Length; j++)
                    {
                        if (existingChildIds[j] == child.Id)
                        {
                            alreadyChild = true;
                            break;
                        }
                    }

                    if (!alreadyChild && newChildIds != null)
                    {
                        for (int j = 0; j < addedCount; j++)
                        {
                            if (newChildIds[j] == child.Id)
                            {
                                alreadyChild = true;
                                break;
                            }
                        }
                    }

                    if (!alreadyChild)
                    {
                        newChildIds ??= new uint[children.Length];
                        newChildIds[addedCount++] = child.Id;
                    }

                    child.ParentId = parent.Id;
                    ensurePicoEntityIndexed(child);
                }

                if (addedCount > 0)
                {
                    int originalCount = parent.ChildIds.Length;
                    var combined = new uint[originalCount + addedCount];
                    if (originalCount > 0)
                    {
                        Array.Copy(parent.ChildIds, combined, originalCount);
                    }
                    Array.Copy(newChildIds!, 0, combined, originalCount, addedCount);
                    parent.ChildIds = combined;
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void validateChildRelationship(PicoEntity child, uint newParentId)
    {
        if (child.ParentId != 0 && child.ParentId != newParentId)
        {
            throw new InvalidOperationException($"Child entity {child.Id} already belongs to parent {child.ParentId}.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ensurePicoEntityIndexed(PicoEntity entity)
    {
        var id = entity.Id;
        // TryAdd returns true if the key was not found and was added.
        // This avoids the O(N) list.Contains check, since an entity not in _idIndex 
        // won't be in _typeLists either.
        if (_idIndex.TryAdd(id, entity))
        {
            var type = entity.GetType();
            if (!_typeLists.TryGetValue(type, out var list))
            {
                list = [];
                _typeLists[type] = list;
            }
            entity.TypeListIndex = list.Count;
            list.Add(entity);
        }
    }

    /// <summary>
    /// Retrieves an entity by its unique ID.
    /// </summary>
    public T? Get<T>(uint id) where T : PicoEntity
    {
        _lock.EnterReadLock();
        try
        {
            return _idIndex.TryGetValue(id, out var entity) ? entity as T : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Retrieves the first entity of a specific type.
    /// </summary>
    public T? First<T>() where T : PicoEntity
    {
        _lock.EnterReadLock();
        try
        {
            return _typeLists.TryGetValue(typeof(T), out var list) && list.Count > 0 
                ? list[0] as T 
                : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes entities and all their descendants from the store.
    /// </summary>
    public void Remove(params PicoEntity[] entities)
    {
        if (entities == null || entities.Length == 0) return;

        _lock.EnterWriteLock();
        try
        {
            var toRemove = new HashSet<uint>();
            var queue = new Queue<PicoEntity>(entities.Length);

            foreach (var e in entities)
            {
                if (e is not null) queue.Enqueue(e);
            }

            while (queue.TryDequeue(out var entity))
            {
                if (!toRemove.Add(entity.Id)) continue;

                foreach (var childId in entity.ChildIds)
                {
                    if (_idIndex.TryGetValue(childId, out var child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            foreach (var id in toRemove)
            {
                if (_idIndex.Remove(id, out var entity))
                {
                    var type = entity.GetType();
                    if (_typeLists.TryGetValue(type, out var list))
                    {
                        int index = entity.TypeListIndex;
                        int lastIndex = list.Count - 1;
                        if (index < lastIndex)
                        {
                            var lastPicoEntity = list[lastIndex];
                            list[index] = lastPicoEntity;
                            lastPicoEntity.TypeListIndex = index;
                        }
                        list.RemoveAt(lastIndex);
                        entity.TypeListIndex = -1;

                        if (list.Count == 0) _typeLists.Remove(type);
                    }
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears all entities from the store.
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _typeLists.Clear();
            _idIndex.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the parent of an entity.
    /// </summary>
    public PicoEntity? Parent(PicoEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        _lock.EnterReadLock();
        try
        {
            var parentId = entity.ParentId;
            return parentId != 0 && _idIndex.TryGetValue(parentId, out var parent) 
                ? parent 
                : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets direct children of an entity.
    /// </summary>
    public List<PicoEntity> Children(PicoEntity parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        _lock.EnterReadLock();
        try
        {
            var childIds = parent.ChildIds;
            var result = new List<PicoEntity>(childIds.Length);
            foreach (var childId in childIds)
            {
                if (_idIndex.TryGetValue(childId, out var child))
                {
                    result.Add(child);
                }
            }
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all descendants of an entity recursively.
    /// </summary>
    public List<PicoEntity> Descendants(PicoEntity parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        _lock.EnterReadLock();
        try
        {
            var result = new List<PicoEntity>();
            var childIds = parent.ChildIds;
            
            // Pre-allocate stack to avoid resizing
            var stack = new Stack<uint>(childIds.Length > 0 ? childIds.Length : 4);
            
            // Push in reverse order to maintain expected traversal order
            for (int i = childIds.Length - 1; i >= 0; i--)
            {
                stack.Push(childIds[i]);
            }

            while (stack.TryPop(out var id))
            {
                if (_idIndex.TryGetValue(id, out var current))
                {
                    result.Add(current);
                    var currentChildIds = current.ChildIds;
                    for (int i = currentChildIds.Length - 1; i >= 0; i--)
                    {
                        stack.Push(currentChildIds[i]);
                    }
                }
            }
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
