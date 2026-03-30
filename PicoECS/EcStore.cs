using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System;

namespace PicoECS;

/// <summary>
/// A fast, thread-safe store for entities and their relationships.
/// </summary>
public sealed class EcStore
{
    private readonly Dictionary<Type, List<Entity>> _typeLists = [];
    private readonly Dictionary<uint, Entity> _idIndex = [];
    private readonly ReaderWriterLockSlim _lock = new();
    
    // Fast atomic counter for ID generation
    private uint _nextId = 0;

    public int Count => GetCount();

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
            var internalParent = (IInternalEntity)parent;
            
            // Assign ID to parent if it doesn't have one
            if (internalParent.Id == 0)
            {
                internalParent.Id = GenerateUniqueId();
            }

            bool childrenChanged = false;
            HashSet<uint>? currentChildren = null;

            if (children.Length > 0)
            {
                currentChildren = [.. internalParent.ChildIds];

                foreach (var child in children)
                {
                    if (child is null) continue;
                    var internalChild = (IInternalEntity)child;

                    // Assign ID to child if it doesn't have one
                    if (internalChild.Id == 0)
                    {
                        internalChild.Id = GenerateUniqueId();
                    }

                    // Validation: Ensure child doesn't already have a different parent
                    ValidateChildRelationship(internalChild, internalParent.Id);

                    if (currentChildren.Add(internalChild.Id))
                    {
                        childrenChanged = true;
                    }
                    
                    internalChild.ParentId = internalParent.Id;
                    EnsureEntityIndexed(child);
                }
            }

            if (childrenChanged && currentChildren != null)
            {
                internalParent.ChildIds = currentChildren.ToArray();
            }

            EnsureEntityIndexed(parent);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void ValidateChildRelationship(IInternalEntity child, uint newParentId)
    {
        if (child.ParentId != 0 && child.ParentId != newParentId)
        {
            throw new InvalidOperationException($"Child entity {child.Id} already belongs to parent {child.ParentId}.");
        }

        if (_idIndex.TryGetValue(child.Id, out var existingChild))
        {
            var existingParentId = ((IInternalEntity)existingChild).ParentId;
            if (existingParentId != 0 && existingParentId != newParentId)
            {
                throw new InvalidOperationException($"Existing child entity {child.Id} already belongs to parent {existingParentId}.");
            }
        }
    }

    private void EnsureEntityIndexed(Entity entity)
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
            list.Add(entity);
        }
    }

    private uint GenerateUniqueId()
    {
        uint id;
        do
        {
            id = Interlocked.Increment(ref _nextId);
        } while (id == 0 || _idIndex.ContainsKey(id));
        return id;
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
    /// Gets all entities, optionally filtered by the types of the provided targets.
    /// </summary>
    public List<Entity> GetAll(params Entity[] filterTargets)
    {
        _lock.EnterReadLock();
        try
        {
            if (filterTargets.Length == 0)
            {
                var result = new List<Entity>(_idIndex.Count);
                foreach (var list in _typeLists.Values)
                {
                    result.AddRange(list);
                }
                return result;
            }

            var capacity = 0;
            var processedTypes = new HashSet<Type>();

            // Calculate exact capacity to avoid re-allocations
            foreach (var target in filterTargets)
            {
                if (target is null) continue;
                var type = target.GetType();
                if (processedTypes.Add(type) && _typeLists.TryGetValue(type, out var list))
                {
                    capacity += list.Count;
                }
            }

            var filteredResult = new List<Entity>(capacity);
            processedTypes.Clear();

            foreach (var target in filterTargets)
            {
                if (target is null) continue;
                var type = target.GetType();
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
    /// Gets the number of children for a given parent entity.
    /// </summary>
    public int GetChildCount(Entity parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        _lock.EnterReadLock();
        try
        {
            return ((IInternalEntity)parent).ChildIds.Length;
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
        if (entities.Length == 0) return;

        _lock.EnterWriteLock();
        try
        {
            var toRemove = new HashSet<uint>();
            var queue = new Queue<Entity>(entities.Where(e => e is not null));

            while (queue.TryDequeue(out var entity))
            {
                if (entity.Id == 0 || !toRemove.Add(entity.Id)) continue;

                foreach (var childId in ((IInternalEntity)entity).ChildIds)
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

    private int GetCount()
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
            var parentId = ((IInternalEntity)entity).ParentId;
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
            var childIds = ((IInternalEntity)parent).ChildIds;
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
            var childIds = ((IInternalEntity)parent).ChildIds;
            
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
                    var currentChildIds = ((IInternalEntity)current).ChildIds;
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
