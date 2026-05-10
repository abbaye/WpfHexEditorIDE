//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.TestHelpers;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.Integration;

[TestClass]
public class EndToEnd_Tests
{
    [TestMethod]
    public void Pipeline_CSharpAndVB_BothProduceValidSourceForSameDiagram()
    {
        var doc = DiagramFactory.WithInheritance(derived: "Dog", baseName: "Animal");

        var csharp = CodeGenerationPipeline.Generate(doc, LanguageIds.CSharp);
        var vb = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        CSharpCompileGate.AssertParsesCleanly(csharp);
        VBCompileGate.AssertParsesCleanly(vb);

        StringAssert.Contains(csharp, "class Dog");
        StringAssert.Contains(csharp, ": Animal");
        StringAssert.Contains(vb, "Class Dog");
        StringAssert.Contains(vb, "Inherits Animal");
    }

    [TestMethod]
    public void Pipeline_OptionsRespectedAcrossLanguages()
    {
        var doc = DiagramFactory.SingleClass("Foo");
        var customOptions = CodeGenOptions.Default with { RootNamespace = "Acme.End2End" };

        var csharp = CodeGenerationPipeline.Generate(doc, LanguageIds.CSharp, customOptions);
        var vb = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic, customOptions);

        StringAssert.Contains(csharp, "Acme.End2End");
        StringAssert.Contains(vb, "Acme.End2End");
    }

    [TestMethod]
    public void Pipeline_LegacyCSharp_ProducesValidSourceWithoutModernFeatures()
    {
        var doc = DiagramFactory.SingleClass("Foo", DiagramFactory.Property("Bar", "int"));

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.CSharp, CodeGenOptions.LegacyCSharp);

        CSharpCompileGate.AssertParsesCleanly(src);
        Assert.IsFalse(src.Contains("#nullable enable"));
        StringAssert.Contains(src, "namespace GeneratedDiagram\n{");
    }
}
