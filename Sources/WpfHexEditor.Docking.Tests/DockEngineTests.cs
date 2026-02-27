//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Tests;

public class DockEngineTests
{
    private static DockItem CreateItem(string id = "item") =>
        new() { Title = id, ContentId = id };

    [Fact]
    public void Dock_Center_AddsItemToTargetGroup()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();

        engine.Dock(item, layout.MainDocumentHost, DockDirection.Center);

        Assert.Contains(item, layout.MainDocumentHost.Items);
        Assert.Equal(DockItemState.Docked, item.State);
    }

    [Fact]
    public void Dock_Left_CreatesSplitWithNewGroup()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();

        engine.Dock(item, layout.MainDocumentHost, DockDirection.Left);

        // Root should now be a split
        Assert.IsType<DockSplitNode>(layout.RootNode);
        var split = (DockSplitNode)layout.RootNode;
        Assert.Equal(2, split.Children.Count);
        Assert.Equal(SplitOrientation.Horizontal, split.Orientation);

        // First child should contain our item (left dock)
        var leftGroup = Assert.IsType<DockGroupNode>(split.Children[0]);
        Assert.Contains(item, leftGroup.Items);
    }

    [Fact]
    public void Dock_Right_CreatesItemOnRight()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();

        engine.Dock(item, layout.MainDocumentHost, DockDirection.Right);

        var split = Assert.IsType<DockSplitNode>(layout.RootNode);
        Assert.Equal(SplitOrientation.Horizontal, split.Orientation);

        // Second child should contain our item (right dock)
        var rightGroup = Assert.IsType<DockGroupNode>(split.Children[1]);
        Assert.Contains(item, rightGroup.Items);
    }

    [Fact]
    public void Dock_Bottom_CreatesVerticalSplit()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();

        engine.Dock(item, layout.MainDocumentHost, DockDirection.Bottom);

        var split = Assert.IsType<DockSplitNode>(layout.RootNode);
        Assert.Equal(SplitOrientation.Vertical, split.Orientation);
    }

    [Fact]
    public void Dock_FiresItemDockedEvent()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();
        DockItem? firedItem = null;
        engine.ItemDocked += i => firedItem = i;

        engine.Dock(item, layout.MainDocumentHost, DockDirection.Center);

        Assert.Same(item, firedItem);
    }

    [Fact]
    public void Undock_RemovesItemFromGroup()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();
        layout.MainDocumentHost.AddItem(item);

        engine.Undock(item);

        Assert.DoesNotContain(item, layout.MainDocumentHost.Items);
        Assert.Equal(DockItemState.Float, item.State);
        Assert.Contains(item, layout.FloatingItems);
    }

    [Fact]
    public void Close_RemovesItemCompletely()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();
        layout.MainDocumentHost.AddItem(item);

        engine.Close(item);

        Assert.DoesNotContain(item, layout.MainDocumentHost.Items);
        Assert.DoesNotContain(item, layout.FloatingItems);
        Assert.Equal(DockItemState.Hidden, item.State);
    }

    [Fact]
    public void Close_ThrowsWhenCanCloseIsFalse()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();
        item.CanClose = false;
        layout.MainDocumentHost.AddItem(item);

        Assert.Throws<InvalidOperationException>(() => engine.Close(item));
    }

    [Fact]
    public void Close_FiresItemClosedEvent()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();
        layout.MainDocumentHost.AddItem(item);
        DockItem? firedItem = null;
        engine.ItemClosed += i => firedItem = i;

        engine.Close(item);

        Assert.Same(item, firedItem);
    }

    [Fact]
    public void Float_MovesItemToFloatingList()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();
        layout.MainDocumentHost.AddItem(item);

        engine.Float(item);

        Assert.DoesNotContain(item, layout.MainDocumentHost.Items);
        Assert.Contains(item, layout.FloatingItems);
        Assert.Equal(DockItemState.Float, item.State);
    }

    [Fact]
    public void AutoHide_MovesItemToAutoHideList()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();
        layout.MainDocumentHost.AddItem(item);

        engine.AutoHide(item);

        Assert.DoesNotContain(item, layout.MainDocumentHost.Items);
        Assert.Contains(item, layout.AutoHideItems);
        Assert.Equal(DockItemState.AutoHide, item.State);
    }

    [Fact]
    public void MoveItem_TransfersToNewGroup()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem();
        var targetGroup = new DockGroupNode();

        layout.MainDocumentHost.AddItem(item);
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        split.AddChild(layout.MainDocumentHost);
        split.AddChild(targetGroup);
        layout.RootNode = split;

        engine.MoveItem(item, targetGroup);

        Assert.DoesNotContain(item, layout.MainDocumentHost.Items);
        Assert.Contains(item, targetGroup.Items);
        Assert.Same(targetGroup, item.Owner);
    }

    [Fact]
    public void NormalizeTree_CollapsesEmptyGroups()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        var emptyGroup = new DockGroupNode();
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        split.AddChild(layout.MainDocumentHost);
        split.AddChild(emptyGroup);
        layout.RootNode = split;

        engine.NormalizeTree();

        // Split with only MainDocumentHost should collapse to just the host
        Assert.Same(layout.MainDocumentHost, layout.RootNode);
    }

    [Fact]
    public void NormalizeTree_KeepsMainDocumentHost_EvenIfEmpty()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        engine.NormalizeTree();

        Assert.Same(layout.MainDocumentHost, layout.RootNode);
    }

    [Fact]
    public void NormalizeTree_CollapsesSingleChildSplit()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        split.AddChild(layout.MainDocumentHost);
        layout.RootNode = split;

        engine.NormalizeTree();

        // Single-child split should collapse
        Assert.Same(layout.MainDocumentHost, layout.RootNode);
    }
}
