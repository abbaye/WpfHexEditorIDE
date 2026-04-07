// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Services/GutterChangeTracker.cs
// Description:
//     Tracks per-line change state (Added / Modified / Deleted / None) relative
//     to the last file save-point.  Uses an INCREMENTAL approach:
//
//       - MarkSavePoint()     : snapshots current lines as the baseline.
//       - OnLineInserted(k)   : marks line k as Added; shifts all saved→current
//                               mappings below k downward.
//       - OnLineDeleted(k)    : removes the mapping for k; marks deletion hint
//                               if it was a saved line.
//       - OnLineModified(k,t) : checks whether line k reverted to its saved hash;
//                               toggles Modified state accordingly.
//
//     The change map is rebuilt and the Changed event fired immediately on every
//     call — no debounce, no polling, zero latency for common edits.
//
// Architecture:
//     Standalone service; no WPF controls dependency; testable in isolation.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Services;

/// <summary>
/// Incrementally tracks per-line <see cref="LineChangeKind"/> relative to the last save-point.
/// </summary>
internal sealed class GutterChangeTracker
{
    // ── Save-point snapshot ───────────────────────────────────────────────────

    private ImmutableArray<int> _savedHashes = ImmutableArray<int>.Empty;

    // _savedToCurrent[s] = current-line-index of saved line s, or -1 if deleted.
    private int[] _savedToCurrent = Array.Empty<int>();

    // ── Live tracking sets (current-line-indices) ─────────────────────────────

    private readonly SortedSet<int> _addedLines    = new();
    private readonly SortedSet<int> _modifiedLines = new();
    // Saved lines marked as deleted: stored as the predecessor current-line-index
    // where the deletion-hint triangle should be drawn.
    private readonly HashSet<int>   _deletedHints  = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Raised synchronously after any change update with the new map.</summary>
    internal event EventHandler<IReadOnlyDictionary<int, LineChangeKind>>? Changed;

    /// <summary>
    /// Snapshots current lines as the save baseline and clears all markers.
    /// </summary>
    internal void MarkSavePoint(IReadOnlyList<string> lines)
    {
        var builder = ImmutableArray.CreateBuilder<int>(lines.Count);
        foreach (var l in lines)
            builder.Add(HashLine(l));
        _savedHashes = builder.MoveToImmutable();

        _savedToCurrent = new int[lines.Count];
        for (int i = 0; i < lines.Count; i++)
            _savedToCurrent[i] = i;

        _addedLines.Clear();
        _modifiedLines.Clear();
        _deletedHints.Clear();

        Changed?.Invoke(this, EmptyMap);
    }

    /// <summary>
    /// Called when a new line is inserted at <paramref name="lineIndex"/> (0-based current index).
    /// </summary>
    internal void OnLineInserted(int lineIndex)
    {
        // Shift saved→current mappings for lines at or below the insertion point.
        for (int s = 0; s < _savedToCurrent.Length; s++)
            if (_savedToCurrent[s] >= lineIndex)
                _savedToCurrent[s]++;

        ShiftSet(_addedLines,    lineIndex, +1);
        ShiftSet(_modifiedLines, lineIndex, +1);
        ShiftHints(lineIndex, +1);

        _addedLines.Add(lineIndex);
        RaiseChanged();
    }

    /// <summary>
    /// Called when the line at <paramref name="lineIndex"/> is deleted.
    /// </summary>
    internal void OnLineDeleted(int lineIndex)
    {
        bool wasAdded = _addedLines.Remove(lineIndex);
        _modifiedLines.Remove(lineIndex);
        _deletedHints.Remove(lineIndex);

        if (!wasAdded)
        {
            // Mark the corresponding saved line as deleted.
            for (int s = 0; s < _savedToCurrent.Length; s++)
            {
                if (_savedToCurrent[s] == lineIndex)
                {
                    _savedToCurrent[s] = -1;
                    // Deletion hint on predecessor.
                    int hint = Math.Max(0, lineIndex - 1);
                    _deletedHints.Add(hint);
                    break;
                }
            }
        }

        // Shift everything after the deleted line down.
        for (int s = 0; s < _savedToCurrent.Length; s++)
            if (_savedToCurrent[s] > lineIndex)
                _savedToCurrent[s]--;

        ShiftSet(_addedLines,    lineIndex + 1, -1);
        ShiftSet(_modifiedLines, lineIndex + 1, -1);
        ShiftHints(lineIndex + 1, -1);

        RaiseChanged();
    }

    /// <summary>
    /// Called when the content of <paramref name="lineIndex"/> changes
    /// (character insert/delete on an existing line).
    /// </summary>
    internal void OnLineModified(int lineIndex, string newText)
    {
        // Added lines have no saved hash to compare against.
        if (_addedLines.Contains(lineIndex)) return;

        // Find the saved line that currently maps to lineIndex.
        int savedLine = FindSavedLine(lineIndex);

        if (savedLine < 0 || savedLine >= _savedHashes.Length)
        {
            _modifiedLines.Add(lineIndex);
        }
        else if (HashLine(newText) == _savedHashes[savedLine])
        {
            _modifiedLines.Remove(lineIndex); // reverted to saved content
        }
        else
        {
            _modifiedLines.Add(lineIndex);
        }

        RaiseChanged();
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void RaiseChanged() => Changed?.Invoke(this, BuildMap());

    private IReadOnlyDictionary<int, LineChangeKind> BuildMap()
    {
        var map = new Dictionary<int, LineChangeKind>();
        foreach (int k in _addedLines)    map[k] = LineChangeKind.Added;
        foreach (int k in _modifiedLines) map[k] = LineChangeKind.Modified;
        foreach (int k in _deletedHints)
        {
            if (!map.ContainsKey(k))
                map[k] = LineChangeKind.Deleted;
        }
        return map;
    }

    private int FindSavedLine(int currentLine)
    {
        for (int s = 0; s < _savedToCurrent.Length; s++)
            if (_savedToCurrent[s] == currentLine) return s;
        return -1;
    }

    /// <summary>
    /// Shifts all set entries >= <paramref name="fromLine"/> by <paramref name="delta"/>.
    /// </summary>
    private static void ShiftSet(SortedSet<int> set, int fromLine, int delta)
    {
        var tail = new List<int>(set.GetViewBetween(fromLine, int.MaxValue));
        foreach (int k in tail) set.Remove(k);
        foreach (int k in tail) set.Add(k + delta);
    }

    private void ShiftHints(int fromLine, int delta)
    {
        var tail = new List<int>();
        foreach (int h in _deletedHints)
            if (h >= fromLine) tail.Add(h);
        foreach (int h in tail) _deletedHints.Remove(h);
        foreach (int h in tail) _deletedHints.Add(h + delta);
    }

    private static int HashLine(string text)
        => text.GetHashCode(StringComparison.Ordinal);

    private static readonly IReadOnlyDictionary<int, LineChangeKind> EmptyMap
        = new Dictionary<int, LineChangeKind>();

    /// <summary>
    /// Rebuilds the entire incremental state from <paramref name="currentLines"/> by performing
    /// a prefix/suffix diff against the saved hashes.  Used after Undo/Redo where incremental
    /// tracking was bypassed due to <c>_isInternalEdit</c>.
    /// </summary>
    internal void RebuildFromLines(IReadOnlyList<string> currentLines)
    {
        if (_savedHashes.IsEmpty)
        {
            // No save-point yet — nothing to compare against.
            Changed?.Invoke(this, EmptyMap);
            return;
        }

        _addedLines.Clear();
        _modifiedLines.Clear();
        _deletedHints.Clear();

        // Rebuild _savedToCurrent via prefix/suffix identity mapping then PrefixSuffixDiff.
        var diff = PrefixSuffixDiff(currentLines, _savedHashes);

        // Reconstruct sets from the diff result.
        foreach (var (line, kind) in diff)
        {
            switch (kind)
            {
                case LineChangeKind.Added:    _addedLines.Add(line);    break;
                case LineChangeKind.Modified: _modifiedLines.Add(line); break;
                case LineChangeKind.Deleted:  _deletedHints.Add(line);  break;
            }
        }

        // Rebuild _savedToCurrent from the diff (best-effort).
        int n = currentLines.Count, m = _savedHashes.Length;
        _savedToCurrent = new int[m];
        for (int i = 0; i < m; i++) _savedToCurrent[i] = -1;

        int pre = 0;
        while (pre < n && pre < m && HashLine(currentLines[pre]) == _savedHashes[pre])
        {
            _savedToCurrent[pre] = pre;
            pre++;
        }
        int suf = 0;
        while (suf < n - pre && suf < m - pre &&
               HashLine(currentLines[n - 1 - suf]) == _savedHashes[m - 1 - suf])
        {
            _savedToCurrent[m - 1 - suf] = n - 1 - suf;
            suf++;
        }
        // Middle region: map 1:1 for unchanged positions.
        int cStart = pre, cEnd = n - suf;
        int sStart = pre, sEnd = m - suf;
        for (int si = sStart; si < sEnd && si - sStart < cEnd - cStart; si++)
        {
            int ci = cStart + (si - sStart);
            if (HashLine(currentLines[ci]) == _savedHashes[si])
                _savedToCurrent[si] = ci;
        }

        RaiseChanged();
    }

    // ── Legacy static helper (kept for unit tests) ────────────────────────────

    /// <summary>
    /// Static diff used by tests.  Not called by the live tracker.
    /// </summary>
    internal static IReadOnlyDictionary<int, LineChangeKind> ComputeChanges(
        IReadOnlyList<string> currentLines,
        ImmutableArray<int>   savedHashes)
    {
        var tracker = new GutterChangeTracker();
        tracker.MarkSavePoint(savedHashes.Length > 0
            ? (IReadOnlyList<string>)new List<string>() // minimal stub — tests should use instance API
            : Array.Empty<string>());
        // Simple fallback: prefix/suffix diff (accurate for pure insert/delete).
        return PrefixSuffixDiff(currentLines, savedHashes);
    }

    private static IReadOnlyDictionary<int, LineChangeKind> PrefixSuffixDiff(
        IReadOnlyList<string> cur, ImmutableArray<int> saved)
    {
        var result = new Dictionary<int, LineChangeKind>();
        int n = cur.Count, m = saved.Length;

        int prefix = 0;
        while (prefix < n && prefix < m && HashLine(cur[prefix]) == saved[prefix])
            prefix++;

        int suffix = 0;
        while (suffix < n - prefix && suffix < m - prefix &&
               HashLine(cur[n - 1 - suffix]) == saved[m - 1 - suffix])
            suffix++;

        int cStart = prefix, cEnd = n - suffix;
        int sStart = prefix, sEnd = m - suffix;
        int cMid = cEnd - cStart, sMid = sEnd - sStart;

        for (int i = cStart; i < cEnd; i++)
        {
            int off = i - cStart;
            if (off < sMid)
            {
                if (HashLine(cur[i]) != saved[sStart + off])
                    result[i] = LineChangeKind.Modified;
            }
            else result[i] = LineChangeKind.Added;
        }

        if (sMid > cMid)
        {
            int pred = Math.Max(0, cEnd - 1);
            if (!result.ContainsKey(pred)) result[pred] = LineChangeKind.Deleted;
        }

        return result;
    }
}
