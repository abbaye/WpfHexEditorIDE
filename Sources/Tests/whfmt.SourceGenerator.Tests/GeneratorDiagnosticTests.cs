// Project      : whfmt.SourceGenerator.Tests
// File         : GeneratorDiagnosticTests.cs
// Description  : Tests that invalid or empty .whfmt files produce WHSG001
//                and do not emit any generated source.

namespace whfmt.SourceGenerator.Tests;

[TestClass]
public class GeneratorDiagnosticTests
{
    [TestMethod]
    public void MalformedJson_EmitsWHSG001()
    {
        var result = GeneratorTestHelper.Run("Bad.whfmt", WhfmtFixtures.Malformed);

        Assert.AreEqual(1, result.Diagnostics.Length, "Malformed JSON should emit exactly one diagnostic.");
        Assert.AreEqual("WHSG001", result.Diagnostics[0].Id);
    }

    [TestMethod]
    public void MalformedJson_ProducesNoGeneratedSource()
    {
        var result = GeneratorTestHelper.Run("Bad.whfmt", WhfmtFixtures.Malformed);

        Assert.AreEqual(0, result.GeneratedTrees.Length,
            "Malformed JSON must not produce any generated source.");
    }

    [TestMethod]
    public void EmptyContent_ProducesNoGeneratedSource()
    {
        var result = GeneratorTestHelper.Run("Empty.whfmt", WhfmtFixtures.Empty);

        Assert.AreEqual(0, result.GeneratedTrees.Length,
            "Empty/whitespace content must not produce any generated source.");
    }

    [TestMethod]
    public void EmptyContent_ProducesNoDiagnostics()
    {
        // Empty is silently skipped (not an error — the file may not have been filled in yet)
        var result = GeneratorTestHelper.Run("Empty.whfmt", WhfmtFixtures.Empty);

        Assert.AreEqual(0, result.Diagnostics.Length,
            "Empty/whitespace content should be skipped silently without a diagnostic.");
    }

    [TestMethod]
    public void MalformedJson_DiagnosticContainsFilePath()
    {
        const string path = "MyBadFormat.whfmt";
        var result = GeneratorTestHelper.Run(path, WhfmtFixtures.Malformed);

        Assert.AreEqual(1, result.Diagnostics.Length);
        StringAssert.Contains(
            result.Diagnostics[0].GetMessage(),
            "MyBadFormat.whfmt",
            "WHSG001 message should include the source file path.");
    }

    [TestMethod]
    public void MalformedJson_DiagnosticSeverityIsError()
    {
        var result = GeneratorTestHelper.Run("Bad.whfmt", WhfmtFixtures.Malformed);

        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual(
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error,
            result.Diagnostics[0].Severity,
            "WHSG001 should be an Error severity diagnostic.");
    }

    [TestMethod]
    public void ValidAndInvalidFiles_OnlyInvalidEmitsDiagnostic()
    {
        var result = GeneratorTestHelper.Run(new[]
        {
            ("Good.whfmt", WhfmtFixtures.Simple,    (Dictionary<string, string>?)null),
            ("Bad.whfmt",  WhfmtFixtures.Malformed, null),
        });

        Assert.AreEqual(1, result.Diagnostics.Length, "Only the invalid file should emit a diagnostic.");
        Assert.AreEqual(1, result.GeneratedTrees.Length,  "Only the valid file should produce a source.");
    }
}
