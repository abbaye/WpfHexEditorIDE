using System.Text.Json;
using WhfmtBackfill;

namespace WhfmtBackfill.Tests;

[TestClass]
public sealed class BackfillEngineTests
{
    private string _tmpDir = "";

    [TestInitialize]
    public void Init()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "whfmt-backfill-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, recursive: true);
    }

    private const string PngLikeWhfmt = """
{
  "formatId": "PNG_TEST",
  "category": "Images",
  "formatName": "PNG-like test fixture",
  "blocks": [
    { "type": "signature", "name": "Magic", "storeAs": "magic", "offset": 0, "length": 4, "valueType": "hex",
      "validation": { "expectedValue": "0x89504E47" } },
    { "type": "field", "name": "Width",     "storeAs": "imageWidth",  "offset": 4,  "length": 4, "valueType": "uint32" },
    { "type": "field", "name": "Height",    "storeAs": "imageHeight", "offset": 8,  "length": 4, "valueType": "uint32" },
    { "type": "field", "name": "ColorType", "storeAs": "colorType",   "offset": 12, "length": 1, "valueType": "uint8",
      "valueMap": { "0": "Gray", "2": "RGB" } },
    { "type": "field", "name": "Padding",   "storeAs": "padding",     "offset": 13, "length": 3, "valueType": "uint8" }
  ],
  "checksums": [
    { "name": "Header CRC", "algorithm": "crc32", "storedAt": { "fixedOffset": 16, "length": 4 } }
  ]
}
""";

    [TestMethod]
    public void ProcessFile_adds_diff_repair_fuzz_and_remains_valid_json()
    {
        string path = Path.Combine(_tmpDir, "PNG.whfmt");
        File.WriteAllText(path, PngLikeWhfmt);

        var engine = new BackfillEngine(dryRun: false);
        var r = engine.ProcessFile(path);

        Assert.IsNull(r.Error,   $"Unexpected error: {r.Error}");
        Assert.IsFalse(r.Skipped);
        Assert.IsTrue (r.AddedDiff);
        Assert.IsTrue (r.AddedRepair);
        Assert.IsTrue (r.AddedFuzz);

        string updated = File.ReadAllText(path);
        using var doc  = JsonDocument.Parse(updated);
        var root       = doc.RootElement;
        Assert.IsTrue(root.TryGetProperty("diff",   out var diff),   "diff block missing");
        Assert.IsTrue(root.TryGetProperty("repair", out var repair), "repair block missing");
        Assert.IsTrue(root.TryGetProperty("fuzz",   out var fuzz),   "fuzz block missing");

        // Diff must contain expected key fields
        var keys = diff.GetProperty("keyFields").EnumerateArray().Select(e => e.GetString()).ToList();
        CollectionAssert.Contains(keys, "magic");
        CollectionAssert.Contains(keys, "image_width");
        CollectionAssert.Contains(keys, "color_type");

        // Diff must include the padding ignore
        var ignored = diff.GetProperty("ignoreFields").EnumerateArray().Select(e => e.GetString()).ToList();
        CollectionAssert.Contains(ignored, "padding");

        // Repair must include both FixSignature and crc32 recompute
        var rules = repair.EnumerateArray().Select(r => r.GetProperty("name").GetString()).ToList();
        CollectionAssert.Contains(rules, "FixSignature");
        Assert.IsTrue(rules.Any(n => n!.StartsWith("RecomputeCRC32")));

        // Fuzz must include all four mutation kinds
        var mutations = fuzz.GetProperty("strategies").EnumerateArray()
            .Select(e => e.GetProperty("mutation").GetString()).ToHashSet();
        Assert.IsTrue(mutations.Contains("corrupt_signature"));
        Assert.IsTrue(mutations.Contains("boundary_values"));
        Assert.IsTrue(mutations.Contains("enum_sweep"));
        Assert.IsTrue(mutations.Contains("random_bytes"));
    }

    [TestMethod]
    public void ProcessFile_dry_run_does_not_modify_disk()
    {
        string path = Path.Combine(_tmpDir, "PNG.whfmt");
        File.WriteAllText(path, PngLikeWhfmt);
        var before = File.ReadAllText(path);

        var engine = new BackfillEngine(dryRun: true);
        var r = engine.ProcessFile(path);

        Assert.IsTrue(r.AddedDiff && r.AddedRepair && r.AddedFuzz);
        Assert.AreEqual(before, File.ReadAllText(path), "dry-run must not write to disk");
    }

    [TestMethod]
    public void ProcessFile_is_idempotent()
    {
        string path = Path.Combine(_tmpDir, "PNG.whfmt");
        File.WriteAllText(path, PngLikeWhfmt);

        var engine = new BackfillEngine(dryRun: false);
        engine.ProcessFile(path);
        var after1 = File.ReadAllText(path);

        var second = engine.ProcessFile(path);
        var after2 = File.ReadAllText(path);

        Assert.IsTrue(second.Skipped, "second run should be skipped");
        Assert.AreEqual(after1, after2, "second run must not change file");
    }

    [TestMethod]
    public void ProcessFile_invalid_json_is_reported_as_error()
    {
        string path = Path.Combine(_tmpDir, "broken.whfmt");
        File.WriteAllText(path, "{ this is not valid json");

        var engine = new BackfillEngine(dryRun: false);
        var r = engine.ProcessFile(path);

        Assert.IsNotNull(r.Error);
    }
}
