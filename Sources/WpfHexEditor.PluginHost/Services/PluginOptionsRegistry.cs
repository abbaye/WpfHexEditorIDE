//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Represents a registered plugin options entry.
/// </summary>
public sealed record PluginOptionsEntry(string PluginId, string PluginName, IPluginWithOptions Plugin);

/// <summary>
/// Runtime registry of plugin options pages.
/// PluginHost auto-registers entries for every loaded plugin that implements IPluginWithOptions.
/// </summary>
public interface IPluginOptionsRegistry
{
    void RegisterPluginPage(string pluginId, string pluginName, IPluginWithOptions plugin);
    void UnregisterPluginPage(string pluginId);
    IReadOnlyList<PluginOptionsEntry> GetAll();
}

/// <summary>
/// Thread-safe implementation of IPluginOptionsRegistry.
/// </summary>
public sealed class PluginOptionsRegistry : IPluginOptionsRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<string, PluginOptionsEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public void RegisterPluginPage(string pluginId, string pluginName, IPluginWithOptions plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_lock)
            _entries[pluginId] = new PluginOptionsEntry(pluginId, pluginName, plugin);
    }

    public void UnregisterPluginPage(string pluginId)
    {
        lock (_lock) _entries.Remove(pluginId);
    }

    public IReadOnlyList<PluginOptionsEntry> GetAll()
    {
        lock (_lock) return [.. _entries.Values];
    }
}
