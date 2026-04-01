// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Serializer/ClassDiagramSerializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Static serializer that converts a DiagramDocument back into
//     round-trip DSL text understood by ClassDiagramParser.
//     Produces class blocks with indented member declarations followed
//     by global relationship lines.
//
// Architecture Notes:
//     Pure StringBuilder-based serialisation — no allocating LINQ chains
//     in the hot loop.
//     Arrow token selection mirrors the same map used in the parser so
//     Parse(Serialize(doc)) produces an equivalent document.
// ==========================================================

using System.Text;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Serializer;

/// <summary>
/// Converts a <see cref="DiagramDocument"/> back into DSL source text.
/// </summary>
public static class ClassDiagramSerializer
{
    /// <summary>
    /// Serialises <paramref name="doc"/> to DSL text suitable for saving to disk
    /// and round-tripping through <c>ClassDiagramParser.Parse</c>.
    /// </summary>
    public static string Serialize(DiagramDocument doc)
    {
        var sb = new StringBuilder();

        // Write class blocks
        foreach (var node in doc.Classes)
        {
            WriteClassHeader(sb, node);
            sb.AppendLine("{");

            foreach (var member in node.Members)
                WriteMember(sb, member);

            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Write relationship lines
        foreach (var rel in doc.Relationships)
            WriteRelationship(sb, rel);

        return sb.ToString();
    }

    // -------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------

    private static void WriteClassHeader(StringBuilder sb, ClassNode node)
    {
        var keyword = node.Kind switch
        {
            ClassKind.Interface => "interface",
            ClassKind.Enum => "enum",
            ClassKind.Struct => "struct",
            ClassKind.Abstract => "abstract class",
            _ when node.IsAbstract => "abstract class",
            _ => "class"
        };

        sb.Append(keyword).Append(' ').Append(node.Name).Append(' ');
    }

    private static void WriteMember(StringBuilder sb, ClassMember member)
    {
        sb.Append("    "); // 4-space indent

        // Visibility prefix
        var prefix = member.Visibility switch
        {
            MemberVisibility.Private => "-",
            MemberVisibility.Protected => "#",
            MemberVisibility.Internal => "~",
            _ => "+"
        };

        sb.Append(prefix);

        if (member.IsStatic) sb.Append("static ");
        if (member.IsAbstract) sb.Append("abstract ");

        switch (member.Kind)
        {
            case MemberKind.Field:
                sb.Append(member.Name);
                if (!string.IsNullOrEmpty(member.TypeName))
                    sb.Append(" : ").Append(member.TypeName);
                break;

            case MemberKind.Property:
                sb.Append("property ").Append(member.Name);
                if (!string.IsNullOrEmpty(member.TypeName))
                    sb.Append(" : ").Append(member.TypeName);
                break;

            case MemberKind.Method:
                sb.Append(member.Name).Append('(');
                sb.Append(string.Join(", ", member.Parameters));
                sb.Append(')');
                if (!string.IsNullOrEmpty(member.TypeName))
                    sb.Append(" : ").Append(member.TypeName);
                break;

            case MemberKind.Event:
                sb.Append("event ").Append(member.Name);
                if (!string.IsNullOrEmpty(member.TypeName))
                    sb.Append(" : ").Append(member.TypeName);
                break;
        }

        sb.AppendLine();
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

        sb.Append(rel.SourceId).Append(' ').Append(arrow).Append(' ').Append(rel.TargetId);

        if (!string.IsNullOrEmpty(rel.Label))
            sb.Append(" : ").Append(rel.Label);

        sb.AppendLine();
    }
}
