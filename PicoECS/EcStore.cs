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

    public int Count => getCount();

    /// <summary>
    /// Retrieves all entities in the store.
    /// </summary>
    public List<Entity> GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            var result = new List<Entity>(_idIndex.Count);
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
    /// Gets all entities filtered by type.
    /// </summary>
    public List<Entity> GetAll(params Type[] types)
    {
        if (types == null || types.Length == 0) return GetAll();

        _lock.EnterReadLock();
        try
        {
            var capacity = 0;
            var processedTypes = new HashSet<Type>();

            foreach (var type in types)
            {
                if (type is null) continue;
                if (processedTypes.Add(type) && _typeLists.TryGetValue(type, out var list))
                {
                    capacity += list.Count;
                }
            }

            var filteredResult = new List<Entity>(capacity);
            processedTypes.Clear();

            foreach (var type in types)
            {
                if (type is null) continue;
                if (processedTypes.Add(type) && _typeLists.TryGetValue(type, out var list))
                {
                    filteredResult.AddRange(list);
                }
            }
            return filteredResult;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets all entities filtered by the types of the provided target instances.
    /// </summary>
    public List<Entity> GetAll(params Entity[] filterTargets)
    {
        if (filterTargets == null || filterTargets.Length == 0) return GetAll();
        var types = new Type[filterTargets.Length];
        for (int i = 0; i < filterTargets.Length; i++)
        {
            if (filterTargets[i] != null) types[i] = filterTargets[i].GetType();
        }
        return GetAll(types);
    }

    /// <summary>
    /// Gets all entities of the specified type.
    /// </summary>
    public List<Entity> GetAll<T1>() where T1 : Entity => GetAll(typeof(T1));

    /// <summary>
    /// Gets all entities of the specified types.
    /// </summary>
    public List<Entity> GetAll<T1, T2>() where T1 : Entity where T2 : Entity 
        => GetAll(typeof(T1), typeof(T2));

    /// <summary>
    /// Gets all entities of the specified types.
    /// </summary>
    public List<Entity> GetAll<T1, T2, T3>() where T1 : Entity where T2 : Entity where T3 : Entity 
        => GetAll(typeof(T1), typeof(T2), typeof(T3));

    /// <summary>
    /// Gets all entities of the specified types.
    /// </summary>
    public List<Entity> GetAll<T1, T2, T3, T4>() 
        where T1 : Entity where T2 : Entity where T3 : Entity where T4 : Entity
        => GetAll(typeof(T1), typeof(T2), typeof(T3), typeof(T4));

    /// <summary>
    /// Gets all entities of the specified types.
    /// </summary>
    public List<Entity> GetAll<T1, T2, T3, T4, T5>() 
        where T1 : Entity where T2 : Entity where T3 : Entity where T4 : Entity where T5 : Entity
        => GetAll(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));

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
                bool childrenChanged = false;
                int originalCount = parent.ChildIds.Length;
                List<uint>? newChildren = null;

                foreach (var child in children)
                {
                    if (child is null) continue;

                    if (child.Id == 0) child.Id = generateUniqueId();

                    // Validation: Ensure child doesn't already have a different parent
                    validateChildRelationship(child, parent.Id);

                    if (Array.IndexOf(parent.ChildIds, child.Id) == -1 && 
                        (newChildren == null || !newChildren.Contains(child.Id)))
                    {
                        newChildren ??= new List<uint>(children.Length);
                        newChildren.Add(child.Id);
                        childrenChanged = true;
                    }
                    
                    child.ParentId = parent.Id;
                    ensureEntityIndexed(child);
                }

                if (childrenChanged && newChildren != null)
                {
                    var combined = new uint[originalCount + newChildren.Count];
                    if (originalCount > 0)
                    {
                        Array.Copy(parent.ChildIds, combined, originalCount);
                    }
                    newChildren.CopyTo(combined, originalCount);
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

    private uint generateUniqueId()
    {
        uint id;
        do
        {
            id = (uint)Random.Shared.Next(1, int.MaxValue);
        } while (_idIndex.ContainsKey(id));
        return id;
    }

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
            list.Add(entity);
        }
    }

    /// <summary>
    /// Retrieves an entity by its unique ID.
    /// </summary>
    public T? Get<T>(uint id) where T : Entity
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
    /// Retrieves all entities of a specific type.
    /// </summary>
    public IEnumerable<T> GetByType<T>() where T : Entity
    {
        _lock.EnterReadLock();
        try
        {
            if (_typeLists.TryGetValue(typeof(T), out var list))
            {
                var result = new T[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    result[i] = (T)list[i];
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
                        list.Remove(entity);
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
