// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/PluginCapabilityRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Live query implementation of IPluginCapabilityRegistry backed
//     by WpfPluginHost's loaded entries dictionary.
//     No caching — queries always reflect the current plugin state.
//
// Architecture Notes:
//     Passed to WpfPluginHost via PluginCapabilityRegistryAdapter to
//     resolve the circular dependency: host needs context in ctor,
//     context needs registry, registry needs host entries.
// ==========================================================

using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Live implementation of <see cref="IPluginCapabilityRegistry"/> backed by
/// the host's loaded plugin entries.
/// </summary>
internal sealed class PluginCapabilityRegistry : IPluginCapabilityRegistry
{
    private readonly IReadOnlyDictionary<string, PluginEntry> _entries;

    public PluginCapabilityRegistry(IReadOnlyDictionary<string, PluginEntry> entries)
    {
        _entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> FindPluginsWithFeature(string feature)
    {
        if (string.IsNullOrWhiteSpace(feature)) return [];
        return _entries.Values
            .Where(e => e.Manifest.Features.Contains(feature, StringComparer.OrdinalIgnoreCase))
            .Select(e => e.Manifest.Id)
            .ToList();
    }

    /// <inheritdoc />
    public bool PluginHasFeature(string pluginId, string feature)
    {
        if (!_entries.TryGetValue(pluginId, out var entry)) return false;
        return entry.Manifest.Features.Contains(feature, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetFeaturesForPlugin(string pluginId)
    {
        if (!_entries.TryGetValue(pluginId, out var entry)) return [];
        return entry.Manifest.Features;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllRegisteredFeatures()
    {
        return _entries.Values
            .SelectMany(e => e.Manifest.Features)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
