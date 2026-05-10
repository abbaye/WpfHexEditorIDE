//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Dialogs;
using WpfHexEditor.Editor.ClassDiagram.Services;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.Dialogs;

[TestClass]
public class ExportCodeDialogViewModel_Tests
{
    [TestMethod]
    public void Constructor_HydratesAllPropertiesFromSettings()
    {
        var settings = new CodeGenSettings
        {
            LanguageId = LanguageIds.VisualBasic,
            Options = CodeGenOptions.LegacyCSharp with { RootNamespace = "Acme.Tests" }
        };

        var vm = new ExportCodeDialogViewModel(settings);

        Assert.AreEqual(LanguageIds.VisualBasic, vm.LanguageId);
        Assert.AreEqual("Acme.Tests", vm.RootNamespace);
        Assert.AreEqual(CSharpLanguageVersion.CSharp7_3, vm.CSharpVersion);
        Assert.IsFalse(vm.UseFileScopedNamespace);
        Assert.IsFalse(vm.NullableContextEnabled);
        Assert.IsFalse(vm.PreferRecords);
    }

    [TestMethod]
    public void BuildOptions_ReturnsCurrentVmState()
    {
        var vm = new ExportCodeDialogViewModel(new CodeGenSettings());
        vm.RootNamespace = "Foo.Bar";
        vm.IndentSize = 2;
        vm.IndentStyle = IndentStyle.Tabs;
        vm.EmitXmlDocs = false;

        var options = vm.BuildOptions();

        Assert.AreEqual("Foo.Bar", options.RootNamespace);
        Assert.AreEqual(2, options.IndentSize);
        Assert.AreEqual(IndentStyle.Tabs, options.IndentStyle);
        Assert.IsFalse(options.EmitXmlDocs);
    }

    [TestMethod]
    public void BuildSettings_PairsLanguageIdWithOptions()
    {
        var vm = new ExportCodeDialogViewModel(new CodeGenSettings());
        vm.LanguageId = LanguageIds.VisualBasic;
        vm.RootNamespace = "Foo";

        var settings = vm.BuildSettings();

        Assert.AreEqual(LanguageIds.VisualBasic, settings.LanguageId);
        Assert.AreEqual("Foo", settings.Options.RootNamespace);
    }

    [TestMethod]
    public void IsCSharpSelected_ReflectsLanguageId()
    {
        var vm = new ExportCodeDialogViewModel(new CodeGenSettings { LanguageId = LanguageIds.CSharp });
        Assert.IsTrue(vm.IsCSharpSelected);

        vm.LanguageId = LanguageIds.VisualBasic;
        Assert.IsFalse(vm.IsCSharpSelected);
    }

    [TestMethod]
    public void ApplyPreset_ModernCSharp_ConfiguresAllOptions()
    {
        var vm = new ExportCodeDialogViewModel(new CodeGenSettings());

        vm.ApplyPreset(CodeGenOptions.ModernCSharp);

        Assert.AreEqual(CSharpLanguageVersion.CSharp12, vm.CSharpVersion);
        Assert.IsTrue(vm.UseFileScopedNamespace);
        Assert.IsTrue(vm.NullableContextEnabled);
        Assert.IsTrue(vm.PreferRecords);
        Assert.IsTrue(vm.EmitAsyncSignatures);
    }

    [TestMethod]
    public void ApplyPreset_LegacyCSharp_DisablesModernOptions()
    {
        var vm = new ExportCodeDialogViewModel(new CodeGenSettings());

        vm.ApplyPreset(CodeGenOptions.LegacyCSharp);

        Assert.AreEqual(CSharpLanguageVersion.CSharp7_3, vm.CSharpVersion);
        Assert.IsFalse(vm.UseFileScopedNamespace);
        Assert.IsFalse(vm.NullableContextEnabled);
        Assert.IsFalse(vm.PreferRecords);
        Assert.IsFalse(vm.EmitAsyncSignatures);
    }

    [TestMethod]
    public void Preview_RegeneratesAfterPropertyChange()
    {
        var vm = new ExportCodeDialogViewModel(new CodeGenSettings());
        var initial = vm.Preview;

        vm.RootNamespace = "Different.Namespace";
        var afterChange = vm.Preview;

        Assert.AreNotEqual(initial, afterChange);
        StringAssert.Contains(afterChange, "Different.Namespace");
    }

    [TestMethod]
    public void IndentSize_IsClampedToValidRange()
    {
        var vm = new ExportCodeDialogViewModel(new CodeGenSettings());

        vm.IndentSize = 100;
        Assert.AreEqual(8, vm.IndentSize);

        vm.IndentSize = 0;
        Assert.AreEqual(1, vm.IndentSize);
    }

    [TestMethod]
    public void AvailableLanguages_IncludesCSharpAndVB()
    {
        var vm = new ExportCodeDialogViewModel(new CodeGenSettings());

        var ids = vm.AvailableLanguages.Select(g => g.LanguageId).ToList();

        CollectionAssert.Contains(ids, LanguageIds.CSharp);
        CollectionAssert.Contains(ids, LanguageIds.VisualBasic);
    }
}
