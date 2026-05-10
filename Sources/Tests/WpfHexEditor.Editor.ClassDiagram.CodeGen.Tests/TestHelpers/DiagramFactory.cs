//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
//////////////////////////////////////////////

using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.CodeGen.Tests.TestHelpers;

/// <summary>
/// Convenience builders for synthetic <see cref="DiagramDocument"/> instances
/// used across the code-generation test suite.
/// </summary>
internal static class DiagramFactory
{
    public static DiagramDocument SingleClass(string name, params ClassMember[] members)
    {
        var node = ClassNode.Create(name);
        node.Members.AddRange(members);
        return new DiagramDocument { Classes = { node } };
    }

    public static DiagramDocument WithInheritance(string derived, string baseName)
    {
        var doc = new DiagramDocument
        {
            Classes =
            {
                ClassNode.Create(baseName),
                ClassNode.Create(derived)
            }
        };
        doc.Relationships.Add(new ClassRelationship
        {
            SourceId = derived,
            TargetId = baseName,
            Kind = RelationshipKind.Inheritance
        });
        return doc;
    }

    public static DiagramDocument Interface(string name, params ClassMember[] methods)
    {
        var node = ClassNode.Create(name, ClassKind.Interface);
        node.Members.AddRange(methods);
        return new DiagramDocument { Classes = { node } };
    }

    public static DiagramDocument Enum(string name, params string[] values)
    {
        var node = ClassNode.Create(name, ClassKind.Enum);
        foreach (var v in values)
            node.Members.Add(new ClassMember { Name = v });
        return new DiagramDocument { Classes = { node } };
    }

    public static ClassMember Field(string name, string type) =>
        new() { Name = name, TypeName = type, Kind = MemberKind.Field };

    public static ClassMember Property(string name, string type) =>
        new() { Name = name, TypeName = type, Kind = MemberKind.Property };

    public static ClassMember Method(string name, string returnType = "", params string[] parameters) =>
        new()
        {
            Name = name,
            TypeName = returnType,
            Kind = MemberKind.Method,
            Parameters = parameters.ToList()
        };

    public static ClassMember Event(string name, string handlerType = "EventHandler") =>
        new() { Name = name, TypeName = handlerType, Kind = MemberKind.Event };
}
