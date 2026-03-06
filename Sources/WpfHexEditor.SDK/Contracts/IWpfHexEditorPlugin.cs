// ==========================================================
// Project: WpfHexEditor.SDK
// File: IWpfHexEditorPlugin.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Main interface that every WpfHexEditor plugin must implement.
//     Entry point declared in the plugin manifest as "entryPoint" field.
//
// Architecture Notes:
//     Pattern: Plugin lifecycle managed by PluginHost (load → init → active → shutdown → unload).
//     InitializeAsync receives the full IIDEHostContext for service access.
//     ShutdownAsync must clean up all resources; UIRegistry cleanup is automatic.
//     Plugins may optionally implement IPluginState and IPluginDiagnostics.
//
// ==========================================================

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Main interface that every WpfHexEditor plugin must implement.
/// The class implementing this interface is the plugin entry point.
/// </summary>
public interface IWpfHexEditorPlugin
{
    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Globally unique plugin identifier matching the manifest <c>id</c> field.
    /// Example: "WpfHexEditor.Plugins.DataInspector"
    /// </summary>
    string Id { get; }

    /// <summary>Display name of the plugin (shown in Plugin Manager).</summary>
    string Name { get; }

    /// <summary>Plugin version matching the manifest <c>version</c> field.</summary>
    Version Version { get; }

    // ── Capabilities ─────────────────────────────────────────────────────────

    /// <summary>
    /// Declares the capabilities this plugin requires.
    /// Must match the permissions declared in the manifest.
    /// </summary>
    PluginCapabilities Capabilities { get; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes the plugin. Called by PluginHost after the assembly is loaded.
    /// Register UI elements, subscribe to events, and initialize resources here.
    /// Must complete within the configured watchdog timeout (default: 5 seconds).
    /// </summary>
    /// <param name="context">Full IDE host context providing access to all IDE services.</param>
    Task InitializeAsync(IIDEHostContext context);

    /// <summary>
    /// Shuts down the plugin cleanly. Called before unloading the assembly.
    /// Release all resources, unsubscribe events, and stop background tasks here.
    /// UI element cleanup is handled automatically by PluginHost via UIRegistry.
    /// </summary>
    Task ShutdownAsync();
}
