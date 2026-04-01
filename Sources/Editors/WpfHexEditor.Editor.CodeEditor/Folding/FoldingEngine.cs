// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: FoldingEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Orchestrates folding region analysis and maintains the collapsed state
//     of each region across document edits.
//
// Architecture Notes:
//     Facade Pattern — exposes a simple Analyze/Toggle API over
//     the pluggable IFoldingStrategy and FoldingRegion list.
//
//     State Preservation — when the document is re-analysed (e.g. after an edit),
//     regions that match a previously collapsed region (same StartLine) keep
//     their IsCollapsed=true flag.
// ==========================================================

using System;
using System.Collections.Generic;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Manages the set of <see cref="FoldingRegion"/> objects for the current document.
/// Call <see cref="Analyze"/> after each document change, then use
/// <see cref="Regions"/> to drive gutter rendering and virtual-scroll skipping.
/// </summary>
public sealed class FoldingEngine
{
    private IFoldingStrategy _strategy;
    private List<FoldingRegion>       _regions    = new();
    // OPT-E: pre-computed hidden-line set — turns IsLineHidden() from O(regions) to O(1).
    private HashSet<int>              _hiddenLines = new();

    /// <summary>Raised after <see cref="Analyze"/> updates the region list.</summary>
    public event EventHandler? RegionsChanged;

    public FoldingEngine(IFoldingStrategy strategy)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }

    /// <summary>Replaces the active folding strategy without reconstructing the engine.</summary>
    public void ReplaceStrategy(IFoldingStrategy strategy)
        => _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

    /// <summary>Current set of folding regions (may be empty before first Analyze call).</summary>
    public IReadOnlyList<FoldingRegion> Regions => _regions;

    /// <summary>
    /// Re-analyses the document and updates <see cref="Regions"/>,
    /// preserving the <see cref="FoldingRegion.IsCollapsed"/> state for any
    /// region whose <see cref="FoldingRegion.StartLine"/> matches a previous region.
    /// </summary>
    /// <param name="lines">Current document lines (snapshot).</param>
    public void Analyze(IReadOnlyList<CodeLine> lines)
    {
        var newRegions = _strategy.Analyze(lines);

        // Build lookup of previous collapsed state by start line.
        var collapsedByStart = new Dictionary<int, bool>(_regions.Count);
        foreach (var r in _regions)
            if (r.IsCollapsed)
                collapsedByStart[r.StartLine] = true;

        // Rebuild list, restoring collapsed state where applicable.
        var updated = new List<FoldingRegion>(newRegions.Count);
        foreach (var r in newRegions)
        {
            if (collapsedByStart.TryGetValue(r.StartLine, out bool wasCollapsed))
                r.IsCollapsed = wasCollapsed;
            updated.Add(r);
        }

        _regions = updated;
        RebuildHiddenSet();
        RegionsChanged?.Invoke(this, EventArgs.Empty);
    }

    // OPT-E: rebuilds the O(1) hidden-line lookup after any region state change.
    private void RebuildHiddenSet()
    {
        _hiddenLines.Clear();
        foreach (var r in _regions)
        {
            if (!r.IsCollapsed) continue;
            for (int i = r.StartLine + 1; i <= r.EndLine; i++)
                _hiddenLines.Add(i);
        }
    }

    /// <summary>
    /// Toggles the collapsed state of the region that starts on <paramref name="line"/>.
    /// </summary>
    /// <param name="line">0-based line index of the region opener.</param>
    /// <returns><c>true</c> if a region was found and toggled; <c>false</c> otherwise.</returns>
    public bool ToggleRegion(int line)
    {
        foreach (var r in _regions)
        {
            if (r.StartLine == line)
            {
                r.IsCollapsed = !r.IsCollapsed;
                RebuildHiddenSet();
                RegionsChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns the <see cref="FoldingRegion"/> whose opener is on <paramref name="line"/>,
    /// or <c>null</c> if no region starts there.
    /// </summary>
    public FoldingRegion? GetRegionAt(int line)
    {
        foreach (var r in _regions)
            if (r.StartLine == line) return r;
        return null;
    }

    /// <summary>
    /// Determines whether the given <paramref name="line"/> is hidden because it
    /// falls inside a collapsed region.  O(1) via pre-computed <see cref="_hiddenLines"/> set.
    /// </summary>
    public bool IsLineHidden(int line) => _hiddenLines.Contains(line);

    /// <summary>
    /// Total number of lines hidden across all currently-collapsed regions.
    /// Uses the pre-computed <see cref="_hiddenLines"/> set so that nested collapsed
    /// regions are not double-counted (a line inside two overlapping collapsed regions
    /// is still only one hidden line).
    /// Used by <c>CodeEditor</c> to adjust the vertical scrollbar range.
    /// </summary>
    public int TotalHiddenLineCount => _hiddenLines.Count;

    /// <summary>
    /// Collapses all regions. Fires <see cref="RegionsChanged"/> once if any changed.
    /// </summary>
    public void CollapseAll()
    {
        bool any = false;
        foreach (var r in _regions)
            if (!r.IsCollapsed) { r.IsCollapsed = true; any = true; }
        if (any) { RebuildHiddenSet(); RegionsChanged?.Invoke(this, EventArgs.Empty); }
    }

    /// <summary>
    /// Expands all regions. Fires <see cref="RegionsChanged"/> once if any changed.
    /// </summary>
    public void ExpandAll()
    {
        bool any = false;
        foreach (var r in _regions)
            if (r.IsCollapsed) { r.IsCollapsed = false; any = true; }
        if (any) { RebuildHiddenSet(); RegionsChanged?.Invoke(this, EventArgs.Empty); }
    }
}
