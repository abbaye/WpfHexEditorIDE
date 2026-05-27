// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor.Tests
// File: EmbeddedRangeClassifier_Tests.cs
// Description:
//     Unit tests for EmbeddedRangeClassifier — 12 cases covering
//     the generic embedded-language zone detection logic.
// ==========================================================

using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Editor.CodeEditor.Tests;

[TestClass]
public class EmbeddedRangeClassifier_Tests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static LanguageDefinition MakeLang(string id) =>
        new() { Id = id, Name = id };

    private static EmbeddedLanguageZone ScriptZone(LanguageDefinition? lang = null) =>
        new("<script", "</script>", "javascript", lang ?? MakeLang("javascript"));

    private static EmbeddedLanguageZone StyleZone(LanguageDefinition? lang = null) =>
        new("<style", "</style>", "css", lang ?? MakeLang("css"));

    // ── Tests — basic ────────────────────────────────────────────────────────

    [TestMethod]
    public void EmptyText_ReturnsEmpty()
    {
        var result = EmbeddedRangeClassifier.ClassifyRanges(string.Empty, [ScriptZone()]);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void NoZones_ReturnsEmpty()
    {
        const string html = "<html><script>var x=1;</script></html>";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, []);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void UnresolvedZone_IsSkipped()
    {
        // Zone with ResolvedLanguage == null must be silently skipped.
        var unresolved = new EmbeddedLanguageZone("<script", "</script>", "javascript", null);
        const string html = "<script>var x=1;</script>";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, [unresolved]);
        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SingleScriptBlock_ContentCaptured()
    {
        const string js = "var x = 1;";
        string html = $"<html><script>{js}</script></html>";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, [ScriptZone()]);

        Assert.AreEqual(1, result.Count);
        var range = result[0];
        Assert.AreEqual("javascript", range.Language.Id);
        Assert.AreEqual(js, html[range.ContentStart..range.ContentEnd]);
    }

    [TestMethod]
    public void ScriptWithAttributes_ContentCaptured()
    {
        // <script type="module" defer> should be tolerated.
        const string js = "\n  export default {};\n";
        string html = $"<script type=\"module\" defer>{js}</script>";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, [ScriptZone()]);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(js, html[result[0].ContentStart..result[0].ContentEnd]);
    }

    [TestMethod]
    public void MultilineScript_ContentCaptured()
    {
        string js = "\nfunction hello() {\n  console.log('hi');\n}\n";
        string html = $"<!DOCTYPE html><html><head></head><body><script>{js}</script></body></html>";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, [ScriptZone()]);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(js, html[result[0].ContentStart..result[0].ContentEnd]);
    }

    [TestMethod]
    public void StyleBlock_ContentCaptured()
    {
        const string css = "body { color: red; }";
        string html = $"<html><head><style>{css}</style></head></html>";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, [StyleZone()]);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("css", result[0].Language.Id);
        Assert.AreEqual(css, html[result[0].ContentStart..result[0].ContentEnd]);
    }

    [TestMethod]
    public void BothScriptAndStyle_TwoRangesReturned()
    {
        const string css = "body{}";
        const string js  = "var x=1;";
        string html = $"<html><head><style>{css}</style></head><body><script>{js}</script></body></html>";

        var zones  = new[] { ScriptZone(), StyleZone() };
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, zones);

        Assert.AreEqual(2, result.Count);
        // Ranges must be sorted by start position.
        Assert.IsTrue(result[0].ContentStart < result[1].ContentStart);
    }

    [TestMethod]
    public void MultipleScriptBlocks_AllCaptured()
    {
        string html = "<script>var a=1;</script><p></p><script>var b=2;</script>";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, [ScriptZone()]);

        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result[0].ContentStart < result[1].ContentStart);
    }

    [TestMethod]
    public void UnclosedScript_NoRange()
    {
        // No closing </script> → regex should not match.
        string html = "<html><script>var x=1;";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, [ScriptZone()]);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void PhpZone_ContentCaptured()
    {
        var phpLang = MakeLang("php");
        var phpZone = new EmbeddedLanguageZone("<?php", "?>", "php", phpLang);
        string doc  = "<html><?php echo 'hi'; ?></html>";

        var result = EmbeddedRangeClassifier.ClassifyRanges(doc, [phpZone]);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("php", result[0].Language.Id);
        StringAssert.Contains(doc[result[0].ContentStart..result[0].ContentEnd], "echo");
    }

    [TestMethod]
    public void MarkdownFenceZone_ContentCaptured()
    {
        var jsLang   = MakeLang("javascript");
        var fenceZone = new EmbeddedLanguageZone("```js", "```", "javascript", jsLang);
        string md     = "# Title\n```js\nconsole.log('hi');\n```\nEnd.";

        var result = EmbeddedRangeClassifier.ClassifyRanges(md, [fenceZone]);

        Assert.AreEqual(1, result.Count);
        StringAssert.Contains(md[result[0].ContentStart..result[0].ContentEnd], "console.log");
    }

    [TestMethod]
    public void CaseInsensitive_UppercaseTagMatched()
    {
        // Some HTML generators emit <SCRIPT> in uppercase.
        const string js  = "var x=1;";
        string html = $"<SCRIPT>{js}</SCRIPT>";
        var result = EmbeddedRangeClassifier.ClassifyRanges(html, [ScriptZone()]);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(js, html[result[0].ContentStart..result[0].ContentEnd]);
    }
}
