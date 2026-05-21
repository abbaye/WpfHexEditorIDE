// Project      : whfmt.SourceGenerator.Tests
// File         : PolyfillAndEdgeCaseTests.cs
// Description  : Tests for the netstandard2.0 polyfills and edge-case behaviors.

using WhfmtCodeGen.Generator;

namespace whfmt.SourceGenerator.Tests;

[TestClass]
public class PolyfillAndEdgeCaseTests
{
    // ── Convert.FromHexString polyfill ────────────────────────────────────────

    [TestMethod]
    public void HexPolyfill_EvenLengthHex_ReturnsCorrectBytes()
    {
        var bytes = Convert.FromHexString("89504E47");
        CollectionAssert.AreEqual(
            new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            bytes,
            "FromHexString should decode hex pairs correctly.");
    }

    [TestMethod]
    public void HexPolyfill_AllZeroes_ReturnsZeroBytes()
    {
        var bytes = Convert.FromHexString("0000");
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00 }, bytes);
    }

    [TestMethod]
    public void HexPolyfill_AllFF_Returns0xFFBytes()
    {
        var bytes = Convert.FromHexString("FFFF");
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF }, bytes);
    }

    [TestMethod]
    public void HexPolyfill_EmptyString_ReturnsEmptyArray()
    {
        var bytes = Convert.FromHexString("");
        Assert.AreEqual(0, bytes.Length, "Empty hex string should return empty array.");
    }

    // ── Generator — source file naming ────────────────────────────────────────

    [TestMethod]
    public void GeneratedSourceFile_IsNamedAfterClass()
    {
        var result = GeneratorTestHelper.Run(
            "myformat.whfmt", WhfmtFixtures.Simple,
            new() { ["build_metadata.AdditionalFiles.WhfmtClass"] = "ZipFormat" });

        Assert.AreEqual(1, result.GeneratedTrees.Length);
        var hint = result.GeneratedTrees[0].FilePath;
        StringAssert.Contains(hint, "ZipFormat",
            "Generated source hint path should include the class name.");
    }

    // ── Generator — namespace in generated file ────────────────────────────────

    [TestMethod]
    public void GeneratedSource_AlwaysHasNamespaceDeclaration()
    {
        var result = GeneratorTestHelper.Run("Simple.whfmt", WhfmtFixtures.Simple);

        StringAssert.Contains(result.GeneratedTrees[0].ToString(), "namespace ",
            "All generated sources must declare a namespace.");
    }

    // ── Generator — big-endian field handling ─────────────────────────────────

    [TestMethod]
    public void BigEndianField_GeneratedSource_ContainsReverseBytes()
    {
        // WithSignatureAndChecksum has Width/Height as big-endian uint32
        var result = GeneratorTestHelper.Run("PNGLike.whfmt", WhfmtFixtures.WithSignatureAndChecksum);

        var source = result.GeneratedTrees[0].ToString();
        // Big-endian fields trigger a byte-reversal helper in the generator
        Assert.IsTrue(
            source.Contains("Reverse") || source.Contains("BinaryPrimitives") || source.Contains("swap"),
            "Big-endian fields should produce byte-reversal logic in the generated parser.");
    }

    // ── GeneratorTestHelper — non-.whfmt extension combinations ───────────────

    [TestMethod]
    [DataRow("config.json")]
    [DataRow("data.bin")]
    [DataRow("format.txt")]
    public void NonWhfmtExtensions_AreIgnored(string filename)
    {
        var result = GeneratorTestHelper.Run(filename, WhfmtFixtures.Simple);

        Assert.AreEqual(0, result.GeneratedTrees.Length,
            $"File '{filename}' should not trigger generation.");
    }

    // ── Fixture sanity — all fixtures are valid JSON (except Malformed/Empty) ──

    [TestMethod]
    [DataRow(nameof(WhfmtFixtures.Simple),                  WhfmtFixtures.Simple)]
    [DataRow(nameof(WhfmtFixtures.WithSignatureAndChecksum), WhfmtFixtures.WithSignatureAndChecksum)]
    [DataRow(nameof(WhfmtFixtures.WithEnum),                 WhfmtFixtures.WithEnum)]
    [DataRow(nameof(WhfmtFixtures.WithRepeating),            WhfmtFixtures.WithRepeating)]
    public void Fixture_IsValidWhfmt_ProducesNoGeneratorError(string name, string fixture)
    {
        var result = GeneratorTestHelper.Run($"{name}.whfmt", fixture);

        Assert.AreEqual(0, result.Diagnostics.Length,
            $"Fixture '{name}' should not produce any generator diagnostics.");
        Assert.AreEqual(1, result.GeneratedTrees.Length,
            $"Fixture '{name}' should produce exactly one generated source.");
    }
}
