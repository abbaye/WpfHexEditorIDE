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
            var effectiveText = _lineCommentPrefix is not null
                ? StripLineComment(text, _lineCommentPrefix)
                : text;
            bool isStart = _starts.Any(r => r.IsMatch(effectiveText));
            bool isEnd   = _ends.Any(r => r.IsMatch(effectiveText));

            if (isStart)
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

    private static string StripLineComment(string line, string prefix)
    {
        var idx = line.IndexOf(prefix, StringComparison.Ordinal);
        return idx >= 0 ? line[..idx] : line;
    }
}
