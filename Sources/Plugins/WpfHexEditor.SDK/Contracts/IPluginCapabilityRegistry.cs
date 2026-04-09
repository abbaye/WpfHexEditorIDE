// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/IPluginCapabilityRegistry.cs
// Created: 2026-03-15
// Description:
//     Query interface for discovering which plugins declare which semantic features.
//     Exposed via IIDEHostContext.CapabilityRegistry.
//
// Architecture Notes:
//     Feature strings are declared in PluginFeature static class.
//     Plugins and IDE code can call FindPluginsWithFeature() without knowing plugin identities.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Registry for querying semantic feature declarations across all loaded (and dormant) plugins.
/// Access via <c>context.CapabilityRegistry</c> in <see cref="IIDEHostContext"/>.
/// </summary>
public interface IPluginCapabilityRegistry
{
    /// <summary>
    /// Returns IDs of all plugins (loaded or dormant) that declare <paramref name="feature"/>.
    /// Use <see cref="WpfHexEditor.SDK.Models.PluginFeature"/> constants for well-known features.
    /// </summary>
    IReadOnlyList<string> FindPluginsWithFeature(string feature);

    /// <summary>Returns true if the plugin with <paramref name="pluginId"/> declares <paramref name="feature"/>.</summary>
    bool PluginHasFeature(string pluginId, string feature);

    /// <summary>Returns all feature strings declared by the plugin with <paramref name="pluginId"/>.</summary>
    IReadOnlyList<string> GetFeaturesForPlugin(string pluginId);

    /// <summary>Returns all distinct feature strings declared across all known plugins, sorted.</summary>
    IReadOnlyList<string> GetAllRegisteredFeatures();

    /// <summary>
    /// Registers a plugin as a workspace-state participant.
    /// Call this from <c>InitializeAsync</c> to opt in to workspace save/restore callbacks.
    /// </summary>
    /// <param name="pluginId">The stable plugin ID (used as the key in <c>plugins.json</c>).</param>
    /// <param name="persistable">The implementation that captures and restores plugin state.</param>
    void RegisterWorkspacePersistable(string pluginId, IWorkspacePersistable persistable);

    /// <summary>
    /// Returns all currently registered workspace-persistable entries.
    /// Called by the IDE during workspace save and restore.
    /// </summary>
    IReadOnlyList<(string PluginId, IWorkspacePersistable Persistable)> GetWorkspacePersistables();
}
