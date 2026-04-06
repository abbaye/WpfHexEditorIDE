// ==========================================================
// Project: WpfHexEditor.Docking.Tests
// File: DockEngineTabGroupTests.cs
// Contributors: Claude Sonnet 4.6
// Description:
//     Unit tests for Tab Groups engine logic (ADR-TABGROUP-01).
//     Pure model tests — no WPF, no visual tree.
// ==========================================================

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Tests;

public class DockEngineTabGroupTests
{
    private static DockItem CreateDocument(string id) =>
        new() { Title = id, ContentId = id, IsDocument = true };

    private static (DockEngine engine, DockLayoutRoot layout) CreateEngine()
    {
        var layout = new DockLayoutRoot();
        return (new DockEngine(layout), layout);
    }

    // ── Split ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SplitDocumentHost_Vertical_CreatesTwoHosts()
    {
        var (engine, layout) = CreateEngine();
        var doc1 = CreateDocument("doc1");
        var doc2 = CreateDocument("doc2");

        engine.Dock(doc1, layout.MainDocumentHost, DockDirection.Center);
        engine.SplitDocumentHost(doc2, layout.MainDocumentHost, DockDirection.Right);

        var hosts = layout.GetAllDocumentHosts().ToList();
        Assert.Equal(2, hosts.Count);
    }

    [Fact]
    public void SplitDocumentHost_Horizontal_CreatesTwoHosts()
    {
        var (engine, layout) = CreateEngine();
        var doc1 = CreateDocument("doc1");
        var doc2 = CreateDocument("doc2");

        engine.Dock(doc1, layout.MainDocumentHost, DockDirection.Center);
        engine.SplitDocumentHost(doc2, layout.MainDocumentHost, DockDirection.Bottom);

        var hosts = layout.GetAllDocumentHosts().ToList();
        Assert.Equal(2, hosts.Count);
    }

    [Fact]
    public void SplitDocumentHost_ItemLandsInNewHost()
    {
        var (engine, layout) = CreateEngine();
        var doc = CreateDocument("doc");

        engine.SplitDocumentHost(doc, layout.MainDocumentHost, DockDirection.Right);

        // The new item should NOT be in the main host
        Assert.DoesNotContain(doc, layout.MainDocumentHost.Items);

        // It should be in the second host
        var secondHost = layout.GetAllDocumentHosts().First(h => !h.IsMain);
        Assert.Contains(doc, secondHost.Items);
    }

    // ── MoveItem ──────────────────────────────────────────────────────────────

    [Fact]
    public void MoveItem_MovesToAdjacentHost()
    {
        var (engine, layout) = CreateEngine();
        var doc1 = CreateDocument("doc1");
        var doc2 = CreateDocument("doc2");

        engine.Dock(doc1, layout.MainDocumentHost, DockDirection.Center);
        engine.SplitDocumentHost(doc2, layout.MainDocumentHost, DockDirection.Right);

        var secondHost = layout.GetAllDocumentHosts().First(h => !h.IsMain);

        // Move doc1 from main host to second host
        engine.MoveItem(doc1, secondHost);

        Assert.DoesNotContain(doc1, layout.MainDocumentHost.Items);
        Assert.Contains(doc1, secondHost.Items);
    }

    // ── GetAllDocumentHosts ───────────────────────────────────────────────────

    [Fact]
    public void GetAllDocumentHosts_ReturnsMainFirst()
    {
        var (engine, layout) = CreateEngine();
        var doc = CreateDocument("doc");

        engine.SplitDocumentHost(doc, layout.MainDocumentHost, DockDirection.Right);

        var hosts = layout.GetAllDocumentHosts().ToList();
        Assert.True(hosts[0].IsMain, "First returned host should be the main document host.");
    }

    [Fact]
    public void GetAllDocumentHosts_SingleHost_ReturnsOne()
    {
        var (_, layout) = CreateEngine();
        Assert.Single(layout.GetAllDocumentHosts());
    }

    // ── NormalizeTree ─────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeTree_EmptyNonMainHost_IsRemoved()
    {
        var (engine, layout) = CreateEngine();
        var doc = CreateDocument("doc");

        engine.SplitDocumentHost(doc, layout.MainDocumentHost, DockDirection.Right);
        Assert.Equal(2, layout.GetAllDocumentHosts().Count());

        // Move the only item back to main host, then normalize
        engine.MoveItem(doc, layout.MainDocumentHost);

        // After normalization, empty secondary host should be gone
        Assert.Single(layout.GetAllDocumentHosts());
    }

    // ── LayoutChanged event ───────────────────────────────────────────────────

    [Fact]
    public void LayoutChanged_RaisedOnSplit()
    {
        var (engine, layout) = CreateEngine();
        var doc = CreateDocument("doc");
        var raised = false;
        engine.LayoutChanged += () => raised = true;

        engine.SplitDocumentHost(doc, layout.MainDocumentHost, DockDirection.Right);

        Assert.True(raised);
    }

    [Fact]
    public void LayoutChanged_RaisedOnMoveItem()
    {
        var (engine, layout) = CreateEngine();
        var doc1 = CreateDocument("doc1");
        var doc2 = CreateDocument("doc2");
        engine.Dock(doc1, layout.MainDocumentHost, DockDirection.Center);
        engine.SplitDocumentHost(doc2, layout.MainDocumentHost, DockDirection.Right);

        var secondHost = layout.GetAllDocumentHosts().First(h => !h.IsMain);
        var raised = false;
        engine.LayoutChanged += () => raised = true;

        engine.MoveItem(doc1, secondHost);

        Assert.True(raised);
    }

    // ── Multiple splits ───────────────────────────────────────────────────────

    [Fact]
    public void MultiSplit_ThreeGroups_AllReturned()
    {
        var (engine, layout) = CreateEngine();
        var doc1 = CreateDocument("doc1");
        var doc2 = CreateDocument("doc2");

        engine.SplitDocumentHost(doc1, layout.MainDocumentHost, DockDirection.Right);
        var secondHost = layout.GetAllDocumentHosts().First(h => !h.IsMain);
        engine.SplitDocumentHost(doc2, secondHost, DockDirection.Right);

        Assert.Equal(3, layout.GetAllDocumentHosts().Count());
    }
}
