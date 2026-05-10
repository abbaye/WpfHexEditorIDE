//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.TestHelpers;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.Languages.VisualBasic;

[TestClass]
public class VBGenerator_Tests
{
    [TestMethod]
    public void Generate_EmptyDocument_ProducesValidSource()
    {
        var src = CodeGenerationPipeline.Generate(new DiagramDocument(), LanguageIds.VisualBasic);
        VBCompileGate.AssertParsesCleanly(src);
    }

    [TestMethod]
    public void Generate_SimpleClassWithProperty_CompilesAndContainsExpectedTokens()
    {
        var doc = DiagramFactory.SingleClass("Foo", DiagramFactory.Property("Bar", "Integer"));

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Public Class Foo");
        StringAssert.Contains(src, "Public Property Bar As Integer");
    }

    [TestMethod]
    public void Generate_AbstractClass_EmitsMustInheritKeyword()
    {
        var node = ClassNode.Create("Shape");
        node.IsAbstract = true;
        node.Members.Add(new ClassMember
        {
            Name = "Area",
            Kind = MemberKind.Method,
            TypeName = "Double",
            IsAbstract = true
        });
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "MustInherit Class Shape");
        StringAssert.Contains(src, "MustOverride");
    }

    [TestMethod]
    public void Generate_SealedClass_EmitsNotInheritableKeyword()
    {
        var node = ClassNode.Create("FinalThing");
        node.IsSealed = true;
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "NotInheritable Class FinalThing");
    }

    [TestMethod]
    public void Generate_Interface_EmitsInterfaceBlock()
    {
        var doc = DiagramFactory.Interface("IRepo",
            DiagramFactory.Method("Fetch", "Entity", "Integer id"));

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Public Interface IRepo");
        StringAssert.Contains(src, "Function Fetch(");
    }

    [TestMethod]
    public void Generate_Inheritance_EmitsInheritsStatement()
    {
        var doc = DiagramFactory.WithInheritance(derived: "Dog", baseName: "Animal");

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Public Class Dog");
        StringAssert.Contains(src, "Inherits Animal");
    }

    [TestMethod]
    public void Generate_Realization_EmitsImplementsStatement()
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

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Public Class Service");
        StringAssert.Contains(src, "Implements IDisposable");
    }

    [TestMethod]
    public void Generate_Enum_EmitsEnumBlockWithValues()
    {
        var doc = DiagramFactory.Enum("Color", "Red", "Green", "Blue");

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Public Enum Color");
        StringAssert.Contains(src, "Red");
        StringAssert.Contains(src, "Green");
        StringAssert.Contains(src, "Blue");
        StringAssert.Contains(src, "End Enum");
    }

    [TestMethod]
    public void Generate_VoidMethod_EmitsSubBlock()
    {
        var node = ClassNode.Create("Service");
        node.Members.Add(DiagramFactory.Method("DoWork", "void"));
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Public Sub DoWork()");
        StringAssert.Contains(src, "End Sub");
    }

    [TestMethod]
    public void Generate_NonVoidMethod_EmitsFunctionBlock()
    {
        var node = ClassNode.Create("Service");
        node.Members.Add(DiagramFactory.Method("Compute", "Integer"));
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Public Function Compute() As Integer");
        StringAssert.Contains(src, "End Function");
    }

    [TestMethod]
    public void Generate_AsyncMethod_EmitsAsyncTaskSignature()
    {
        var node = ClassNode.Create("Service");
        node.Members.Add(new ClassMember
        {
            Name = "FetchAsync",
            Kind = MemberKind.Method,
            TypeName = "String",
            IsAsync = true
        });
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Async");
        StringAssert.Contains(src, "Task(Of String)");
    }

    [TestMethod]
    public void Generate_OverrideMethod_EmitsOverridesKeyword()
    {
        var node = ClassNode.Create("Foo");
        node.Members.Add(new ClassMember
        {
            Name = "ToString",
            Kind = MemberKind.Method,
            TypeName = "String",
            IsOverride = true
        });
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Overrides");
    }

    [TestMethod]
    public void Generate_StaticField_EmitsSharedKeyword()
    {
        var node = ClassNode.Create("Foo");
        node.Members.Add(new ClassMember
        {
            Name = "Counter",
            Kind = MemberKind.Field,
            TypeName = "Integer",
            Visibility = MemberVisibility.Private,
            IsStatic = true
        });
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Private Shared Counter As Integer");
    }

    [TestMethod]
    public void Generate_Event_EmitsEventStatement()
    {
        var node = ClassNode.Create("Foo");
        node.Members.Add(DiagramFactory.Event("Changed"));
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Public Event Changed As EventHandler");
    }

    [TestMethod]
    public void Generate_XmlDocSummary_EmitsTripleQuoteSummary()
    {
        var node = ClassNode.Create("Foo");
        node.XmlDocSummary = "A foo type.";
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "''' <summary>");
        StringAssert.Contains(src, "A foo type.");
    }

    [TestMethod]
    public void Generate_Namespace_WrapsTypeInNamespaceBlock()
    {
        var doc = DiagramFactory.SingleClass("Foo");
        var options = CodeGenOptions.Default with { RootNamespace = "Acme.Domain" };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic, options);

        VBCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Namespace Acme.Domain");
        StringAssert.Contains(src, "End Namespace");
    }

    [TestMethod]
    public void Generate_HeaderDisabled_EmitsNoBanner()
    {
        var doc = DiagramFactory.SingleClass("Foo");
        var options = CodeGenOptions.Default with { EmitHeader = false };

        var src = CodeGenerationPipeline.Generate(doc, LanguageIds.VisualBasic, options);

        VBCompileGate.AssertParsesCleanly(src);
        Assert.IsFalse(src.StartsWith("' <auto-generated />"),
            "Header should not be emitted when EmitHeader is false.");
    }
}
