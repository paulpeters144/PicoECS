using BenchmarkDotNet.Attributes;
using PicoECS;

namespace PicoECS.Benchmarks;

public class BenchmarkPicoEntity : PicoEntity { }
public class OtherBenchmarkPicoEntity : PicoEntity { }

[MemoryDiagnoser]
public class StoreBenchmarks
{
    private PicoStore _store = null!;
    private uint[] _ids = null!;
    private BenchmarkPicoEntity _root = null!;
    private List<PicoEntity> _toRemove = null!;

    [Params(1000, 10000)]
    public int PicoEntityCount;

    [GlobalSetup]
    public void Setup()
    {
        _store = new PicoStore();
        _ids = new uint[PicoEntityCount];
        
        for (int i = 0; i < PicoEntityCount; i++)
        {
            PicoEntity ent = (i % 2 == 0) ? new BenchmarkPicoEntity() : new OtherBenchmarkPicoEntity();
            _store.Add(ent);
            _ids[i] = ent.Id;
        }

        _root = new BenchmarkPicoEntity();
        _store.Add(_root);
        var current = _root;
        for (int i = 0; i < 100; i++)
        {
            var next = new BenchmarkPicoEntity();
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
            var ent = new BenchmarkPicoEntity();
            _store.Add(ent);
            _toRemove.Add(ent);
        }
    }

    [Benchmark]
    public void AddEntities()
    {
        var store = new PicoStore();
        for (int i = 0; i < PicoEntityCount; i++)
        {
            store.Add(new BenchmarkPicoEntity());
        }
    }

    [Benchmark]
    public void GetPicoEntityById()
    {
        for (int i = 0; i < PicoEntityCount; i++)
        {
            _store.Get<BenchmarkPicoEntity>(_ids[i]);
        }
    }

    [Benchmark]
    public void TraverseDescendants()
    {
        var descendants = _store.Descendants(_root);
    }

    [Benchmark]
    public void All()
    {
        var all = _store.All();
    }

    [Benchmark]
    public void AllGeneric()
    {
        var all = _store.All<BenchmarkPicoEntity>();
    }

    [Benchmark]
    public void First()
    {
        var first = _store.First<BenchmarkPicoEntity>();
    }

    [Benchmark]
    public void ForEach()
    {
        _store.ForEach(e => { });
    }

    [Benchmark]
    public void ForEachGeneric()
    {
        _store.ForEach<BenchmarkPicoEntity>(e => { });
    }

    [Benchmark]
    public void RemoveEntities()
    {
        _store.Remove([.. _toRemove]);
    }
}
