// ==========================================================
// Project: WpfHexEditor.Benchmarks
// File: Benchmarks/DiffEngineBenchmarks.cs
// Description:
//     Benchmarks for DiffEngine on 10 KB, 1 MB, and 10 MB file pairs
//     with ~10% random mutations. Temp files are written in GlobalSetup
//     to keep IO out of the measured benchmark loop.
//
// Baseline targets:
//     Myers diff 10 KB pair   : < 5 ms
//     Myers diff 1 MB pair    : < 200 ms
//     Binary diff 10 MB pair  : < 1000 ms
// ==========================================================

using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Core.Diff.Services;

namespace WpfHexEditor.Benchmarks.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[BenchmarkCategory("Diff")]
public class DiffEngineBenchmarks
{
    private string _left10K  = string.Empty;
    private string _right10K = string.Empty;
    private string _left1M   = string.Empty;
    private string _right1M  = string.Empty;
    private string _left10M  = string.Empty;
    private string _right10M = string.Empty;

    private DiffEngine _engine = null!;

    [GlobalSetup]
    public void Setup()
    {
        _engine = new DiffEngine();

        _left10K  = WriteTempFile(GenerateData(10   * 1024));
        _right10K = WriteTempFile(MutateData(File.ReadAllBytes(_left10K), 0.10));
        _left1M   = WriteTempFile(GenerateData(1    * 1024 * 1024));
        _right1M  = WriteTempFile(MutateData(File.ReadAllBytes(_left1M),  0.10));
        _left10M  = WriteTempFile(GenerateData(10   * 1024 * 1024));
        _right10M = WriteTempFile(MutateData(File.ReadAllBytes(_left10M), 0.10));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var f in new[] { _left10K, _right10K, _left1M, _right1M, _left10M, _right10M })
            if (File.Exists(f)) File.Delete(f);
    }

    [Benchmark] public Task<DiffEngineResult> MyersDiff_10K()  => _engine.CompareAsync(_left10K,  _right10K,  DiffMode.Text);
    [Benchmark] public Task<DiffEngineResult> MyersDiff_1M()   => _engine.CompareAsync(_left1M,   _right1M,   DiffMode.Text);
    [Benchmark] public Task<DiffEngineResult> BinaryDiff_10M() => _engine.CompareAsync(_left10M,  _right10M,  DiffMode.Binary);

    private static string WriteTempFile(byte[] data)
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, data);
        return path;
    }

    private static byte[] GenerateData(int size)
    {
        var data = new byte[size];
        Random.Shared.NextBytes(data);
        return data;
    }

    private static byte[] MutateData(byte[] source, double mutationRate)
    {
        var result = (byte[])source.Clone();
        int mutations = (int)(source.Length * mutationRate);
        for (int i = 0; i < mutations; i++)
            result[Random.Shared.Next(result.Length)] = (byte)Random.Shared.Next(256);
        return result;
    }
}
