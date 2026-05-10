using WhfmtBackfill.Inferrers;

namespace WhfmtBackfill.Tests;

[TestClass]
public sealed class NameNormalizerTests
{
    [TestMethod]
    public void ToSnakeCase_camelCase_splits_correctly()
    {
        Assert.AreEqual("image_width",  NameNormalizer.ToSnakeCase("imageWidth"));
        Assert.AreEqual("ihdr_chunk_length", NameNormalizer.ToSnakeCase("ihdrChunkLength"));
        Assert.AreEqual("png_signature", NameNormalizer.ToSnakeCase("pngSignature"));
    }

    [TestMethod]
    public void ToSnakeCase_PascalCase_handles_acronyms()
    {
        Assert.AreEqual("crc32_valid", NameNormalizer.ToSnakeCase("CRC32Valid"));
        Assert.AreEqual("ihdr_crc",    NameNormalizer.ToSnakeCase("IHDRCrc"));
    }

    [TestMethod]
    public void ToSnakeCase_handles_spaces_and_hyphens()
    {
        Assert.AreEqual("primary_chunk_id", NameNormalizer.ToSnakeCase("Primary Chunk Id"));
        Assert.AreEqual("dash_case",        NameNormalizer.ToSnakeCase("dash-case"));
    }

    [TestMethod]
    public void IsLikelyIgnored_detects_volatile_metadata()
    {
        Assert.IsTrue (NameNormalizer.IsLikelyIgnored("modification_time"));
        Assert.IsTrue (NameNormalizer.IsLikelyIgnored("creation_date"));
        Assert.IsTrue (NameNormalizer.IsLikelyIgnored("padding"));
        Assert.IsTrue (NameNormalizer.IsLikelyIgnored("reserved_bytes"));
        Assert.IsTrue (NameNormalizer.IsLikelyIgnored("created_at"));

        Assert.IsFalse(NameNormalizer.IsLikelyIgnored("image_width"));
        Assert.IsFalse(NameNormalizer.IsLikelyIgnored("crc32"));
    }

    [TestMethod]
    public void IsNumericMagnitude_detects_size_count_offset_dimensions()
    {
        Assert.IsTrue(NameNormalizer.IsNumericMagnitude("file_size"));
        Assert.IsTrue(NameNormalizer.IsNumericMagnitude("entry_count"));
        Assert.IsTrue(NameNormalizer.IsNumericMagnitude("data_offset"));
        Assert.IsTrue(NameNormalizer.IsNumericMagnitude("image_width"));

        Assert.IsFalse(NameNormalizer.IsNumericMagnitude("color_type"));
        Assert.IsFalse(NameNormalizer.IsNumericMagnitude("flags"));
    }

    [TestMethod]
    public void IsNumericValueType_recognizes_all_int_widths()
    {
        Assert.IsTrue (NameNormalizer.IsNumericValueType("uint8"));
        Assert.IsTrue (NameNormalizer.IsNumericValueType("int32"));
        Assert.IsTrue (NameNormalizer.IsNumericValueType("uint64"));

        Assert.IsFalse(NameNormalizer.IsNumericValueType("ascii"));
        Assert.IsFalse(NameNormalizer.IsNumericValueType("hex"));
    }
}
