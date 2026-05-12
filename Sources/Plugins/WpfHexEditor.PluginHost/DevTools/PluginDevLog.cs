// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: DevTools/PluginDevLog.cs
// Description:
//     Rolling buffer of plugin lifecycle events for the Plugin Dev Log
//     panel. Filters by category (Load / Unload / HotReload / Crash /
//     Slow / Info). Thread-safe; capacity-bounded.
// ==========================================================

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace WpfHexEditor.PluginHost.DevTools;

/// <summary>Category of a plugin dev-log entry.</summary>
public enum PluginDevLogCategory
{
    Info,
    Load,
    Unload,
    HotReload,
    Crash,
    Slow,
}

/// <summary>A single entry in the plugin dev log.</summary>
public sealed record PluginDevLogEntry(
    DateTime              Timestamp,
    PluginDevLogCategory  Category,
    string                PluginId,
    string                Message);

/// <summary>Thread-safe rolling buffer feeding a UI <see cref="ObservableCollection{T}"/>.</summary>
public sealed class PluginDevLog
{
    public const int DefaultCapacity = 1000;

    private readonly ConcurrentQueue<PluginDevLogEntry> _buffer = new();
    private readonly int _capacity;
    private readonly Dispatcher? _dispatcher;

    public PluginDevLog(int capacity = DefaultCapacity, Dispatcher? dispatcher = null)
    {
        _capacity   = capacity;
        _dispatcher = dispatcher;
    }

    /// <summary>UI-bindable rolling view (cleared/added on the dispatcher).</summary>
    public ObservableCollection<PluginDevLogEntry> Entries { get; } = new();

    public void Append(PluginDevLogCategory category, string pluginId, string message)
    {
        var entry = new PluginDevLogEntry(DateTime.UtcNow, category, pluginId, message);
        _buffer.Enqueue(entry);

        while (_buffer.Count > _capacity && _buffer.TryDequeue(out _)) { /* trim */ }

        if (_dispatcher is null || _dispatcher.CheckAccess())
            AppendOnUi(entry);
        else
            _dispatcher.BeginInvoke(() => AppendOnUi(entry));
    }

    public IReadOnlyList<PluginDevLogEntry> Filter(IReadOnlyCollection<PluginDevLogCategory> categories)
        => _buffer.Where(e => categories.Contains(e.Category)).ToList();

    public void Clear()
    {
        while (_buffer.TryDequeue(out _)) { }
        if (_dispatcher is null || _dispatcher.CheckAccess()) Entries.Clear();
        else _dispatcher.BeginInvoke(() => Entries.Clear());
    }

    private void AppendOnUi(PluginDevLogEntry entry)
    {
        Entries.Add(entry);
        while (Entries.Count > _capacity) Entries.RemoveAt(0);
    }
}
