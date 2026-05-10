//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.TestHelpers;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Mapping;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.Mapping;

[TestClass]
public class DiagramToCodeModelMapper_Tests
{
    [TestMethod]
    public void Map_EmptyDocument_ProducesEmptyModel()
    {
        var model = DiagramToCodeModelMapper.Map(new DiagramDocument(), CodeGenOptions.Default);

        Assert.AreEqual(0, model.Types.Count);
        Assert.AreEqual(CodeGenOptions.Default.RootNamespace, model.RootNamespace);
    }

    [TestMethod]
    public void Map_SingleClassWithProperty_PreservesNameAndKind()
    {
        var doc = DiagramFactory.SingleClass("Foo", DiagramFactory.Property("Bar", "int"));

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        Assert.AreEqual(1, model.Types.Count);
        var type = model.Types[0];
        Assert.AreEqual("Foo", type.Name);
        Assert.AreEqual(CodeTypeKind.Class, type.Kind);
        Assert.AreEqual(1, type.Members.Count);
        Assert.AreEqual(CodeMemberKind.Property, type.Members[0].Kind);
    }

    [TestMethod]
    public void Map_AbstractClass_MapsToAbstractClassKind()
    {
        var node = ClassNode.Create("Shape");
        node.IsAbstract = true;
        var doc = new DiagramDocument { Classes = { node } };

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        Assert.AreEqual(CodeTypeKind.AbstractClass, model.Types[0].Kind);
    }

    [TestMethod]
    public void Map_RecordClass_MapsToRecordKind()
    {
        var node = ClassNode.Create("Point");
        node.IsRecord = true;
        var doc = new DiagramDocument { Classes = { node } };

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        Assert.AreEqual(CodeTypeKind.Record, model.Types[0].Kind);
    }

    [TestMethod]
    public void Map_RecordStruct_MapsToRecordStructKind()
    {
        var node = ClassNode.Create("Vec", ClassKind.Struct);
        node.IsRecord = true;
        var doc = new DiagramDocument { Classes = { node } };

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        Assert.AreEqual(CodeTypeKind.RecordStruct, model.Types[0].Kind);
    }

    [TestMethod]
    public void Map_InheritanceRelationship_PopulatesBaseType()
    {
        var doc = DiagramFactory.WithInheritance(derived: "Dog", baseName: "Animal");

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        var dog = model.Types.Single(t => t.Name == "Dog");
        Assert.AreEqual("Animal", dog.BaseType);
    }

    [TestMethod]
    public void Map_RealizationRelationship_PopulatesImplementedInterfaces()
    {
        var doc = new DiagramDocument
        {
            Classes =
            {
                ClassNode.Create("IDisposable", ClassKind.Interface),
                ClassNode.Create("Service")
            },
            Relationships =
            {
                new ClassRelationship
                {
                    SourceId = "Service",
                    TargetId = "IDisposable",
                    Kind = RelationshipKind.Realization
                }
            }
        };

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        var service = model.Types.Single(t => t.Name == "Service");
        CollectionAssert.Contains(service.ImplementedInterfaces.ToList(), "IDisposable");
    }

    [TestMethod]
    public void Map_InterfaceMember_DropsAccessibility()
    {
        var doc = DiagramFactory.Interface("IRepo",
            DiagramFactory.Method("Get", "Entity", "int id"));

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        var member = model.Types[0].Members.Single();
        Assert.AreEqual(CodeAccessibility.NotApplicable, member.Accessibility);
        Assert.IsTrue(member.IsAbstract);
    }

    [TestMethod]
    public void Map_GenericConstraints_AreSplitPerWhereClause()
    {
        var node = ClassNode.Create("Bag");
        node.Members.Add(new ClassMember
        {
            Name = "Add",
            Kind = MemberKind.Method,
            GenericConstraints = "where T : class where U : IDisposable, new()"
        });
        var doc = new DiagramDocument { Classes = { node } };

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        var member = model.Types[0].Members.Single();
        Assert.AreEqual(2, member.GenericParameters.Count);
        Assert.AreEqual("T", member.GenericParameters[0].Name);
        Assert.AreEqual("U", member.GenericParameters[1].Name);
        Assert.AreEqual(2, member.GenericParameters[1].Constraints.Count);
    }

    [TestMethod]
    public void Map_Parameters_SplitTypeAndName()
    {
        var doc = DiagramFactory.SingleClass("Repo",
            DiagramFactory.Method("Save", "void", "Entity entity", "CancellationToken ct"));

        var model = DiagramToCodeModelMapper.Map(doc, CodeGenOptions.Default);

        var method = model.Types[0].Members.Single();
        Assert.AreEqual(2, method.Parameters.Count);
        Assert.AreEqual("Entity", method.Parameters[0].Type);
        Assert.AreEqual("entity", method.Parameters[0].Name);
        Assert.AreEqual("CancellationToken", method.Parameters[1].Type);
        Assert.AreEqual("ct", method.Parameters[1].Name);
    }
}
