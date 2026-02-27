//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Tests;

public class DockEngineAutoHideTests
{
    private static DockItem CreateItem(string id = "item") =>
        new() { Title = id, ContentId = id };

    [Fact]
    public void AutoHide_RemovesFromGroup_AddsToAutoHideList()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        engine.AutoHide(item);

        Assert.DoesNotContain(item, layout.MainDocumentHost.Items);
        Assert.Contains(item, layout.AutoHideItems);
        Assert.Equal(DockItemState.AutoHide, item.State);
    }

    [Fact]
    public void AutoHide_FromFloating_MovesToAutoHide()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        // Float first, then auto-hide
        engine.Float(item);
        Assert.Contains(item, layout.FloatingItems);

        engine.AutoHide(item);

        Assert.DoesNotContain(item, layout.FloatingItems);
        Assert.Contains(item, layout.AutoHideItems);
        Assert.Equal(DockItemState.AutoHide, item.State);
    }

    [Fact]
    public void AutoHide_DoesNotDuplicate()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        engine.AutoHide(item);
        engine.AutoHide(item); // call again

        Assert.Single(layout.AutoHideItems, i => i == item);
    }

    [Fact]
    public void RestoreFromAutoHide_DocksToMainDocumentHost_ByDefault()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        engine.AutoHide(item);
        Assert.Contains(item, layout.AutoHideItems);

        engine.RestoreFromAutoHide(item);

        Assert.DoesNotContain(item, layout.AutoHideItems);
        Assert.Contains(item, layout.MainDocumentHost.Items);
        Assert.Equal(DockItemState.Docked, item.State);
    }

    [Fact]
    public void RestoreFromAutoHide_DocksToSpecifiedTarget()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        // Create a side group
        var sideGroup = new DockGroupNode();
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        split.AddChild(layout.MainDocumentHost);
        split.AddChild(sideGroup);
        layout.RootNode = split;

        engine.AutoHide(item);
        engine.RestoreFromAutoHide(item, sideGroup);

        Assert.DoesNotContain(item, layout.AutoHideItems);
        Assert.Contains(item, sideGroup.Items);
        Assert.Equal(DockItemState.Docked, item.State);
    }

    [Fact]
    public void RestoreFromAutoHide_WithDirection_CreatesNewSplit()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        engine.AutoHide(item);
        engine.RestoreFromAutoHide(item, layout.MainDocumentHost, DockDirection.Left);

        Assert.DoesNotContain(item, layout.AutoHideItems);
        Assert.Equal(DockItemState.Docked, item.State);

        // Should have created a split
        Assert.IsType<DockSplitNode>(layout.RootNode);
    }

    [Fact]
    public void RestoreFromAutoHide_NotInAutoHide_DoesNothing()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        // Item is docked, not auto-hidden
        engine.RestoreFromAutoHide(item);

        // Should still be in original position
        Assert.Contains(item, layout.MainDocumentHost.Items);
        Assert.Equal(DockItemState.Docked, item.State);
    }

    [Fact]
    public void AutoHide_ThenClose_RemovesFromAutoHide()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        engine.AutoHide(item);
        Assert.Contains(item, layout.AutoHideItems);

        engine.Close(item);

        Assert.DoesNotContain(item, layout.AutoHideItems);
        Assert.Equal(DockItemState.Hidden, item.State);
    }

    [Fact]
    public void RoundTrip_Dock_AutoHide_Restore_Dock()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");

        // Start: dock to center
        engine.Dock(item, layout.MainDocumentHost, DockDirection.Center);
        Assert.Contains(item, layout.MainDocumentHost.Items);
        Assert.Equal(DockItemState.Docked, item.State);

        // Auto-hide
        engine.AutoHide(item);
        Assert.DoesNotContain(item, layout.MainDocumentHost.Items);
        Assert.Contains(item, layout.AutoHideItems);
        Assert.Equal(DockItemState.AutoHide, item.State);

        // Restore
        engine.RestoreFromAutoHide(item);
        Assert.Contains(item, layout.MainDocumentHost.Items);
        Assert.DoesNotContain(item, layout.AutoHideItems);
        Assert.Equal(DockItemState.Docked, item.State);
    }

    [Fact]
    public void RoundTrip_Float_AutoHide_Restore()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var item = CreateItem("panel1");
        layout.MainDocumentHost.AddItem(item);

        // Float
        engine.Float(item);
        Assert.Contains(item, layout.FloatingItems);

        // Auto-hide from float
        engine.AutoHide(item);
        Assert.DoesNotContain(item, layout.FloatingItems);
        Assert.Contains(item, layout.AutoHideItems);

        // Restore from auto-hide
        engine.RestoreFromAutoHide(item);
        Assert.Contains(item, layout.MainDocumentHost.Items);
        Assert.Equal(DockItemState.Docked, item.State);
    }
}
