//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using WpfHexEditor.Core.Bytes;

Console.WriteLine("ByteProvider micro-benchmark starting...");

const int sizeMb = 64;
var size = sizeMb * 1024 * 1024;
var data = new byte[size];
for (int i = 0; i < size; i++) data[i] = (byte)(i & 0xFF);

using var ms = new MemoryStream(data, 0, data.Length, writable: true, publiclyVisible: true);
var provider = new ByteProvider(ms, canInsertAnywhere: true);

const int maxCount = 100_000;
const int iterations = 8;

void RunOnce(string label, Action action)
{
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    action();
    sw.Stop();
    var afterAlloc = GC.GetAllocatedBytesForCurrentThread();
    Console.WriteLine($"{label}: {sw.ElapsedMilliseconds} ms, allocated={afterAlloc - beforeAlloc} bytes");
}

void RunRepeated(string label, Action action)
{
    long totalMs = 0;
    long totalAlloc = 0;
    for (int i = 0; i < iterations; i++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var beforeAlloc = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        var afterAlloc = GC.GetAllocatedBytesForCurrentThread();
        totalMs += sw.ElapsedMilliseconds;
        totalAlloc += (afterAlloc - beforeAlloc);
    }
    Console.WriteLine($"{label} (avg over {iterations}): {totalMs / iterations} ms, allocated(avg)={totalAlloc / iterations} bytes");
}

// Warmup
for (int i = 0; i < 2; i++)
{
    RunOnce("Warmup", () => provider.GetCopyDataWithPosition(0, 1000, true));
}

// Baseline repeated
RunRepeated("Baseline GetCopyDataWithPosition", () => {
    var (buf, pos) = provider.GetCopyDataWithPosition(0, maxCount, true);
    Console.WriteLine($"  returned {buf.Length} bytes, positions {pos.Length}");
});

// Add many inserted bytes to stress added-path
for (int i = 0; i < 5000; i++)
{
    provider.AddByteAdded(0xAA, i * 10);
}
RunRepeated("After AddByteAdded (5k) GetCopyDataWithPosition", () => {
    var (buf, pos) = provider.GetCopyDataWithPosition(0, maxCount, true);
    Console.WriteLine($"  returned {buf.Length} bytes, positions {pos.Length}");
});

// Read using GetCopyData (selection) repeated
RunRepeated("GetCopyData selection", () => {
    var buf = provider.GetCopyData(0, maxCount - 1, true);
    Console.WriteLine($"  returned {buf.Length} bytes");
});

Console.WriteLine("Benchmark complete.");
