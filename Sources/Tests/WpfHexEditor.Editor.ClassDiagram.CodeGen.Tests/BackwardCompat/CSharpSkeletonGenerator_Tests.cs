//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.TestHelpers;
using WpfHexEditor.Editor.ClassDiagram.Core.Export;

namespace WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.BackwardCompat;

/// <summary>
/// Pins the public surface of the legacy <see cref="CSharpSkeletonGenerator"/>
/// façade so external callers (e.g. ClassDiagramExportService) keep working
/// after the Roslyn-based reimplementation.
/// </summary>
[TestClass]
public class CSharpSkeletonGenerator_Tests
{
    [TestMethod]
    public void Generate_LegacyApi_StillProducesValidCSharp()
    {
        var doc = DiagramFactory.SingleClass("Foo", DiagramFactory.Property("Bar", "int"));

        var src = CSharpSkeletonGenerator.Generate(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "class Foo");
        StringAssert.Contains(src, "Bar");
    }

    [TestMethod]
    public void Generate_LegacyApi_NeverThrows_OnEmptyDocument()
    {
        var src = CSharpSkeletonGenerator.Generate(new Core.Model.DiagramDocument());

        CSharpCompileGate.AssertParsesCleanly(src);
    }
}
