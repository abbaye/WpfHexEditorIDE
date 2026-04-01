//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Tests;

public class DockLockModeTests
{
    private static DockItem CreateItem(string id = "item") =>
        new() { Title = id, ContentId = id };

    [Fact]
    public void PreventSplitting_BlocksDockWithDirection()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        layout.MainDocumentHost.LockMode = DockLockMode.PreventSplitting;
        var item = CreateItem();

        Assert.Throws<InvalidOperationException>(
            () => engine.Dock(item, layout.MainDocumentHost, DockDirection.Left));
    }

    [Fact]
    public void PreventSplitting_AllowsDockCenter()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        layout.MainDocumentHost.LockMode = DockLockMode.PreventSplitting;
        var item = CreateItem();

        engine.Dock(item, layout.MainDocumentHost, DockDirection.Center);

        Assert.Contains(item, layout.MainDocumentHost.Items);
    }

    [Fact]
    public void PreventUndocking_BlocksUndock()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        layout.MainDocumentHost.LockMode = DockLockMode.PreventUndocking;
        var item = CreateItem();
        layout.MainDocumentHost.AddItem(item);

        Assert.Throws<InvalidOperationException>(() => engine.Undock(item));
    }

    [Fact]
    public void PreventClosing_BlocksClose()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        layout.MainDocumentHost.LockMode = DockLockMode.PreventClosing;
        var item = CreateItem();
        layout.MainDocumentHost.AddItem(item);

        Assert.Throws<InvalidOperationException>(() => engine.Close(item));
    }

    [Fact]
    public void FullLock_BlocksAllOperations()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        layout.MainDocumentHost.LockMode = DockLockMode.Full;
        var item = CreateItem();
        layout.MainDocumentHost.AddItem(item);

        Assert.Throws<InvalidOperationException>(
            () => engine.Dock(CreateItem("other"), layout.MainDocumentHost, DockDirection.Left));
        Assert.Throws<InvalidOperationException>(() => engine.Undock(item));
        Assert.Throws<InvalidOperationException>(() => engine.Close(item));
    }

    [Fact]
    public void Full_HasAllFlags()
    {
        Assert.True(DockLockMode.Full.HasFlag(DockLockMode.PreventSplitting));
        Assert.True(DockLockMode.Full.HasFlag(DockLockMode.PreventUndocking));
        Assert.True(DockLockMode.Full.HasFlag(DockLockMode.PreventClosing));
    }
}
