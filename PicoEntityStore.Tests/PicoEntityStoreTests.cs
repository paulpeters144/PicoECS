namespace PicoEntityStoreCore.Tests;

using PicoEntityStoreCore;

public class TestPicoEntity : PicoEntity { }
public class OtherPicoEntity : PicoEntity { }
public class DerivedTestPicoEntity : TestPicoEntity { }

public class PicoEntityStoreTests
{
    #region Add & Count Tests

    [Fact]
    public void Add_SinglePicoEntity_AssignsId()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity();
        store.Add(ent);
        Assert.NotEqual(0u, ent.Id);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Add_MultipleEntities_AssignsUniqueIds()
    {
        var store = new PicoEntityStore();
        var ent1 = new TestPicoEntity();
        var ent2 = new TestPicoEntity();
        store.Add(ent1);
        store.Add(ent2);
        Assert.NotEqual(ent1.Id, ent2.Id);
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void Add_ParentAndChildren_MaintainsHierarchy()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        var child1 = new TestPicoEntity();
        var child2 = new OtherPicoEntity();

        store.Add(parent, child1, child2);

        Assert.Equal(3, store.Count);
        Assert.Equal(parent.Id, child1.ParentId);
        Assert.Equal(parent.Id, child2.ParentId);
        Assert.Contains(child1.Id, parent.ChildIds);
        Assert.Contains(child2.Id, parent.ChildIds);
    }

    [Fact]
    public void Add_NullParent_ThrowsArgumentNullException()
    {
        var store = new PicoEntityStore();
        Assert.Throws<ArgumentNullException>(() => store.Add(null!));
    }

    [Fact]
    public void Add_NullChild_IsIgnored()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        store.Add(parent, null!);
        Assert.Equal(1, store.Count);
        Assert.Empty(parent.ChildIds);
    }

    [Fact]
    public void Add_ChildAlreadyHasDifferentParent_ThrowsInvalidOperationException()
    {
        var store = new PicoEntityStore();
        var parent1 = new TestPicoEntity();
        var parent2 = new TestPicoEntity();
        var child = new TestPicoEntity();

        store.Add(parent1, child);
        Assert.Throws<InvalidOperationException>(() => store.Add(parent2, child));
    }

    [Fact]
    public void Add_ChildToSameParentTwice_IsIdempotent()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        var child = new TestPicoEntity();

        store.Add(parent, child);
        store.Add(parent, child);

        Assert.Equal(2, store.Count);
        Assert.Single(parent.ChildIds);
    }

    [Fact]
    public void Add_PicoEntityTwice_DoesNotDuplicateInIndex()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity();
        store.Add(ent);
        store.Add(ent);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Count_EmptyStore_ReturnsZero()
    {
        var store = new PicoEntityStore();
        Assert.Equal(0, store.Count);
    }

    #endregion

    #region Get & Query Tests

    [Fact]
    public void Get_ExistingPicoEntity_ReturnsCorrectInstance()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity();
        store.Add(ent);
        var fetched = store.Get<TestPicoEntity>(ent.Id);
        Assert.Same(ent, fetched);
    }

    [Fact]
    public void Get_NonExistentId_ReturnsNull()
    {
        var store = new PicoEntityStore();
        Assert.Null(store.Get<TestPicoEntity>(999));
    }

    [Fact]
    public void Get_WrongType_ReturnsNull()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity();
        store.Add(ent);
        Assert.Null(store.Get<OtherPicoEntity>(ent.Id));
    }

    [Fact]
    public void First_Existing_ReturnsFirstPicoEntity()
    {
        var store = new PicoEntityStore();
        var ent1 = new TestPicoEntity();
        var ent2 = new TestPicoEntity();
        store.Add(ent1);
        store.Add(ent2);
        var first = store.First<TestPicoEntity>();
        Assert.True(first == ent1 || first == ent2);
    }

    [Fact]
    public void First_NonExistent_ReturnsNull()
    {
        var store = new PicoEntityStore();
        Assert.Null(store.First<TestPicoEntity>());
    }

    [Fact]
    public void All_Unfiltered_ReturnsEverything()
    {
        var store = new PicoEntityStore();
        store.Add(new TestPicoEntity());
        store.Add(new OtherPicoEntity());
        Assert.Equal(2, store.All().Count);
    }

    [Fact]
    public void All_WithTypes_ReturnsOnlyMatchingTypes()
    {
        var store = new PicoEntityStore();
        store.Add(new TestPicoEntity());
        store.Add(new OtherPicoEntity());
        store.Add(new TestPicoEntity()); // Add another of same type

        // Dummy entity type to verify excluded types
        var excluded = new DummyPicoEntity();
        store.Add(excluded);

        var allItems = store.All(typeof(TestPicoEntity), typeof(OtherPicoEntity));
        Assert.Equal(3, allItems.Count);
        Assert.DoesNotContain(excluded, allItems);
    }

    private class DummyPicoEntity : PicoEntity { }

    #endregion

    #region Hierarchy Navigation Tests

    [Fact]
    public void Parent_RootPicoEntity_ReturnsNull()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity();
        store.Add(ent);
        Assert.Null(store.Parent(ent));
    }

    [Fact]
    public void Parent_ChildPicoEntity_ReturnsParent()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        var child = new TestPicoEntity();
        store.Add(parent, child);
        Assert.Same(parent, store.Parent(child));
    }

    [Fact]
    public void Parent_NullPicoEntity_ThrowsArgumentNullException()
    {
        var store = new PicoEntityStore();
        Assert.Throws<ArgumentNullException>(() => store.Parent(null!));
    }

    [Fact]
    public void Children_LeafPicoEntity_ReturnsEmpty()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity();
        store.Add(ent);
        Assert.Empty(store.Children(ent));
    }

    [Fact]
    public void Children_ReturnsDirectChildren()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        var child = new DerivedTestPicoEntity();
        store.Add(parent, child);
        var children = store.Children(parent);
        Assert.Single(children);
        Assert.Same(child, children[0]);
    }

    [Fact]
    public void Children_NullParent_ThrowsArgumentNullException()
    {
        var store = new PicoEntityStore();
        Assert.Throws<ArgumentNullException>(() => store.Children(null!));
    }

    [Fact]
    public void Descendants_DeepHierarchy_ReturnsAllRecursively()
    {
        var store = new PicoEntityStore();
        var root = new TestPicoEntity();
        var c1 = new TestPicoEntity();
        var c2 = new TestPicoEntity();
        var g1 = new TestPicoEntity();
        store.Add(root, c1, c2);
        store.Add(c1, g1);

        var descendants = store.Descendants(root);
        Assert.Equal(3, descendants.Count);
        Assert.Contains(c1, descendants);
        Assert.Contains(c2, descendants);
        Assert.Contains(g1, descendants);
    }

    [Fact]
    public void Descendants_NullParent_ThrowsArgumentNullException()
    {
        var store = new PicoEntityStore();
        Assert.Throws<ArgumentNullException>(() => store.Descendants(null!));
    }

    #endregion

    #region Removal & Clear Tests

    [Fact]
    public void Remove_SingleRoot_RemovesFromStore()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity();
        store.Add(ent);
        store.Remove(ent);
        Assert.Equal(0, store.Count);
        Assert.Null(store.Get<TestPicoEntity>(ent.Id));
    }

    [Fact]
    public void Remove_WithChildren_RemovesRecursively()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        var child = new TestPicoEntity();
        store.Add(parent, child);
        store.Remove(parent);
        Assert.Equal(0, store.Count);
        Assert.Null(store.Get<TestPicoEntity>(child.Id));
    }

    [Fact]
    public void Remove_NonExistentPicoEntity_DoesNothing()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity(); // Not added to store
        store.Remove(ent);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Remove_NullPicoEntity_IsIgnored()
    {
        var store = new PicoEntityStore();
        store.Remove(null!);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Remove_ChildDirectly_LeavesDanglingIdInParent_KnownBehavior()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        var child = new TestPicoEntity();
        store.Add(parent, child);
        
        store.Remove(child);
        
        // This highlights that Children returns 0 despite dangling ID
        Assert.Empty(store.Children(parent));
    }

    [Fact]
    public void Clear_RemovesAllEntitiesAndResetsCount()
    {
        var store = new PicoEntityStore();
        store.Add(new TestPicoEntity());
        store.Add(new OtherPicoEntity());
        store.Clear();
        Assert.Equal(0, store.Count);
        Assert.Empty(store.All());
    }

    [Fact]
    public void Clear_Then_Add_Entity_Again_Can_Query_By_Id()
    {
        var store = new PicoEntityStore();
        var entity = new TestPicoEntity();
        uint id = entity.Id;

        store.Add(entity);
        Assert.Equal(1, store.Count);

        store.Clear();
        Assert.Equal(0, store.Count);
        Assert.Null(store.Get<TestPicoEntity>(id));

        store.Add(entity);
        Assert.Equal(1, store.Count);
        var retrieved = store.Get<TestPicoEntity>(id);
        Assert.NotNull(retrieved);
        Assert.Equal(entity, retrieved);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public void Concurrent_Add_IsThreadSafe()
    {
        var store = new PicoEntityStore();
        int count = 1000;
        Parallel.For(0, count, i => store.Add(new TestPicoEntity()));
        Assert.Equal(count, store.Count);
    }

    [Fact]
    public void Concurrent_Remove_IsThreadSafe()
    {
        var store = new PicoEntityStore();
        var entities = Enumerable.Range(0, 500).Select(_ => new TestPicoEntity()).ToArray();
        foreach (var e in entities) store.Add(e);

        Parallel.ForEach(entities, e => store.Remove(e));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task Concurrent_ReadWrite_IsThreadSafe()
    {
        var store = new PicoEntityStore();
        var task1 = Task.Run(() => 
        {
            for(int i=0; i<500; i++) store.Add(new TestPicoEntity());
        });
        var task2 = Task.Run(() => 
        {
            for(int i=0; i<500; i++) store.All();
        });

        await Task.WhenAll(task1, task2);
        Assert.Equal(500, store.Count);
    }

    #endregion

    #region Complex Scenarios & Edge Cases

    [Fact]
    public void Hierarchy_DeeplyNested_RemovalWorks()
    {
        var store = new PicoEntityStore();
        PicoEntity current = new TestPicoEntity();
        store.Add(current);
        var root = current;

        for (int i = 0; i < 100; i++)
        {
            var next = new TestPicoEntity();
            store.Add(current, next);
            current = next;
        }

        Assert.Equal(101, store.Count);
        store.Remove(root);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Remove_MiddleNode_RemovesSubtreeOnly()
    {
        var store = new PicoEntityStore();
        var root = new TestPicoEntity();
        var middle = new TestPicoEntity();
        var leaf = new TestPicoEntity();

        store.Add(root, middle);
        store.Add(middle, leaf);

        store.Remove(middle);

        Assert.Equal(1, store.Count);
        Assert.Same(root, store.Get<TestPicoEntity>(root.Id));
        Assert.Null(store.Get<TestPicoEntity>(middle.Id));
        Assert.Null(store.Get<TestPicoEntity>(leaf.Id));
    }

    [Fact]
    public void Add_BatchChildren()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        var children = Enumerable.Range(0, 10).Select(_ => new TestPicoEntity()).ToArray();
        
        store.Add(parent, children);
        
        Assert.Equal(11, store.Count);
        Assert.Equal(10, store.Children(parent).Count);
    }

    [Fact]
    public void Remove_MultipleTimes_Idempotent()
    {
        var store = new PicoEntityStore();
        var ent = new TestPicoEntity();
        store.Add(ent);
        store.Remove(ent);
        store.Remove(ent);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Add_ExistingPicoEntityAsChild_ToSameParent_DoesNotChangeCount()
    {
        var store = new PicoEntityStore();
        var parent = new TestPicoEntity();
        var child = new TestPicoEntity();
        store.Add(parent, child);
        int countBefore = store.Count;
        
        store.Add(parent, child);
        
        Assert.Equal(countBefore, store.Count);
        Assert.Single(store.Children(parent));
    }

    [Fact]
    public void Get_NullId_ReturnsNull()
    {
        var store = new PicoEntityStore();
        Assert.Null(store.Get<PicoEntity>(0));
    }

    #endregion
}
