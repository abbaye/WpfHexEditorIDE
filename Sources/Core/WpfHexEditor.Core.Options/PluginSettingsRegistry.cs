// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: PluginSettingsRegistry.cs
// Description:
//     Central registry of plugin-contributed settings sections. Each
//     plugin registers a getter + setter so the IDE can collect all
//     plugin state for the global preferences bundle (export) and push
//     it back at import time without each plugin owning its own file.
// ==========================================================

using System.Collections.Concurrent;

namespace WpfHexEditor.Core.Options;

/// <summary>
/// Registry that collects plugin-contributed settings handlers. Each handler
/// exposes a typed payload that can be serialized into the global
/// preferences bundle.
/// </summary>
public sealed class PluginSettingsRegistry
{
    public static PluginSettingsRegistry Instance { get; } = new();

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    private PluginSettingsRegistry() { }

    /// <summary>
    /// Registers a plugin settings handler. The same plugin id may register
    /// multiple times — the latest registration wins.
    /// </summary>
    public void Register(string pluginId, Func<object> getter, Action<object> setter)
    {
        ArgumentNullException.ThrowIfNull(pluginId);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _entries[pluginId] = new Entry(getter, setter);
    }

    public void Unregister(string pluginId) => _entries.TryRemove(pluginId, out _);

    /// <summary>Returns a snapshot of all currently-registered plugin payloads.</summary>
    public IReadOnlyDictionary<string, object> Snapshot()
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, entry) in _entries)
        {
            try { dict[id] = entry.Getter(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PluginSettings] snapshot failed for {id}: {ex.Message}"); }
        }
        return dict;
    }

    /// <summary>Pushes payloads back to their registered setters.</summary>
    public void Apply(IReadOnlyDictionary<string, object> payloads)
    {
        if (payloads is null) return;
        foreach (var (id, payload) in payloads)
        {
            if (!_entries.TryGetValue(id, out var entry)) continue;
            try { entry.Setter(payload); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PluginSettings] apply failed for {id}: {ex.Message}"); }
        }
    }

    private sealed record Entry(Func<object> Getter, Action<object> Setter);
}
