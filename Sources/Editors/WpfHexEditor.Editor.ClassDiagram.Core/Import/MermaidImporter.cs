// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Import/MermaidImporter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-10
// Description:
//     Phase 4B — Mermaid classDiagram → DiagramDocument importer.
//     Handles the subset of the Mermaid class-diagram DSL that
//     matters for round-trip with our existing MermaidExporter:
//     class declarations, members (fields/methods/visibility),
//     and 6 relationship arrows.
//
// Architecture Notes:
//     Regex-based, line-by-line. No external grammar library.
//     Tolerant: lines that fail to match are skipped and surfaced
//     as ImportResult.Warnings rather than aborting the import.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Import;

/// <summary>Mermaid <c>classDiagram</c> DSL importer.</summary>
public sealed partial class MermaidImporter : IDiagramImporter
{
    /// <inheritdoc/>
    public string FormatId => "mermaid";

    /// <inheritdoc/>
    public string DisplayName => "Mermaid";

    /// <inheritdoc/>
    public IReadOnlyList<string> FileExtensions => [".mmd", ".mermaid"];

    /// <inheritdoc/>
    public bool CanHandle(string content) =>
        !string.IsNullOrWhiteSpace(content)
        && content.Contains("classDiagram", StringComparison.OrdinalIgnoreCase);

    // ── Regex grammar ────────────────────────────────────────────────────────

    [GeneratedRegex(@"^\s*class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{?\s*$", RegexOptions.Compiled)]
    private static partial Regex ClassDeclRegex();

    [GeneratedRegex(@"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<body>.+)$", RegexOptions.Compiled)]
    private static partial Regex MemberRegex();

    // Relationship lines: A <|-- B, A --|> B, A *-- B, A o-- B, A --> B, A -- B
    [GeneratedRegex(@"^\s*(?<a>[A-Za-z_][A-Za-z0-9_]*)\s*(?<arrow><\|--|--\|>|\*--|o--|-->|--)\s*(?<b>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*(?<label>.+))?\s*$", RegexOptions.Compiled)]
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

            // Skip header / closing brace
            if (trimmed.Equals("classDiagram", StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed == "}") { currentClass = null; continue; }

            // class Foo  OR  class Foo {
            var cm = ClassDeclRegex().Match(line);
            if (cm.Success)
            {
                string name = cm.Groups["name"].Value;
                if (!byName.TryGetValue(name, out var node))
                {
                    node = new ClassNode { Name = name, Id = Guid.NewGuid().ToString() };
                    byName[name] = node;
                    doc.Classes.Add(node);
                }
                if (line.Contains('{', StringComparison.Ordinal))
                    currentClass = node;
                continue;
            }

            // Relationship line — try before member to avoid 'A : ...' winning over 'A -- B'
            var rm = RelationshipRegex().Match(line);
            if (rm.Success)
            {
                var srcName = rm.Groups["a"].Value;
                var tgtName = rm.Groups["b"].Value;
                var kind    = ArrowToKind(rm.Groups["arrow"].Value);

                var src = ImporterHelpers.GetOrAddPlaceholder(srcName, byName, doc);
                var tgt = ImporterHelpers.GetOrAddPlaceholder(tgtName, byName, doc);

                doc.Relationships.Add(new ClassRelationship
                {
                    SourceId = src.Id,
                    TargetId = tgt.Id,
                    Kind     = kind,
                    Label    = rm.Groups["label"].Success ? rm.Groups["label"].Value.Trim() : null
                });
                continue;
            }

            // Member: A : +foo() void  OR  inside { ... }  short form  +foo() void
            if (currentClass is not null && !line.Contains(':') && !line.TrimStart().StartsWith("class"))
            {
                AddMemberFromShortForm(currentClass, trimmed, warnings, lineNo);
                continue;
            }

            var mm = MemberRegex().Match(line);
            if (mm.Success)
            {
                var ownerName = mm.Groups["name"].Value;
                var owner     = ImporterHelpers.GetOrAddPlaceholder(ownerName, byName, doc);
                AddMemberFromShortForm(owner, mm.Groups["body"].Value.Trim(), warnings, lineNo);
                continue;
            }

            warnings.Add($"Line {lineNo}: unrecognised — '{line.Trim()}'");
        }

        return new ImportResult(doc, warnings);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RelationshipKind ArrowToKind(string arrow) => arrow switch
    {
        "<|--" => RelationshipKind.Inheritance,
        "--|>" => RelationshipKind.Inheritance,
        "*--"  => RelationshipKind.Composition,
        "o--"  => RelationshipKind.Aggregation,
        "-->"  => RelationshipKind.Association,
        "--"   => RelationshipKind.Association,
        _      => RelationshipKind.Association
    };

    private static void AddMemberFromShortForm(ClassNode owner, string body, List<string> warnings, int lineNo)
    {
        // Mermaid member forms:
        //   +name : type            (field)
        //   +method(args) returnType (method)
        //   visibility ∈ { +, -, #, ~ }
        ImporterHelpers.TryConsumeVisibilityPrefix(ref body, out var vis);

        if (body.Contains('(', StringComparison.Ordinal))
        {
            int openParen = body.IndexOf('(');
            int closeParen = body.IndexOf(')');
            if (closeParen < 0) { warnings.Add($"Line {lineNo}: unbalanced parens in '{body}'"); return; }
            string mname  = body[..openParen].Trim();
            string after  = body[(closeParen + 1)..].Trim();
            string retType = after.Length == 0 ? "void" : after;
            owner.Members.Add(new ClassMember
            {
                Name       = mname,
                Kind       = MemberKind.Method,
                Visibility = vis,
                TypeName   = retType
            });
        }
        else
        {
            // Field forms in Mermaid:
            //   name : type   →  "name", "type"
            //   type name     →  rightmost token is name, rest is type (e.g. "int x")
            //   name          →  "name", "object"
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
                else                     { fname = tokens[^1]; ftype = string.Join(' ', tokens[..^1]); }
            }
            if (fname.Length == 0) { warnings.Add($"Line {lineNo}: empty field name"); return; }
            owner.Members.Add(new ClassMember
            {
                Name       = fname,
                Kind       = MemberKind.Field,
                Visibility = vis,
                TypeName   = ftype
            });
        }
    }
}
