// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/DecompileCache.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Thread-safe LRU cache for decompiled text output.
//     Prevents re-decompilation when the user switches back to a previously
//     viewed node. Evicts the oldest entry when the maximum size is reached.
//
// Architecture Notes:
//     Pattern: LRU Cache (Dictionary + LinkedList).
//     Key: (filePath, metadataToken, tabKind) — tabKind ∈ {"code","il"}.
//     Thread safety: lock(_sync) around all mutating operations.
//     Max size defaults to AssemblyExplorerOptions.DecompileCacheSizeMax (50).
//     Invalidate(filePath) removes all entries for a given assembly so stale
//     cache hits do not survive a file rebuild (wired to FileSystemWatcher).
// ==========================================================

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Thread-safe LRU cache for decompiled text.
/// Cache key = (filePath, metadataToken, tabKind).
/// </summary>
public sealed class DecompileCache
{
    private readonly record struct CacheKey(string FilePath, int MetadataToken, string TabKind);

    private readonly Dictionary<CacheKey, (string Value, LinkedListNode<CacheKey> Order)> _map = [];
    private readonly LinkedList<CacheKey> _order = new();
    private readonly object _sync = new();

    public int MaxSize { get; set; } = 50;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to retrieve a cached value. Returns <c>true</c> and sets
    /// <paramref name="value"/> when a fresh entry is found; otherwise <c>false</c>.
    /// Moves the hit entry to the front (most-recently used).
    /// </summary>
    public bool TryGet(string filePath, int metadataToken, string tabKind, out string value)
    {
        var key = new CacheKey(filePath, metadataToken, tabKind);
        lock (_sync)
        {
            if (!_map.TryGetValue(key, out var entry))
            {
                value = string.Empty;
                return false;
            }

            // Move to front (most recently used).
            _order.Remove(entry.Order);
            var newNode = _order.AddFirst(key);
            _map[key] = (entry.Value, newNode);

            value = entry.Value;
            return true;
        }
    }

    /// <summary>
    /// Stores <paramref name="value"/> in the cache.
    /// Evicts the least-recently-used entry if <see cref="MaxSize"/> is reached.
    /// </summary>
    public void Set(string filePath, int metadataToken, string tabKind, string value)
    {
        var key = new CacheKey(filePath, metadataToken, tabKind);
        lock (_sync)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                _order.Remove(existing.Order);
                var updatedNode = _order.AddFirst(key);
                _map[key] = (value, updatedNode);
                return;
            }

            if (_map.Count >= MaxSize)
                EvictLast();

            var node = _order.AddFirst(key);
            _map[key] = (value, node);
        }
    }

    /// <summary>
    /// Removes all cache entries whose <c>FilePath</c> matches <paramref name="filePath"/>
    /// (case-insensitive). Called when a file changes on disk.
    /// </summary>
    public void Invalidate(string filePath)
    {
        lock (_sync)
        {
            var toRemove = _map.Keys
                .Where(k => string.Equals(k.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in toRemove)
            {
                _order.Remove(_map[key].Order);
                _map.Remove(key);
            }
        }
    }

    /// <summary>Removes all entries from the cache.</summary>
    public void Clear()
    {
        lock (_sync)
        {
            _map.Clear();
            _order.Clear();
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void EvictLast()
    {
        var last = _order.Last;
        if (last is null) return;
        _map.Remove(last.Value);
        _order.RemoveLast();
    }
}
