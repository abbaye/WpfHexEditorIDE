// ==========================================================
// Project: whfmt.Fuzz.Tests
// File: FuzzVariantTests.cs
// Description: Unit tests for FuzzVariant model and MutationType enum.
// ==========================================================

namespace WhfmtFuzz.Tests;

[TestClass]
public sealed class FuzzVariantTests
{
    // ── FuzzVariant model ─────────────────────────────────────────────────────

    [TestMethod]
    public void FuzzVariant_IsError_false_when_no_error()
    {
        var v = new FuzzVariant { Data = [0x01, 0x02] };
        Assert.IsFalse(v.IsError);
    }

    [TestMethod]
    public void FuzzVariant_IsError_true_when_error_set()
    {
        var v = new FuzzVariant { Error = "something failed" };
        Assert.IsTrue(v.IsError);
    }

    [TestMethod]
    public void FuzzVariant_error_variant_from_unknown_format_has_error()
    {
        // FormatFuzzer.Generate returns an error variant when catalog is empty
        var cat      = new FuzzStubCatalog();
        var data     = WhfmtFuzzFixtures.BuildSimple();
        var variants = FormatFuzzer.Generate(cat, data, "input.png");
        Assert.AreEqual(1, variants.Count);
        Assert.IsTrue(variants[0].IsError);
        Assert.IsNotNull(variants[0].Error);
        Assert.AreEqual(0, variants[0].Data.Length);
    }

    [TestMethod]
    public void FuzzVariant_SuggestedFileName_contains_index_and_strategy()
    {
        var v = new FuzzVariant
        {
            Index        = 7,
            OriginalFile = "test.png",
            Strategy     = "BitFlip",
        };
        StringAssert.Contains(v.SuggestedFileName, "0007");
        StringAssert.Contains(v.SuggestedFileName, "BitFlip");
        StringAssert.Contains(v.SuggestedFileName, ".png");
    }

    [TestMethod]
    public void FuzzVariant_SuggestedFileName_preserves_extension()
    {
        var v = new FuzzVariant { OriginalFile = "sample.bin", Strategy = "ZeroField", Index = 0 };
        Assert.IsTrue(v.SuggestedFileName.EndsWith(".bin"), "Extension should be preserved");
    }

    // ── MutationType enum completeness ────────────────────────────────────────

    [TestMethod]
    public void MutationType_has_12_values()
    {
        var values = Enum.GetValues<MutationType>();
        Assert.AreEqual(12, values.Length, "Expected 12 mutation types");
    }

    [TestMethod]
    public void MutationType_all_expected_strategies_present()
    {
        var names = Enum.GetNames<MutationType>().ToHashSet();
        foreach (var expected in new[]
        {
            "BoundaryValues", "EnumSweep", "CorruptSignature", "BitFlip",
            "ZeroField", "Overflow", "RandomBytes", "Truncate",
            "Duplicate", "InsertBytes", "SliceRepeat", "NegateField"
        })
        {
            Assert.IsTrue(names.Contains(expected), $"MutationType.{expected} missing");
        }
    }

    // ── MutationLogEntry ──────────────────────────────────────────────────────

    [TestMethod]
    public void MutationLogEntry_stores_mutation_and_field()
    {
        var entry = new MutationLogEntry
        {
            Mutation    = MutationType.BitFlip,
            Field       = "image_width",
            Description = "Flip a bit",
        };
        Assert.AreEqual(MutationType.BitFlip, entry.Mutation);
        Assert.AreEqual("image_width", entry.Field);
    }
}
