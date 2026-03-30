// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: BinaryMap/BinaryMap.cs
// Description:
//     Bidirectional map between DocumentBlock and byte offsets.
//     Block lookup by offset uses binary search (O(log n)) over the
//     sorted entries list for fast hex-→text navigation.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;

/// <summary>
/// Bidirectional mapping: <see cref="DocumentBlock"/> ↔ byte offset/length.
/// Thread-safe for reads after initial population.
/// </summary>
public sealed class BinaryMap
{
    // Sorted by Offset for binary search.
    private readonly List<BinaryMapEntry> _sortedEntries = [];
    private readonly Dictionary<DocumentBlock, BinaryMapEntry> _blockIndex = [];
    private bool _sorted = true;

    // ──────────────────────────────── Population ──────────────────────────────

    /// <summary>
    /// Registers a block↔offset mapping.
    /// Must be called from a single thread during loader execution;
    /// <see cref="Seal"/> must be called before any lookup.
    /// </summary>
    public void Add(DocumentBlock block, long offset, int length)
    {
        var entry = new BinaryMapEntry(block, offset, length);
        _sortedEntries.Add(entry);
        _blockIndex[block] = entry;
        _sorted = false;
    }

    /// <summary>
    /// Sorts entries by offset — call once after all <see cref="Add"/> calls.
    /// </summary>
    public void Seal()
    {
        if (_sorted) return;
        _sortedEntries.Sort(static (a, b) => a.Offset.CompareTo(b.Offset));
        _sorted = true;
    }

    /// <summary>Clears all entries (used after undo/redo that rebuilds the map).</summary>
    public void Clear()
    {
        _sortedEntries.Clear();
        _blockIndex.Clear();
        _sorted = true;
    }

    // ──────────────────────────────── Lookups ─────────────────────────────────

    /// <summary>
    /// Returns the block whose range contains <paramref name="offset"/>,
    /// or <c>null</c> if none.
    /// </summary>
    public DocumentBlock? BlockAt(long offset)
    {
        if (!_sorted) Seal();
        var idx = BinarySearchOffset(offset);
        return idx >= 0 ? _sortedEntries[idx].Block : null;
    }

    /// <summary>
    /// Returns the <see cref="BinaryMapEntry"/> for <paramref name="offset"/>,
    /// or <c>null</c> if none.
    /// </summary>
    public BinaryMapEntry? EntryAt(long offset)
    {
        if (!_sorted) Seal();
        var idx = BinarySearchOffset(offset);
        return idx >= 0 ? _sortedEntries[idx] : null;
    }

    /// <summary>
    /// Returns the entry associated with <paramref name="block"/>, or <c>null</c>.
    /// </summary>
    public BinaryMapEntry? EntryOf(DocumentBlock block) =>
        _blockIndex.TryGetValue(block, out var e) ? e : null;

    /// <summary>All entries in offset order (after <see cref="Seal"/>).</summary>
    public IReadOnlyList<BinaryMapEntry> Entries => _sortedEntries;

    /// <summary>Returns all entries enumerable — alias for <see cref="Entries"/>.</summary>
    public IEnumerable<BinaryMapEntry> GetAll() => _sortedEntries;

    /// <summary>Number of mapped blocks.</summary>
    public int Count => _sortedEntries.Count;

    /// <summary>Total byte span covered by all entries (offset of last entry end).</summary>
    public long TotalMappedLength =>
        _sortedEntries.Count == 0 ? 0 :
        _sortedEntries[^1].Offset + _sortedEntries[^1].Length;

    /// <summary>Raised after <see cref="MergeFrom"/> or <see cref="Clear"/> rebuilds the map.</summary>
    public event EventHandler? MapRebuilt;

    /// <summary>
    /// Merges all entries from <paramref name="source"/> into this map and seals.
    /// Used by DocumentEditorHost after a loader populates a temporary BinaryMap.
    /// </summary>
    public void MergeFrom(BinaryMap source)
    {
        foreach (var e in source._sortedEntries)
        {
            _sortedEntries.Add(e);
            _blockIndex[e.Block] = e;
        }
        _sorted = false;
        Seal();
        MapRebuilt?.Invoke(this, EventArgs.Empty);
    }

    // ──────────────────────────────── Binary search ───────────────────────────

    private int BinarySearchOffset(long offset)
    {
        int lo = 0, hi = _sortedEntries.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >>> 1;
            var entry = _sortedEntries[mid];
            if (entry.Contains(offset)) return mid;
            if (offset < entry.Offset) hi = mid - 1;
            else lo = mid + 1;
        }
        return -1;
    }
}
