using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PicoECS.Tests;

public class TestEntity : Entity { }
public class OtherEntity : Entity { }
public class DerivedTestEntity : TestEntity { }

public class PicoStoreTests
{
    #region Add & Count Tests

    [Fact]
    public void Add_SingleEntity_AssignsId()
    {
        var store = new PicoStore();
        var ent = new TestEntity();
        store.Add(ent);
        Assert.NotEqual(0u, ent.Id);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Add_MultipleEntities_AssignsUniqueIds()
    {
        var store = new PicoStore();
        var ent1 = new TestEntity();
        var ent2 = new TestEntity();
        store.Add(ent1);
        store.Add(ent2);
        Assert.NotEqual(ent1.Id, ent2.Id);
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void Add_ParentAndChildren_MaintainsHierarchy()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var child1 = new TestEntity();
        var child2 = new OtherEntity();

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
        var store = new PicoStore();
        Assert.Throws<ArgumentNullException>(() => store.Add(null!));
    }

    [Fact]
    public void Add_NullChild_IsIgnored()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        store.Add(parent, null!);
        Assert.Equal(1, store.Count);
        Assert.Empty(parent.ChildIds);
    }

    [Fact]
    public void Add_ChildAlreadyHasDifferentParent_ThrowsInvalidOperationException()
    {
        var store = new PicoStore();
        var parent1 = new TestEntity();
        var parent2 = new TestEntity();
        var child = new TestEntity();

        store.Add(parent1, child);
        Assert.Throws<InvalidOperationException>(() => store.Add(parent2, child));
    }

    [Fact]
    public void Add_ChildToSameParentTwice_IsIdempotent()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var child = new TestEntity();

        store.Add(parent, child);
        store.Add(parent, child);

        Assert.Equal(2, store.Count);
        Assert.Single(parent.ChildIds);
    }

    [Fact]
    public void Add_EntityTwice_DoesNotDuplicateInIndex()
    {
        var store = new PicoStore();
        var ent = new TestEntity();
        store.Add(ent);
        store.Add(ent);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Count_EmptyStore_ReturnsZero()
    {
        var store = new PicoStore();
        Assert.Equal(0, store.Count);
    }

    #endregion

    #region Get & Query Tests

    [Fact]
    public void Get_ExistingEntity_ReturnsCorrectInstance()
    {
        var store = new PicoStore();
        var ent = new TestEntity();
        store.Add(ent);
        var fetched = store.GetById<TestEntity>(ent.Id);
        Assert.Same(ent, fetched);
    }

    [Fact]
    public void Get_NonExistentId_ReturnsNull()
    {
        var store = new PicoStore();
        Assert.Null(store.GetById<TestEntity>(999));
    }

    [Fact]
    public void Get_WrongType_ReturnsNull()
    {
        var store = new PicoStore();
        var ent = new TestEntity();
        store.Add(ent);
        Assert.Null(store.GetById<OtherEntity>(ent.Id));
    }

    [Fact]
    public void GetFirst_Existing_ReturnsFirstEntity()
    {
        var store = new PicoStore();
        var ent1 = new TestEntity();
        var ent2 = new TestEntity();
        store.Add(ent1);
        store.Add(ent2);
        var first = store.GetFirst<TestEntity>();
        Assert.True(first == ent1 || first == ent2);
    }

    [Fact]
    public void GetFirst_NonExistent_ReturnsNull()
    {
        var store = new PicoStore();
        Assert.Null(store.GetFirst<TestEntity>());
    }

    [Fact]
    public void GetAll_Unfiltered_ReturnsEverything()
    {
        var store = new PicoStore();
        store.Add(new TestEntity());
        store.Add(new OtherEntity());
        Assert.Equal(2, store.GetAll().Count);
    }

    [Fact]
    public void ForEach_ExecutesOnAllEntities()
    {
        var store = new PicoStore();
        store.Add(new TestEntity());
        store.Add(new OtherEntity());
        int count = 0;
        store.ForEach(e => count++);
        Assert.Equal(2, count);
    }

    [Fact]
    public void ForEach_Generic_ExecutesOnMatchingType()
    {
        var store = new PicoStore();
        store.Add(new TestEntity());
        store.Add(new TestEntity());
        store.Add(new OtherEntity());
        int count = 0;
        store.ForEach<TestEntity>(e => count++);
        Assert.Equal(2, count);
    }

    #endregion

    #region Hierarchy Navigation Tests

    [Fact]
    public void GetParent_RootEntity_ReturnsNull()
    {
        var store = new PicoStore();
        var ent = new TestEntity();
        store.Add(ent);
        Assert.Null(store.GetParent<Entity>(ent));
    }

    [Fact]
    public void GetParent_ChildEntity_ReturnsParent()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var child = new TestEntity();
        store.Add(parent, child);
        Assert.Same(parent, store.GetParent<TestEntity>(child));
    }

    [Fact]
    public void GetParent_WrongType_ReturnsNull()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var child = new TestEntity();
        store.Add(parent, child);
        Assert.Null(store.GetParent<OtherEntity>(child));
    }

    [Fact]
    public void GetParent_NullEntity_ThrowsArgumentNullException()
    {
        var store = new PicoStore();
        Assert.Throws<ArgumentNullException>(() => store.GetParent<Entity>(null!));
    }

    [Fact]
    public void GetChildren_LeafEntity_ReturnsEmpty()
    {
        var store = new PicoStore();
        var ent = new TestEntity();
        store.Add(ent);
        Assert.Empty(store.GetChildren<Entity>(ent));
    }

    [Fact]
    public void GetChildren_Polymorphic_ReturnsDerivedTypes()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var child = new DerivedTestEntity();
        store.Add(parent, child);
        var children = store.GetChildren<TestEntity>(parent);
        Assert.Single(children);
        Assert.IsType<DerivedTestEntity>(children[0]);
    }

    [Fact]
    public void GetChildren_NullParent_ThrowsArgumentNullException()
    {
        var store = new PicoStore();
        Assert.Throws<ArgumentNullException>(() => store.GetChildren<Entity>(null!));
    }

    [Fact]
    public void GetChildCount_ReturnsCorrectValue()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        store.Add(parent, new TestEntity(), new TestEntity());
        Assert.Equal(2, store.GetChildCount(parent));
    }

    [Fact]
    public void GetChildCount_NullParent_ThrowsArgumentNullException()
    {
        var store = new PicoStore();
        Assert.Throws<ArgumentNullException>(() => store.GetChildCount(null!));
    }

    [Fact]
    public void GetDescendants_DeepHierarchy_ReturnsAllRecursively()
    {
        var store = new PicoStore();
        var root = new TestEntity();
        var c1 = new TestEntity();
        var c2 = new TestEntity();
        var g1 = new TestEntity();
        store.Add(root, c1, c2);
        store.Add(c1, g1);

        var descendants = store.GetDescendants(root);
        Assert.Equal(3, descendants.Count);
        Assert.Contains(c1, descendants);
        Assert.Contains(c2, descendants);
        Assert.Contains(g1, descendants);
    }

    [Fact]
    public void GetDescendants_NullParent_ThrowsArgumentNullException()
    {
        var store = new PicoStore();
        Assert.Throws<ArgumentNullException>(() => store.GetDescendants(null!));
    }

    #endregion

    #region Removal & Clear Tests

    [Fact]
    public void Remove_SingleRoot_RemovesFromStore()
    {
        var store = new PicoStore();
        var ent = new TestEntity();
        store.Add(ent);
        store.Remove(ent);
        Assert.Equal(0, store.Count);
        Assert.Null(store.GetById<TestEntity>(ent.Id));
    }

    [Fact]
    public void Remove_WithChildren_RemovesRecursively()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var child = new TestEntity();
        store.Add(parent, child);
        store.Remove(parent);
        Assert.Equal(0, store.Count);
        Assert.Null(store.GetById<TestEntity>(child.Id));
    }

    [Fact]
    public void Remove_NonExistentEntity_DoesNothing()
    {
        var store = new PicoStore();
        var ent = new TestEntity(); // Not added to store
        store.Remove(ent);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Remove_NullEntity_IsIgnored()
    {
        var store = new PicoStore();
        store.Remove(null!);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Remove_ChildDirectly_LeavesDanglingIdInParent_KnownBehavior()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var child = new TestEntity();
        store.Add(parent, child);
        
        store.Remove(child);
        
        // This highlights that GetChildCount returns raw array length
        Assert.Equal(1, store.GetChildCount(parent));
        // But GetChildren filters correctly
        Assert.Empty(store.GetChildren<Entity>(parent));
    }

    [Fact]
    public void Clear_RemovesAllEntitiesAndResetsCount()
    {
        var store = new PicoStore();
        store.Add(new TestEntity());
        store.Add(new OtherEntity());
        store.Clear();
        Assert.Equal(0, store.Count);
        Assert.Empty(store.GetAll());
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public void Concurrent_Add_IsThreadSafe()
    {
        var store = new PicoStore();
        int count = 1000;
        Parallel.For(0, count, i => store.Add(new TestEntity()));
        Assert.Equal(count, store.Count);
    }

    [Fact]
    public void Concurrent_Remove_IsThreadSafe()
    {
        var store = new PicoStore();
        var entities = Enumerable.Range(0, 500).Select(_ => new TestEntity()).ToArray();
        foreach (var e in entities) store.Add(e);

        Parallel.ForEach(entities, e => store.Remove(e));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task Concurrent_ReadWrite_IsThreadSafe()
    {
        var store = new PicoStore();
        var task1 = Task.Run(() => 
        {
            for(int i=0; i<500; i++) store.Add(new TestEntity());
        });
        var task2 = Task.Run(() => 
        {
            for(int i=0; i<500; i++) store.ForEach(e => { });
        });

        await Task.WhenAll(task1, task2);
        Assert.Equal(500, store.Count);
    }

    #endregion

    #region Complex Scenarios & Edge Cases

    [Fact]
    public void Hierarchy_DeeplyNested_RemovalWorks()
    {
        var store = new PicoStore();
        Entity current = new TestEntity();
        store.Add(current);
        var root = current;

        for (int i = 0; i < 100; i++)
        {
            var next = new TestEntity();
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
        var store = new PicoStore();
        var root = new TestEntity();
        var middle = new TestEntity();
        var leaf = new TestEntity();

        store.Add(root, middle);
        store.Add(middle, leaf);

        store.Remove(middle);

        Assert.Equal(1, store.Count);
        Assert.Same(root, store.GetById<TestEntity>(root.Id));
        Assert.Null(store.GetById<TestEntity>(middle.Id));
        Assert.Null(store.GetById<TestEntity>(leaf.Id));
    }

    [Fact]
    public void Add_BatchChildren()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var children = Enumerable.Range(0, 10).Select(_ => new TestEntity()).ToArray();
        
        store.Add(parent, children);
        
        Assert.Equal(11, store.Count);
        Assert.Equal(10, store.GetChildCount(parent));
    }

    [Fact]
    public void ForEach_Generic_ExecutesZeroTimesWhenNoneMatch()
    {
        var store = new PicoStore();
        store.Add(new OtherEntity());
        int count = 0;
        store.ForEach<TestEntity>(e => count++);
        Assert.Equal(0, count);
    }

    [Fact]
    public void Remove_MultipleTimes_Idempotent()
    {
        var store = new PicoStore();
        var ent = new TestEntity();
        store.Add(ent);
        store.Remove(ent);
        store.Remove(ent);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Add_ExistingEntityAsChild_ToSameParent_DoesNotChangeCount()
    {
        var store = new PicoStore();
        var parent = new TestEntity();
        var child = new TestEntity();
        store.Add(parent, child);
        int countBefore = store.Count;
        
        store.Add(parent, child);
        
        Assert.Equal(countBefore, store.Count);
        Assert.Equal(1, store.GetChildCount(parent));
    }

    [Fact]
    public void Get_NullId_ReturnsNull()
    {
        var store = new PicoStore();
        Assert.Null(store.GetById<Entity>(0));
    }

    #endregion
}
