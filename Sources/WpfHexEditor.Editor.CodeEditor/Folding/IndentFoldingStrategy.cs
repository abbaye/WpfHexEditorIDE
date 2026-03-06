// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: IndentFoldingStrategy.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Indent-based folding strategy: groups consecutive lines that share
//     a deeper indentation level under their common header line.
//     Suitable for Python, YAML, Markdown, plain text blocks, etc.
// ==========================================================

using System.Collections.Generic;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Produces foldable regions based on indentation depth.
/// A region starts when indentation increases and ends when it returns
/// to the same (or shallower) level.
/// </summary>
public sealed class IndentFoldingStrategy : IFoldingStrategy
{
    private readonly int _tabWidth;

    /// <param name="tabWidth">Number of spaces equivalent to one tab character (default 4).</param>
    public IndentFoldingStrategy(int tabWidth = 4) => _tabWidth = tabWidth;

    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var regions = new List<FoldingRegion>();
        var stack   = new Stack<(int startLine, int indent)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                continue; // skip blank lines

            int indent = MeasureIndent(text);

            // Close any open regions that are deeper than the current indent.
            while (stack.Count > 0 && stack.Peek().indent >= indent)
            {
                var (startLine, _) = stack.Pop();
                int endLine = i - 1;
                if (endLine > startLine + 1)
                    regions.Add(new FoldingRegion(startLine, endLine, "\u2026"));
            }

            // Peek: if the next non-blank line is deeper, this line opens a region.
            int nextIndent = GetNextNonBlankIndent(lines, i + 1);
            if (nextIndent > indent)
                stack.Push((i, indent));
        }

        // Close remaining open regions at end-of-document.
        while (stack.Count > 0)
        {
            var (startLine, _) = stack.Pop();
            int endLine = lines.Count - 1;
            if (endLine > startLine + 1)
                regions.Add(new FoldingRegion(startLine, endLine, "\u2026"));
        }

        return regions;
    }

    private int MeasureIndent(string line)
    {
        int indent = 0;
        foreach (char ch in line)
        {
            if (ch == ' ')       indent++;
            else if (ch == '\t') indent += _tabWidth;
            else                 break;
        }
        return indent;
    }

    private int GetNextNonBlankIndent(IReadOnlyList<CodeLine> lines, int from)
    {
        for (int j = from; j < lines.Count; j++)
        {
            var text = lines[j].Text;
            if (!string.IsNullOrWhiteSpace(text))
                return MeasureIndent(text);
        }
        return 0;
    }
}
