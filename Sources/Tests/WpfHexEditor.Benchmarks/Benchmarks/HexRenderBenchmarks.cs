// ==========================================================
// Project: WpfHexEditor.Benchmarks
// File: Benchmarks/HexRenderBenchmarks.cs
// Description:
//     Micro-benchmarks for HexViewport hot-path methods:
//       - Byte-to-hex string conversion (GetHexText / ToString("X2"))
//       - TBL hex-key building via StringBuilder
//       - ByteData.GetHexText for all 256 values
//
// Baseline targets (Phase 0):
//     GetHexText_256Values   : establish alloc baseline → 0 alloc after Phase 4
//     TblKeyBuilder_16bytes  : establish alloc baseline → 0 alloc after Phase 3
// ==========================================================

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Text;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.Benchmarks.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[BenchmarkCategory("HexRender")]
public class HexRenderBenchmarks
{
    private ByteData[] _byteData256 = [];
    private byte[][] _tblWindows = [];

    [GlobalSetup]
    public void Setup()
    {
        // 256 ByteData objects — one for each possible byte value
        _byteData256 = new ByteData[256];
        for (int i = 0; i < 256; i++)
            _byteData256[i] = new ByteData { Value = (byte)i };

        // 10 TBL windows of 8 bytes each (greedy match scenario)
        _tblWindows = new byte[10][];
        var rng = new Random(42);
        for (int i = 0; i < 10; i++)
        {
            _tblWindows[i] = new byte[8];
            rng.NextBytes(_tblWindows[i]);
        }
    }

    /// <summary>
    /// Baseline: GetHexText for all 256 byte values (Hexadecimal visual type).
    /// Phase 4 target: 0 allocations (lookup table replaces ToString("X2")).
    /// </summary>
    [Benchmark]
    public string GetHexText_256Values()
    {
        string last = "";
        for (int i = 0; i < 256; i++)
            last = _byteData256[i].GetHexText(DataVisualType.Hexadecimal);
        return last;
    }

    /// <summary>
    /// Baseline: ToString("X2") per byte — current hot path allocation.
    /// </summary>
    [Benchmark]
    public string ToStringX2_256Values()
    {
        string last = "";
        for (int i = 0; i < 256; i++)
            last = ((byte)i).ToString("X2");
        return last;
    }

    /// <summary>
    /// Baseline: StringBuilder-based TBL hex key building (greedy 8-byte window).
    /// Phase 3 target: 0 StringBuilder allocs (ThreadStatic char[] buffer).
    /// </summary>
    [Benchmark]
    public string TblKeyBuilder_8ByteWindow()
    {
        string last = "";
        foreach (var window in _tblWindows)
        {
            var sb = new StringBuilder(window.Length * 2);
            foreach (var b in window)
                sb.Append(b.ToString("X2"));
            last = sb.ToString();
        }
        return last;
    }

    /// <summary>
    /// Baseline: multi-byte GetHexText (concatenate all bytes in Hexadecimal mode).
    /// Exercises the LINQ Select + string.Concat path in ByteData.GetHexText.
    /// </summary>
    [Benchmark]
    public string GetHexText_MultiBytes_AllWindows()
    {
        string last = "";
        foreach (var window in _tblWindows)
        {
            var bd = new ByteData
            {
                Value     = window[0],
                Values    = window,
                ByteSize  = ByteSizeType.Bit16,
                ByteOrder = ByteOrderType.LoHi,
            };
            last = bd.GetHexText(DataVisualType.Hexadecimal);
        }
        return last;
    }
}
