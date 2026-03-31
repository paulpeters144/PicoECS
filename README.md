# PicoECS

PicoECS is a fast, thread-safe, and simple way to store and find entities in your .NET applications. It is designed to handle hierarchical data—where objects are naturally nested, like items in an inventory, UI elements in a tree, or objects in a game scene.

## Why PicoECS?

- **Hierarchy-First:** Easily link entities as parents and children. Removing a parent automatically cleans up all its descendants.
- **Thread-Safe:** Built-in support for multiple threads reading and writing at the same time using `ReaderWriterLockSlim`.
- **Fast:** Find entities by their unique ID in O(1) time or query all entities of a specific type instantly.
- **Simple API:** No complex setup. Just inherit from `Entity` and start using the `PicoStore`.

## Getting Started

### 1. Define Your Entities

Entities are just classes that inherit from `Entity`.

```csharp
public class Player : Entity { public string Name { get; set; } = "Hero"; }
public class Position : Entity { public float X, Y; }
public class Inventory : Entity { }
public class Sword : Entity { }
public class Shield : Entity { }
```

### 2. Basic Usage

```csharp
using PicoECS;

// Create the store
var store = new PicoStore();

// Add entities and establish a hierarchy
var player = new Player();
var pos = new Position { X = 10, Y = 20 };

// 'pos' becomes a child of 'player' and both are added to the store
store.Add(player, pos);
```

### Creating a Hierarchy

PicoECS manages nested entity relationships. For example, a Player can own an Inventory that contains various Items:

```csharp
// create the store
var store = new PicoStore();

// initialize your entities
var player = new Player();
var inventory = new Inventory();
var position = new Position();
var sword = new Sword();
var shield = new Shield();

// add player to the store with inventory and position as children
store.Add(player, inventory, position);

// add sword and shield to the store with inventory as the parent
store.Add(inventory, sword, shield);
```
#### Created Hierarchy
```mermaid
graph TD
    Player[Player]
    Position[Position]
    Inventory[Inventory]
    Sword[Sword]
    Shield[Shield]

    Player --> Position
    Player --> Inventory
    Inventory --> Sword
    Inventory --> Shield
```
#### Querying the Hierarchy
```csharp
var player = store.GetFirst<Player>();
var inventory = store.GetChildren<Inventory>(player).First();
var sword = store.GetChildren<Sword>(inventory).First();

// Removing an entity recursively removes all of the entity's descendants
store.Remove(player); 
```

### 3. Using ForEach

You can quickly run code on every entity of a certain type without creating a new list. 

> **Note:** `ForEach<T>` uses **exact type matching** for maximum performance (O(1)). It will not include entities of derived types.

```csharp
// Run an action on every Position entity
store.ForEach<Position>(p => {
    p.X += 1.0f;
    Console.WriteLine($"Moved to {p.X}, {p.Y}");
});

// Or run it on every single entity in the store
store.ForEach(e => Console.WriteLine($"Entity ID: {e.Id}"));
```

## Querying

### Get By ID
Retrieve a specific entity in O(1) time:
```csharp
var player = store.GetById<Player>(player.Id);
```

### Generic Type Lookup
Get every entity of a specific type. Like `ForEach<T>`, this uses **exact type matching**.
```csharp
var allPlayers = store.GetAll<Player>();
```

### Multiple Types
If you need entities of several different types at once, you can pass them as parameters:
```csharp
var results = store.GetAll(typeof(Player), typeof(Sword));
```

### Children (Polymorphic)
Retrieve the direct children of an entity. Unlike `GetAll<T>`, relationship queries are **polymorphic** and will include derived types.
```csharp
// Will find both Sword and Shield items if they inherit from Item
var items = store.GetChildren<Item>(inventory);
```

### Descendants
Get every entity nested under a parent, recursively:
```csharp
var allDescendants = store.GetDescendants(player);
```

## More Examples

For a full look at the API, check out the test suite:
👉 **[PicoECS.Tests/StoreApiTests.cs](./PicoECS.Tests/StoreApiTests.cs)**

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
