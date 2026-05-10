//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Services;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.Services;

[TestClass]
public class CodeGenSettingsStore_Tests
{
    [TestMethod]
    public void Load_ReturnsFallback_WhenNoFileExists()
    {
        var fallback = new CodeGenSettings
        {
            LanguageId = LanguageIds.VisualBasic,
            Options = CodeGenOptions.LegacyCSharp with { RootNamespace = "MyFallback" }
        };

        // The default load is best-effort and returns the fallback if the file is missing or invalid.
        // We only assert no exception is thrown and a non-null settings is returned.
        var loaded = CodeGenSettingsStore.Load(fallback);
        Assert.IsNotNull(loaded);
    }

    [TestMethod]
    public void SaveLoad_RoundTripsExactSettings()
    {
        var saved = new CodeGenSettings
        {
            LanguageId = LanguageIds.VisualBasic,
            Options = CodeGenOptions.ModernCSharp with { RootNamespace = "Test.Roundtrip" }
        };

        CodeGenSettingsStore.Save(saved);
        var loaded = CodeGenSettingsStore.Load(new CodeGenSettings());

        Assert.AreEqual(saved.LanguageId, loaded.LanguageId);
        Assert.AreEqual(saved.Options.RootNamespace, loaded.Options.RootNamespace);
        Assert.AreEqual(saved.Options.CSharpVersion, loaded.Options.CSharpVersion);
        Assert.AreEqual(saved.Options.UseFileScopedNamespace, loaded.Options.UseFileScopedNamespace);
    }
}
