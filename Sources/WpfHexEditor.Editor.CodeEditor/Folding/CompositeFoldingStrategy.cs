// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: CompositeFoldingStrategy.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Composite folding strategy that delegates to N child strategies,
//     merges their results, and returns them sorted by StartLine.
//
// Architecture Notes:
//     Composite Pattern over IFoldingStrategy.
//     Allows independent strategies (e.g. BraceFoldingStrategy +
//     RegionDirectiveFoldingStrategy) to coexist without coupling.
// ==========================================================

using System.Collections.Generic;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Runs multiple <see cref="IFoldingStrategy"/> instances on the same document
/// and returns their combined regions sorted by <see cref="FoldingRegion.StartLine"/>.
/// </summary>
public sealed class CompositeFoldingStrategy : IFoldingStrategy
{
    private readonly IFoldingStrategy[] _strategies;

    public CompositeFoldingStrategy(params IFoldingStrategy[] strategies)
        => _strategies = strategies;

    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var all = new List<FoldingRegion>();

        foreach (var strategy in _strategies)
            all.AddRange(strategy.Analyze(lines));

        all.Sort((a, b) => a.StartLine.CompareTo(b.StartLine));
        return all;
    }
}
