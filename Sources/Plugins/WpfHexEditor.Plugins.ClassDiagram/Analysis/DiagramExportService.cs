// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Analysis/DiagramExportService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-08
// Description:
//     Exports a DiagramDocument to text-based diagram formats:
//     PlantUML and Mermaid classDiagram.
//     Grouping by DiagramProjectGroup is respected when present
//     (namespace blocks in PlantUML, comments in Mermaid).
//
// Architecture Notes:
//     Static service — no state. All methods are pure functions
//     over immutable DiagramDocument input.
//     PlantUML: uses @startuml/@enduml with skinparam + namespace blocks.
//     Mermaid: uses classDiagram directive (GitHub-compatible).
//     VB.NET: member visibility symbols follow UML convention (+/-/#/~).
// ==========================================================

using System.Text;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Plugins.ClassDiagram.Analysis;

/// <summary>
/// Exports a <see cref="DiagramDocument"/> to PlantUML or Mermaid text format.
/// </summary>
public static class DiagramExportService
{
    // ── PlantUML ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="document"/> to a PlantUML class diagram string.
    /// Project groups become <c>namespace</c> blocks; swimlane colors are applied
    /// via <c>BackgroundColor</c> on each namespace.
    /// </summary>
    public static string ToPlantUml(DiagramDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@startuml");
        sb.AppendLine("skinparam classAttributeIconSize 0");
        sb.AppendLine("skinparam monochrome false");
        sb.AppendLine("skinparam shadowing false");
        sb.AppendLine("skinparam roundcorner 8");
        sb.AppendLine("skinparam classFontSize 13");
        sb.AppendLine("hide empty members");
        sb.AppendLine();

        // Build a lookup: classId → projectGroup color
        var colorById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in document.ProjectGroups)
            foreach (var id in group.ClassIds)
                colorById[id] = group.Color;

        if (document.ProjectGroups.Count > 0)
        {
            // Emit one namespace block per project group
            foreach (var group in document.ProjectGroups)
            {
                if (group.ClassIds.Count == 0) continue;

                string safeName = SanitizePlantUml(group.ProjectName);
                sb.AppendLine($"namespace {safeName} #{group.Color.TrimStart('#')} {{");

                foreach (string classId in group.ClassIds)
                {
                    var node = document.FindById(classId);
                    if (node is null) continue;
                    AppendPlantUmlType(sb, node, indent: "  ");
                }

                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Nodes not attributed to any group
            var attributed = document.ProjectGroups
                .SelectMany(g => g.ClassIds)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var node in document.Classes.Where(n => !attributed.Contains(n.Id)))
                AppendPlantUmlType(sb, node, indent: "");
        }
        else
        {
            foreach (var node in document.Classes)
                AppendPlantUmlType(sb, node, indent: "");
        }

        sb.AppendLine();

        // Relationships
        foreach (var rel in document.Relationships)
        {
            string arrow = PlantUmlArrow(rel.Kind);
            string label = string.IsNullOrEmpty(rel.Label) ? "" : $" : {rel.Label}";
            string srcMul = string.IsNullOrEmpty(rel.SourceMultiplicity) ? "" : $"\"{rel.SourceMultiplicity}\" ";
            string tgtMul = string.IsNullOrEmpty(rel.TargetMultiplicity) ? "" : $" \"{rel.TargetMultiplicity}\"";

            var src = document.FindById(rel.SourceId);
            var tgt = document.FindById(rel.TargetId);
            if (src is null || tgt is null) continue;

            sb.AppendLine($"{srcMul}{SanitizePlantUml(src.Name)} {arrow}{tgtMul}{SanitizePlantUml(tgt.Name)}{label}");
        }

        sb.AppendLine();
        sb.Append("@enduml");
        return sb.ToString();
    }

    private static void AppendPlantUmlType(StringBuilder sb, ClassNode node, string indent)
    {
        string keyword = node.Kind switch
        {
            ClassKind.Interface => "interface",
            ClassKind.Enum      => "enum",
            ClassKind.Struct    => "class",   // PlantUML has no struct keyword
            _                   => "class"
        };

        string modifiers = "";
        if (node.IsAbstract && node.Kind != ClassKind.Interface)
            modifiers = " <<abstract>>";
        else if (node.IsRecord)
            modifiers = " <<record>>";
        else if (node.IsSealed)
            modifiers = " <<sealed>>";

        sb.AppendLine($"{indent}{keyword} {SanitizePlantUml(node.Name)}{modifiers} {{");

        foreach (var m in node.Members)
        {
            string vis  = UmlVisibility(m.Visibility);
            string stat = m.IsStatic ? "{static} " : "";
            string abst = m.IsAbstract ? "{abstract} " : "";
            string line = m.Kind switch
            {
                MemberKind.Field    => $"{indent}  {vis}{stat}{abst}{m.TypeName} {m.Name}",
                MemberKind.Property => $"{indent}  {vis}{stat}{abst}{m.TypeName} {m.Name}",
                MemberKind.Event    => $"{indent}  {vis}{stat}<<event>> {m.TypeName} {m.Name}",
                MemberKind.Method   => BuildPlantUmlMethod(m, indent, vis, stat, abst),
                _                   => $"{indent}  {vis}{m.Name}"
            };
            sb.AppendLine(line);
        }

        sb.AppendLine($"{indent}}}");
    }

    private static string BuildPlantUmlMethod(ClassMember m, string indent, string vis, string stat, string abst)
    {
        string parms = m.Parameters.Count > 0
            ? string.Join(", ", m.Parameters)
            : "";
        string returnType = string.IsNullOrEmpty(m.TypeName) ? "void" : m.TypeName;
        return $"{indent}  {vis}{stat}{abst}{m.Name}({parms}) : {returnType}";
    }

    private static string PlantUmlArrow(RelationshipKind kind) => kind switch
    {
        RelationshipKind.Inheritance  => "<|--",
        RelationshipKind.Realization  => "<|..",
        RelationshipKind.Composition  => "*--",
        RelationshipKind.Aggregation  => "o--",
        RelationshipKind.Dependency   => "..",
        RelationshipKind.Association  => "--",
        RelationshipKind.Uses         => "..>",
        RelationshipKind.Creates      => "-->",
        _                             => "--"
    };

    private static string UmlVisibility(MemberVisibility v) => v switch
    {
        MemberVisibility.Public    => "+",
        MemberVisibility.Protected => "#",
        MemberVisibility.Internal  => "~",
        _                          => "-"
    };

    private static string SanitizePlantUml(string name)
        => name.Replace('<', '_').Replace('>', '_').Replace(',', '_').Replace(' ', '_');

    // ── Mermaid ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts <paramref name="document"/> to a Mermaid <c>classDiagram</c> string.
    /// Project groups are emitted as comment headers for visual separation.
    /// </summary>
    public static string ToMermaid(DiagramDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("classDiagram");

        if (document.ProjectGroups.Count > 0)
        {
            var attributed = document.ProjectGroups
                .SelectMany(g => g.ClassIds)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var group in document.ProjectGroups)
            {
                if (group.ClassIds.Count == 0) continue;
                sb.AppendLine($"  %% ── {group.ProjectName} ──────────────────────");

                foreach (string classId in group.ClassIds)
                {
                    var node = document.FindById(classId);
                    if (node is null) continue;
                    AppendMermaidType(sb, node);
                }
            }

            foreach (var node in document.Classes.Where(n => !attributed.Contains(n.Id)))
                AppendMermaidType(sb, node);
        }
        else
        {
            foreach (var node in document.Classes)
                AppendMermaidType(sb, node);
        }

        sb.AppendLine();

        // Relationships
        foreach (var rel in document.Relationships)
        {
            var src = document.FindById(rel.SourceId);
            var tgt = document.FindById(rel.TargetId);
            if (src is null || tgt is null) continue;

            string arrow = MermaidArrow(rel.Kind);
            string label = string.IsNullOrEmpty(rel.Label) ? "" : $" : {rel.Label}";
            sb.AppendLine($"  {SanitizeMermaid(src.Name)} {arrow} {SanitizeMermaid(tgt.Name)}{label}");
        }

        return sb.ToString();
    }

    private static void AppendMermaidType(StringBuilder sb, ClassNode node)
    {
        string annotation = node.Kind switch
        {
            ClassKind.Interface => "  <<interface>>",
            ClassKind.Enum      => "  <<enumeration>>",
            _                   => node.IsAbstract ? "  <<abstract>>" : (node.IsRecord ? "  <<record>>" : null!)
        };

        sb.AppendLine($"  class {SanitizeMermaid(node.Name)} {{");
        if (annotation is not null)
            sb.AppendLine(annotation);

        foreach (var m in node.Members)
        {
            string vis  = MermaidVisibility(m.Visibility);
            string stat = m.IsStatic ? "$" : "";
            string abst = m.IsAbstract ? "*" : "";

            string line = m.Kind switch
            {
                MemberKind.Method =>
                    $"    {vis}{m.Name}({string.Join(", ", m.Parameters)}) {m.TypeName}{stat}{abst}",
                _ =>
                    $"    {vis}{m.TypeName} {m.Name}{stat}{abst}"
            };
            sb.AppendLine(line);
        }

        sb.AppendLine("  }");
    }

    private static string MermaidArrow(RelationshipKind kind) => kind switch
    {
        RelationshipKind.Inheritance => "<|--",
        RelationshipKind.Realization => "<|..",
        RelationshipKind.Composition => "*--",
        RelationshipKind.Aggregation => "o--",
        RelationshipKind.Dependency  => "..",
        RelationshipKind.Association => "--",
        RelationshipKind.Uses        => "..>",
        RelationshipKind.Creates     => "-->",
        _                            => "--"
    };

    private static string MermaidVisibility(MemberVisibility v) => v switch
    {
        MemberVisibility.Public    => "+",
        MemberVisibility.Protected => "#",
        MemberVisibility.Internal  => "~",
        _                          => "-"
    };

    private static string SanitizeMermaid(string name)
        => name.Replace('<', '_').Replace('>', '_').Replace(',', '_').Replace(' ', '_');
}
