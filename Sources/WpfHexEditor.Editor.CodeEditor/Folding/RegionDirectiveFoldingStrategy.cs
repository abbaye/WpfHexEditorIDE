// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: RegionDirectiveFoldingStrategy.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Folding strategy that detects #region / #endregion preprocessor
//     directives and produces one FoldingRegion per matched pair.
//     Language-agnostic: works for C#, VB.NET, and any language that
//     uses the same directive syntax.
//
// Architecture Notes:
//     Strategy Pattern — implements IFoldingStrategy.
//     Intended to be composed with BraceFoldingStrategy via CompositeFoldingStrategy.
// ==========================================================

using System;
using System.Collections.Generic;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Detects foldable regions delimited by <c>#region</c> / <c>#endregion</c> directives.
/// Each matched pair produces a <see cref="FoldingRegion"/> with
/// <see cref="FoldingRegionKind.Directive"/>, rendered as a filled triangle in the gutter.
/// </summary>
public sealed class RegionDirectiveFoldingStrategy : IFoldingStrategy
{
    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var regions = new List<FoldingRegion>();
        var stack   = new Stack<(int LineIndex, string Name)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Text?.TrimStart() ?? string.Empty;

            if (trimmed.StartsWith("#region", StringComparison.OrdinalIgnoreCase))
            {
                // Capture optional region name that follows the directive keyword.
                var name = trimmed.Length > 7 ? trimmed[7..].Trim() : string.Empty;
                stack.Push((i, name));
            }
            else if (trimmed.StartsWith("#endregion", StringComparison.OrdinalIgnoreCase)
                     && stack.Count > 0)
            {
                var (startLine, name) = stack.Pop();
                if (i > startLine + 1)
                {
                    var label = string.IsNullOrEmpty(name)
                        ? "#region \u2026"
                        : $"#region {name} \u2026";

                    regions.Add(new FoldingRegion(startLine, i, label, FoldingRegionKind.Directive));
                }
            }
        }

        return regions;
    }
}
