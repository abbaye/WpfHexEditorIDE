// ==========================================================
// Project: whfmt.Analysis.Tests
// File: FormatDiffTests.cs
// Description: Unit tests for FormatDiff — field-level semantic comparison.
// ==========================================================

namespace WhfmtAnalysis.Tests;

[TestClass]
public sealed class FormatDiffTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StubCatalog PngCatalog() =>
        new StubCatalog().Register("Test PNG-like", WhfmtAnalysisFixtures.PngLike,
            extensions: [".png"], category: "image", formatId: "test-png");

    // ── Identity ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void Compare_identical_files_returns_IsIdentical_true()
    {
        var cat  = PngCatalog();
        var data = WhfmtAnalysisFixtures.BuildPngLike();
        var r    = FormatDiff.Compare(cat, data, "a.png", data, "b.png", "Test PNG-like");

        Assert.IsTrue(r.IsIdentical);
        Assert.AreEqual(0, r.ChangedCount);
        Assert.IsNull(r.Error);
    }

    // ── Changed field ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Compare_different_width_reports_image_width_changed()
    {
        var cat  = PngCatalog();
        var a    = WhfmtAnalysisFixtures.BuildPngLike(width: 640);
        var b    = WhfmtAnalysisFixtures.BuildPngLike(width: 1920);
        var r    = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        Assert.IsFalse(r.IsIdentical);
        var widthChange = r.FieldChanges.FirstOrDefault(f => f.FieldName == "image_width");
        Assert.IsNotNull(widthChange, "image_width change should be reported");
        Assert.IsTrue(widthChange.IsChanged);
    }

    [TestMethod]
    public void Compare_different_color_type_reports_change()
    {
        var cat = PngCatalog();
        var a   = WhfmtAnalysisFixtures.BuildPngLike(colorType: 2);  // RGB
        var b   = WhfmtAnalysisFixtures.BuildPngLike(colorType: 6);  // RGBA
        var r   = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        var ct = r.FieldChanges.First(f => f.FieldName == "color_type");
        Assert.IsTrue(ct.IsChanged);
    }

    // ── Ignored field ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Compare_padding_change_is_marked_ignored()
    {
        var cat = PngCatalog();
        var a   = WhfmtAnalysisFixtures.BuildPngLike(padding: [0x00, 0x00, 0x00]);
        var b   = WhfmtAnalysisFixtures.BuildPngLike(padding: [0xFF, 0xFF, 0xFF]);
        var r   = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        var pad = r.FieldChanges.First(f => f.FieldName == "padding");
        Assert.IsTrue(pad.IsIgnored, "padding should be in ignoreFields");
    }

    // ── Size delta ────────────────────────────────────────────────────────────

    [TestMethod]
    public void Compare_tracks_raw_byte_delta()
    {
        var cat = PngCatalog();
        var a   = WhfmtAnalysisFixtures.BuildPngLike();               // 20 bytes
        var baseB = WhfmtAnalysisFixtures.BuildPngLike();
        var b     = new byte[baseB.Length + 2];
        baseB.CopyTo(b, 0); b[baseB.Length] = 0xAA; b[baseB.Length + 1] = 0xBB; // 22 bytes
        var r = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        Assert.AreEqual(2, r.RawByteDelta);
        Assert.AreEqual(20, r.SizeA);
        Assert.AreEqual(22, r.SizeB);
    }

    // ── Checksum validation ───────────────────────────────────────────────────

    [TestMethod]
    public void Compare_valid_crc32_in_both_files_is_reported_valid()
    {
        var cat = PngCatalog();
        var a   = WhfmtAnalysisFixtures.BuildPngLike(correctCrc: true);
        var b   = WhfmtAnalysisFixtures.BuildPngLike(width: 800, correctCrc: true);
        var r   = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        Assert.AreEqual(0, r.CorruptedCountA, "A's CRC should be valid");
        Assert.AreEqual(0, r.CorruptedCountB, "B's CRC should be valid");
    }

    [TestMethod]
    public void Compare_corrupt_crc32_in_B_is_reported()
    {
        var cat = PngCatalog();
        var a   = WhfmtAnalysisFixtures.BuildPngLike(correctCrc: true);
        var b   = WhfmtAnalysisFixtures.BuildPngLike(correctCrc: false);
        var r   = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        Assert.AreEqual(0, r.CorruptedCountA);
        Assert.AreEqual(1, r.CorruptedCountB, "B has a bad CRC32");
    }

    // ── Structural diff ───────────────────────────────────────────────────────

    [TestMethod]
    public void Compare_structural_diff_populated()
    {
        var cat = PngCatalog();
        var a   = WhfmtAnalysisFixtures.BuildPngLike();
        var b   = WhfmtAnalysisFixtures.BuildPngLike(width: 100);
        var r   = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        Assert.IsNotNull(r.StructuralDiff);
    }

    [TestMethod]
    public void Compare_identical_files_structural_in_both_not_empty()
    {
        var cat  = PngCatalog();
        var data = WhfmtAnalysisFixtures.BuildPngLike();
        var r    = FormatDiff.Compare(cat, data, "a.png", data, "b.png", "Test PNG-like");

        Assert.IsNotNull(r.StructuralDiff);
        Assert.AreEqual(0, r.StructuralDiff.OnlyInA.Count);
        Assert.AreEqual(0, r.StructuralDiff.OnlyInB.Count);
    }

    // ── Unknown format ────────────────────────────────────────────────────────

    [TestMethod]
    public void Compare_unknown_format_returns_error_result()
    {
        var cat  = new StubCatalog(); // empty catalog
        var data = WhfmtAnalysisFixtures.BuildPngLike();
        var r    = FormatDiff.Compare(cat, data, "a.png", data, "b.png");

        Assert.IsNotNull(r.Error);
    }

    // ── HexDiff ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Compare_hex_diff_populated_for_changed_field()
    {
        var cat = PngCatalog();
        var a   = WhfmtAnalysisFixtures.BuildPngLike(width: 640);
        var b   = WhfmtAnalysisFixtures.BuildPngLike(width: 1280);
        var r   = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        var widthChange = r.FieldChanges.First(f => f.FieldName == "image_width");
        Assert.IsNotNull(widthChange.HexDiff, "HexDiff should be populated for changed field");
        Assert.IsTrue(widthChange.HexDiff.DifferentBytes > 0);
    }

    // ── Async overload ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CompareAsync_stream_returns_same_result_as_sync()
    {
        var cat  = PngCatalog();
        var a    = WhfmtAnalysisFixtures.BuildPngLike();
        var b    = WhfmtAnalysisFixtures.BuildPngLike(width: 800);
        var sync = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        await using var msA = new MemoryStream(a);
        await using var msB = new MemoryStream(b);
        var async = await FormatDiff.CompareAsync(cat, msA, "a.png", msB, "b.png", "Test PNG-like");

        Assert.AreEqual(sync.IsIdentical,     async.IsIdentical);
        Assert.AreEqual(sync.ChangedCount,    async.ChangedCount);
        Assert.AreEqual(sync.RawByteDelta,    async.RawByteDelta);
    }

    // ── KeyFields / IgnoreFields populated ───────────────────────────────────

    [TestMethod]
    public void Compare_keyfields_and_ignorefields_populated_from_diff_block()
    {
        var cat = PngCatalog();
        var d   = WhfmtAnalysisFixtures.BuildPngLike();
        var r   = FormatDiff.Compare(cat, d, "a.png", d, "b.png", "Test PNG-like");

        CollectionAssert.Contains(r.KeyFields.ToList(), "image_width");
        CollectionAssert.Contains(r.KeyFields.ToList(), "magic");
        CollectionAssert.Contains(r.IgnoreFields.ToList(), "padding");
        Assert.AreEqual("category", r.GroupBy);
    }

    // ── Magic field unchanged ─────────────────────────────────────────────────

    [TestMethod]
    public void Compare_magic_field_unchanged_when_same_signature()
    {
        var cat = PngCatalog();
        var a   = WhfmtAnalysisFixtures.BuildPngLike();
        var b   = WhfmtAnalysisFixtures.BuildPngLike(height: 240);
        var r   = FormatDiff.Compare(cat, a, "a.png", b, "b.png", "Test PNG-like");

        var magic = r.FieldChanges.First(f => f.FieldName == "magic");
        Assert.IsFalse(magic.IsChanged, "magic bytes should be identical");
    }
}
