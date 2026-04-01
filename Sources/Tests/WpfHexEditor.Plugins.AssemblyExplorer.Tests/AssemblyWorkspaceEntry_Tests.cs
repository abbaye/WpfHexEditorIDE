// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer.Tests
// File: AssemblyWorkspaceEntry_Tests.cs
// Description:
//     Tests for AssemblyWorkspaceEntry — pin propagation, cancellation state.
// ==========================================================

using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Tests;

[TestClass]
public sealed class AssemblyWorkspaceEntry_Tests
{
    private static AssemblyWorkspaceEntry MakeEntry(string filePath = "test.dll")
    {
        var model = new AssemblyModel { FilePath = filePath };
        var root  = new AssemblyRootNodeViewModel(model);
        var cts   = new CancellationTokenSource();
        return new AssemblyWorkspaceEntry(model, root, cts);
    }

    [TestMethod]
    public void IsPinned_DefaultsFalse()
    {
        var entry = MakeEntry();
        Assert.IsFalse(entry.IsPinned);
    }

    [TestMethod]
    public void IsPinned_Set_PropagatestoRootNode()
    {
        var entry    = MakeEntry();
        entry.IsPinned = true;

        Assert.IsTrue(entry.IsPinned);
        Assert.IsTrue(entry.Root.IsPinned);
    }

    [TestMethod]
    public void IsPinned_Unset_PropagatestoRootNode()
    {
        var entry    = MakeEntry();
        entry.IsPinned = true;
        entry.IsPinned = false;

        Assert.IsFalse(entry.Root.IsPinned);
    }

    [TestMethod]
    public void Cts_NotCancelled_Initially()
    {
        var entry = MakeEntry();
        Assert.IsFalse(entry.Cts.Token.IsCancellationRequested);
    }

    [TestMethod]
    public void Model_FilePath_Matches()
    {
        var entry = MakeEntry("assembly.dll");
        Assert.AreEqual("assembly.dll", entry.Model.FilePath);
    }

    [TestMethod]
    public void LoadTimeMs_CanBeSet()
    {
        var entry     = MakeEntry();
        entry.LoadTimeMs = 350L;
        Assert.AreEqual(350L, entry.LoadTimeMs);
    }
}
