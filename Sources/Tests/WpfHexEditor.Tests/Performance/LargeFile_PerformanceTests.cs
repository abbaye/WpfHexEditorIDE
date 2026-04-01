using System.Diagnostics;
using WpfHexEditor.Core.Diff.Algorithms;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.Services;

namespace WpfHexEditor.Tests.Performance;

/// <summary>
/// Performance benchmarks for large file operations (1MB-1GB scale).
/// These tests measure throughput and memory, not correctness.
/// Run with: dotnet test --filter "Category=Performance"
/// </summary>
[TestClass]
[TestCategory("Performance")]
public sealed class LargeFile_PerformanceTests
{
    // ── Binary Diff Performance ──────────────────────────────────────────────

    [TestMethod]
    public void BinaryDiff_1MB_CompletesUnder100ms()
    {
        var left  = GenerateRandomBytes(1 * 1024 * 1024, seed: 42);
        var right = FlipRandomBytes(left, count: 1000, seed: 99);

        var algo = new BinaryDiffAlgorithm();
        var sw = Stopwatch.StartNew();
        var result = algo.ComputeBytes(left, right);
        sw.Stop();

        Assert.IsTrue(result.Regions.Count > 0);
        Assert.IsTrue(sw.ElapsedMilliseconds < 100,
            $"BinaryDiff 1MB took {sw.ElapsedMilliseconds}ms (expected <100ms)");
    }

    [TestMethod]
    public void BinaryDiff_10MB_CompletesUnder500ms()
    {
        var left  = GenerateRandomBytes(10 * 1024 * 1024, seed: 42);
        var right = FlipRandomBytes(left, count: 5000, seed: 99);

        var algo = new BinaryDiffAlgorithm();
        var sw = Stopwatch.StartNew();
        var result = algo.ComputeBytes(left, right);
        sw.Stop();

        Assert.IsTrue(result.Regions.Count > 0);
        Assert.IsTrue(sw.ElapsedMilliseconds < 500,
            $"BinaryDiff 10MB took {sw.ElapsedMilliseconds}ms (expected <500ms)");
    }

    // ── Format Detection Performance ─────────────────────────────────────────

    [TestMethod]
    public void FormatDetection_1MB_CompletesUnder50ms()
    {
        // Simulate a ZIP file header + 1MB payload
        var data = new byte[1 * 1024 * 1024];
        data[0] = 0x50; data[1] = 0x4B; data[2] = 0x03; data[3] = 0x04; // PK\x03\x04

        var service = new FormatDetectionService();

        var sw = Stopwatch.StartNew();
        var result = service.DetectFormat(data);
        sw.Stop();

        // Detection should be fast regardless of file size (capped at 4KB scan)
        Assert.IsTrue(sw.ElapsedMilliseconds < 50,
            $"FormatDetection took {sw.ElapsedMilliseconds}ms (expected <50ms)");
    }

    // ── Checksum Engine Performance ──────────────────────────────────────────

    [TestMethod]
    public void ChecksumEngine_CRC32_1MB_CompletesUnder50ms()
    {
        var data = GenerateRandomBytes(1 * 1024 * 1024, seed: 42);
        var engine = new ChecksumEngine();
        var defs = new List<ChecksumDefinition>
        {
            new()
            {
                Name = "CRC32 test",
                Algorithm = "crc32",
                DataRange = new ChecksumRange { FixedOffset = 0, FixedLength = data.Length },
                ExpectedValue = "00000000" // will fail but we're measuring speed
            }
        };

        var sw = Stopwatch.StartNew();
        var results = engine.Execute(defs, data, new Dictionary<string, object>());
        sw.Stop();

        Assert.AreEqual(1, results.Count);
        Assert.IsTrue(sw.ElapsedMilliseconds < 50,
            $"CRC32 1MB took {sw.ElapsedMilliseconds}ms (expected <50ms)");
    }

    // ── Myers Diff Performance (text) ────────────────────────────────────────

    [TestMethod]
    public void MyersDiff_10KLines_CompletesUnder500ms()
    {
        var left  = GenerateLines(10_000, seed: 42);
        var right = ModifyLines(left, changeCount: 500, seed: 99);

        var algo = new MyersDiffAlgorithm();
        var sw = Stopwatch.StartNew();
        var result = algo.ComputeLines(left, right);
        sw.Stop();

        Assert.IsTrue(result.Lines.Count > 0);
        Assert.IsTrue(sw.ElapsedMilliseconds < 500,
            $"Myers 10K lines took {sw.ElapsedMilliseconds}ms (expected <500ms)");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static byte[] GenerateRandomBytes(int size, int seed)
    {
        var rng = new Random(seed);
        var buf = new byte[size];
        rng.NextBytes(buf);
        return buf;
    }

    private static byte[] FlipRandomBytes(byte[] source, int count, int seed)
    {
        var result = (byte[])source.Clone();
        var rng = new Random(seed);
        for (int i = 0; i < count; i++)
        {
            int idx = rng.Next(result.Length);
            result[idx] = (byte)(result[idx] ^ 0xFF);
        }
        return result;
    }

    private static string[] GenerateLines(int count, int seed)
    {
        var rng = new Random(seed);
        var lines = new string[count];
        for (int i = 0; i < count; i++)
            lines[i] = $"Line {i}: value={rng.Next(10000)} hash={rng.Next():X8}";
        return lines;
    }

    private static string[] ModifyLines(string[] source, int changeCount, int seed)
    {
        var result = (string[])source.Clone();
        var rng = new Random(seed);
        for (int i = 0; i < changeCount; i++)
        {
            int idx = rng.Next(result.Length);
            result[idx] = $"MODIFIED-{i}: {rng.Next():X8}";
        }
        return result;
    }
}
