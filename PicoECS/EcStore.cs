using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PicoECS.Tests")]

namespace PicoECS;

/// <summary>
/// A fast, thread-safe store for entities and their relationships.
/// </summary>
public sealed class EcStore
{
    private readonly Dictionary<Type, List<Entity>> _typeLists = [];
    private readonly Dictionary<uint, Entity> _idIndex = [];
    private readonly ReaderWriterLockSlim _lock = new();
    private uint _nextId = 0;

    public int Count => getCount();

    /// <summary>
    /// Retrieves all entities in the store.
    /// </summary>
    public List<Entity> GetAll()
    {
        var result = new List<Entity>(Count);
        GetAll(result);
        return result;
    }

    /// <summary>
    /// Retrieves all entities of the specified type.
    /// </summary>
    public List<T> GetAll<T>() where T : Entity
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
    /// Retrieves all entities matching one or more of the specified types.
    /// </summary>
    public List<Entity> GetAll(params Type[] types)
    {
        var result = new List<Entity>();
        if (types == null || types.Length == 0) return result;
        
        var uniqueTypes = types.Where(t => t != null).Distinct().ToHashSet();
        
        _lock.EnterReadLock();
        try
        {
            foreach (var type in uniqueTypes)
            {
                if (_typeLists.TryGetValue(type, out var list))
                {
                    result.AddRange(list);
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
        return result;
    }

    internal void ForEachInternal(Type[] types, Action<Entity> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (types == null || types.Length == 0) return;

        _lock.EnterReadLock();
        try
        {
            // Use HashSet for distinct types if multiple provided, otherwise direct lookup
            if (types.Length == 1)
            {
                if (types[0] != null && _typeLists.TryGetValue(types[0], out var list))
                {
                    foreach (var entity in list) action(entity);
                }
            }
            else
            {
                var processed = new HashSet<Type>();
                foreach (var type in types)
                {
                    if (type != null && processed.Add(type) && _typeLists.TryGetValue(type, out var list))
                    {
                        foreach (var entity in list) action(entity);
                    }
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Fills the provided collection with all entities in the store.
    /// </summary>
    public void GetAll(ICollection<Entity> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _lock.EnterReadLock();
        try
        {
            if (result is List<Entity> listResult)
            {
                foreach (var list in _typeLists.Values)
                {
                    listResult.AddRange(list);
                }
            }
            else
            {
                foreach (var list in _typeLists.Values)
                {
                    foreach (var entity in list)
                    {
                        result.Add(entity);
                    }
                }
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Executes the provided action on every entity in the store.
    /// </summary>
    public void ForEach(Action<Entity> action)
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
    public void ForEach<T>(Action<T> action) where T : Entity
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
    public void Add(Entity parent, params Entity[] children)
    {
        ArgumentNullException.ThrowIfNull(parent);

        _lock.EnterWriteLock();
        try
        {
            ensureEntityIndexed(parent);

            if (children != null && children.Length > 0)
            {
                uint[]? newChildIds = null;
                int addedCount = 0;

                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (child is null) continue;

                    if (child.Id == 0) child.Id = generateUniqueId();
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
                    ensureEntityIndexed(child);
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

    private void validateChildRelationship(Entity child, uint newParentId)
    {
        if (child.ParentId != 0 && child.ParentId != newParentId)
        {
            throw new InvalidOperationException($"Child entity {child.Id} already belongs to parent {child.ParentId}.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint generateUniqueId()
    {
        return Interlocked.Increment(ref _nextId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ensureEntityIndexed(Entity entity)
    {
        if (entity.Id == 0)
        {
            entity.Id = generateUniqueId();
        }

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
    public T? GetById<T>(uint id) where T : Entity
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
    public T? GetFirst<T>() where T : Entity
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
    /// Gets the number of children for a given parent entity.
    /// </summary>
    public int GetChildCount(Entity parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        _lock.EnterReadLock();
        try
        {
            return parent.ChildIds.Length;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Removes entities and all their descendants from the store.
    /// </summary>
    public void Remove(params Entity[] entities)
    {
        if (entities == null || entities.Length == 0) return;

        _lock.EnterWriteLock();
        try
        {
            var toRemove = new HashSet<uint>();
            var queue = new Queue<Entity>(entities.Length);

            foreach (var e in entities)
            {
                if (e is not null) queue.Enqueue(e);
            }

            while (queue.TryDequeue(out var entity))
            {
                if (entity.Id == 0 || !toRemove.Add(entity.Id)) continue;

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
                            var lastEntity = list[lastIndex];
                            list[index] = lastEntity;
                            lastEntity.TypeListIndex = index;
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

    private int getCount()
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

    /// <summary>
    /// Gets the parent of an entity.
    /// </summary>
    public T? GetParent<T>(Entity entity) where T : Entity
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        _lock.EnterReadLock();
        try
        {
            var parentId = entity.ParentId;
            return parentId != 0 && _idIndex.TryGetValue(parentId, out var parent) 
                ? parent as T 
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
    public List<Entity> GetChildren(Entity parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        _lock.EnterReadLock();
        try
        {
            var childIds = parent.ChildIds;
            var result = new List<Entity>(childIds.Length);
            foreach (var childId in childIds)
            {
                if (_idIndex.TryGetValue(childId, out var child) && child is Entity typedChild)
                {
                    result.Add(typedChild);
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
    /// Gets direct children of an entity.
    /// </summary>
    public List<T> GetChildren<T>(Entity parent) where T : Entity
    {
        ArgumentNullException.ThrowIfNull(parent);

        _lock.EnterReadLock();
        try
        {
            var childIds = parent.ChildIds;
            var result = new List<T>(childIds.Length);
            foreach (var childId in childIds)
            {
                if (_idIndex.TryGetValue(childId, out var child) && child is T typedChild)
                {
                    result.Add(typedChild);
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
    public List<Entity> GetDescendants(Entity parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        _lock.EnterReadLock();
        try
        {
            var result = new List<Entity>();
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
