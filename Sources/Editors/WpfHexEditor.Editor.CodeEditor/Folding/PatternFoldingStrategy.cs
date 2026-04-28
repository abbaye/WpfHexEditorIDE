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
    private readonly string? _blockCommentStart;
    private readonly string? _blockCommentEnd;

    public PatternFoldingStrategy(IReadOnlyList<string> startPatterns,
                                   IReadOnlyList<string> endPatterns,
                                   string? lineCommentPrefix    = null,
                                   string? blockCommentStart    = null,
                                   string? blockCommentEnd      = null)
    {
        _starts            = [.. startPatterns.Select(p => new Regex(p, RegexOptions.Compiled))];
        _ends              = [.. endPatterns.Select(p => new Regex(p, RegexOptions.Compiled))];
        _lineCommentPrefix = lineCommentPrefix;
        _blockCommentStart = blockCommentStart;
        _blockCommentEnd   = blockCommentEnd;
    }

    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var regions = new List<FoldingRegion>();
        var stack   = new Stack<int>();

        // Cross-line block-comment tracking: StripNonCode stops at the comment opener
        // per-line, but continuation lines of a multi-line block comment would still be
        // analysed as real code, pushing phantom openers onto the stack and producing
        // wrong EndLine values.  The delimiters come from the whfmt blockCommentStart /
        // blockCommentEnd fields so no C-style token is hardcoded here.
        bool inBlockComment = false;
        bool hasBlockComment = _blockCommentStart is not null && _blockCommentEnd is not null;

        for (int i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Text ?? string.Empty;

            if (hasBlockComment)
            {
                // Advance (or exit) multi-line block-comment state before pattern matching.
                if (inBlockComment)
                {
                    int close = text.IndexOf(_blockCommentEnd!, System.StringComparison.Ordinal);
                    if (close < 0)
                        continue; // entire line is still inside the block comment
                    inBlockComment = false;
                    text = text[(close + _blockCommentEnd!.Length)..]; // code after closer
                }

                // Detect opening of a block comment that does not close on this line.
                int blockOpen = text.IndexOf(_blockCommentStart!, System.StringComparison.Ordinal);
                if (blockOpen >= 0)
                {
                    int blockClose = text.IndexOf(_blockCommentEnd!,
                        blockOpen + _blockCommentStart!.Length, System.StringComparison.Ordinal);
                    if (blockClose < 0)
                    {
                        inBlockComment = true;
                        text = text[..blockOpen]; // only code before opener
                    }
                }
            }

            var effectiveText = StripNonCode(text, _lineCommentPrefix);

            // Count open and close matches independently so that lines with both
            // { and } (e.g. C# property initializers, pattern-matching `is { } x`,
            // object initializers closed on the same line) are handled correctly.
            // A net positive opens a region; a net negative closes one; zero = balanced.
            int opens  = _starts.Sum(r => r.Matches(effectiveText).Count);
            int closes = _ends.Sum(r => r.Matches(effectiveText).Count);
            int net    = opens - closes;

            if (net > 0)
            {
                // More openers than closers: push one opener for the net surplus.
                // Allman-style: when the entire trimmed line IS a lone opener token
                // (e.g. bare '{'), attach the fold to the preceding declaration line.
                bool isAlone = opens == 1 && closes == 0 && _starts.Any(r =>
                {
                    var m = r.Match(effectiveText);
                    return m.Success && effectiveText.Trim() == m.Value.Trim();
                });
                stack.Push(isAlone && i > 0 ? i - 1 : i);
            }
            else if (net < 0 && stack.Count > 0)
            {
                // More closers than openers: pop one frame.
                int start = stack.Pop();
                if (i > start + 1)
                    regions.Add(new FoldingRegion(start, i, "{ \u2026 }", FoldingRegionKind.Brace));
            }
            // net == 0: balanced on this line (e.g. `is { } x`) — no stack change.
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
