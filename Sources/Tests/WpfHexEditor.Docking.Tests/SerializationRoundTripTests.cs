//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Core.Serialization;

namespace WpfHexEditor.Docking.Tests;

public class SerializationRoundTripTests
{
    private static DockItem CreateItem(string id, string title = "") =>
        new() { Title = string.IsNullOrEmpty(title) ? id : title, ContentId = id };

    [Fact]
    public void RoundTrip_EmptyLayout_PreservesStructure()
    {
        var layout = new DockLayoutRoot();

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        Assert.NotNull(restored);
        Assert.NotNull(restored.MainDocumentHost);
        Assert.True(restored.MainDocumentHost.IsMain);
        Assert.Empty(restored.FloatingItems);
        Assert.Empty(restored.AutoHideItems);
    }

    [Fact]
    public void RoundTrip_SingleDocumentInHost()
    {
        var layout = new DockLayoutRoot();
        var doc = CreateItem("doc-1", "Document 1");
        layout.MainDocumentHost.AddItem(doc);
        layout.MainDocumentHost.ActiveItem = doc;

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        Assert.Single(restored.MainDocumentHost.Items);
        var restoredDoc = restored.MainDocumentHost.Items[0];
        Assert.Equal("doc-1", restoredDoc.ContentId);
        Assert.Equal("Document 1", restoredDoc.Title);
        Assert.Equal("doc-1", restored.MainDocumentHost.ActiveItem?.ContentId);
    }

    [Fact]
    public void RoundTrip_SplitWithGroups_PreservesTree()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        var doc = CreateItem("doc-1", "Doc 1");
        engine.Dock(doc, layout.MainDocumentHost, DockDirection.Center);

        var leftPanel = CreateItem("panel-left", "Left Panel");
        engine.Dock(leftPanel, layout.MainDocumentHost, DockDirection.Left);

        var bottomPanel = CreateItem("panel-bottom", "Bottom Panel");
        engine.Dock(bottomPanel, layout.MainDocumentHost, DockDirection.Bottom);

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        // Verify we can find all items
        var allItems = GetAllItems(restored.RootNode);
        Assert.Contains(allItems, i => i.ContentId == "doc-1");
        Assert.Contains(allItems, i => i.ContentId == "panel-left");
        Assert.Contains(allItems, i => i.ContentId == "panel-bottom");
        Assert.Equal(3, allItems.Count);
    }

    [Fact]
    public void RoundTrip_SplitRatios_Preserved()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        var panel = CreateItem("panel-1");
        engine.Dock(panel, layout.MainDocumentHost, DockDirection.Left);

        // Verify root is a split
        var split = Assert.IsType<DockSplitNode>(layout.RootNode);
        var originalRatios = split.Ratios.ToArray();

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        var restoredSplit = Assert.IsType<DockSplitNode>(restored.RootNode);
        Assert.Equal(originalRatios.Length, restoredSplit.Ratios.Count);
        for (var i = 0; i < originalRatios.Length; i++)
            Assert.Equal(originalRatios[i], restoredSplit.Ratios[i], 6);
    }

    [Fact]
    public void RoundTrip_SplitOrientation_Preserved()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        var panel = CreateItem("panel-1");
        engine.Dock(panel, layout.MainDocumentHost, DockDirection.Bottom);

        var split = Assert.IsType<DockSplitNode>(layout.RootNode);
        Assert.Equal(SplitOrientation.Vertical, split.Orientation);

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        var restoredSplit = Assert.IsType<DockSplitNode>(restored.RootNode);
        Assert.Equal(SplitOrientation.Vertical, restoredSplit.Orientation);
    }

    [Fact]
    public void RoundTrip_FloatingItems_Preserved()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        var doc = CreateItem("doc-1", "Floating Doc");
        layout.MainDocumentHost.AddItem(doc);
        engine.Float(doc);

        Assert.Single(layout.FloatingItems);

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        Assert.Single(restored.FloatingItems);
        Assert.Equal("doc-1", restored.FloatingItems[0].ContentId);
        Assert.Equal("Floating Doc", restored.FloatingItems[0].Title);
        Assert.Equal(DockItemState.Float, restored.FloatingItems[0].State);
    }

    [Fact]
    public void RoundTrip_AutoHideItems_Preserved()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        var panel = CreateItem("panel-1", "Hidden Panel");
        layout.MainDocumentHost.AddItem(panel);
        engine.AutoHide(panel);

        Assert.Single(layout.AutoHideItems);

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        Assert.Single(restored.AutoHideItems);
        Assert.Equal("panel-1", restored.AutoHideItems[0].ContentId);
        Assert.Equal("Hidden Panel", restored.AutoHideItems[0].Title);
        Assert.Equal(DockItemState.AutoHide, restored.AutoHideItems[0].State);
    }

    [Fact]
    public void RoundTrip_ItemProperties_CanClose_CanFloat()
    {
        var layout = new DockLayoutRoot();
        var item = new DockItem
        {
            Title = "Special",
            ContentId = "special-1",
            CanClose = false,
            CanFloat = false
        };
        layout.MainDocumentHost.AddItem(item);

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        var restoredItem = restored.MainDocumentHost.Items[0];
        Assert.False(restoredItem.CanClose);
        Assert.False(restoredItem.CanFloat);
    }

    [Fact]
    public void RoundTrip_LockMode_Preserved()
    {
        var layout = new DockLayoutRoot();
        layout.MainDocumentHost.LockMode = DockLockMode.PreventClosing;

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        Assert.Equal(DockLockMode.PreventClosing, restored.MainDocumentHost.LockMode);
    }

    [Fact]
    public void RoundTrip_ComplexLayout_AllItemsPreserved()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        // Build a complex layout
        var doc1 = CreateItem("doc-1", "Document 1");
        var doc2 = CreateItem("doc-2", "Document 2");
        engine.Dock(doc1, layout.MainDocumentHost, DockDirection.Center);
        engine.Dock(doc2, layout.MainDocumentHost, DockDirection.Center);

        var leftPanel = CreateItem("panel-left", "Solution Explorer");
        engine.Dock(leftPanel, layout.MainDocumentHost, DockDirection.Left);

        var rightPanel = CreateItem("panel-right", "Properties");
        engine.Dock(rightPanel, layout.MainDocumentHost, DockDirection.Right);

        var bottomPanel = CreateItem("panel-bottom", "Output");
        engine.Dock(bottomPanel, layout.MainDocumentHost, DockDirection.Bottom);

        var floatingItem = CreateItem("float-1", "Floating");
        layout.MainDocumentHost.AddItem(floatingItem);
        engine.Float(floatingItem);

        var autoHideItem = CreateItem("hidden-1", "Auto-Hidden");
        layout.MainDocumentHost.AddItem(autoHideItem);
        engine.AutoHide(autoHideItem);

        // Verify pre-serialization state
        var preItems = GetAllItems(layout.RootNode);
        Assert.Equal(5, preItems.Count); // doc1, doc2, left, right, bottom

        var json = DockLayoutSerializer.Serialize(layout);
        var restored = DockLayoutSerializer.Deserialize(json);

        // Count all docked items
        var allDockedItems = GetAllItems(restored.RootNode);
        Assert.Contains(allDockedItems, i => i.ContentId == "doc-1");
        Assert.Contains(allDockedItems, i => i.ContentId == "doc-2");
        Assert.Contains(allDockedItems, i => i.ContentId == "panel-left");
        Assert.Contains(allDockedItems, i => i.ContentId == "panel-right");
        Assert.Contains(allDockedItems, i => i.ContentId == "panel-bottom");

        Assert.Single(restored.FloatingItems);
        Assert.Equal("float-1", restored.FloatingItems[0].ContentId);

        Assert.Single(restored.AutoHideItems);
        Assert.Equal("hidden-1", restored.AutoHideItems[0].ContentId);
    }

    [Fact]
    public void RoundTrip_JsonIsValid_CanDeserializeTwice()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("doc-1");
        engine.Dock(item, layout.MainDocumentHost, DockDirection.Center);

        var json = DockLayoutSerializer.Serialize(layout);

        // Should be able to deserialize multiple times
        var restored1 = DockLayoutSerializer.Deserialize(json);
        var restored2 = DockLayoutSerializer.Deserialize(json);

        Assert.Single(GetAllItems(restored1.RootNode));
        Assert.Single(GetAllItems(restored2.RootNode));
    }

    /// <summary>
    /// Helper: collects all DockItems from a node tree.
    /// </summary>
    private static List<DockItem> GetAllItems(DockNode node)
    {
        var items = new List<DockItem>();
        CollectItems(node, items);
        return items;
    }

    private static void CollectItems(DockNode node, List<DockItem> items)
    {
        switch (node)
        {
            case DockGroupNode group:
                items.AddRange(group.Items);
                break;
            case DockSplitNode split:
                foreach (var child in split.Children)
                    CollectItems(child, items);
                break;
        }
    }
}
