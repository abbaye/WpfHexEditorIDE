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

        string currentNamespace = "(global)";
        string currentType      = string.Empty;

        for (int i = 0; i < lines.Count; i++)
        {
            string text = lines[i].Text;
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (s_skipLine.IsMatch(text))         continue;

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
                currentType = name;
                types.Add(new NavigationBarItem(
                    NavigationItemKind.Type, name,
                    QualifiedName(currentNamespace, name),
                    i, kind));
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
        => name.Length > 1 && !s_reserved.Contains(name) && char.IsUpper(name[0]);
}
