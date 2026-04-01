// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/IExtensionRegistry.cs
// Created: 2026-03-15
// Description:
//     Registry for plugin extension point contributions.
//     IDE calls GetExtensions<T>() to obtain all contributors for a given extension point.
//     WpfPluginHost calls Register<T>() after loading each plugin.
//
// Architecture Notes:
//     Pattern: Extension Points. IDE exposes named contracts; plugins register implementations.
//     The IDE iterates contributors without knowing plugin identities.
//     Extension point contracts are defined in SDK/ExtensionPoints/.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Registry for plugin contributions to well-known IDE extension points.
/// The IDE obtains all contributors via <c>GetExtensions&lt;T&gt;()</c> without
/// knowing which plugins are registered.
/// </summary>
public interface IExtensionRegistry
{
    /// <summary>
    /// Returns a snapshot of all registered implementations for extension point <typeparamref name="T"/>.
    /// Returns empty list when no plugins have contributed to this extension point.
    /// </summary>
    IReadOnlyList<T> GetExtensions<T>() where T : class;

    /// <summary>
    /// Registers a plugin's implementation of extension point <typeparamref name="T"/>.
    /// Called by WpfPluginHost after a plugin's <c>InitializeAsync</c> completes.
    /// </summary>
    void Register<T>(string pluginId, T implementation) where T : class;

    /// <summary>
    /// Registers a plugin's implementation by runtime contract type.
    /// Used by the reflection-based manifest extension registration path
    /// when the contract type is not known at compile time.
    /// </summary>
    void Register(string pluginId, Type contractType, object implementation);

    /// <summary>
    /// Removes all extension contributions registered by <paramref name="pluginId"/>.
    /// Called during plugin unload.
    /// </summary>
    void UnregisterAll(string pluginId);

    /// <summary>
    /// Returns a diagnostic snapshot of all registered contributions across all extension points.
    /// </summary>
    IReadOnlyList<ExtensionRegistryEntry> GetAllEntries();
}

/// <summary>Diagnostic snapshot of a single registered extension contribution.</summary>
public sealed record ExtensionRegistryEntry(
    string PluginId,
    string ExtensionPointName,
    Type ContractType);
