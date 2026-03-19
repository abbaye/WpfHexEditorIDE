// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Export/CSharpSkeletonGenerator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Generates a compilable C# skeleton source file from a
//     DiagramDocument.  Produces namespace block, type declarations
//     with correct modifiers, auto-properties, method stubs with empty
//     bodies, and enum members.
//
// Architecture Notes:
//     Pattern: Generator/Template Method — each node kind has a
//     dedicated private method; the top-level Generate method
//     orchestrates calls in document order.
//
//     No WPF or platform dependencies — pure StringBuilder output.
//     Relationship info is not emitted (no base-class syntax from
//     relationships) because the diagram may reference external types
//     not present in the document.  Inheritance from ClassRelationship
//     IS included for Inheritance-kind relationships.
// ==========================================================

using System.Text;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Export;

/// <summary>
/// Produces a valid C# skeleton source from a <see cref="DiagramDocument"/>.
/// </summary>
public static class CSharpSkeletonGenerator
{
    private const string Indent = "    ";

    /// <summary>
    /// Generates C# skeleton source text for all types in <paramref name="doc"/>.
    /// </summary>
    public static string Generate(DiagramDocument doc)
    {
        var sb = new StringBuilder();

        // Build a lookup: type id → base type name (first Inheritance relationship)
        var baseTypeOf = doc.Relationships
            .Where(r => r.Kind == RelationshipKind.Inheritance)
            .GroupBy(r => r.SourceId)
            .ToDictionary(
                g => g.Key,
                g => g.First().TargetId,
                StringComparer.Ordinal);

        sb.AppendLine("// Auto-generated C# skeleton — WpfHexEditor ClassDiagram");
        sb.AppendLine();
        sb.AppendLine("namespace GeneratedDiagram;");
        sb.AppendLine();

        foreach (var node in doc.Classes)
        {
            baseTypeOf.TryGetValue(node.Id, out var baseType);
            WriteTypeDeclaration(sb, node, baseType);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // -------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------

    private static void WriteTypeDeclaration(
        StringBuilder sb, ClassNode node, string? baseType)
    {
        // Type keyword + modifiers
        var keyword = node.Kind switch
        {
            ClassKind.Interface => "interface",
            ClassKind.Enum => "enum",
            ClassKind.Struct => "struct",
            ClassKind.Abstract => "abstract class",
            _ when node.IsAbstract => "abstract class",
            _ => "class"
        };

        sb.Append("public ").Append(keyword).Append(' ').Append(node.Name);

        if (!string.IsNullOrEmpty(baseType) && node.Kind != ClassKind.Enum)
            sb.Append(" : ").Append(baseType);

        sb.AppendLine();
        sb.AppendLine("{");

        if (node.Kind == ClassKind.Enum)
        {
            WriteEnumMembers(sb, node);
        }
        else
        {
            WriteTypeMembers(sb, node);
        }

        sb.AppendLine("}");
    }

    private static void WriteEnumMembers(StringBuilder sb, ClassNode node)
    {
        for (var i = 0; i < node.Members.Count; i++)
        {
            var member = node.Members[i];
            var comma = i < node.Members.Count - 1 ? "," : string.Empty;
            sb.AppendLine($"{Indent}{member.Name}{comma}");
        }
    }

    private static void WriteTypeMembers(StringBuilder sb, ClassNode node)
    {
        // Fields
        foreach (var m in node.Fields)
            WriteField(sb, m);

        if (node.Fields.Any() && (node.Properties.Any() || node.Methods.Any() || node.Events.Any()))
            sb.AppendLine();

        // Properties
        foreach (var m in node.Properties)
            WriteProperty(sb, m);

        if (node.Properties.Any() && (node.Methods.Any() || node.Events.Any()))
            sb.AppendLine();

        // Events
        foreach (var m in node.Events)
            WriteEvent(sb, m);

        if (node.Events.Any() && node.Methods.Any())
            sb.AppendLine();

        // Methods
        foreach (var m in node.Methods)
            WriteMethod(sb, m, node.Kind == ClassKind.Interface || m.IsAbstract);
    }

    private static void WriteField(StringBuilder sb, ClassMember m)
    {
        var vis = VisibilityKeyword(m.Visibility);
        var staticMod = m.IsStatic ? "static " : string.Empty;
        var typeName = string.IsNullOrEmpty(m.TypeName) ? "object" : m.TypeName;
        sb.AppendLine($"{Indent}{vis} {staticMod}{typeName} {m.Name};");
    }

    private static void WriteProperty(StringBuilder sb, ClassMember m)
    {
        var vis = VisibilityKeyword(m.Visibility);
        var staticMod = m.IsStatic ? "static " : string.Empty;
        var typeName = string.IsNullOrEmpty(m.TypeName) ? "object" : m.TypeName;
        sb.AppendLine($"{Indent}{vis} {staticMod}{typeName} {m.Name} {{ get; set; }}");
    }

    private static void WriteEvent(StringBuilder sb, ClassMember m)
    {
        var vis = VisibilityKeyword(m.Visibility);
        var typeName = string.IsNullOrEmpty(m.TypeName) ? "EventHandler" : m.TypeName;
        sb.AppendLine($"{Indent}{vis} event {typeName} {m.Name};");
    }

    private static void WriteMethod(StringBuilder sb, ClassMember m, bool isAbstractContext)
    {
        var vis = VisibilityKeyword(m.Visibility);
        var staticMod = m.IsStatic ? "static " : string.Empty;
        var abstractMod = (m.IsAbstract || isAbstractContext) && !m.IsStatic ? "abstract " : string.Empty;
        var returnType = string.IsNullOrEmpty(m.TypeName) ? "void" : m.TypeName;
        var paramsText = string.Join(", ", m.Parameters);

        if (isAbstractContext || m.IsAbstract)
        {
            // Interface member or abstract method — no body
            sb.AppendLine($"{Indent}{vis} {abstractMod}{returnType} {m.Name}({paramsText});");
        }
        else
        {
            sb.AppendLine($"{Indent}{vis} {staticMod}{returnType} {m.Name}({paramsText})");
            sb.AppendLine($"{Indent}{{");
            if (returnType != "void")
                sb.AppendLine($"{Indent}{Indent}return default!;");
            sb.AppendLine($"{Indent}}}");
        }
    }

    private static string VisibilityKeyword(MemberVisibility v) =>
        v switch
        {
            MemberVisibility.Private => "private",
            MemberVisibility.Protected => "protected",
            MemberVisibility.Internal => "internal",
            _ => "public"
        };
}
