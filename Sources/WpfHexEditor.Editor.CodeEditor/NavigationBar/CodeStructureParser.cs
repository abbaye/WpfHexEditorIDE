// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: CodeStructureParser.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Stateless single-pass parser that extracts namespace / type / member
//     declarations from a CodeDocument using regex patterns.
//     Supports C#, Java, C++, TypeScript / JavaScript heuristics.
//     No external dependencies — pure BCL regex only.
//
// Architecture Notes:
//     Strategy Pattern — language-agnostic regex bank, single-pass O(n).
//     Returns an immutable CodeStructureSnapshot; caller owns the result.
// ==========================================================

using System.Collections.Generic;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.NavigationBar;

public static class CodeStructureParser
{
    // ── Regex patterns ────────────────────────────────────────────────────────

    // namespace Foo.Bar  /  namespace Foo.Bar {
    private static readonly Regex s_namespace = new(
        @"^\s*namespace\s+([\w.]+)",
        RegexOptions.Compiled);

    // [modifiers] class/interface/struct/enum/record[struct] Name[<T>]
    private static readonly Regex s_type = new(
        @"^\s*(?:(?:public|private|protected|internal|static|abstract|sealed|partial|file|record)\s+)*" +
        @"(class|interface|struct|enum|record)\s+([\w<>]+)",
        RegexOptions.Compiled);

    // [modifiers] ReturnType MethodName(  — excludes property-only lines
    private static readonly Regex s_method = new(
        @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|async|sealed|new|extern|unsafe)\s+)*" +
        @"(?:[\w<>\[\],\s\?]+\s+)?([\w]+)\s*(?:<[^>]*>)?\s*\(",
        RegexOptions.Compiled);

    // [modifiers] Type PropertyName { or => (no parenthesis on same line)
    private static readonly Regex s_property = new(
        @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|new)\s+)*" +
        @"[\w<>\[\],\s\?]+\s+([\w]+)\s*(?:\{|=>)",
        RegexOptions.Compiled);

    // Delegate declaration
    private static readonly Regex s_delegate = new(
        @"^\s*(?:(?:public|private|protected|internal)\s+)*delegate\s+[\w<>\[\],\s\?]+\s+([\w]+)\s*\(",
        RegexOptions.Compiled);

    // [modifiers] event EventType[<T>] EventName  (semicolon or { follows elsewhere)
    private static readonly Regex s_event = new(
        @"^\s*(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|new)\s+)*" +
        @"event\s+[\w<>\[\],\?\s]+\s+([\w]+)\s*[;{]?",
        RegexOptions.Compiled);

    // [modifiers] [readonly|const|volatile] Type fieldName ; or = (requires ≥1 access/storage modifier)
    // Parenthesis absent ensures it is not a method.  No { or => ensures it is not a property.
    private static readonly Regex s_field = new(
        @"^\s*(?:(?:public|private|protected|internal|static|readonly|const|volatile|new)\s+)+" +
        @"[\w<>\[\],\?\s]+\s+([\w_]+)\s*(?:;|=[^>=])",
        RegexOptions.Compiled);

    // Enum value: PascalCase identifier, optional = expression (may include |, spaces, hex),
    // optional trailing comma.  Safe to be permissive here because this branch is only
    // entered when currentTypeKind == TypeKind.Enum.
    private static readonly Regex s_enumValue = new(
        @"^\s*([A-Z]\w*)\s*(?:=\s*.+?)?\s*,?\s*$",
        RegexOptions.Compiled);

    // Lines to ignore (avoid false positives)
    private static readonly Regex s_skipLine = new(
        @"^\s*(?://|/\*|\*|#|using\s|var\s|return\s|if\s*\(|else|for\s*\(|foreach|while|switch|catch|finally|throw|new\s)",
        RegexOptions.Compiled);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="lines"/> and returns a structure snapshot.
    /// Called on a background thread; must not touch WPF.
    /// </summary>
    public static CodeStructureSnapshot Parse(IReadOnlyList<CodeLine> lines)
    {
        var namespaces = new List<NavigationBarItem>();
        var types      = new List<NavigationBarItem>();
        var members    = new List<NavigationBarItem>();

        string   currentNamespace = "(global)";
        string   currentType      = string.Empty;
        TypeKind currentTypeKind  = TypeKind.Unknown;

        for (int i = 0; i < lines.Count; i++)
        {
            string text = lines[i].Text;
            if (string.IsNullOrWhiteSpace(text)) continue;

            // Closing brace exits the current type scope (best-effort; handles enum body close).
            if (text.TrimStart() == "}")
            {
                currentTypeKind = TypeKind.Unknown;
                continue;
            }

            if (s_skipLine.IsMatch(text)) continue;

            // ── Namespace ─────────────────────────────────────────────────
            var m = s_namespace.Match(text);
            if (m.Success)
            {
                currentNamespace = m.Groups[1].Value;
                namespaces.Add(new NavigationBarItem(
                    NavigationItemKind.Namespace,
                    currentNamespace,
                    currentNamespace,
                    i));
                currentType = string.Empty;
                continue;
            }

            // ── Delegate ──────────────────────────────────────────────────
            m = s_delegate.Match(text);
            if (m.Success)
            {
                string name = m.Groups[1].Value;
                types.Add(new NavigationBarItem(
                    NavigationItemKind.Type, name,
                    QualifiedName(currentNamespace, name),
                    i, TypeKind.Delegate));
                continue;
            }

            // ── Type ──────────────────────────────────────────────────────
            m = s_type.Match(text);
            if (m.Success)
            {
                string keyword = m.Groups[1].Value;
                string name    = m.Groups[2].Value;
                var    kind    = ParseTypeKind(keyword);
                currentType     = name;
                currentTypeKind = kind;
                types.Add(new NavigationBarItem(
                    NavigationItemKind.Type, name,
                    QualifiedName(currentNamespace, name),
                    i, kind));
                continue;
            }

            // ── Enum value (only when current type is an enum) ────────────
            if (currentTypeKind == TypeKind.Enum)
            {
                m = s_enumValue.Match(text);
                if (m.Success)
                {
                    string name = m.Groups[1].Value;
                    members.Add(new NavigationBarItem(
                        NavigationItemKind.Member, name,
                        QualifiedName(currentType, name),
                        i, MemberKind: MemberKind.Field));  // Field icon matches VS enum-value icon
                    continue;
                }
            }

            // ── Event (test before property/field — 'event' keyword is unambiguous) ──
            m = s_event.Match(text);
            if (m.Success)
            {
                string name = m.Groups[1].Value;
                if (IsValidMemberName(name))
                    members.Add(new NavigationBarItem(
                        NavigationItemKind.Member, name,
                        QualifiedName(currentType, name),
                        i, MemberKind: MemberKind.Event));
                continue;
            }

            // ── Property (must test before method — no '(' on line) ───────
            m = s_property.Match(text);
            if (m.Success && !text.Contains('('))
            {
                string name = m.Groups[1].Value;
                if (IsValidMemberName(name))
                    members.Add(new NavigationBarItem(
                        NavigationItemKind.Member, name,
                        QualifiedName(currentType, name),
                        i, MemberKind: MemberKind.Property));
                continue;
            }

            // ── Method / Constructor ──────────────────────────────────────
            m = s_method.Match(text);
            if (m.Success)
            {
                string name = m.Groups[1].Value;
                if (IsValidMemberName(name))
                {
                    var mk = string.Equals(name, currentType, StringComparison.Ordinal)
                        ? MemberKind.Constructor
                        : MemberKind.Method;
                    members.Add(new NavigationBarItem(
                        NavigationItemKind.Member, name,
                        QualifiedName(currentType, name),
                        i, MemberKind: mk));
                }
                continue;
            }

            // ── Field (last — most permissive pattern, no '(' and no '{'/'=>') ──
            if (!text.Contains('(') && !text.Contains('{') && !text.Contains("=>"))
            {
                m = s_field.Match(text);
                if (m.Success)
                {
                    string name = m.Groups[1].Value;
                    if (IsValidMemberName(name))
                        members.Add(new NavigationBarItem(
                            NavigationItemKind.Member, name,
                            QualifiedName(currentType, name),
                            i, MemberKind: MemberKind.Field));
                }
            }
        }

        // Ensure at least one namespace entry so the left combo is never empty.
        if (namespaces.Count == 0)
            namespaces.Add(new NavigationBarItem(
                NavigationItemKind.Namespace, "(global)", "(global)", 0));

        return new CodeStructureSnapshot
        {
            Namespaces = namespaces,
            Types      = types,
            Members    = members,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TypeKind ParseTypeKind(string keyword) => keyword switch
    {
        "class"     => TypeKind.Class,
        "interface" => TypeKind.Interface,
        "struct"    => TypeKind.Struct,
        "enum"      => TypeKind.Enum,
        "record"    => TypeKind.Record,
        _           => TypeKind.Unknown,
    };

    private static string QualifiedName(string parent, string name)
        => string.IsNullOrEmpty(parent) ? name : $"{parent}.{name}";

    // Filter out keywords and single-char tokens that match loosely.
    private static readonly HashSet<string> s_reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "else", "for", "foreach", "while", "do", "switch", "case",
        "return", "break", "continue", "throw", "new", "var", "using",
        "true", "false", "null", "void", "int", "string", "bool", "get", "set",
    };

    private static bool IsValidMemberName(string name)
        => name.Length > 1 && !s_reserved.Contains(name)
           && (char.IsUpper(name[0]) || name[0] == '_');  // allow _camelCase private fields
}
