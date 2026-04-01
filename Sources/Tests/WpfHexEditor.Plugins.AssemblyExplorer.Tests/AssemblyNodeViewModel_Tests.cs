// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer.Tests
// File: AssemblyNodeViewModel_Tests.cs
// Description:
//     Tests for AssemblyNodeViewModel tree state, INPC, and filtering logic.
//     Tests node expansion/selection/visibility state without loading real assemblies.
// ==========================================================

using System.ComponentModel;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Tests;

// Minimal concrete node for testing abstract base
internal sealed class TestNode : AssemblyNodeViewModel
{
    public override string DisplayName { get; }
    public override string IconGlyph  => "\uE74C";

    public TestNode(string name = "TestNode") => DisplayName = name;
}

[TestClass]
public sealed class AssemblyNodeViewModel_Tests
{
    // ── Tree state ────────────────────────────────────────────────────────────

    [TestMethod]
    public void IsExpanded_DefaultsFalse()
    {
        var node = new TestNode();
        Assert.IsFalse(node.IsExpanded);
    }

    [TestMethod]
    public void IsSelected_DefaultsFalse()
    {
        var node = new TestNode();
        Assert.IsFalse(node.IsSelected);
    }

    [TestMethod]
    public void IsLoading_DefaultsFalse()
    {
        var node = new TestNode();
        Assert.IsFalse(node.IsLoading);
    }

    [TestMethod]
    public void IsVisible_DefaultsTrue()
    {
        var node = new TestNode();
        Assert.IsTrue(node.IsVisible);
    }

    [TestMethod]
    public void IsPublic_DefaultsTrue()
    {
        var node = new TestNode();
        Assert.IsTrue(node.IsPublic);
    }

    // ── INPC ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public void IsExpanded_RaisesPropertyChanged()
    {
        var node     = new TestNode();
        var fired    = false;
        node.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(node.IsExpanded)) fired = true; };
        node.IsExpanded = true;
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void IsSelected_RaisesPropertyChanged()
    {
        var node  = new TestNode();
        var fired = false;
        node.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(node.IsSelected)) fired = true; };
        node.IsSelected = true;
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void IsVisible_RaisesPropertyChanged()
    {
        var node  = new TestNode();
        var fired = false;
        node.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(node.IsVisible)) fired = true; };
        node.IsVisible = false;
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void SetField_SameValue_DoesNotRaisePropertyChanged()
    {
        var node  = new TestNode();
        var count = 0;
        node.PropertyChanged += (_, _) => count++;
        node.IsExpanded = false; // same as default
        Assert.AreEqual(0, count);
    }

    // ── Tree structure ────────────────────────────────────────────────────────

    [TestMethod]
    public void Children_DefaultsEmpty()
    {
        var node = new TestNode();
        Assert.IsNotNull(node.Children);
        Assert.AreEqual(0, node.Children.Count);
    }

    [TestMethod]
    public void AddChild_AppendedToChildren()
    {
        var parent = new TestNode("Parent");
        var child  = new TestNode("Child");
        parent.Children.Add(child);

        Assert.AreEqual(1, parent.Children.Count);
        Assert.AreSame(child, parent.Children[0]);
    }

    [TestMethod]
    public void ToolTipText_DefaultsToDisplayName()
    {
        var node = new TestNode("My Node");
        Assert.AreEqual("My Node", node.ToolTipText);
    }

    // ── PE metadata ──────────────────────────────────────────────────────────

    [TestMethod]
    public void PeOffset_DefaultsZero()
    {
        var node = new TestNode();
        Assert.AreEqual(0L, node.PeOffset);
    }

    [TestMethod]
    public void MetadataToken_DefaultsZero()
    {
        var node = new TestNode();
        Assert.AreEqual(0, node.MetadataToken);
    }

    [TestMethod]
    public void ByteLength_DefaultsZero()
    {
        var node = new TestNode();
        Assert.AreEqual(0, node.ByteLength);
    }

    [TestMethod]
    public void OwnerFilePath_DefaultsNull()
    {
        var node = new TestNode();
        Assert.IsNull(node.OwnerFilePath);
    }

    [TestMethod]
    public void ByteLength_CanBeSet()
    {
        var node = new TestNode();
        node.ByteLength = 42;
        Assert.AreEqual(42, node.ByteLength);
    }

    [TestMethod]
    public void OwnerFilePath_CanBeSet()
    {
        var node = new TestNode();
        node.OwnerFilePath = "/path/to/test.dll";
        Assert.AreEqual("/path/to/test.dll", node.OwnerFilePath);
    }
}
