// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Services/LruCache.cs
// Description:
//     Lightweight LRU cache (Dictionary + LinkedList). Single-threaded —
//     callers must synchronise if accessed from multiple threads.
// Architecture: Generic utility; capacity enforced on Add.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Services;

/// <summary>
/// Fixed-capacity LRU cache. Evicts the least-recently-used entry when
/// <see cref="Capacity"/> is exceeded. Values may be <c>null</c>.
/// </summary>
internal sealed class LruCache<TKey, TValue>(int capacity) where TKey : notnull
{
    private readonly Dictionary<TKey, (TValue Value, LinkedListNode<TKey> Node)> _map = new(capacity);
    private readonly LinkedList<TKey> _order = new();

    public int Capacity { get; } = capacity;
    public int Count    => _map.Count;

    public bool TryGetValue(TKey key, out TValue? value)
    {
        if (!_map.TryGetValue(key, out var entry))
        {
            value = default;
            return false;
        }

        // promote to MRU
        _order.Remove(entry.Node);
        var node = _order.AddFirst(key);
        _map[key] = (entry.Value, node);

        value = entry.Value;
        return true;
    }

    public void Add(TKey key, TValue value)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            _order.Remove(existing.Node);
            var updatedNode = _order.AddFirst(key);
            _map[key] = (value, updatedNode);
            return;
        }

        if (_map.Count >= Capacity)
            EvictLast();

        var node = _order.AddFirst(key);
        _map[key] = (value, node);
    }

    public bool Remove(TKey key)
    {
        if (!_map.TryGetValue(key, out var entry)) return false;
        _order.Remove(entry.Node);
        _map.Remove(key);
        return true;
    }

    public bool ContainsKey(TKey key) => _map.ContainsKey(key);

    public void Clear() { _map.Clear(); _order.Clear(); }

    public IEnumerable<KeyValuePair<TKey, TValue>> Entries =>
        _map.Select(kv => new KeyValuePair<TKey, TValue>(kv.Key, kv.Value.Value));

    private void EvictLast()
    {
        var last = _order.Last;
        if (last is null) return;
        _map.Remove(last.Value);
        _order.RemoveLast();
    }
}
