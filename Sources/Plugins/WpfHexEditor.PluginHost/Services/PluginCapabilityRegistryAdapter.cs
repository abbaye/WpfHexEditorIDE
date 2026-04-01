// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/PluginCapabilityRegistryAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Lazy adapter for IPluginCapabilityRegistry that resolves a circular
//     construction dependency between WpfPluginHost and IDEHostContext.
//
// Architecture Notes:
//     Pattern: Lazy Proxy (Gang of Four Proxy variant).
//     1. App constructs PluginCapabilityRegistryAdapter (empty).
//     2. App passes adapter into IDEHostContext constructor.
//     3. App constructs WpfPluginHost passing IDEHostContext.
//     4. App calls adapter.SetInner(pluginHost.CapabilityRegistry).
//     Plugins only call CapabilityRegistry after InitializeAsync completes,
//     so SetInner is always called before any real query.
// ==========================================================

using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Lazy proxy for <see cref="IPluginCapabilityRegistry"/> that resolves the
/// circular dependency between <c>WpfPluginHost</c> and <c>IDEHostContext</c>.
/// </summary>
public sealed class PluginCapabilityRegistryAdapter : IPluginCapabilityRegistry
{
    private IPluginCapabilityRegistry? _inner;

    /// <summary>
    /// Sets the real implementation. Must be called before any plugin calls
    /// <see cref="IIDEHostContext.CapabilityRegistry"/> (i.e., before <c>LoadAllAsync</c> resolves).
    /// </summary>
    public void SetInner(IPluginCapabilityRegistry inner)
        => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    private IPluginCapabilityRegistry Inner
        => _inner ?? throw new InvalidOperationException(
            "PluginCapabilityRegistryAdapter.SetInner() was not called before first use.");

    /// <inheritdoc />
    public IReadOnlyList<string> FindPluginsWithFeature(string feature)
        => Inner.FindPluginsWithFeature(feature);

    /// <inheritdoc />
    public bool PluginHasFeature(string pluginId, string feature)
        => Inner.PluginHasFeature(pluginId, feature);

    /// <inheritdoc />
    public IReadOnlyList<string> GetFeaturesForPlugin(string pluginId)
        => Inner.GetFeaturesForPlugin(pluginId);

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllRegisteredFeatures()
        => Inner.GetAllRegisteredFeatures();
}
