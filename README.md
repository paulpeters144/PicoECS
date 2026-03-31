# PicoECS

PicoECS is a lightweight, thread-safe, hierarchy-first Entity Component System (ECS) for .NET. It is designed for scenarios where entities are naturally organized into parent-child relationships, such as scene graphs, nested inventory systems, or UI trees.

## Features

- **Hierarchy-First:** Native support for parent-child relationships and recursive descendant management.
- **Thread-Safe:** Built-in concurrency support using `ReaderWriterLockSlim`.
- **Fast Lookups:** O(1) entity retrieval by ID and high-performance type-based queries.
- **Ergonomic API:** Clean generic overloads for querying multiple types simultaneously.
- **Polymorphic Queries:** Relationship-based queries (children, parents) support polymorphism.

## Getting Started

### 1. Define Your Entities

Entities are defined by inheriting from the `Entity` base class.

```csharp
public class Player : Entity { public string Name { get; set; } = "Hero"; }
public class Transform : Entity { public float X { get; set; } public float Y { get; set; } }
public class InventoryItem : Entity { public string ItemId { get; set; } = string.Empty; }
```

### 2. Basic Usage

```csharp
using PicoECS;

// Initialize the store
var store = new PicoStore();

// Create and add entities while establishing hierarchy
var player = new Player();
var position = new Transform { X = 10, Y = 20 };
store.Add(player, position); // 'position' becomes a child of 'player'

// Query entities using ergonomic generics (supports up to 5 types)
var heroes = store.GetAll<Player>();
var combo = store.GetAll<Player, Transform, InventoryItem>();

// Retrieve specific entities
var firstPlayer = store.GetFirst<Player>();
var specificPlayer = store.Get<Player>(player.Id);

// Lifecycle management
// Removing a parent automatically removes all descendants (recursively)
store.Remove(player); 
```

## Advanced Querying

### Multiple Type Overloads
PicoECS provides high-performance generic overloads for querying multiple types at once:
```csharp
var results = store.GetAll<Type1, Type2, Type3, Type4, Type5>();
```

### Hierarchy & Relationships
```csharp
var children = store.GetChildren<Transform>(player);
var parent = store.GetParent<Player>(position);
var allDescendants = store.GetDescendants(player);
```

## More Examples

For a comprehensive look at the API, including concurrent usage and polymorphic vs. exact type matching, please refer to the functional test suite:

👉 **[PicoECS.Tests/StoreApiTests.cs](./PicoECS.Tests/StoreApiTests.cs)**

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
