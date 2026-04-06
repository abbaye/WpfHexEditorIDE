// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer.Tests
// File: AssemblyRootNodeViewModelTests.cs
// Contributors: Claude Sonnet 4.6
// Description:
//     Tests for AssemblyRootNodeViewModel — DisplayName formatting,
//     IsPinned change notification, ToolTipText, defaults.
// ==========================================================

using System.ComponentModel;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Tests;

[TestClass]
public sealed class AssemblyRootNodeViewModelTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AssemblyModel MakeModel(
        string name          = "TestAssembly",
        string filePath      = @"C:\plugins\test.dll",
        Version? version     = null,
        string? targetFw     = null,
        bool isManaged       = true,
        int typeCount        = 0,
        int refCount         = 0) =>
        new AssemblyModel
        {
            Name            = name,
            FilePath        = filePath,
            Version         = version,
            TargetFramework = targetFw,
            IsManaged       = isManaged,
            Types           = Enumerable.Repeat(CreateType(), typeCount).ToList(),
            References      = Enumerable.Repeat(CreateRef(), refCount).ToList()
        };

    private static TypeModel CreateType() =>
        new TypeModel { Namespace = "MyNs", Name = "MyType" };

    private static AssemblyRef CreateRef() =>
        new AssemblyRef("Ref", new Version(1, 0), string.Empty);

    // ── DisplayName formatting ────────────────────────────────────────────────

    [TestMethod]
    public void DisplayName_NoVersionNoTfm_OnlyName()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel("MyLib"));
        Assert.AreEqual("MyLib", vm.DisplayName);
    }

    [TestMethod]
    public void DisplayName_WithVersion_IncludesVersion()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel("MyLib", version: new Version(1, 2, 3)));
        Assert.AreEqual("MyLib v1.2.3", vm.DisplayName);
    }

    [TestMethod]
    public void DisplayName_WithTargetFramework_IncludesBadge()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel("MyLib", targetFw: ".NET 8.0"));
        Assert.AreEqual("MyLib  [.NET 8.0]", vm.DisplayName);
    }

    [TestMethod]
    public void DisplayName_VersionAndTfm_BothIncluded()
    {
        var vm = new AssemblyRootNodeViewModel(
            MakeModel("MyLib", version: new Version(2, 0, 0), targetFw: ".NET 8.0"));
        Assert.AreEqual("MyLib v2.0.0  [.NET 8.0]", vm.DisplayName);
    }

    [TestMethod]
    public void DisplayName_EmptyTargetFramework_NoBadge()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel("MyLib", targetFw: ""));
        Assert.IsFalse(vm.DisplayName.Contains('['), "No badge expected for empty TFM");
    }

    // ── Defaults ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void IsExpanded_DefaultsTrue()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel());
        Assert.IsTrue(vm.IsExpanded, "Root nodes should be expanded by default");
    }

    [TestMethod]
    public void IsPinned_DefaultsFalse()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel());
        Assert.IsFalse(vm.IsPinned);
    }

    [TestMethod]
    public void IconGlyph_IsNotEmpty()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel());
        Assert.IsFalse(string.IsNullOrEmpty(vm.IconGlyph));
    }

    // ── IsPinned PropertyChanged ──────────────────────────────────────────────

    [TestMethod]
    public void IsPinned_Set_RaisesPropertyChanged()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel());
        string? raised = null;
        vm.PropertyChanged += (_, e) => raised = e.PropertyName;

        vm.IsPinned = true;

        Assert.AreEqual(nameof(AssemblyRootNodeViewModel.IsPinned), raised);
    }

    [TestMethod]
    public void IsPinned_SameValue_DoesNotRaisePropertyChanged()
    {
        var vm = new AssemblyRootNodeViewModel(MakeModel());
        var count = 0;
        vm.PropertyChanged += (_, _) => count++;

        vm.IsPinned = false; // already false

        Assert.AreEqual(0, count);
    }

    // ── ToolTipText ───────────────────────────────────────────────────────────

    [TestMethod]
    public void ToolTipText_Managed_ContainsFilePath()
    {
        var vm = new AssemblyRootNodeViewModel(
            MakeModel(filePath: @"C:\test.dll", isManaged: true, typeCount: 5, refCount: 3));

        Assert.IsTrue(vm.ToolTipText.Contains(@"C:\test.dll"));
    }

    [TestMethod]
    public void ToolTipText_Managed_ContainsTypesAndRefs()
    {
        var vm = new AssemblyRootNodeViewModel(
            MakeModel(isManaged: true, typeCount: 7, refCount: 4));

        Assert.IsTrue(vm.ToolTipText.Contains("Types: 7"),  $"Got: {vm.ToolTipText}");
        Assert.IsTrue(vm.ToolTipText.Contains("References: 4"), $"Got: {vm.ToolTipText}");
    }

    [TestMethod]
    public void ToolTipText_Native_ContainsNativePEMessage()
    {
        var vm = new AssemblyRootNodeViewModel(
            MakeModel(isManaged: false));

        Assert.IsTrue(vm.ToolTipText.Contains("Native PE"), $"Got: {vm.ToolTipText}");
    }

    // ── Model binding ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Model_ReturnsConstructorArgument()
    {
        var model = MakeModel("ModelTest");
        var vm = new AssemblyRootNodeViewModel(model);

        Assert.AreSame(model, vm.Model);
    }
}
