using BenchmarkDotNet.Running;
using PicoEntityStoreCore.Benchmarks;

var summary = BenchmarkRunner.Run<StoreBenchmarks>();
