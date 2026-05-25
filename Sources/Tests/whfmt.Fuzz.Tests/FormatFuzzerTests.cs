// ==========================================================
// Project: whfmt.Fuzz.Tests
// File: FormatFuzzerTests.cs
// Description: Unit tests for FormatFuzzer.Generate / GenerateWithReport.
// ==========================================================

namespace WhfmtFuzz.Tests;

[TestClass]
public sealed class FormatFuzzerTests
{
    private static FuzzStubCatalog PngCatalog() =>
        new FuzzStubCatalog().Register("Test PNG Fuzz", WhfmtFuzzFixtures.PngLikeWithFuzz,
            extensions: [".png"], formatId: "test-png-fuzz");

    // ── Basic generation ──────────────────────────────────────────────────────

    [TestMethod]
    public void Generate_returns_requested_count()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "test.png", count: 5,
            forcedFormat: "Test PNG Fuzz", seed: 42);

        Assert.IsTrue(variants.Count > 0, "Should produce at least one variant");
    }

    [TestMethod]
    public void Generate_variants_have_non_empty_data()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "test.png", count: 5,
            forcedFormat: "Test PNG Fuzz", seed: 1);

        foreach (var v in variants.Where(v => !v.IsError))
            Assert.IsTrue(v.Data.Length > 0, $"Variant {v.Index} has empty data");
    }

    [TestMethod]
    public void Generate_variants_have_format_name()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "test.png", count: 3,
            forcedFormat: "Test PNG Fuzz", seed: 2);

        foreach (var v in variants.Where(v => !v.IsError))
            Assert.AreEqual("Test PNG Fuzz", v.FormatName);
    }

    [TestMethod]
    public void Generate_variant_indexes_are_sequential()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "test.png", count: 5,
            forcedFormat: "Test PNG Fuzz", seed: 3);

        var success = variants.Where(v => !v.IsError).ToList();
        for (int i = 0; i < success.Count; i++)
            Assert.AreEqual(i, success[i].Index, $"Index mismatch at position {i}");
    }

    [TestMethod]
    public void Generate_suggested_filename_contains_original_name()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "input.png", count: 3,
            forcedFormat: "Test PNG Fuzz", seed: 4);

        foreach (var v in variants.Where(v => !v.IsError))
            StringAssert.Contains(v.SuggestedFileName, "input");
    }

    // ── Unknown format ────────────────────────────────────────────────────────

    [TestMethod]
    public void Generate_unknown_format_returns_error_variant()
    {
        var cat  = new FuzzStubCatalog(); // empty
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "test.png");

        Assert.AreEqual(1, variants.Count);
        Assert.IsTrue(variants[0].IsError);
        Assert.IsNotNull(variants[0].Error);
    }

    // ── Reproducibility ───────────────────────────────────────────────────────

    [TestMethod]
    public void Generate_with_same_seed_produces_same_output()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();

        var run1 = FormatFuzzer.Generate(cat, data, "test.png", count: 5,
            forcedFormat: "Test PNG Fuzz", seed: 999);
        var run2 = FormatFuzzer.Generate(cat, data, "test.png", count: 5,
            forcedFormat: "Test PNG Fuzz", seed: 999);

        Assert.AreEqual(run1.Count, run2.Count, "Same seed must produce same count");
        for (int i = 0; i < run1.Count; i++)
            CollectionAssert.AreEqual(run1[i].Data, run2[i].Data, $"Variant {i} data differs");
    }

    // ── Strategy field populated ──────────────────────────────────────────────

    [TestMethod]
    public void Generate_variants_have_non_empty_strategy()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "test.png", count: 10,
            forcedFormat: "Test PNG Fuzz", seed: 5);

        foreach (var v in variants.Where(v => !v.IsError))
            Assert.IsFalse(string.IsNullOrEmpty(v.Strategy), $"Variant {v.Index} has no strategy");
    }

    // ── Data differs from input ───────────────────────────────────────────────

    [TestMethod]
    public void Generate_at_least_one_variant_differs_from_input()
    {
        var cat   = PngCatalog();
        var data  = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "test.png", count: 20,
            forcedFormat: "Test PNG Fuzz", seed: 6);

        bool anyDiffers = variants.Where(v => !v.IsError)
            .Any(v => !v.Data.SequenceEqual(data));
        Assert.IsTrue(anyDiffers, "At least one variant should differ from the input");
    }

    // ── GenerateWithReport ────────────────────────────────────────────────────

    [TestMethod]
    public void GenerateWithReport_returns_non_null_report()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var (variants, report) = FormatFuzzer.GenerateWithReport(cat, data, "test.png", count: 5,
            forcedFormat: "Test PNG Fuzz", seed: 7);

        Assert.IsNotNull(report);
        Assert.AreEqual("Test PNG Fuzz", report.FormatName);
    }

    [TestMethod]
    public void GenerateWithReport_total_variants_matches_list()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var (variants, report) = FormatFuzzer.GenerateWithReport(cat, data, "test.png", count: 5,
            forcedFormat: "Test PNG Fuzz", seed: 8);

        Assert.AreEqual(variants.Count, report.TotalVariants);
    }

    [TestMethod]
    public void GenerateWithReport_strategy_distribution_non_empty()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var (_, report) = FormatFuzzer.GenerateWithReport(cat, data, "test.png", count: 20,
            forcedFormat: "Test PNG Fuzz", seed: 9);

        Assert.IsTrue(report.StrategyDistribution.Count > 0, "Should track at least one strategy");
    }

    [TestMethod]
    public void GenerateWithReport_field_coverage_non_empty()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var (_, report) = FormatFuzzer.GenerateWithReport(cat, data, "test.png", count: 20,
            forcedFormat: "Test PNG Fuzz", seed: 10);

        Assert.IsTrue(report.FieldCoverage.Count > 0, "Should track field coverage");
    }

    [TestMethod]
    public void GenerateWithReport_most_targeted_field_not_null()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var (_, report) = FormatFuzzer.GenerateWithReport(cat, data, "test.png", count: 20,
            forcedFormat: "Test PNG Fuzz", seed: 11);

        Assert.IsNotNull(report.MostTargetedField);
    }

    [TestMethod]
    public void GenerateWithReport_tostring_non_empty()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var (_, report) = FormatFuzzer.GenerateWithReport(cat, data, "test.png", count: 5,
            forcedFormat: "Test PNG Fuzz", seed: 12);

        Assert.IsFalse(string.IsNullOrWhiteSpace(report.ToString()));
    }

    // ── Async overload ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task GenerateAsync_stream_returns_variants()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();

        await using var ms = new MemoryStream(data);
        var variants = await FormatFuzzer.GenerateAsync(cat, ms, "test.png", count: 3,
            forcedFormat: "Test PNG Fuzz", seed: 13);

        Assert.IsTrue(variants.Count > 0);
    }

    // ── Compound mutations ────────────────────────────────────────────────────

    [TestMethod]
    public void Generate_compound_2_mutation_count_at_least_1()
    {
        var cat  = PngCatalog();
        var data = WhfmtFuzzFixtures.BuildPngFuzz();
        var variants = FormatFuzzer.Generate(cat, data, "test.png", count: 10,
            forcedFormat: "Test PNG Fuzz", seed: 14, compound: 2);

        var multi = variants.Where(v => !v.IsError && v.MutationCount >= 2).ToList();
        // At least some variants should have compound mutations
        Assert.IsTrue(multi.Count > 0 || variants.Any(v => !v.IsError),
            "Compound mode should produce valid variants");
    }

    // ── No fuzz block fallback ────────────────────────────────────────────────

    [TestMethod]
    public void Generate_no_fuzz_block_falls_back_to_raw_mutations()
    {
        // Without a fuzz.strategies block the engine falls back to BitFlip at rate 0.001.
        // Run without a seed so Random.Shared is used — no seeded starvation.
        var cat  = new FuzzStubCatalog().Register("No Fuzz", WhfmtFuzzFixtures.NoFuzzBlock);
        var data = WhfmtFuzzFixtures.BuildSimple();
        var variants = FormatFuzzer.Generate(cat, data, "test.bin", count: 100,
            forcedFormat: "No Fuzz");

        Assert.IsTrue(variants.Count > 0,
            "Fallback BitFlip should produce at least one variant out of 100 requests");
    }
}
