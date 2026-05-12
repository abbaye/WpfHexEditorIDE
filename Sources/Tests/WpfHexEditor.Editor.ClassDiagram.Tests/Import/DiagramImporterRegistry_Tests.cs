// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Tests
// File: Import/DiagramImporterRegistry_Tests.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Import;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.Import;

[TestClass]
public class DiagramImporterRegistry_Tests
{
    [TestInitialize] public void Reset() => DiagramImporterRegistry.ResetForTests();
    [TestCleanup]    public void Cleanup() => DiagramImporterRegistry.ResetForTests();

    [TestMethod]
    public void Register_LookupByIdAndExtension()
    {
        var m = new MermaidImporter();
        DiagramImporterRegistry.Register(m);

        Assert.AreSame(m, DiagramImporterRegistry.TryGetById("mermaid"));
        Assert.AreSame(m, DiagramImporterRegistry.TryGetByFilePath(@"C:\foo.mmd"));
        Assert.AreSame(m, DiagramImporterRegistry.TryGetByFilePath("foo.mermaid"));
    }

    [TestMethod]
    public void TryDetectByContent_ReturnsMatching()
    {
        DiagramImporterRegistry.Register(new MermaidImporter());
        DiagramImporterRegistry.Register(new PlantUmlImporter());

        Assert.AreEqual("mermaid",  DiagramImporterRegistry.TryDetectByContent("classDiagram\n  class A")?.FormatId);
        Assert.AreEqual("plantuml", DiagramImporterRegistry.TryDetectByContent("@startuml\nclass A\n@enduml")?.FormatId);
        Assert.IsNull(DiagramImporterRegistry.TryDetectByContent(""));
    }

    [TestMethod]
    public void All_StableOrder()
    {
        DiagramImporterRegistry.Register(new PlantUmlImporter());
        DiagramImporterRegistry.Register(new MermaidImporter());

        var ids = DiagramImporterRegistry.All().Select(i => i.FormatId).ToArray();
        CollectionAssert.AreEqual(new[] { "mermaid", "plantuml" }, ids);
    }

    [TestMethod]
    public void Register_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            DiagramImporterRegistry.Register(null!));
    }
}
