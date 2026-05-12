// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Tests
// File: Import/MermaidImporter_Tests.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Import;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.Import;

[TestClass]
public class MermaidImporter_Tests
{
    private MermaidImporter _importer = null!;

    [TestInitialize] public void Setup() => _importer = new MermaidImporter();

    [TestMethod]
    public void Metadata_IsMermaid()
    {
        Assert.AreEqual("mermaid", _importer.FormatId);
        Assert.AreEqual("Mermaid", _importer.DisplayName);
        CollectionAssert.AreEqual(new[] { ".mmd", ".mermaid" }, _importer.FileExtensions.ToArray());
    }

    [TestMethod]
    public void CanHandle_DetectsClassDiagramHeader()
    {
        Assert.IsTrue(_importer.CanHandle("classDiagram\n  class A"));
        Assert.IsFalse(_importer.CanHandle(""));
        Assert.IsFalse(_importer.CanHandle("not mermaid at all"));
    }

    [TestMethod]
    public void Import_TwoClassesInheritance()
    {
        const string src = @"classDiagram
class Animal
class Dog
Animal <|-- Dog";
        var res = _importer.Import(src);
        Assert.AreEqual(2, res.Document.Classes.Count);
        Assert.AreEqual(1, res.Document.Relationships.Count);
        Assert.AreEqual(RelationshipKind.Inheritance, res.Document.Relationships[0].Kind);
    }

    [TestMethod]
    public void Import_MemberWithVisibility()
    {
        const string src = @"classDiagram
class Foo {
  +int x
  -bar() string
}";
        var res = _importer.Import(src);
        var foo = res.Document.Classes.Single();
        Assert.AreEqual(2, foo.Members.Count);
        var field = foo.Members.First(m => m.Kind == MemberKind.Field);
        Assert.AreEqual("x", field.Name);
        Assert.AreEqual(MemberVisibility.Public, field.Visibility);
        var method = foo.Members.First(m => m.Kind == MemberKind.Method);
        Assert.AreEqual("bar", method.Name);
        Assert.AreEqual(MemberVisibility.Private, method.Visibility);
        Assert.AreEqual("string", method.TypeName);
    }

    [TestMethod]
    public void Import_RelationshipKinds()
    {
        const string src = @"classDiagram
A <|-- B
A *-- C
A o-- D
A --> E";
        var res = _importer.Import(src);
        Assert.AreEqual(5, res.Document.Classes.Count);
        Assert.AreEqual(4, res.Document.Relationships.Count);
        var byTarget = res.Document.Relationships.ToDictionary(
            r => res.Document.Classes.First(c => c.Id == r.TargetId).Name, r => r.Kind);
        Assert.AreEqual(RelationshipKind.Inheritance, byTarget["B"]);
        Assert.AreEqual(RelationshipKind.Composition, byTarget["C"]);
        Assert.AreEqual(RelationshipKind.Aggregation, byTarget["D"]);
        Assert.AreEqual(RelationshipKind.Association, byTarget["E"]);
    }

    [TestMethod]
    public void Import_EmptyContent_Throws()
    {
        Assert.ThrowsExactly<ImportException>(() => _importer.Import(""));
    }
}
