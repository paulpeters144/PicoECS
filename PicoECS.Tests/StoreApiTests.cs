using Xunit;

namespace PicoECS.Tests;

public class StoreApiTests
{
    // Example entity classes to be used in API tests
    public class Player : Entity
    {
        public string Name { get; set; } = string.Empty;
        public int Health { get; set; } = 100;
    }

    public class Transform : Entity
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public class InventoryItem : Entity
    {
        public string ItemId { get; set; } = string.Empty;
    }

    public class Weapon : InventoryItem
    {
        public int Damage { get; set; }
    }
    

    [Fact]
    public void Store_Can_Initialize_And_Add_Entities()
    {
        var store = new EcStore();
        var player = new Player { Name = "Hero" };
        store.Add(player);

        Assert.NotEqual(0u, player.Id);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Store_Can_Establish_And_Retrieve_Parent_Child_Hierarchies()
    {
        var store = new EcStore();
        var player = new Player { Name = "Hero" };
        var transform = new Transform { X = 10, Y = 20 };
        var sword = new InventoryItem { ItemId = "sword_01" };

        store.Add(player, transform, sword);

        var children = store.GetChildren<Entity>(player);
        var swordParent = store.GetParent<Player>(sword);

        Assert.Equal(2, children.Count);
        Assert.Contains(transform, children);
        Assert.Same(player, swordParent);
    }

    [Fact]
    public void Store_Can_Query_Entities_By_Id_Type_And_First_Match()
    {
        var store = new EcStore();
        var player1 = new Player { Name = "Player 1" };
        var player2 = new Player { Name = "Player 2" };
        store.Add(player1);
        store.Add(player2);
        
        var transform = new Transform { X = 5, Y = 5 };
        store.Add(player1, transform);

        var allPlayers = store.GetAll<Player>();
        Assert.Equal(2, allPlayers.Count);

        var firstTransform = store.GetFirst<Transform>();
        Assert.NotNull(firstTransform);

        var specificPlayer = store.Get<Player>(player2.Id);
        Assert.Same(player2, specificPlayer);

        var descendants = store.GetDescendants(player1);
        Assert.Single(descendants); 
    }

    [Fact]
    public void Store_Can_Remove_Entities_Recursively_By_Hierarchy()
    {
        var store = new EcStore();
        var player = new Player();
        var transform = new Transform();
        var item = new InventoryItem();

        store.Add(player, transform);
        store.Add(transform, item);

        Assert.Equal(3, store.Count);

        store.Remove(player);

        Assert.Equal(0, store.Count);
        Assert.Null(store.Get<Player>(player.Id));
        Assert.Null(store.Get<Transform>(transform.Id));
        Assert.Null(store.Get<InventoryItem>(item.Id));
    }

    [Fact]
    public void Store_Can_Query_Direct_Parent_And_Child_Relationships_Polymorphically()
    {
        var store = new EcStore();
        var player = new Player { Name = "Hero" };
        var sword = new Weapon { ItemId = "sword_01", Damage = 50 };
        var shield = new InventoryItem { ItemId = "shield_01" };

        store.Add(player, sword, shield);

        Assert.Equal(2, store.GetChildCount(player));

        var allItems = store.GetChildren<InventoryItem>(player);
        Assert.Equal(2, allItems.Count);

        var parentOfSword = store.GetParent<Player>(sword);
        Assert.Same(player, parentOfSword);
    }

    [Fact]
    public void Store_Can_Retrieve_All_Entities_Unfiltered()
    {
        var store = new EcStore();
        store.Add(new Player());
        store.Add(new Transform());
        store.Add(new InventoryItem());

        Assert.Equal(3, store.Count);

        var allEntities = store.GetAll();
        Assert.Equal(3, allEntities.Count);
    }

    [Fact]
    public void Store_Is_ThreadSafe_During_Concurrent_Adds_And_Queries()
    {
        var store = new EcStore();
        
        Parallel.For(0, 100, i => 
        {
            var player = new Player { Name = $"Player {i}" };
            store.Add(player);
            
            var players = store.GetAll<Player>();
            Assert.NotEmpty(players);
        });

        Assert.Equal(100, store.Count);
    }

    [Fact]
    public void Store_Can_Filter_All_Entities_By_Multiple_Type_Instances()
    {
        var store = new EcStore();
        store.Add(new Player { Name = "Hero" });
        store.Add(new Transform { X = 0, Y = 0 });
        store.Add(new Weapon { Damage = 10 });
        store.Add(new InventoryItem { ItemId = "Potion" });

        var typesToFetch = new Entity[] { new Player(), new Transform(), new Weapon() };
        var filteredEntities = store.GetAll(typesToFetch);
        
        Assert.Equal(3, filteredEntities.Count);
        Assert.Contains(filteredEntities, e => e is Player);
        Assert.Contains(filteredEntities, e => e is Transform);
        Assert.Contains(filteredEntities, e => e is Weapon);
        Assert.DoesNotContain(filteredEntities, e => e.GetType() == typeof(InventoryItem));
    }

    [Fact]
    public void Store_Can_Clear_All_Indexed_Entities()
    {
        var store = new EcStore();
        store.Add(new Player());
        store.Add(new Transform());

        Assert.Equal(2, store.Count);

        store.Clear();

        Assert.Equal(0, store.Count);
        var allEntities = store.GetAll();
        Assert.Empty(allEntities);
    }

    [Fact]
    public void Store_Can_Retrieve_All_Descendants_Recursively()
    {
        var store = new EcStore();
        var grandparent = new Player { Name = "Grandparent" };
        var parent = new Transform { X = 1, Y = 1 };
        var child = new InventoryItem { ItemId = "Heirloom" };

        store.Add(grandparent, parent);
        store.Add(parent, child);

        var directChildren = store.GetChildren<Entity>(grandparent);
        Assert.Single(directChildren);
        Assert.Contains(parent, directChildren);

        var allDescendants = store.GetDescendants(grandparent);
        Assert.Equal(2, allDescendants.Count);
        Assert.Contains(parent, allDescendants);
        Assert.Contains(child, allDescendants);
    }

    [Fact]
    public void Store_Differentiates_Between_Exact_Type_Queries_And_Polymorphic_Relationships()
    {
        var store = new EcStore();
        var weapon = new Weapon { Damage = 50 };
        var basicItem = new InventoryItem { ItemId = "Apple" };

        store.Add(weapon);
        store.Add(basicItem);

        // 1. GetAll<T> uses EXACT type matching for maximum performance (O(1) dictionary lookup).
        // It will NOT return derived types.
        var exactItems = store.GetAll<InventoryItem>();
        Assert.Single(exactItems); // Only the "Apple", not the Weapon
        
        var weapons = store.GetAll<Weapon>();
        Assert.Single(weapons);

        // 2. In contrast, relationship queries like GetChildren are polymorphic.
        var player = new Player();
        store.Add(player, weapon, basicItem);

        // Querying children by the base class (InventoryItem) returns BOTH the basic item and the derived Weapon.
        var childItems = store.GetChildren<InventoryItem>(player);
        Assert.Equal(2, childItems.Count);
    }

    [Fact]
    public void Store_Throws_When_Adding_Child_To_A_Second_Parent()
    {
        var store = new EcStore();
        var parent1 = new Player { Name = "Parent 1" };
        var parent2 = new Player { Name = "Parent 2" };
        var child = new Transform { X = 0, Y = 0 };

        store.Add(parent1, child);

        // An entity can only have one parent.
        // Attempting to add an existing child to a NEW parent throws an InvalidOperationException.
        Assert.Throws<InvalidOperationException>(() => store.Add(parent2, child));
    }

    [Fact]
    public void Store_Supports_Generic_GetAll_Queries_Up_To_Five_Types()
    {
        var store = new EcStore();
        store.Add(new Player { Name = "Hero" });
        store.Add(new Transform { X = 0, Y = 0 });
        store.Add(new Weapon { Damage = 10 });
        store.Add(new InventoryItem { ItemId = "Potion" });

        // Generic overloads for zero-allocation-boilerplate querying
        var playersAndTransforms = store.GetAll<Player, Transform>();
        Assert.Equal(2, playersAndTransforms.Count);

        // 4-type query
        var fourTypes = store.GetAll<Player, Transform, Weapon, InventoryItem>();
        Assert.Equal(4, fourTypes.Count);

        // 5-type query (reusing a type since we only have 4 defined here, but it verifies the overload works)
        var fiveTypes = store.GetAll<Player, Transform, Weapon, InventoryItem, Player>();
        Assert.Equal(4, fiveTypes.Count); // HashSet internally handles the duplicate type

        // Type-based queries
        var weaponsAndPlayers = store.GetAll(typeof(Weapon), typeof(Player));
        Assert.Equal(2, weaponsAndPlayers.Count);
    }

    [Fact]
    public void Store_Can_Batch_Remove_Multiple_Root_Entities()
    {
        var store = new EcStore();
        var player1 = new Player();
        var player2 = new Player();
        var player3 = new Player();
        
        store.Add(player1);
        store.Add(player2);
        store.Add(player3);
        
        Assert.Equal(3, store.Count);

        // Remove accepts a params array, allowing you to efficiently batch-remove entities
        store.Remove(player1, player2);

        // Only player3 remains
        Assert.Equal(1, store.Count);
        Assert.NotNull(store.Get<Player>(player3.Id));
    }

    [Fact]
    public void Store_Example_Query_Children_And_Identify_Types()
    {
        var store = new EcStore();
        
        // 1. Setup: Create a player and give them a Transform and a Weapon as children
        var player = new Player { Name = "Hero" };
        var position = new Transform { X = 100, Y = 200 };
        var sword = new Weapon { ItemId = "excalibur", Damage = 99 };
        
        store.Add(player);
        store.Add(player, position); // Position is a child of Player
        store.Add(player, sword);    // Sword is a child of Player

        // 2. Query: Get the player back from the store (simulating a search)
        var hero = store.GetFirst<Player>();
        Assert.NotNull(hero);

        // 3. Query Children: Get all direct children as the base 'Entity' type
        var children = store.GetChildren<Entity>(hero);
        Assert.Equal(2, children.Count);

        // 4. Identify Types: Use C# pattern matching (is/as) to figure out what each child is
        int weaponCount = 0;
        int transformCount = 0;

        foreach (var child in children)
        {
            if (child is Weapon weapon)
            {
                // We found a weapon! We can now access Weapon-specific properties.
                Assert.Equal("excalibur", weapon.ItemId);
                weaponCount++;
            }
            else if (child is Transform transform)
            {
                // We found the position! We can now access Transform-specific properties.
                Assert.Equal(100, transform.X);
                transformCount++;
            }
        }

        Assert.Equal(1, weaponCount);
        Assert.Equal(1, transformCount);
    }
}
