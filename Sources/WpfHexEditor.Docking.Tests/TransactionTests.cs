//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Tests;

public class TransactionTests
{
    private static DockItem CreateItem(string id = "item") =>
        new() { Title = id, ContentId = id };

    [Fact]
    public void BeginTransaction_SetsIsInTransaction()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        engine.BeginTransaction();

        Assert.True(engine.IsInTransaction);
    }

    [Fact]
    public void CommitTransaction_ClearsIsInTransaction()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        engine.BeginTransaction();
        engine.CommitTransaction();

        Assert.False(engine.IsInTransaction);
    }

    [Fact]
    public void CommitTransaction_ThrowsWhenNoTransaction()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);

        Assert.Throws<InvalidOperationException>(() => engine.CommitTransaction());
    }

    [Fact]
    public void Transaction_DefersLayoutChanged()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var changedCount = 0;
        engine.LayoutChanged += () => changedCount++;

        engine.BeginTransaction();
        engine.Dock(CreateItem("a"), layout.MainDocumentHost, DockDirection.Center);
        engine.Dock(CreateItem("b"), layout.MainDocumentHost, DockDirection.Center);

        Assert.Equal(0, changedCount);

        engine.CommitTransaction();

        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void NestedTransactions_OnlyFinalCommitNormalizes()
    {
        var layout = new DockLayoutRoot();
        var engine = new DockEngine(layout);
        var changedCount = 0;
        engine.LayoutChanged += () => changedCount++;

        engine.BeginTransaction();
        engine.BeginTransaction();

        engine.Dock(CreateItem("a"), layout.MainDocumentHost, DockDirection.Center);
        engine.CommitTransaction(); // inner - should not fire

        Assert.Equal(0, changedCount);
        Assert.True(engine.IsInTransaction);

        engine.CommitTransaction(); // outer - should fire

        Assert.Equal(1, changedCount);
        Assert.False(engine.IsInTransaction);
    }
}
