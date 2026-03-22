// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: NamedRegionFoldingStrategy.cs
// Author: WpfHexEditor Team
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-22
// Description:
//     Configurable named-region directive folding.
//     Generalizes #region / #endregion for any language that uses
//     custom region-directive syntax.
//
// Architecture Notes:
//     Strategy Pattern — implements IFoldingStrategy.
//     Data-driven: start/end patterns come from FoldingRules deserialized from .whfmt.
// ==========================================================

using System.Collections.Generic;
using System.Text.RegularExpressions;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Detects foldable regions delimited by configurable start/end regex patterns that
/// act like <c>#region</c> / <c>#endregion</c> directives.  Each matched pair produces
/// a <see cref="FoldingRegion"/> with <see cref="FoldingRegionKind.Directive"/>.
/// </summary>
internal sealed class NamedRegionFoldingStrategy : IFoldingStrategy
{
    private readonly Regex _start;
    private readonly Regex _end;

    public NamedRegionFoldingStrategy(string startPattern, string endPattern)
    {
        _start = new Regex(startPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        _end   = new Regex(endPattern,   RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var regions = new List<FoldingRegion>();
        var stack   = new Stack<(int Line, string Name)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var text       = lines[i].Text ?? string.Empty;
            var startMatch = _start.Match(text);
            if (startMatch.Success)
            {
                // Capture the optional region name from the first group, if present.
                var name = startMatch.Groups.Count > 1 ? startMatch.Groups[1].Value.Trim() : string.Empty;
                stack.Push((i, name));
                continue;
            }

            if (_end.IsMatch(text) && stack.Count > 0)
            {
                var (startLine, regionName) = stack.Pop();
                if (i > startLine + 1)
                {
                    var label = string.IsNullOrEmpty(regionName)
                        ? "#region \u2026"
                        : $"#region {regionName} \u2026";

                    regions.Add(new FoldingRegion(startLine, i, label, FoldingRegionKind.Directive));
                }
            }
        }

        return regions;
    }
}
