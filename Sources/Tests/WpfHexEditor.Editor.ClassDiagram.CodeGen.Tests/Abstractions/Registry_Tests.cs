//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.Abstractions;

[TestClass]
public class Registry_Tests
{
    [TestMethod]
    public void Registry_AfterFirstGenerate_ContainsBuiltInGenerators()
    {
        // Trigger the static cctor by calling the pipeline once.
        _ = CodeGenerationPipeline.Generate(new DiagramDocument(), LanguageIds.CSharp);

        var ids = CodeGenLanguageRegistry.All.Select(g => g.LanguageId).ToList();
        CollectionAssert.Contains(ids, LanguageIds.CSharp);
        CollectionAssert.Contains(ids, LanguageIds.VisualBasic);
    }

    [TestMethod]
    public void Registry_Resolve_ReturnsCorrectGenerator()
    {
        _ = CodeGenerationPipeline.Generate(new DiagramDocument(), LanguageIds.CSharp);

        Assert.AreEqual(LanguageIds.CSharp, CodeGenLanguageRegistry.Resolve(LanguageIds.CSharp)?.LanguageId);
        Assert.AreEqual(LanguageIds.VisualBasic, CodeGenLanguageRegistry.Resolve(LanguageIds.VisualBasic)?.LanguageId);
    }

    [TestMethod]
    public void Pipeline_UnknownLanguageId_Throws()
    {
        var thrown = false;
        try
        {
            _ = CodeGenerationPipeline.Generate(new DiagramDocument(), "klingon");
        }
        catch (InvalidOperationException)
        {
            thrown = true;
        }
        Assert.IsTrue(thrown, "Expected InvalidOperationException for unknown language id.");
    }
}
