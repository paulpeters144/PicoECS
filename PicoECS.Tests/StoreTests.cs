using Xunit;

namespace PicoECS.Tests;

public class TestEntity : Entity { }
public class OtherEntity : Entity { }

public class StoreTests
{
    [Fact]
    public void Add_AssignsIdsAndMaintainsHierarchy()
    {
        var store = new EcStore();
        var parent = new TestEntity();
        var child1 = new TestEntity();
        var child2 = new OtherEntity();

        store.Add(parent, child1, child2);

        Assert.NotEqual(0u, parent.Id);
        Assert.NotEqual(0u, child1.Id);
        Assert.NotEqual(0u, child2.Id);
        Assert.Equal(parent.Id, child1.ParentId);
        Assert.Equal(parent.Id, child2.ParentId);
        Assert.Contains(child1.Id, parent.ChildIds);
        Assert.Contains(child2.Id, parent.ChildIds);
    }

    [Fact]
    public void Get_ReturnsCorrectEntity()
    {
        var store = new EcStore();
        var ent = new TestEntity();
        store.Add(ent);

        var fetched = store.Get<TestEntity>(ent.Id);
        Assert.Same(ent, fetched);
    }

    [Fact]
    public void GetFirst_ReturnsCorrectType()
    {
        var store = new EcStore();
        var ent = new TestEntity();
        store.Add(ent);

        var fetched = store.GetFirst<TestEntity>();
        Assert.Same(ent, fetched);
        Assert.Null(store.GetFirst<OtherEntity>());
    }

    [Fact]
    public void Remove_RemovesRecursive()
    {
        var store = new EcStore();
        var parent = new TestEntity();
        var child = new TestEntity();
        store.Add(parent, child);

        Assert.Equal(2, store.Count);

        store.Remove(parent);

        Assert.Equal(0, store.Count);
        Assert.Null(store.Get<TestEntity>(parent.Id));
        Assert.Null(store.Get<TestEntity>(child.Id));
    }

    [Fact]
    public void GetDescendants_ReturnsAllChildren()
    {
        var store = new EcStore();
        var root = new TestEntity();
        var child = new TestEntity();
        var grandchild = new TestEntity();

        store.Add(root, child);
        store.Add(child, grandchild);

        var descendants = store.GetDescendants(root);
        Assert.Equal(2, descendants.Count);
        Assert.Contains(child, descendants);
        Assert.Contains(grandchild, descendants);
    }

    [Fact]
    public void Concurrent_AddAndRead()
    {
        var store = new EcStore();
        int count = 1000;

        Parallel.For(0, count, i =>
        {
            store.Add(new TestEntity());
        });

        Assert.Equal(count, store.Count);
        var all = store.GetAll();
        Assert.Equal(count, all.Count);
    }

    [Fact]
    public void GetChildCount_ReturnsCorrectCount()
    {
        var store = new EcStore();
        var parent = new TestEntity();
        store.Add(parent, new TestEntity(), new OtherEntity());

        Assert.Equal(2, store.GetChildCount(parent));
    }

    [Fact]
    public void GetByType_ReturnsOnlySpecificType()
    {
        var store = new EcStore();
        store.Add(new TestEntity());
        store.Add(new TestEntity());
        store.Add(new OtherEntity());

        var testEntities = store.GetByType<TestEntity>();
        var otherEntities = store.GetByType<OtherEntity>();

        Assert.Equal(2, testEntities.Count());
        Assert.Single(otherEntities);
    }

    [Fact]
    public void Add_ThrowsIfChildHasDifferentParent()
    {
        var store = new EcStore();
        var parent1 = new TestEntity();
        var parent2 = new TestEntity();
        var child = new TestEntity();

        store.Add(parent1, child);
        
        Assert.Throws<InvalidOperationException>(() => store.Add(parent2, child));
    }
}
