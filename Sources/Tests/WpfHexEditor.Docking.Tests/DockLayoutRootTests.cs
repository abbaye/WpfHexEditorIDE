//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Tests;

public class DockLayoutRootTests
{
    [Fact]
    public void NewLayout_HasMainDocumentHost()
    {
        var layout = new DockLayoutRoot();

        Assert.NotNull(layout.MainDocumentHost);
        Assert.True(layout.MainDocumentHost.IsMain);
    }

    [Fact]
    public void NewLayout_RootNode_IsMainDocumentHost()
    {
        var layout = new DockLayoutRoot();

        Assert.Same(layout.MainDocumentHost, layout.RootNode);
    }

    [Fact]
    public void NewLayout_RootNode_IsNeverNull()
    {
        var layout = new DockLayoutRoot();

        Assert.NotNull(layout.RootNode);
    }

    [Fact]
    public void FindItem_ReturnsItemInGroup()
    {
        var layout = new DockLayoutRoot();
        var item = new DockItem { Title = "Test", ContentId = "test" };
        layout.MainDocumentHost.AddItem(item);

        var found = layout.FindItem(item.Id);

        Assert.Same(item, found);
    }

    [Fact]
    public void FindItem_ReturnsFloatingItem()
    {
        var layout = new DockLayoutRoot();
        var item = new DockItem { Title = "Float", ContentId = "float" };
        layout.FloatingItems.Add(item);

        var found = layout.FindItem(item.Id);

        Assert.Same(item, found);
    }

    [Fact]
    public void FindItemByContentId_Works()
    {
        var layout = new DockLayoutRoot();
        var item = new DockItem { Title = "Test", ContentId = "unique-content" };
        layout.MainDocumentHost.AddItem(item);

        var found = layout.FindItemByContentId("unique-content");

        Assert.Same(item, found);
    }

    [Fact]
    public void FindItem_ReturnsNull_WhenNotFound()
    {
        var layout = new DockLayoutRoot();

        var found = layout.FindItem(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public void GetAllDocumentHosts_ReturnsMainHost()
    {
        var layout = new DockLayoutRoot();

        var hosts = layout.GetAllDocumentHosts().ToList();

        Assert.Single(hosts);
        Assert.True(hosts[0].IsMain);
    }

    [Fact]
    public void GetAllGroups_ReturnsAllGroupsInTree()
    {
        var layout = new DockLayoutRoot();
        var group = new DockGroupNode();

        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        split.AddChild(layout.MainDocumentHost);
        split.AddChild(group);
        layout.RootNode = split;

        var groups = layout.GetAllGroups().ToList();

        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void FindNode_ReturnsNodeById()
    {
        var layout = new DockLayoutRoot();
        var group = new DockGroupNode();
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        split.AddChild(layout.MainDocumentHost);
        split.AddChild(group);
        layout.RootNode = split;

        Assert.Same(group, layout.FindNode(group.Id));
        Assert.Same(split, layout.FindNode(split.Id));
    }
}
