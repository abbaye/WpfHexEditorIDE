// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor.Tests
// File: FoldingEngine_Tests.cs
// Description:
//     Tests for FoldingEngine: brace folding, region detection, nested blocks,
//     collapse state preservation, and IsLineHidden logic.
// ==========================================================

using WpfHexEditor.Editor.CodeEditor.Folding;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Tests;

[TestClass]
public sealed class FoldingEngine_Tests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<CodeLine> Lines(params string[] lines)
        => lines.Select(t => new CodeLine { Text = t }).ToList();

    private static FoldingEngine MakeBraceEngine()
        => new FoldingEngine(new BraceFoldingStrategy());

    // ── Basic brace detection ─────────────────────────────────────────────────

    [TestMethod]
    public void Analyze_EmptyDocument_NoRegions()
    {
        var engine = MakeBraceEngine();
        engine.Analyze(Lines());
        Assert.AreEqual(0, engine.Regions.Count);
    }

    [TestMethod]
    public void Analyze_SingleLineBrace_NoRegion()
    {
        // { and } on same line → no multiline region
        var engine = MakeBraceEngine();
        engine.Analyze(Lines("class Foo { }"));
        Assert.AreEqual(0, engine.Regions.Count);
    }

    [TestMethod]
    public void Analyze_TwoLineBraces_NoRegion()
    {
        // Strategy requires } to be at least 2 lines after { (i > startLine+1)
        var engine = MakeBraceEngine();
        engine.Analyze(Lines("class Foo {", "}"));
        Assert.AreEqual(0, engine.Regions.Count);
    }

    [TestMethod]
    public void Analyze_ThreeLineBraces_CreatesRegion()
    {
        var engine = MakeBraceEngine();
        engine.Analyze(Lines("class Foo {", "    int x;", "}"));
        Assert.IsTrue(engine.Regions.Count >= 1);
    }

    [TestMethod]
    public void Analyze_MultilineBraces_RegionSpansCorrectLines()
    {
        var engine = MakeBraceEngine();
        engine.Analyze(Lines(
            "class Foo {",    // 0
            "    int x;",     // 1
            "    int y;",     // 2
            "}"               // 3
        ));
        Assert.IsTrue(engine.Regions.Count >= 1);
        var region = engine.Regions[0];
        Assert.AreEqual(0, region.StartLine);
        Assert.AreEqual(3, region.EndLine);
    }

    // ── Nested blocks ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Analyze_NestedBraces_CreatesMultipleRegions()
    {
        var engine = MakeBraceEngine();
        engine.Analyze(Lines(
            "class Foo {",      // 0
            "    void M() {",   // 1
            "        int x;",   // 2
            "    }",            // 3
            "}"                 // 4
        ));
        Assert.IsTrue(engine.Regions.Count >= 2, $"Expected ≥2 regions, got {engine.Regions.Count}.");
    }

    // ── Collapse / expand state ───────────────────────────────────────────────

    [TestMethod]
    public void Toggle_CollapsesThenExpands()
    {
        var engine = MakeBraceEngine();
        engine.Analyze(Lines("void M() {", "    int x;", "}"));
        Assert.IsTrue(engine.Regions.Count >= 1);

        var region = engine.Regions[0];
        Assert.IsFalse(region.IsCollapsed);

        engine.ToggleRegion(region.StartLine);
        Assert.IsTrue(engine.Regions[0].IsCollapsed);

        engine.ToggleRegion(region.StartLine);
        Assert.IsFalse(engine.Regions[0].IsCollapsed);
    }

    [TestMethod]
    public void Analyze_PreservesCollapsedStateForSameStartLine()
    {
        var engine = MakeBraceEngine();
        engine.Analyze(Lines("void M() {", "    int x;", "}"));
        engine.ToggleRegion(0);
        Assert.IsTrue(engine.Regions[0].IsCollapsed);

        // Re-analyze same document (simulates minor edit)
        engine.Analyze(Lines("void M() {", "    int x; // edit", "}"));
        Assert.IsTrue(engine.Regions[0].IsCollapsed, "Collapsed state must be preserved after re-analyze.");
    }

    // ── IsLineHidden ──────────────────────────────────────────────────────────

    [TestMethod]
    public void IsLineHidden_NotCollapsed_ReturnsFalse()
    {
        var engine = MakeBraceEngine();
        engine.Analyze(Lines("void M() {", "    int x;", "}"));
        Assert.IsFalse(engine.IsLineHidden(1));
    }

    [TestMethod]
    public void IsLineHidden_Collapsed_BodyLinesHidden()
    {
        var engine = MakeBraceEngine();
        engine.Analyze(Lines("void M() {", "    int x;", "}"));
        engine.ToggleRegion(0);

        // Line 1 (body) should be hidden; lines 0 and 2 should not
        Assert.IsTrue(engine.IsLineHidden(1),  "Body line must be hidden when collapsed.");
        Assert.IsFalse(engine.IsLineHidden(0), "Start line must not be hidden.");
    }

    // ── RegionsChanged event ──────────────────────────────────────────────────

    [TestMethod]
    public void Analyze_FiresRegionsChangedEvent()
    {
        var engine  = MakeBraceEngine();
        var fired   = false;
        engine.RegionsChanged += (_, _) => fired = true;
        engine.Analyze(Lines("class Foo {", "}"));
        Assert.IsTrue(fired);
    }

    // ── ReplaceStrategy ───────────────────────────────────────────────────────

    [TestMethod]
    public void ReplaceStrategy_NullThrows()
    {
        var engine = MakeBraceEngine();
        try
        {
            engine.ReplaceStrategy(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown.");
        }
        catch (ArgumentNullException) { /* expected */ }
    }
}
