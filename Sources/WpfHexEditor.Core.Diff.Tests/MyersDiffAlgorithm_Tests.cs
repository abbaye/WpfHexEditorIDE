using WpfHexEditor.Core.Diff.Algorithms;
using WpfHexEditor.Core.Diff.Models;

namespace WpfHexEditor.Core.Diff.Tests;

[TestClass]
public sealed class MyersDiffAlgorithm_Tests
{
    private readonly MyersDiffAlgorithm _algo = new();

    // ── Line-level tests ────────────────────────────────────────────────────

    [TestMethod]
    public void IdenticalLines_NoDifferences()
    {
        string[] lines = ["alpha", "beta", "gamma"];
        var result = _algo.ComputeLines(lines, lines);

        // Myers: identical inputs produce 0 diff edits (no output lines)
        Assert.AreEqual(0, result.Stats.ModifiedLines);
        Assert.AreEqual(0, result.Stats.InsertedLines);
        Assert.AreEqual(0, result.Stats.DeletedLines);
        Assert.AreEqual(1.0, result.Stats.Similarity, 0.001);
    }

    [TestMethod]
    public void EmptyInputs_ReturnsEmpty()
    {
        var result = _algo.ComputeLines([], []);

        Assert.AreEqual(0, result.Lines.Count);
        Assert.AreEqual(1.0, result.Stats.Similarity, 0.001);
    }

    [TestMethod]
    public void InsertedLine_Detected()
    {
        string[] left  = ["a", "b"];
        string[] right = ["a", "x", "b"];

        var result = _algo.ComputeLines(left, right);

        Assert.IsTrue(result.Lines.Any(l => l.Kind == TextLineKind.InsertedRight));
        Assert.AreEqual(1, result.Stats.InsertedLines);
    }

    [TestMethod]
    public void DeletedLine_Detected()
    {
        string[] left  = ["a", "x", "b"];
        string[] right = ["a", "b"];

        var result = _algo.ComputeLines(left, right);

        Assert.IsTrue(result.Lines.Any(l => l.Kind == TextLineKind.DeletedLeft));
        Assert.AreEqual(1, result.Stats.DeletedLines);
    }

    [TestMethod]
    public void ModifiedLine_ProducesWordEdits()
    {
        string[] left  = ["hello world"];
        string[] right = ["hello earth"];

        var result = _algo.ComputeLines(left, right);

        var modifiedLines = result.Lines.Where(l => l.Kind == TextLineKind.Modified).ToList();
        Assert.IsTrue(modifiedLines.Count > 0, "Should detect modification");
    }

    [TestMethod]
    public void CompletelyDifferent_ProducesDiffOutput()
    {
        string[] left  = ["aaa", "bbb"];
        string[] right = ["xxx", "yyy"];

        var result = _algo.ComputeLines(left, right);

        // Should produce diff output (modified pairs or delete+insert)
        Assert.IsTrue(result.Lines.Count > 0);
        Assert.IsTrue(result.Stats.TotalLines > 0);
    }

    [TestMethod]
    public void LeftEmpty_AllInserted()
    {
        string[] left  = [];
        string[] right = ["a", "b", "c"];

        var result = _algo.ComputeLines(left, right);

        Assert.AreEqual(3, result.Stats.InsertedLines);
        Assert.AreEqual(0, result.Stats.DeletedLines);
    }

    [TestMethod]
    public void RightEmpty_AllDeleted()
    {
        string[] left  = ["a", "b", "c"];
        string[] right = [];

        var result = _algo.ComputeLines(left, right);

        // All left lines should appear as deleted (may be grouped into one edit)
        Assert.IsTrue(result.Stats.DeletedLines > 0);
        Assert.AreEqual(0, result.Stats.InsertedLines);
    }

    // ── Byte-level tests ────────────────────────────────────────────────────

    [TestMethod]
    public void ByteDiff_IdenticalInputs_NoRegions()
    {
        byte[] data = [0x01, 0x02, 0x03];
        var result = _algo.ComputeBytes(data, data);

        Assert.AreEqual(0, result.Stats.TotalRegions);
    }

    [TestMethod]
    public void ByteDiff_DetectsModification()
    {
        byte[] left  = [0x01, 0x02, 0x03];
        byte[] right = [0x01, 0xFF, 0x03];

        var result = _algo.ComputeBytes(left, right);

        Assert.IsTrue(result.Regions.Count > 0);
    }

    // ── Stats consistency ───────────────────────────────────────────────────

    [TestMethod]
    public void Stats_TotalLines_EqualsSum()
    {
        string[] left  = ["a", "b", "c", "d"];
        string[] right = ["a", "x", "d", "e"];

        var result = _algo.ComputeLines(left, right);
        var s = result.Stats;

        Assert.AreEqual(s.TotalLines,
            s.EqualLines + s.ModifiedLines + s.InsertedLines + s.DeletedLines);
    }
}
