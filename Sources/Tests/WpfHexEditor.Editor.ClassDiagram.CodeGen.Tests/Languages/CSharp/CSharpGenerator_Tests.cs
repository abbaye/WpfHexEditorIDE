//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.TestHelpers;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.Languages.CSharp;

[TestClass]
public class CSharpGenerator_Tests
{
    [TestMethod]
    public void Generate_EmptyDocument_StillProducesValidSource()
    {
        var src = CodeGenerationPipeline.GenerateCSharp(new DiagramDocument());

        CSharpCompileGate.AssertParsesCleanly(src);
    }

    [TestMethod]
    public void Generate_SimpleClassWithProperty_CompilesAndContainsExpectedTokens()
    {
        var doc = DiagramFactory.SingleClass("Foo", DiagramFactory.Property("Bar", "int"));

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "public class Foo");
        StringAssert.Contains(src, "public int Bar");
        StringAssert.Contains(src, "{ get; set; }");
    }

    [TestMethod]
    public void Generate_AbstractClass_EmitsAbstractKeyword()
    {
        var node = ClassNode.Create("Shape");
        node.IsAbstract = true;
        node.Members.Add(new ClassMember
        {
            Name = "Area",
            Kind = MemberKind.Method,
            TypeName = "double",
            IsAbstract = true
        });
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "public abstract class Shape");
        StringAssert.Contains(src, "abstract double Area();");
    }

    [TestMethod]
    public void Generate_Interface_EmitsAbstractMembersWithoutBody()
    {
        var doc = DiagramFactory.Interface("IRepo",
            DiagramFactory.Method("Get", "Entity", "int id"));

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "public interface IRepo");
        StringAssert.Contains(src, "Entity Get(int id);");
    }

    [TestMethod]
    public void Generate_Inheritance_EmitsBaseTypeColon()
    {
        var doc = DiagramFactory.WithInheritance(derived: "Dog", baseName: "Animal");

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "public class Dog : Animal");
    }

    [TestMethod]
    public void Generate_Enum_EmitsEnumDeclarationWithValues()
    {
        var doc = DiagramFactory.Enum("Color", "Red", "Green", "Blue");

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "public enum Color");
        StringAssert.Contains(src, "Red");
        StringAssert.Contains(src, "Green");
        StringAssert.Contains(src, "Blue");
    }

    [TestMethod]
    public void Generate_Record_EmitsRecordKeyword()
    {
        var node = ClassNode.Create("Point");
        node.IsRecord = true;
        node.Members.Add(DiagramFactory.Property("X", "int"));
        node.Members.Add(DiagramFactory.Property("Y", "int"));
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "record Point");
    }

    [TestMethod]
    public void Generate_PartialClass_EmitsPartialKeyword()
    {
        var node = ClassNode.Create("Foo");
        node.IsPartial = true;
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "partial class Foo");
    }

    [TestMethod]
    public void Generate_AsyncMethod_EmitsAsyncTaskSignature()
    {
        var node = ClassNode.Create("Service");
        node.Members.Add(new ClassMember
        {
            Name = "FetchAsync",
            Kind = MemberKind.Method,
            TypeName = "string",
            IsAsync = true
        });
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "async Task<string> FetchAsync");
    }

    [TestMethod]
    public void Generate_OverrideMethod_EmitsOverrideKeyword()
    {
        var node = ClassNode.Create("Foo");
        node.Members.Add(new ClassMember
        {
            Name = "ToString",
            Kind = MemberKind.Method,
            TypeName = "string",
            IsOverride = true
        });
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "public override string ToString");
    }

    [TestMethod]
    public void Generate_FieldWithStaticModifier_EmitsStaticKeyword()
    {
        var node = ClassNode.Create("Foo");
        node.Members.Add(new ClassMember
        {
            Name = "_count",
            Kind = MemberKind.Field,
            TypeName = "int",
            Visibility = MemberVisibility.Private,
            IsStatic = true
        });
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "private static int _count");
    }

    [TestMethod]
    public void Generate_Event_EmitsEventDeclaration()
    {
        var node = ClassNode.Create("Foo");
        node.Members.Add(DiagramFactory.Event("Changed"));
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "public event EventHandler Changed");
    }

    [TestMethod]
    public void Generate_XmlDocSummary_EmitsSummaryTags()
    {
        var node = ClassNode.Create("Foo");
        node.XmlDocSummary = "A foo type.";
        var doc = new DiagramDocument { Classes = { node } };

        var src = CodeGenerationPipeline.GenerateCSharp(doc);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "<summary>");
        StringAssert.Contains(src, "A foo type.");
    }

    [TestMethod]
    public void Generate_TabIndent_UsesTabCharacters()
    {
        var doc = DiagramFactory.SingleClass("Foo", DiagramFactory.Property("Bar", "int"));
        var options = CodeGenOptions.Default with { IndentStyle = IndentStyle.Tabs };

        var src = CodeGenerationPipeline.GenerateCSharp(doc, options);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "\tpublic int Bar");
    }

    [TestMethod]
    public void Generate_LegacyCSharp7_3_DoesNotEmitNullableContext()
    {
        var doc = DiagramFactory.SingleClass("Foo");

        var src = CodeGenerationPipeline.GenerateCSharp(doc, CodeGenOptions.LegacyCSharp);

        CSharpCompileGate.AssertParsesCleanly(src);
        Assert.IsFalse(src.Contains("#nullable enable"),
            "Legacy options must not emit a #nullable directive.");
    }

    [TestMethod]
    public void Generate_LegacyCSharp_UsesBlockScopedNamespace()
    {
        var doc = DiagramFactory.SingleClass("Foo");

        var src = CodeGenerationPipeline.GenerateCSharp(doc, CodeGenOptions.LegacyCSharp);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "namespace GeneratedDiagram\n{");
    }

    [TestMethod]
    public void Generate_ModernCSharp_UsesFileScopedNamespace()
    {
        var doc = DiagramFactory.SingleClass("Foo");

        var src = CodeGenerationPipeline.GenerateCSharp(doc, CodeGenOptions.ModernCSharp);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "namespace GeneratedDiagram;");
    }

    [TestMethod]
    public void Generate_UsesCustomRootNamespace()
    {
        var doc = DiagramFactory.SingleClass("Foo");
        var options = CodeGenOptions.Default with { RootNamespace = "Acme.Domain" };

        var src = CodeGenerationPipeline.GenerateCSharp(doc, options);

        CSharpCompileGate.AssertParsesCleanly(src);
        StringAssert.Contains(src, "Acme.Domain");
    }

    [TestMethod]
    public void Generate_HeaderDisabled_EmitsNoBanner()
    {
        var doc = DiagramFactory.SingleClass("Foo");
        var options = CodeGenOptions.Default with { EmitHeader = false };

        var src = CodeGenerationPipeline.GenerateCSharp(doc, options);

        CSharpCompileGate.AssertParsesCleanly(src);
        Assert.IsFalse(src.StartsWith("// <auto-generated />"),
            "Header should not be emitted when EmitHeader is false.");
    }
}
