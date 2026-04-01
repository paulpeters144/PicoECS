using BenchmarkDotNet.Attributes;
using PicoEntityStoreCore;

namespace PicoEntityStoreCore.Benchmarks;

public class BenchmarkPicoEntity : PicoEntity { }
public class OtherBenchmarkPicoEntity : PicoEntity { }

[MemoryDiagnoser]
public class StoreBenchmarks
{
    private PicoEntityStore _store = null!;
    private uint[] _ids = null!;
    private BenchmarkPicoEntity _root = null!;
    private List<PicoEntity> _toRemove = null!;

    [Params(1000, 10000)]
    public int PicoEntityCount;

    [GlobalSetup]
    public void Setup()
    {
        _store = new PicoEntityStore();
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

    [IterationSetup(Target = nameof(Remove))]
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
    public void Add()
    {
        var store = new PicoEntityStore();
        for (int i = 0; i < PicoEntityCount; i++)
        {
            store.Add(new BenchmarkPicoEntity());
        }
    }

    [Benchmark]
    public void Get()
    {
        var entity = _store.Get<BenchmarkPicoEntity>(_ids[0]);
    }

    [Benchmark]
    public void Descendants()
    {
        var descendants = _store.Descendants(_root);
    }

    [Benchmark]
    public void All()
    {
        var all = _store.All();
    }

    [Benchmark]
    public void AllType()
    {
        var all = _store.All(typeof(BenchmarkPicoEntity));
    }

    [Benchmark]
    public void First()
    {
        var first = _store.First<BenchmarkPicoEntity>();
    }

    [Benchmark]
    public void Remove()
    {
        _store.Remove([.. _toRemove]);
    }
}
