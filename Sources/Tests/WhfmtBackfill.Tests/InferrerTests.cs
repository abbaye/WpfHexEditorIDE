using WhfmtBackfill.Inferrers;
using WhfmtBackfill.Models;

namespace WhfmtBackfill.Tests;

[TestClass]
public sealed class DiffInferrerTests
{
    [TestMethod]
    public void Infer_with_signature_yields_keyfield()
    {
        var summary = new WhfmtSummary
        {
            FormatId   = "PNG",
            Category   = "Images",
            FormatName = "Portable Network Graphics",
            Blocks =
            [
                new BlockInfo("signature", "PNG Signature", "pngSignature", 0, 8, "hex",   IsSignature: true,  HasValueMap: false, HasBitfields: false, ExpectedValue: "0x89504E470D0A1A0A"),
                new BlockInfo("field",     "Image Width",   "imageWidth",  16, 4, "uint32", IsSignature: false, HasValueMap: false, HasBitfields: false, ExpectedValue: null),
                new BlockInfo("field",     "Color Type",    "colorType",   25, 1, "uint8",  IsSignature: false, HasValueMap: true,  HasBitfields: true,  ExpectedValue: null),
                new BlockInfo("field",     "Modification Time", "modTime",  4, 8, "uint64", IsSignature: false, HasValueMap: false, HasBitfields: false, ExpectedValue: null),
            ],
        };

        var diff = DiffInferrer.Infer(summary);
        Assert.IsNotNull(diff);
        CollectionAssert.Contains(diff!.KeyFields.ToList(),    "png_signature");
        CollectionAssert.Contains(diff.KeyFields.ToList(),     "image_width");
        CollectionAssert.Contains(diff.KeyFields.ToList(),     "color_type");
        CollectionAssert.Contains(diff.IgnoreFields.ToList(),  "mod_time");
    }

    [TestMethod]
    public void Infer_without_meaningful_blocks_returns_null()
    {
        var summary = new WhfmtSummary
        {
            FormatId   = "X",
            Category   = "Other",
            FormatName = "X",
            Blocks     = [], // no blocks
        };

        Assert.IsNull(DiffInferrer.Infer(summary));
    }

    [TestMethod]
    public void Infer_includes_checksum_algorithm_as_keyfield()
    {
        var summary = new WhfmtSummary
        {
            FormatId   = "PNG",
            Category   = "Images",
            FormatName = "PNG",
            Blocks =
            [
                new BlockInfo("signature", "Sig", "sig", 0, 4, "hex", true, false, false, "0xDEADBEEF"),
            ],
            Checksums = [new ChecksumInfo("crc32", 100, 4)],
        };

        var diff = DiffInferrer.Infer(summary);
        Assert.IsNotNull(diff);
        CollectionAssert.Contains(diff!.KeyFields.ToList(), "crc32");
    }
}

[TestClass]
public sealed class RepairInferrerTests
{
    [TestMethod]
    public void Infer_emits_set_value_for_signature_with_hex_expected()
    {
        var summary = new WhfmtSummary
        {
            FormatId = "PNG", Category = "Images", FormatName = "PNG",
            Blocks =
            [
                new BlockInfo("signature", "PNG sig", "pngSig", 0, 8, "hex", true, false, false, "0x89504E470D0A1A0A"),
            ],
        };

        var rules = RepairInferrer.Infer(summary);
        Assert.AreEqual(1, rules.Count);
        Assert.AreEqual("FixSignature",       rules[0].Name);
        Assert.AreEqual("set_value",          rules[0].Action);
        Assert.AreEqual("89504E470D0A1A0A",   rules[0].Value);
        Assert.AreEqual(8,                    rules[0].Length);
    }

    [TestMethod]
    public void Infer_emits_recompute_for_each_known_checksum()
    {
        var summary = new WhfmtSummary
        {
            FormatId = "X", Category = "Y", FormatName = "X",
            Checksums =
            [
                new ChecksumInfo("crc32",  100, 4),
                new ChecksumInfo("sha256", 200, 32),
            ],
        };

        var rules = RepairInferrer.Infer(summary);
        Assert.AreEqual(2, rules.Count);
        Assert.IsTrue(rules.Any(r => r.Algorithm == "crc32"  && r.Action == "recompute_checksum"));
        Assert.IsTrue(rules.Any(r => r.Algorithm == "sha256" && r.Action == "recompute_checksum"));
    }

    [TestMethod]
    public void Infer_skips_nonhex_signature_expected_value()
    {
        var summary = new WhfmtSummary
        {
            FormatId = "X", Category = "Y", FormatName = "X",
            Blocks = [new BlockInfo("signature", "S", "s", 0, 4, "ascii", true, false, false, "IHDR")],
        };
        var rules = RepairInferrer.Infer(summary);
        Assert.AreEqual(0, rules.Count, "ASCII expected value should not produce a set_value rule");
    }

    [TestMethod]
    public void Infer_skips_unknown_checksum_algorithms()
    {
        var summary = new WhfmtSummary
        {
            FormatId = "X", Category = "Y", FormatName = "X",
            Checksums = [new ChecksumInfo("xor8", 0, 1)],
        };
        Assert.AreEqual(0, RepairInferrer.Infer(summary).Count);
    }
}

[TestClass]
public sealed class FuzzInferrerTests
{
    [TestMethod]
    public void Infer_includes_corrupt_signature_and_enum_sweep_and_boundary()
    {
        var summary = new WhfmtSummary
        {
            FormatId   = "PNG",
            Category   = "Images",
            FormatName = "PNG",
            Blocks =
            [
                new BlockInfo("signature", "PNG sig",   "pngSig",     0,  8, "hex",    true,  false, false, "0xDEADBEEF"),
                new BlockInfo("field",     "Width",     "imageWidth", 16, 4, "uint32", false, false, false, null),
                new BlockInfo("field",     "ColorType", "colorType",  25, 1, "uint8",  false, true,  false, null),
            ],
            Checksums = [new ChecksumInfo("crc32", 100, 4)],
        };

        var fuzz = FuzzInferrer.Infer(summary);
        Assert.IsNotNull(fuzz);
        var byMutation = fuzz!.Strategies.GroupBy(s => s.Mutation).ToDictionary(g => g.Key, g => g.ToList());
        Assert.IsTrue(byMutation.ContainsKey("corrupt_signature"));
        Assert.IsTrue(byMutation.ContainsKey("enum_sweep"));
        Assert.IsTrue(byMutation.ContainsKey("boundary_values"));
        Assert.IsTrue(byMutation.ContainsKey("random_bytes"));

        Assert.IsTrue(fuzz.PreserveChecksums);
    }

    [TestMethod]
    public void Infer_returns_null_when_no_fuzzable_fields()
    {
        var summary = new WhfmtSummary { FormatId = "X", Category = "Y", FormatName = "X" };
        Assert.IsNull(FuzzInferrer.Infer(summary));
    }
}
