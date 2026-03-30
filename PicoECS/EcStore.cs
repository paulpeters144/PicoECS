using System.Security.Cryptography;

namespace PicoECS;

/// <summary>
/// A fast, thread-safe store for entities and their relationships.
/// </summary>
public sealed class EcStore
{
    private readonly Dictionary<Type, List<Entity>> _typeLists = [];
    private readonly Dictionary<uint, Entity> _idIndex = [];
    private readonly ReaderWriterLockSlim _lock = new();

    public const uint NoneId = 0;

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
            var currentChildren = new HashSet<uint>(internalParent.ChildIds);

            // Assign ID to parent if it doesn't have one
            if (internalParent.Id == NoneId)
            {
                internalParent.Id = GenerateUniqueId();
            }

            foreach (var child in children)
            {
                if (child is null) continue;
                var internalChild = (IInternalEntity)child;

                // Assign ID to child if it doesn't have one
                if (internalChild.Id == NoneId)
                {
                    internalChild.Id = GenerateUniqueId();
                }

                // Validation: Ensure child doesn't already have a different parent
                ValidateChildRelationship(internalChild, internalParent.Id);

                currentChildren.Add(internalChild.Id);
                internalChild.ParentId = internalParent.Id;
                
                EnsureEntityIndexed(child);
            }

            internalParent.ChildIds = [.. currentChildren];
            EnsureEntityIndexed(parent);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void ValidateChildRelationship(IInternalEntity child, uint newParentId)
    {
        if (child.ParentId != NoneId && child.ParentId != newParentId)
        {
            throw new InvalidOperationException($"Child entity {child.Id} already belongs to parent {child.ParentId}.");
        }

        if (_idIndex.TryGetValue(child.Id, out var existingChild))
        {
            var existingParentId = ((IInternalEntity)existingChild).ParentId;
            if (existingParentId != NoneId && existingParentId != newParentId)
            {
                throw new InvalidOperationException($"Existing child entity {child.Id} already belongs to parent {existingParentId}.");
            }
        }
    }

    private void EnsureEntityIndexed(Entity entity)
    {
        var id = entity.Id;
        _idIndex[id] = entity;

        var type = entity.GetType();
        if (!_typeLists.TryGetValue(type, out var list))
        {
            list = [];
            _typeLists[type] = list;
        }

        if (!list.Contains(entity))
        {
            list.Add(entity);
        }
    }

    /// <summary>
    /// Generates a new unique entity ID.
    /// </summary>
    public uint NewId()
    {
        _lock.EnterReadLock();
        try
        {
            return GenerateUniqueId();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private uint GenerateUniqueId()
    {
        var buffer = new byte[4];
        uint id;
        do
        {
            RandomNumberGenerator.Fill(buffer);
            id = BitConverter.ToUInt32(buffer, 0);
        } while (id == NoneId || _idIndex.ContainsKey(id));
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
                return [.. _typeLists.Values.SelectMany(l => l)];
            }

            var result = new List<Entity>();
            var processedTypes = new HashSet<Type>();

            foreach (var target in filterTargets)
            {
                if (target is null) continue;
                var type = target.GetType();
                if (processedTypes.Add(type) && _typeLists.TryGetValue(type, out var list))
                {
                    result.AddRange(list);
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
            return _typeLists.TryGetValue(typeof(T), out var list) 
                ? [.. list.Cast<T>()] 
                : [];
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
                if (entity.Id == NoneId || !toRemove.Add(entity.Id)) continue;

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
            return parentId != NoneId && _idIndex.TryGetValue(parentId, out var parent) 
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
            var result = new List<T>();
            foreach (var childId in ((IInternalEntity)parent).ChildIds)
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
            var stack = new Stack<uint>(((IInternalEntity)parent).ChildIds.Reverse());

            while (stack.TryPop(out var id))
            {
                if (_idIndex.TryGetValue(id, out var current))
                {
                    result.Add(current);
                    foreach (var childId in ((IInternalEntity)current).ChildIds.Reverse())
                    {
                        stack.Push(childId);
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
