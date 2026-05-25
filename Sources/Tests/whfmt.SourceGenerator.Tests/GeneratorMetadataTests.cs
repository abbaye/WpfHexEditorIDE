// Project      : whfmt.SourceGenerator.Tests
// File         : GeneratorMetadataTests.cs
// Description  : Tests for per-file MSBuild metadata options:
//                WhfmtNamespace, WhfmtClass, WhfmtAsync, WhfmtValidate, WhfmtLanguage.

using Microsoft.CodeAnalysis;

namespace whfmt.SourceGenerator.Tests;

[TestClass]
public class GeneratorMetadataTests
{
    // ── WhfmtNamespace ─────────────────────────────────────────────────────────

    [TestMethod]
    public void CustomNamespace_AppearsInGeneratedSource()
    {
        var result = GeneratorTestHelper.Run(
            "Simple.whfmt", WhfmtFixtures.Simple,
            new() { ["build_metadata.AdditionalFiles.WhfmtNamespace"] = "Acme.Parsers" });

        StringAssert.Contains(GetSource(result), "namespace Acme.Parsers",
            "Custom namespace must appear in generated source.");
    }

    [TestMethod]
    public void DefaultNamespace_IsWhfmtGenerated()
    {
        var result = GeneratorTestHelper.Run("Simple.whfmt", WhfmtFixtures.Simple);

        StringAssert.Contains(GetSource(result), "namespace WhfmtGenerated",
            "Default namespace should be 'WhfmtGenerated'.");
    }

    // ── WhfmtClass ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void CustomClassName_AppearsInGeneratedSource()
    {
        var result = GeneratorTestHelper.Run(
            "Simple.whfmt", WhfmtFixtures.Simple,
            new() { ["build_metadata.AdditionalFiles.WhfmtClass"] = "MyCustomParser" });

        StringAssert.Contains(GetSource(result), "MyCustomParser",
            "Custom class name must appear in generated source.");
    }

    [TestMethod]
    public void DefaultClassName_IsDerivedFromFileName()
    {
        var result = GeneratorTestHelper.Run("myformat.whfmt", WhfmtFixtures.Simple);

        // ToPascalCase("myformat") → "Myformat" (generator uses formatName from JSON or file name)
        // The class should contain "Myformat" as part of its name
        StringAssert.Contains(GetSource(result), "Myformat",
            "Default class name should start with PascalCase of the file name.");
    }

    // ── WhfmtAsync ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void AsyncTrue_GeneratesParseAsyncMethod()
    {
        var result = GeneratorTestHelper.Run(
            "Simple.whfmt", WhfmtFixtures.ForAsync,
            new() { ["build_metadata.AdditionalFiles.WhfmtAsync"] = "true" });

        StringAssert.Contains(GetSource(result), "ParseAsync",
            "WhfmtAsync=true should produce a ParseAsync method.");
    }

    [TestMethod]
    public void AsyncFalse_OmitsParseAsyncMethod()
    {
        var result = GeneratorTestHelper.Run(
            "Simple.whfmt", WhfmtFixtures.ForAsync,
            new() { ["build_metadata.AdditionalFiles.WhfmtAsync"] = "false" });

        Assert.IsFalse(GetSource(result).Contains("ParseAsync"),
            "WhfmtAsync=false must not produce a ParseAsync method.");
    }

    [TestMethod]
    public void AsyncDefault_OmitsParseAsyncMethod()
    {
        // Default is false when metadata key is absent
        var result = GeneratorTestHelper.Run("Simple.whfmt", WhfmtFixtures.Simple);

        Assert.IsFalse(GetSource(result).Contains("ParseAsync"),
            "Default (no WhfmtAsync key) must not produce a ParseAsync method.");
    }

    // ── WhfmtValidate ──────────────────────────────────────────────────────────

    [TestMethod]
    public void ValidateTrue_IsDefaultBehavior()
    {
        // Default validate = true — signature fixture should produce exception class
        var result = GeneratorTestHelper.Run("PNGLike.whfmt", WhfmtFixtures.WithSignatureAndChecksum);

        StringAssert.Contains(GetSource(result), "InvalidSignatureException",
            "Validation is enabled by default — exception class must be present.");
    }

    [TestMethod]
    public void ValidateFalseString_DisablesValidation()
    {
        var result = GeneratorTestHelper.Run(
            "PNGLike.whfmt", WhfmtFixtures.WithSignatureAndChecksum,
            new() { ["build_metadata.AdditionalFiles.WhfmtValidate"] = "False" }); // case-insensitive

        Assert.IsFalse(GetSource(result).Contains("InvalidSignatureException"),
            "Validate=False (case-insensitive) must disable exception class generation.");
    }

    // ── WhfmtLanguage ──────────────────────────────────────────────────────────

    [TestMethod]
    public void LanguageCSharp_IsDefault()
    {
        var result = GeneratorTestHelper.Run("Simple.whfmt", WhfmtFixtures.Simple);

        var source = GetSource(result);
        Assert.IsFalse(source.Contains("ref struct"),
            "Default language (CSharp) must not produce ref struct.");
        StringAssert.Contains(source, "class ", "Default language must produce a class.");
    }

    [TestMethod]
    public void LanguageCSharpSpan_ProducesRefStruct()
    {
        var result = GeneratorTestHelper.Run(
            "Simple.whfmt", WhfmtFixtures.Simple,
            new() { ["build_metadata.AdditionalFiles.WhfmtLanguage"] = "CSharpSpan" });

        StringAssert.Contains(GetSource(result), "ref struct",
            "CSharpSpan language must produce a ref struct.");
    }

    [TestMethod]
    public void UnknownLanguage_FallsBackToCSharp()
    {
        var result = GeneratorTestHelper.Run(
            "Simple.whfmt", WhfmtFixtures.Simple,
            new() { ["build_metadata.AdditionalFiles.WhfmtLanguage"] = "CobolPlus" });

        Assert.AreEqual(1, result.Diagnostics.Length,
            "Unknown language should produce exactly one WHSG002 warning.");
        Assert.AreEqual("WHSG002", result.Diagnostics[0].Id,
            "The warning should be WHSG002 (unsupported language).");
        Assert.AreEqual(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, result.Diagnostics[0].Severity,
            "WHSG002 must be a Warning, not an Error.");
        Assert.AreEqual(1, result.GeneratedTrees.Length, "Fallback should still generate a source file.");
        StringAssert.Contains(result.GeneratedTrees[0].ToString(), "class ",
            "Unknown language fallback should produce a regular class.");
    }

    [TestMethod]
    public void LanguageIsCaseInsensitive()
    {
        var result = GeneratorTestHelper.Run(
            "Simple.whfmt", WhfmtFixtures.Simple,
            new() { ["build_metadata.AdditionalFiles.WhfmtLanguage"] = "csharpspan" }); // lowercase

        StringAssert.Contains(GetSource(result), "ref struct",
            "Language value matching should be case-insensitive.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetSource(Microsoft.CodeAnalysis.GeneratorDriverRunResult result)
    {
        Assert.AreEqual(0, result.Diagnostics.Length,
            $"Unexpected diagnostic: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");
        Assert.AreEqual(1, result.GeneratedTrees.Length, "Expected exactly one generated source.");
        return result.GeneratedTrees[0].ToString();
    }
}
