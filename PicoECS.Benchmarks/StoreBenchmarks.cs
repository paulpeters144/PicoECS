using BenchmarkDotNet.Attributes;
using PicoECS;

namespace PicoECS.Benchmarks;

public class BenchmarkEntity : Entity { }
public class OtherBenchmarkEntity : Entity { }

[MemoryDiagnoser]
public class StoreBenchmarks
{
    private EcStore _store = null!;
    private uint[] _ids = null!;
    private BenchmarkEntity _root = null!;
    private List<Entity> _toRemove = null!;

    [Params(1000, 10000)]
    public int EntityCount;

    [GlobalSetup]
    public void Setup()
    {
        _store = new EcStore();
        _ids = new uint[EntityCount];
        
        for (int i = 0; i < EntityCount; i++)
        {
            Entity ent = (i % 2 == 0) ? new BenchmarkEntity() : new OtherBenchmarkEntity();
            _store.Add(ent);
            _ids[i] = ent.Id;
        }

        _root = new BenchmarkEntity();
        _store.Add(_root);
        var current = _root;
        for (int i = 0; i < 100; i++)
        {
            var next = new BenchmarkEntity();
            _store.Add(current, next);
            current = next;
        }
    }

    [IterationSetup(Target = nameof(RemoveEntities))]
    public void IterationSetup()
    {
        _toRemove = [];
        for (int i = 0; i < 100; i++)
        {
            var ent = new BenchmarkEntity();
            _store.Add(ent);
            _toRemove.Add(ent);
        }
    }

    [Benchmark]
    public void AddEntities()
    {
        var store = new EcStore();
        for (int i = 0; i < EntityCount; i++)
        {
            store.Add(new BenchmarkEntity());
        }
    }

    [Benchmark]
    public void GetEntityById()
    {
        for (int i = 0; i < EntityCount; i++)
        {
            _store.Get<BenchmarkEntity>(_ids[i]);
        }
    }

    [Benchmark]
    public void TraverseDescendants()
    {
        var descendants = _store.GetDescendants(_root);
    }

    [Benchmark]
    public void GetAll()
    {
        var all = _store.GetAll();
    }

    [Benchmark]
    public void GetFirst()
    {
        var first = _store.GetFirst<BenchmarkEntity>();
    }

    [Benchmark]
    public void ForEach()
    {
        _store.ForEach(e => { });
    }

    [Benchmark]
    public void ForEachGeneric()
    {
        _store.ForEach<BenchmarkEntity>(e => { });
    }

    [Benchmark]
    public void QueryForEach()
    {
        _store.Query<BenchmarkEntity, OtherBenchmarkEntity, Entity>().ForEach(e => { });
    }

    [Benchmark]
    public void RemoveEntities()
    {
        _store.Remove([.. _toRemove]);
    }
}
