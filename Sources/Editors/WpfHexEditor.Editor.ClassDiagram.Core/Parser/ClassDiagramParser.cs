// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram.Core
// File: Parser/ClassDiagramParser.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Static DSL parser that converts a plain-text class diagram source
//     into a DiagramDocument.  Supports class/interface/enum/struct
//     declarations with members, inline extends, and five relationship
//     arrow tokens.
//
// Architecture Notes:
//     Single-pass line-by-line parser — O(n) in source lines.
//     Regex patterns are compiled once as static fields.
//     ParseMember handles the ± prefix convention and the keyword-first
//     convention (field/property/method/event).
//     Partial documents are still returned when errors are encountered
//     so the diagram can render as much as possible.
// ==========================================================

using System.Text.RegularExpressions;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Core.Parser;

/// <summary>
/// Parses a plain-text DSL into a <see cref="DiagramDocument"/>.
/// </summary>
public static partial class ClassDiagramParser
{
    // -------------------------------------------------------
    // Compiled regex patterns
    // -------------------------------------------------------

    // Matches: abstract class Foo, class Foo, interface Foo, enum Foo, struct Foo
    // Optional: extends Bar  |  Optional trailing {
    [GeneratedRegex(
        @"^(abstract\s+class|class|interface|enum|struct)\s+(\w+)(?:\s+extends\s+(\w+))?\s*\{?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClassDeclRegex();

    // Matches: Foo extends Bar  (outside a class block)
    [GeneratedRegex(
        @"^(\w+)\s+extends\s+(\w+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex InlineExtendsRegex();

    // Matches relationship lines outside class blocks:
    //   Foo --> Bar : label
    //   Foo ..> Bar
    //   Foo o-- Bar
    //   Foo <|-- Bar
    //   Foo <-- Bar
    [GeneratedRegex(
        @"^(\w+)\s*(-->|\.\.>|o--|<\|--|<--)\s*(\w+)(?:\s*:\s*(.+))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RelationshipRegex();

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    /// <summary>
    /// Parses <paramref name="dslText"/> and returns a <see cref="ParseResult"/>
    /// containing the diagram document and any diagnostics.
    /// </summary>
    public static ParseResult Parse(string dslText)
    {
        var document = new DiagramDocument();
        var errors = new List<ParseError>();
        ClassNode? currentClass = null;

        var lines = dslText.Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var rawLine = lines[lineIndex];
            var line = rawLine.Trim();
            var lineNumber = lineIndex + 1; // 1-based

            // Skip blank lines and comment lines
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith('#') ||
                line.StartsWith("//"))
            {
                continue;
            }

            // Closing brace — end current class block
            if (line == "}")
            {
                currentClass = null;
                continue;
            }

            // Class declaration
            var classMatch = ClassDeclRegex().Match(line);
            if (classMatch.Success)
            {
                var kindToken = classMatch.Groups[1].Value.Trim().ToLowerInvariant();
                var className = classMatch.Groups[2].Value;
                var extendsName = classMatch.Groups[3].Value;

                var kind = ParseClassKind(kindToken, out var isAbstract);

                var node = new ClassNode { Name = className, Kind = kind, IsAbstract = isAbstract };
                node.Id = className;
                document.Classes.Add(node);
                currentClass = node;

                // Inline extends → add inheritance relationship
                if (!string.IsNullOrEmpty(extendsName))
                {
                    document.Relationships.Add(new ClassRelationship
                    {
                        SourceId = className,
                        TargetId = extendsName,
                        Kind = RelationshipKind.Inheritance
                    });
                }

                // If declaration is one-liner (no opening brace) — no block follows
                if (!line.Contains('{'))
                    currentClass = null;

                continue;
            }

            if (currentClass is not null)
            {
                // Inside a class block — parse member
                var member = ParseMember(line, lineNumber, errors);
                if (member is not null)
                    currentClass.Members.Add(member);

                continue;
            }

            // Outside a class block — check for inline extends (one-liner)
            var extendsMatch = InlineExtendsRegex().Match(line);
            if (extendsMatch.Success)
            {
                document.Relationships.Add(new ClassRelationship
                {
                    SourceId = extendsMatch.Groups[1].Value,
                    TargetId = extendsMatch.Groups[2].Value,
                    Kind = RelationshipKind.Inheritance
                });
                continue;
            }

            // Check for relationship arrow
            var relMatch = RelationshipRegex().Match(line);
            if (relMatch.Success)
            {
                var srcId = relMatch.Groups[1].Value;
                var arrow = relMatch.Groups[2].Value;
                var tgtId = relMatch.Groups[3].Value;
                var label = relMatch.Groups[4].Success ? relMatch.Groups[4].Value.Trim() : null;

                document.Relationships.Add(new ClassRelationship
                {
                    SourceId = srcId,
                    TargetId = tgtId,
                    Kind = ArrowToRelationshipKind(arrow),
                    Label = string.IsNullOrEmpty(label) ? null : label
                });
                continue;
            }

            // Unrecognised line outside a class block — emit warning
            errors.Add(new ParseError(lineNumber, 1, $"Unrecognised statement: '{line}'"));
        }

        return new ParseResult { Document = document, Errors = errors };
    }

    // -------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------

    private static ClassKind ParseClassKind(string token, out bool isAbstract)
    {
        isAbstract = false;

        return token switch
        {
            "interface" => ClassKind.Interface,
            "enum" => ClassKind.Enum,
            "struct" => ClassKind.Struct,
            var t when t.Contains("abstract") => (isAbstract = true, ClassKind.Abstract).Item2,
            _ => ClassKind.Class
        };
    }

    private static RelationshipKind ArrowToRelationshipKind(string arrow) =>
        arrow switch
        {
            "<|--" => RelationshipKind.Inheritance,
            "..>" => RelationshipKind.Dependency,
            "o--" => RelationshipKind.Aggregation,
            _ => RelationshipKind.Association  // --> and <--
        };

    private static ClassMember? ParseMember(
        string line, int lineNumber, List<ParseError> errors)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var visibility = MemberVisibility.Public;
        var isStatic = false;
        var isAbstract = false;
        var parameters = new List<string>();
        string typeName = string.Empty;
        MemberKind kind;
        string name;

        // Strip visibility prefix: + public, - private, # protected, ~ internal
        var rest = line;
        if (rest.Length > 0)
        {
            switch (rest[0])
            {
                case '+': visibility = MemberVisibility.Public; rest = rest[1..].TrimStart(); break;
                case '-': visibility = MemberVisibility.Private; rest = rest[1..].TrimStart(); break;
                case '#': visibility = MemberVisibility.Protected; rest = rest[1..].TrimStart(); break;
                case '~': visibility = MemberVisibility.Internal; rest = rest[1..].TrimStart(); break;
            }
        }

        // Detect static / abstract keywords
        if (rest.StartsWith("static ", StringComparison.OrdinalIgnoreCase))
        {
            isStatic = true;
            rest = rest[7..].TrimStart();
        }

        if (rest.StartsWith("abstract ", StringComparison.OrdinalIgnoreCase))
        {
            isAbstract = true;
            rest = rest[9..].TrimStart();
        }

        // Keyword-first convention
        if (rest.StartsWith("field ", StringComparison.OrdinalIgnoreCase))
        {
            kind = MemberKind.Field;
            rest = rest[6..].TrimStart();
        }
        else if (rest.StartsWith("property ", StringComparison.OrdinalIgnoreCase))
        {
            kind = MemberKind.Property;
            rest = rest[9..].TrimStart();
        }
        else if (rest.StartsWith("method ", StringComparison.OrdinalIgnoreCase))
        {
            kind = MemberKind.Method;
            rest = rest[7..].TrimStart();
        }
        else if (rest.StartsWith("event ", StringComparison.OrdinalIgnoreCase))
        {
            kind = MemberKind.Event;
            rest = rest[6..].TrimStart();
        }
        else if (rest.Contains('('))
        {
            // Presence of parentheses → method
            kind = MemberKind.Method;
        }
        else
        {
            // Default: treat as field (most common inline declaration)
            kind = MemberKind.Field;
        }

        // Extract parameters for methods: everything between ( and )
        if (kind == MemberKind.Method)
        {
            var openParen = rest.IndexOf('(');
            var closeParen = rest.LastIndexOf(')');

            if (openParen >= 0)
            {
                name = rest[..openParen].Trim();

                if (closeParen > openParen)
                {
                    var paramText = rest[(openParen + 1)..closeParen].Trim();
                    if (!string.IsNullOrEmpty(paramText))
                    {
                        parameters.AddRange(
                            paramText.Split(',').Select(p => p.Trim())
                                     .Where(p => !string.IsNullOrEmpty(p)));
                    }
                }

                // Optional return type after ): rest after close paren with colon
                if (closeParen >= 0 && closeParen < rest.Length - 1)
                {
                    var afterParen = rest[(closeParen + 1)..].Trim();
                    if (afterParen.StartsWith(':'))
                        typeName = afterParen[1..].Trim();
                }
            }
            else
            {
                name = rest.Trim();
            }
        }
        else
        {
            // Extract optional type annotation: name : Type
            var colonIdx = rest.IndexOf(':');
            if (colonIdx >= 0)
            {
                name = rest[..colonIdx].Trim();
                typeName = rest[(colonIdx + 1)..].Trim();
            }
            else
            {
                name = rest.Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(new ParseError(lineNumber, 1, $"Member declaration has an empty name: '{line}'"));
            return null;
        }

        return new ClassMember
        {
            Name = name,
            TypeName = typeName,
            Kind = kind,
            Visibility = visibility,
            IsStatic = isStatic,
            IsAbstract = isAbstract,
            Parameters = parameters
        };
    }
}
