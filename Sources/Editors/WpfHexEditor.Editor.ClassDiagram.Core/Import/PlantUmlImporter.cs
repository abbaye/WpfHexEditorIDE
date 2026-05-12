// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Import/PlantUmlImporter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Phase 4C — PlantUML class-diagram DSL → DiagramDocument
//     importer. Subset focuses on round-trip with PlantUmlExporter:
//     class/interface/enum declarations, members with visibility,
//     and the 8 relationship arrow styles.
//
// Architecture Notes:
//     Regex-based, no external grammar. Tolerant: unknown lines
//     become Warnings. Anything between @startuml/@enduml markers
//     is parsed; markers themselves are skipped.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Import;

/// <summary>PlantUML class-diagram DSL importer.</summary>
public sealed partial class PlantUmlImporter : IDiagramImporter
{
    /// <inheritdoc/>
    public string FormatId => "plantuml";

    /// <inheritdoc/>
    public string DisplayName => "PlantUML";

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions => [".puml", ".plantuml", ".iuml"];

    /// <inheritdoc/>
    public bool CanHandle(string content) =>
        !string.IsNullOrWhiteSpace(content)
        && (content.Contains("@startuml", StringComparison.OrdinalIgnoreCase)
            || content.Contains("class ", StringComparison.Ordinal));

    // ── Regex grammar ────────────────────────────────────────────────────────

    // class|interface|enum|abstract  Name  [extends X] [implements Y, Z]  {
    [GeneratedRegex(@"^\s*(?<kind>class|interface|enum|abstract|abstract\s+class|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:extends\s+(?<base>[A-Za-z_][A-Za-z0-9_.<>,\s]*?))?\s*(?:implements\s+(?<ifaces>[A-Za-z_][A-Za-z0-9_.<>,\s]*?))?\s*\{?\s*$", RegexOptions.Compiled)]
    private static partial Regex TypeDeclRegex();

    // A <|-- B, A --|> B, A *-- B, A o-- B, A --> B, A ..> B, A .. B, A -- B
    [GeneratedRegex(@"^\s*(?<a>[A-Za-z_][A-Za-z0-9_]*)\s*(?<arrow><\|--|--\|>|<\|\.\.|\.\.\|>|\*--|o--|-->|\.\.>|\.\.|--)\s*(?<b>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<label>.+))?\s*$", RegexOptions.Compiled)]
    private static partial Regex RelationshipRegex();

    // ── Import ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ImportResult Import(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ImportException("Empty content.");

        var warnings = new List<string>();
        var doc      = new DiagramDocument();
        var byName   = new Dictionary<string, ClassNode>(StringComparer.Ordinal);
        ClassNode? currentClass = null;

        int lineNo = 0;
        foreach (var raw in content.Split('\n'))
        {
            lineNo++;
            string line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            string trimmed = line.Trim();

            // Markers and comments
            if (trimmed.StartsWith('\'') || trimmed.StartsWith("/'")) continue;
            if (trimmed.StartsWith("@startuml", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("@enduml",   StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("skinparam", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("hide",      StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith("title",     StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed == "}") { currentClass = null; continue; }

            // Type declaration
            var tm = TypeDeclRegex().Match(line);
            if (tm.Success)
            {
                string kindWord = tm.Groups["kind"].Value.Trim();
                string name     = tm.Groups["name"].Value;
                if (!byName.TryGetValue(name, out var node))
                {
                    node = new ClassNode
                    {
                        Name = name,
                        Id   = Guid.NewGuid().ToString(),
                        Kind = ParseKind(kindWord, out bool isAbstract),
                        IsAbstract = isAbstract
                    };
                    byName[name] = node;
                    doc.Classes.Add(node);
                }

                // extends → Inheritance relationship
                if (tm.Groups["base"].Success)
                {
                    foreach (var b in SplitList(tm.Groups["base"].Value))
                    {
                        var baseNode = ImporterHelpers.GetOrAddPlaceholder(b, byName, doc);
                        doc.Relationships.Add(new ClassRelationship
                        {
                            SourceId = node.Id, TargetId = baseNode.Id,
                            Kind = RelationshipKind.Inheritance
                        });
                    }
                }
                if (tm.Groups["ifaces"].Success)
                {
                    foreach (var i in SplitList(tm.Groups["ifaces"].Value))
                    {
                        var ifaceNode = ImporterHelpers.GetOrAddPlaceholder(i, byName, doc);
                        doc.Relationships.Add(new ClassRelationship
                        {
                            SourceId = node.Id, TargetId = ifaceNode.Id,
                            Kind = RelationshipKind.Realization
                        });
                    }
                }

                if (line.Contains('{', StringComparison.Ordinal))
                    currentClass = node;
                continue;
            }

            // Relationship
            var rm = RelationshipRegex().Match(line);
            if (rm.Success)
            {
                var src = ImporterHelpers.GetOrAddPlaceholder(rm.Groups["a"].Value, byName, doc);
                var tgt = ImporterHelpers.GetOrAddPlaceholder(rm.Groups["b"].Value, byName, doc);
                doc.Relationships.Add(new ClassRelationship
                {
                    SourceId = src.Id, TargetId = tgt.Id,
                    Kind  = ArrowToKind(rm.Groups["arrow"].Value),
                    Label = rm.Groups["label"].Success ? rm.Groups["label"].Value.Trim() : null
                });
                continue;
            }

            // Member inside { } block
            if (currentClass is not null)
            {
                AddMemberFromBody(currentClass, trimmed, warnings, lineNo);
                continue;
            }

            warnings.Add($"Line {lineNo}: unrecognised — '{trimmed}'");
        }

        return new ImportResult(doc, warnings);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClassKind ParseKind(string word, out bool isAbstract)
    {
        isAbstract = false;
        word = word.ToLowerInvariant().Trim();
        if (word == "abstract" || word == "abstract class") { isAbstract = true; return ClassKind.Class; }
        return word switch
        {
            "interface" => ClassKind.Interface,
            "enum"      => ClassKind.Enum,
            "struct"    => ClassKind.Struct,
            _           => ClassKind.Class
        };
    }

    private static RelationshipKind ArrowToKind(string arrow) => arrow switch
    {
        "<|--" => RelationshipKind.Inheritance,
        "--|>" => RelationshipKind.Inheritance,
        "<|.." => RelationshipKind.Realization,
        "..|>" => RelationshipKind.Realization,
        "*--"  => RelationshipKind.Composition,
        "o--"  => RelationshipKind.Aggregation,
        "-->"  => RelationshipKind.Association,
        "..>"  => RelationshipKind.Dependency,
        ".."   => RelationshipKind.Dependency,
        "--"   => RelationshipKind.Association,
        _      => RelationshipKind.Association
    };

    private static IEnumerable<string> SplitList(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void AddMemberFromBody(ClassNode owner, string body, List<string> warnings, int lineNo)
    {
        if (string.IsNullOrWhiteSpace(body)) return;

        // Visibility and {static} can appear in either order in PlantUML.
        // Each modifier is consumed at most once; loop until neither matches.
        MemberVisibility vis = MemberVisibility.Public;
        bool isStatic   = false;
        bool gotVis     = false;
        while (true)
        {
            bool progressed = false;
            if (!gotVis && ImporterHelpers.TryConsumeVisibilityPrefix(ref body, out var v))
            { vis = v; gotVis = true; progressed = true; }
            if (!isStatic && body.StartsWith("{static}", StringComparison.OrdinalIgnoreCase))
            { isStatic = true; body = body[8..].TrimStart(); progressed = true; }
            if (!progressed) break;
        }

        if (body.Contains('(', StringComparison.Ordinal))
        {
            int openParen  = body.IndexOf('(');
            int closeParen = body.IndexOf(')');
            if (closeParen < 0) { warnings.Add($"Line {lineNo}: unbalanced parens"); return; }
            string mname = body[..openParen].Trim();
            string after = body[(closeParen + 1)..].Trim();
            string retType = "void";
            if (after.StartsWith(':')) retType = after[1..].Trim();
            else if (after.Length > 0) retType = after;
            owner.Members.Add(new ClassMember
            {
                Name       = mname,
                Kind       = MemberKind.Method,
                Visibility = vis,
                TypeName   = retType,
                IsStatic   = isStatic
            });
        }
        else
        {
            // Field: "name : type"  OR  "type name"
            int colon = body.IndexOf(':');
            string fname, ftype;
            if (colon >= 0)
            {
                fname = body[..colon].Trim();
                ftype = body[(colon + 1)..].Trim();
            }
            else
            {
                var tokens = body.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tokens.Length == 1) { fname = tokens[0]; ftype = "object"; }
                else                     { ftype = tokens[0]; fname = tokens[^1]; }
            }
            if (fname.Length == 0) { warnings.Add($"Line {lineNo}: empty field name"); return; }
            owner.Members.Add(new ClassMember
            {
                Name       = fname,
                Kind       = MemberKind.Field,
                Visibility = vis,
                TypeName   = ftype,
                IsStatic   = isStatic
            });
        }
    }
}
