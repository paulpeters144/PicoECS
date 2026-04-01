namespace PicoEntityStoreCore.Tests;

using PicoEntityStoreCore;

public class StoreApiTests
{
    // Example entity classes to be used in API tests
    public class Player : PicoEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Health { get; set; } = 100;
    }

    public class Transform : PicoEntity
    {
        public float X { get; set; }
        public float Y { get; set; }
    }

    public class InventoryItem : PicoEntity
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
        var store = new PicoEntityStore();
        var player = new Player { Name = "Hero" };
        store.Add(player);

        Assert.NotEqual(0u, player.Id);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Store_Can_Establish_And_Retrieve_Parent_Child_Hierarchies()
    {
        var store = new PicoEntityStore();
        var player = new Player { Name = "Hero" };
        var transform = new Transform { X = 10, Y = 20 };
        var sword = new InventoryItem { ItemId = "sword_01" };

        store.Add(player, transform, sword);

        var children = store.Children(player);
        var swordParent = store.Parent(sword);

        Assert.Equal(2, children.Count);
        Assert.Contains(transform, children);
        Assert.Same(player, swordParent);
    }

    [Fact]
    public void Store_Can_Query_Entities_By_Id_Type_And_First_Match()
    {
        var store = new PicoEntityStore();
        var player1 = new Player { Name = "Player 1" };
        var player2 = new Player { Name = "Player 2" };
        store.Add(player1);
        store.Add(player2);
        
        var transform = new Transform { X = 5, Y = 5 };
        store.Add(player1, transform);

        int playerCount = store.All(typeof(Player)).Count;
        Assert.Equal(2, playerCount);

        var firstTransform = store.First<Transform>();
        Assert.NotNull(firstTransform);

        var specificPlayer = store.Get<Player>(player2.Id);
        Assert.Same(player2, specificPlayer);

        var descendants = store.Descendants(player1);
        Assert.Single(descendants); 
    }

    [Fact]
    public void Store_Can_Remove_Entities_Recursively_By_Hierarchy()
    {
        var store = new PicoEntityStore();
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
        var store = new PicoEntityStore();
        var player = new Player { Name = "Hero" };
        var sword = new Weapon { ItemId = "sword_01", Damage = 50 };
        var shield = new InventoryItem { ItemId = "shield_01" };

        store.Add(player, sword, shield);

        Assert.Equal(2, store.Children(player).Count);

        var allItems = store.Children(player);
        Assert.Equal(2, allItems.Count);

        var parentOfSword = store.Parent(sword);
        Assert.Same(player, parentOfSword);
    }

    [Fact]
    public void Store_Can_Retrieve_All_Entities_Unfiltered()
    {
        var store = new PicoEntityStore();
        store.Add(new Player());
        store.Add(new Transform());
        store.Add(new InventoryItem());

        Assert.Equal(3, store.Count);

        var allEntities = store.All();
        Assert.Equal(3, allEntities.Count);
    }

    [Fact]
    public void Store_Can_Retrieve_Entities_By_Type_Runtime()
    {
        var store = new PicoEntityStore();
        store.Add(new Player { Name = "Hero" });
        store.Add(new Transform { X = 10 });

        // Example of querying by type at runtime after GetAll(Type[]) removal
        var players = store.All().Where(e => e.GetType() == typeof(Player)).ToList();
        Assert.Single(players);
        Assert.IsType<Player>(players[0]);
    }

    [Fact]
    public void Store_Can_Retrieve_Entities_By_Type_Generic()
    {
        var store = new PicoEntityStore();
        store.Add(new Player { Name = "Hero" });
        store.Add(new Transform { X = 10 });
        store.Add(new Player { Name = "Villain" });

        var players = store.All(typeof(Player));
        Assert.Equal(2, players.Count);
        Assert.All(players, p => Assert.IsType<Player>(p));
    }

    [Fact]
    public void Store_All_Type_Array_Optimized()
    {
        var store = new PicoEntityStore();
        store.Add(new Player { Name = "Hero" });
        store.Add(new Transform { X = 10 });
        store.Add(new InventoryItem { ItemId = "sword" });

        var results = store.All(typeof(Player), typeof(Transform));
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r is Player);
        Assert.Contains(results, r => r is Transform);
    }

    [Fact]
    public void Store_All_Type_Array_Duplicate_Types()
    {
        var store = new PicoEntityStore();
        var player = new Player { Name = "Hero" };
        store.Add(player);

        // Current implementation will include the player twice if the type is passed twice
        var results = store.All(typeof(Player), typeof(Player));
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Same(player, r));
    }

    [Fact]
    public void Store_Is_ThreadSafe_During_Concurrent_Adds_And_Queries()
    {
        var store = new PicoEntityStore();
        
        Parallel.For(0, 100, i =>
        {
            var player = new Player { Name = $"Player {i}" };
            store.Add(player);

            bool found = store.All(typeof(Player)).Count > 0;
            Assert.True(found);
        });
        Assert.Equal(100, store.Count);
    }

    [Fact]
    public void Store_Can_Clear_All_Indexed_Entities()
    {
        var store = new PicoEntityStore();
        store.Add(new Player());
        store.Add(new Transform());

        Assert.Equal(2, store.Count);

        store.Clear();

        Assert.Equal(0, store.Count);
        var allEntities = store.All();
        Assert.Empty(allEntities);
    }

    [Fact]
    public void Store_Can_Retrieve_All_Descendants_Recursively()
    {
        var store = new PicoEntityStore();
        var grandparent = new Player { Name = "Grandparent" };
        var parent = new Transform { X = 1, Y = 1 };
        var child = new InventoryItem { ItemId = "Heirloom" };

        store.Add(grandparent, parent);
        store.Add(parent, child);

        var directChildren = store.Children(grandparent);
        Assert.Single(directChildren);
        Assert.Contains(parent, directChildren);

        var allDescendants = store.Descendants(grandparent);
        Assert.Equal(2, allDescendants.Count);
        Assert.Contains(parent, allDescendants);
        Assert.Contains(child, allDescendants);
    }

    [Fact]
    public void Store_Throws_When_Adding_Child_To_A_Second_Parent()
    {
        var store = new PicoEntityStore();
        var parent1 = new Player { Name = "Parent 1" };
        var parent2 = new Player { Name = "Parent 2" };
        var child = new Transform { X = 0, Y = 0 };

        store.Add(parent1, child);

        // An entity can only have one parent.
        // Attempting to add an existing child to a NEW parent throws an InvalidOperationException.
        Assert.Throws<InvalidOperationException>(() => store.Add(parent2, child));
    }

    [Fact]
    public void Store_Can_Batch_Remove_Multiple_Root_Entities()
    {
        var store = new PicoEntityStore();
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
        var store = new PicoEntityStore();
        
        // 1. Setup: Create a player and give them a Transform and a Weapon as children
        var player = new Player { Name = "Hero" };
        var position = new Transform { X = 100, Y = 200 };
        var sword = new Weapon { ItemId = "excalibur", Damage = 99 };
        
        store.Add(player);
        store.Add(player, position); // Position is a child of Player
        store.Add(player, sword);    // Sword is a child of Player

        // 2. Query: Get the player back from the store (simulating a search)
        var hero = store.First<Player>();
        Assert.NotNull(hero);

        // 3. Query Children: Get all direct children as the base 'PicoEntity' type
        var children = store.Children(hero);
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
