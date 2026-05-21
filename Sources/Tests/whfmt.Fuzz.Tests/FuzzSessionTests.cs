// ==========================================================
// Project: whfmt.Fuzz.Tests
// File: FuzzSessionTests.cs
// Description: Unit tests for FuzzSession — multi-generation corpus management.
// ==========================================================

namespace WhfmtFuzz.Tests;

[TestClass]
public sealed class FuzzSessionTests
{
    private static FuzzStubCatalog PngCatalog() =>
        new FuzzStubCatalog().Register("Test PNG Fuzz", WhfmtFuzzFixtures.PngLikeWithFuzz,
            extensions: [".png"], formatId: "test-png-fuzz");

    // ── NextGeneration accumulates corpus ─────────────────────────────────────

    [TestMethod]
    public void NextGeneration_adds_variants_to_corpus()
    {
        var session = new FuzzSession(PngCatalog(), seed: 1);
        var data    = WhfmtFuzzFixtures.BuildPngFuzz();

        var gen1 = session.NextGeneration(data, "test.png", count: 3, forcedFormat: "Test PNG Fuzz");

        Assert.IsTrue(session.Corpus.Count > 0, "Corpus should have variants after gen 1");
        Assert.IsTrue(gen1.Count > 0);
    }

    [TestMethod]
    public void NextGeneration_increments_generation_counter()
    {
        var session = new FuzzSession(PngCatalog(), seed: 2);
        var data    = WhfmtFuzzFixtures.BuildPngFuzz();

        Assert.AreEqual(0, session.Generation);
        session.NextGeneration(data, "test.png", count: 2, forcedFormat: "Test PNG Fuzz");
        Assert.AreEqual(1, session.Generation);
        session.NextGeneration(data, "test.png", count: 2, forcedFormat: "Test PNG Fuzz");
        Assert.AreEqual(2, session.Generation);
    }

    [TestMethod]
    public void NextGeneration_two_generations_accumulate()
    {
        var session = new FuzzSession(PngCatalog(), seed: 3);
        var data    = WhfmtFuzzFixtures.BuildPngFuzz();

        var g1 = session.NextGeneration(data, "test.png", count: 3, forcedFormat: "Test PNG Fuzz");
        var g2 = session.NextGeneration(data, "test.png", count: 3, forcedFormat: "Test PNG Fuzz");

        Assert.AreEqual(g1.Count + g2.Count, session.Corpus.Count,
            "Corpus should contain variants from both generations");
    }

    // ── Corpus indexing ───────────────────────────────────────────────────────

    [TestMethod]
    public void Corpus_indexes_are_sequential_across_generations()
    {
        var session = new FuzzSession(PngCatalog(), seed: 4);
        var data    = WhfmtFuzzFixtures.BuildPngFuzz();

        session.NextGeneration(data, "test.png", count: 3, forcedFormat: "Test PNG Fuzz");
        session.NextGeneration(data, "test.png", count: 3, forcedFormat: "Test PNG Fuzz");

        for (int i = 0; i < session.Corpus.Count; i++)
            Assert.AreEqual(i, session.Corpus[i].Index, $"Corpus index mismatch at position {i}");
    }

    // ── SaveCorpus / LoadCorpusAsync round-trip ───────────────────────────────

    [TestMethod]
    public async Task SaveCorpus_and_LoadCorpusAsync_roundtrip()
    {
        var dir     = Path.Combine(Path.GetTempPath(), $"whfmt_fuzz_test_{Guid.NewGuid():N}");
        var cat     = PngCatalog();
        var session = new FuzzSession(cat, seed: 5);
        var data    = WhfmtFuzzFixtures.BuildPngFuzz();

        try
        {
            session.NextGeneration(data, "test.png", count: 3, forcedFormat: "Test PNG Fuzz");
            await session.SaveCorpusAsync(dir);

            Assert.IsTrue(File.Exists(Path.Combine(dir, "manifest.json")), "manifest.json must exist");

            var loaded = await FuzzSession.LoadCorpusAsync(cat, dir, seed: 5);
            Assert.IsTrue(loaded.Corpus.Count > 0, "Loaded session should have variants");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveCorpus_writes_variant_files()
    {
        var dir     = Path.Combine(Path.GetTempPath(), $"whfmt_fuzz_test_{Guid.NewGuid():N}");
        var session = new FuzzSession(PngCatalog(), seed: 6);
        var data    = WhfmtFuzzFixtures.BuildPngFuzz();

        try
        {
            session.NextGeneration(data, "test.png", count: 5, forcedFormat: "Test PNG Fuzz");
            await session.SaveCorpusAsync(dir);

            var files = Directory.GetFiles(dir, "*.png");
            Assert.IsTrue(files.Length > 0, "Should write at least one .png variant file");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadCorpusAsync_missing_directory_returns_empty_session()
    {
        var cat     = PngCatalog();
        var missing = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}");
        var loaded  = await FuzzSession.LoadCorpusAsync(cat, missing);

        Assert.AreEqual(0, loaded.Corpus.Count);
    }

    // ── Reproducibility with fixed seed ──────────────────────────────────────

    [TestMethod]
    public void Fixed_seed_produces_same_corpus_across_sessions()
    {
        var data = WhfmtFuzzFixtures.BuildPngFuzz();

        var s1 = new FuzzSession(PngCatalog(), seed: 777);
        s1.NextGeneration(data, "test.png", count: 5, forcedFormat: "Test PNG Fuzz");

        var s2 = new FuzzSession(PngCatalog(), seed: 777);
        s2.NextGeneration(data, "test.png", count: 5, forcedFormat: "Test PNG Fuzz");

        Assert.AreEqual(s1.Corpus.Count, s2.Corpus.Count, "Same seed must produce same count");
        for (int i = 0; i < s1.Corpus.Count; i++)
            CollectionAssert.AreEqual(s1.Corpus[i].Data, s2.Corpus[i].Data,
                $"Corpus variant {i} differs between sessions with same seed");
    }
}
