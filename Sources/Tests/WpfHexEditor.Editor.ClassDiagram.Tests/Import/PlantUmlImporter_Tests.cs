// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Tests
// File: Import/PlantUmlImporter_Tests.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// ==========================================================

using WpfHexEditor.Editor.ClassDiagram.Core.Import;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Tests.Import;

[TestClass]
public class PlantUmlImporter_Tests
{
    private PlantUmlImporter _importer = null!;

    [TestInitialize] public void Setup() => _importer = new PlantUmlImporter();

    [TestMethod]
    public void Metadata_IsPlantUml()
    {
        Assert.AreEqual("plantuml", _importer.FormatId);
        CollectionAssert.AreEqual(new[] { ".puml", ".plantuml", ".iuml" }, _importer.FileExtensions.ToArray());
    }

    [TestMethod]
    public void Import_ClassWithMembers()
    {
        const string src = @"@startuml
class Foo {
  +int x
  -bar(): string
  {static} #count: int
}
@enduml";
        var res = _importer.Import(src);
        var foo = res.Document.Classes.Single();
        Assert.AreEqual(3, foo.Members.Count);

        var x = foo.Members.First(m => m.Name == "x");
        Assert.AreEqual(MemberKind.Field, x.Kind);
        Assert.AreEqual(MemberVisibility.Public, x.Visibility);

        var bar = foo.Members.First(m => m.Name == "bar");
        Assert.AreEqual(MemberKind.Method, bar.Kind);
        Assert.AreEqual("string", bar.TypeName);

        var count = foo.Members.First(m => m.Name == "count");
        Assert.IsTrue(count.IsStatic);
        Assert.AreEqual(MemberVisibility.Protected, count.Visibility);
    }

    [TestMethod]
    public void Import_InterfaceAndAbstract()
    {
        const string src = @"@startuml
interface IFoo
abstract class Bar
class Baz extends Bar implements IFoo
@enduml";
        var res = _importer.Import(src);
        Assert.AreEqual(3, res.Document.Classes.Count);

        var iface = res.Document.Classes.First(c => c.Name == "IFoo");
        Assert.AreEqual(ClassKind.Interface, iface.Kind);

        var bar = res.Document.Classes.First(c => c.Name == "Bar");
        Assert.IsTrue(bar.IsAbstract);

        var baz = res.Document.Classes.First(c => c.Name == "Baz");
        var inheritance = res.Document.Relationships.Single(r =>
            r.SourceId == baz.Id && r.Kind == RelationshipKind.Inheritance);
        Assert.AreEqual(bar.Id, inheritance.TargetId);
        var realization = res.Document.Relationships.Single(r =>
            r.SourceId == baz.Id && r.Kind == RelationshipKind.Realization);
        Assert.AreEqual(iface.Id, realization.TargetId);
    }

    [TestMethod]
    public void Import_RelationshipArrows()
    {
        const string src = @"@startuml
A <|-- B
A *-- C
A o-- D
A --> E
A ..> F
@enduml";
        var res = _importer.Import(src);
        var byTarget = res.Document.Relationships.ToDictionary(
            r => res.Document.Classes.First(c => c.Id == r.TargetId).Name, r => r.Kind);
        Assert.AreEqual(RelationshipKind.Inheritance, byTarget["B"]);
        Assert.AreEqual(RelationshipKind.Composition, byTarget["C"]);
        Assert.AreEqual(RelationshipKind.Aggregation, byTarget["D"]);
        Assert.AreEqual(RelationshipKind.Association, byTarget["E"]);
        Assert.AreEqual(RelationshipKind.Dependency,  byTarget["F"]);
    }

    [TestMethod]
    public void Import_IgnoresSkinparamAndComments()
    {
        const string src = @"@startuml
skinparam classBackgroundColor white
' this is a comment
class A
@enduml";
        var res = _importer.Import(src);
        Assert.AreEqual(1, res.Document.Classes.Count);
    }

    [TestMethod]
    public void Import_EmptyContent_Throws()
    {
        Assert.ThrowsExactly<ImportException>(() => _importer.Import(""));
    }
}
