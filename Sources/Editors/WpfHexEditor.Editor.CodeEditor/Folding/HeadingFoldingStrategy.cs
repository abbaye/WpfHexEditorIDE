// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: HeadingFoldingStrategy.cs
// Author: WpfHexEditor Team
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Description:
//     Heading-based folding for Markdown documents.
//     Groups lines under the heading that introduces them.
//
// Architecture Notes:
//     Strategy Pattern — implements IFoldingStrategy.
//     Data-driven: minimum heading level comes from FoldingRules deserialized from .whfmt.
// ==========================================================

using System.Collections.Generic;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Produces foldable regions for Markdown documents by grouping all content
/// under each heading until the next heading of equal or higher importance.
/// Only headings at or below <c>minLevel</c> are treated as fold openers.
/// </summary>
internal sealed class HeadingFoldingStrategy : IFoldingStrategy
{
    private readonly int    _minLevel;
    private static readonly Regex s_heading = new(@"^(?<hashes>#{1,6})\s+", RegexOptions.Compiled);

    public HeadingFoldingStrategy(int minLevel) => _minLevel = minLevel;

    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var regions   = new List<FoldingRegion>();
        var openStack = new Stack<(int Line, int Level)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var m = s_heading.Match(lines[i].Text ?? string.Empty);
            if (!m.Success) continue;

            int level = m.Groups["hashes"].Length;
            if (level < _minLevel) continue;

            // Close any open headings of same or lower level.
            while (openStack.Count > 0 && openStack.Peek().Level >= level)
            {
                var (startLine, _) = openStack.Pop();
                if (i > startLine + 1)
                    regions.Add(new FoldingRegion(startLine, i - 1, "\u2026", FoldingRegionKind.Brace));
            }

            openStack.Push((i, level));
        }

        // Close remaining open headings at end of document.
        while (openStack.Count > 0)
        {
            var (startLine, _) = openStack.Pop();
            if (lines.Count - 1 > startLine + 1)
                regions.Add(new FoldingRegion(startLine, lines.Count - 1, "\u2026", FoldingRegionKind.Brace));
        }

        return regions;
    }
}
