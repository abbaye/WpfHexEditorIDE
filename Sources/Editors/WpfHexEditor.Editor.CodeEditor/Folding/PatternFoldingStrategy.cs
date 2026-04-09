// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: PatternFoldingStrategy.cs
// Author: WpfHexEditor Team
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Description:
//     Configurable regex-pattern-based folding strategy.
//     Matches arbitrary start and end patterns to produce fold regions.
//
// Architecture Notes:
//     Strategy Pattern — implements IFoldingStrategy.
//     Data-driven: patterns come from FoldingRules deserialized from .whfmt.
// ==========================================================

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Produces foldable regions by matching configurable start and end regex patterns
/// against document lines.  Suitable for any language whose block delimiters can be
/// expressed as regular expressions.
/// </summary>
internal sealed class PatternFoldingStrategy : IFoldingStrategy
{
    private readonly Regex[] _starts;
    private readonly Regex[] _ends;
    private readonly string? _lineCommentPrefix;

    public PatternFoldingStrategy(IReadOnlyList<string> startPatterns,
                                   IReadOnlyList<string> endPatterns,
                                   string? lineCommentPrefix = null)
    {
        _starts = [.. startPatterns.Select(p => new Regex(p, RegexOptions.Compiled))];
        _ends   = [.. endPatterns.Select(p => new Regex(p, RegexOptions.Compiled))];
        _lineCommentPrefix = lineCommentPrefix;
    }

    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var regions = new List<FoldingRegion>();
        var stack   = new Stack<int>();

        for (int i = 0; i < lines.Count; i++)
        {
            var text          = lines[i].Text ?? string.Empty;
            var effectiveText = StripNonCode(text, _lineCommentPrefix);
            bool isStart = _starts.Any(r => r.IsMatch(effectiveText));
            bool isEnd   = _ends.Any(r => r.IsMatch(effectiveText));

            if (isStart && !isEnd)
            {
                // Allman-style: when the entire trimmed line IS the start-pattern match
                // (e.g. a lone '{'), attach the fold to the previous line (the declaration).
                // Mirrors BraceFoldingStrategy's braceAlone logic, generalized for any pattern.
                bool isAlone = _starts.Any(r =>
                {
                    var m = r.Match(effectiveText);
                    return m.Success && effectiveText.Trim() == m.Value.Trim();
                });
                stack.Push(isAlone && i > 0 ? i - 1 : i);
            }

            if (isEnd && stack.Count > 0)
            {
                int start = stack.Pop();
                if (i > start + 1)
                    regions.Add(new FoldingRegion(start, i, "{ \u2026 }", FoldingRegionKind.Brace));
            }
        }

        return regions;
    }

    /// <summary>
    /// Strips string literals, char literals, verbatim strings, and line comments
    /// from a line of code so that pattern matching only sees structural tokens.
    /// </summary>
    private static string StripNonCode(string line, string? lineCommentPrefix)
    {
        var sb = new System.Text.StringBuilder(line.Length);
        for (int j = 0; j < line.Length; j++)
        {
            char ch = line[j];
            char next = j + 1 < line.Length ? line[j + 1] : '\0';

            // Line comment — stop here.
            if (lineCommentPrefix is not null && j + lineCommentPrefix.Length <= line.Length &&
                line.AsSpan(j, lineCommentPrefix.Length).SequenceEqual(lineCommentPrefix.AsSpan()))
                break;

            // Block comment start — stop (may not close on same line).
            if (ch == '/' && next == '*')
                break;

            // Verbatim string @"..."
            if (ch == '@' && next == '"')
            {
                j += 2;
                while (j < line.Length)
                {
                    if (line[j] == '"')
                    {
                        if (j + 1 < line.Length && line[j + 1] == '"') { j += 2; continue; }
                        break;
                    }
                    j++;
                }
                sb.Append(' ');
                continue;
            }

            // Regular string "..."
            if (ch == '"')
            {
                j++;
                while (j < line.Length)
                {
                    if (line[j] == '\\') { j += 2; continue; }
                    if (line[j] == '"') break;
                    j++;
                }
                sb.Append(' ');
                continue;
            }

            // Char literal '...'
            if (ch == '\'')
            {
                j++;
                while (j < line.Length)
                {
                    if (line[j] == '\\') { j += 2; continue; }
                    if (line[j] == '\'') break;
                    j++;
                }
                sb.Append(' ');
                continue;
            }

            sb.Append(ch);
        }
        return sb.ToString();
    }
}
