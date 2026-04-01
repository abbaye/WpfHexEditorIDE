// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Export/MermaidExporter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Exports a DiagramDocument to Mermaid classDiagram syntax that can
//     be rendered by any Mermaid-aware Markdown processor or the
//     Mermaid live editor.
//
// Architecture Notes:
//     Pattern: Exporter — pure transformation, no side-effects.
//     Mermaid arrow notation:
//       <|--  Inheritance
//       ..>   Dependency
//       o--   Aggregation
//       *--   Composition
//       -->   Association
//     Member notation:  +/-/#/~  prefix, type name, method parentheses.
// ==========================================================

using System.Text;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Export;

/// <summary>
/// Exports a <see cref="DiagramDocument"/> to Mermaid <c>classDiagram</c> syntax.
/// </summary>
public static class MermaidExporter
{
    /// <summary>
    /// Converts <paramref name="doc"/> to a Mermaid <c>classDiagram</c> string.
    /// </summary>
    public static string Export(DiagramDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("classDiagram");

        // Type declarations
        foreach (var node in doc.Classes)
        {
            WriteClassBlock(sb, node);
            sb.AppendLine();
        }

        // Relationships
        foreach (var rel in doc.Relationships)
            WriteRelationship(sb, rel);

        return sb.ToString();
    }

    // -------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------

    private static void WriteClassBlock(StringBuilder sb, ClassNode node)
    {
        // Mermaid class keyword annotation for non-class types
        if (node.Kind == ClassKind.Interface)
            sb.AppendLine($"    class {node.Name}{{");
        else
            sb.AppendLine($"    class {node.Name} {{");

        if (node.Kind == ClassKind.Interface)
            sb.AppendLine($"        <<interface>>");
        else if (node.Kind == ClassKind.Enum)
            sb.AppendLine($"        <<enumeration>>");
        else if (node.Kind == ClassKind.Struct)
            sb.AppendLine($"        <<struct>>");
        else if (node.Kind == ClassKind.Abstract || node.IsAbstract)
            sb.AppendLine($"        <<abstract>>");

        foreach (var member in node.Members)
            WriteMember(sb, member);

        sb.AppendLine("    }");
    }

    private static void WriteMember(StringBuilder sb, ClassMember m)
    {
        var visPrefix = m.Visibility switch
        {
            MemberVisibility.Private => "-",
            MemberVisibility.Protected => "#",
            MemberVisibility.Internal => "~",
            _ => "+"
        };

        sb.Append("        ").Append(visPrefix);

        switch (m.Kind)
        {
            case MemberKind.Field:
                if (!string.IsNullOrEmpty(m.TypeName))
                    sb.Append(m.TypeName).Append(' ');
                sb.AppendLine(m.Name);
                break;

            case MemberKind.Property:
                if (!string.IsNullOrEmpty(m.TypeName))
                    sb.Append(m.TypeName).Append(' ');
                sb.AppendLine(m.Name);
                break;

            case MemberKind.Method:
                var returnType = string.IsNullOrEmpty(m.TypeName) ? "void" : m.TypeName;
                var paramsText = string.Join(", ", m.Parameters);
                sb.AppendLine($"{m.Name}({paramsText}) {returnType}");
                break;

            case MemberKind.Event:
                sb.Append("event ");
                if (!string.IsNullOrEmpty(m.TypeName))
                    sb.Append(m.TypeName).Append(' ');
                sb.AppendLine(m.Name);
                break;
        }
    }

    private static void WriteRelationship(StringBuilder sb, ClassRelationship rel)
    {
        var arrow = rel.Kind switch
        {
            RelationshipKind.Inheritance => "<|--",
            RelationshipKind.Dependency => "..>",
            RelationshipKind.Aggregation => "o--",
            RelationshipKind.Composition => "*--",
            _ => "-->"
        };

        sb.Append("    ")
          .Append(rel.TargetId)
          .Append(' ')
          .Append(arrow)
          .Append(' ')
          .Append(rel.SourceId);

        if (!string.IsNullOrEmpty(rel.Label))
            sb.Append(" : ").Append(rel.Label);

        sb.AppendLine();
    }
}
