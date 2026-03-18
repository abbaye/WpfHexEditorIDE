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
    private readonly IFoldingStrategy _strategy;
    private List<FoldingRegion>       _regions = new();

    /// <summary>Raised after <see cref="Analyze"/> updates the region list.</summary>
    public event EventHandler? RegionsChanged;

    public FoldingEngine(IFoldingStrategy strategy)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }

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
        RegionsChanged?.Invoke(this, EventArgs.Empty);
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
    /// falls inside a collapsed region.
    /// </summary>
    public bool IsLineHidden(int line)
    {
        foreach (var r in _regions)
        {
            if (!r.IsCollapsed || line <= r.StartLine) continue;
            // Both brace and directive regions hide their closing line
            // ('}' for brace, '#endregion' for directive) — matches VS behavior.
            if (line <= r.EndLine) return true;
        }
        return false;
    }

    /// <summary>
    /// Collapses all regions. Fires <see cref="RegionsChanged"/> once if any changed.
    /// </summary>
    public void CollapseAll()
    {
        bool any = false;
        foreach (var r in _regions)
            if (!r.IsCollapsed) { r.IsCollapsed = true; any = true; }
        if (any) RegionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Expands all regions. Fires <see cref="RegionsChanged"/> once if any changed.
    /// </summary>
    public void ExpandAll()
    {
        bool any = false;
        foreach (var r in _regions)
            if (r.IsCollapsed) { r.IsCollapsed = false; any = true; }
        if (any) RegionsChanged?.Invoke(this, EventArgs.Empty);
    }
}
