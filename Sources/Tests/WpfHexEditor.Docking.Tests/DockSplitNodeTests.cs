//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Tests;

public class DockSplitNodeTests
{
    [Fact]
    public void AddChild_SetsParent()
    {
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        var group = new DockGroupNode();

        split.AddChild(group);

        Assert.Same(split, group.Parent);
        Assert.Single(split.Children);
    }

    [Fact]
    public void RemoveChild_ClearsParent()
    {
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        var group = new DockGroupNode();
        split.AddChild(group);

        split.RemoveChild(group);

        Assert.Null(group.Parent);
        Assert.Empty(split.Children);
    }

    [Fact]
    public void InsertChild_InsertsAtIndex()
    {
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        var group1 = new DockGroupNode();
        var group2 = new DockGroupNode();
        var group3 = new DockGroupNode();

        split.AddChild(group1);
        split.AddChild(group3);
        split.InsertChild(1, group2);

        Assert.Equal(3, split.Children.Count);
        Assert.Same(group2, split.Children[1]);
    }

    [Fact]
    public void ReplaceChild_SwapsNodes()
    {
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        var old = new DockGroupNode();
        var replacement = new DockGroupNode();
        split.AddChild(old);

        split.ReplaceChild(old, replacement);

        Assert.Same(replacement, split.Children[0]);
        Assert.Same(split, replacement.Parent);
        Assert.Null(old.Parent);
    }

    [Fact]
    public void NormalizeRatios_SumsToOne()
    {
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        split.AddChild(new DockGroupNode(), 3.0);
        split.AddChild(new DockGroupNode(), 7.0);

        split.NormalizeRatios();

        Assert.Equal(1.0, split.Ratios.Sum(), precision: 10);
        Assert.Equal(0.3, split.Ratios[0], precision: 10);
        Assert.Equal(0.7, split.Ratios[1], precision: 10);
    }

    [Fact]
    public void EqualizeRatios_DistributesEvenly()
    {
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        split.AddChild(new DockGroupNode(), 0.1);
        split.AddChild(new DockGroupNode(), 0.9);
        split.AddChild(new DockGroupNode(), 0.5);

        split.EqualizeRatios();

        var expected = 1.0 / 3.0;
        foreach (var ratio in split.Ratios)
            Assert.Equal(expected, ratio, precision: 10);
    }

    [Fact]
    public void ReplaceChild_ThrowsIfNotFound()
    {
        var split = new DockSplitNode { Orientation = SplitOrientation.Horizontal };
        var notChild = new DockGroupNode();
        var replacement = new DockGroupNode();

        Assert.Throws<ArgumentException>(() => split.ReplaceChild(notChild, replacement));
    }
}
