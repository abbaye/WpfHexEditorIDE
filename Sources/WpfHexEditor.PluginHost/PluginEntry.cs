// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginEntry.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Internal record of a single loaded (or failed) plugin, held by PluginHost.
//     Aggregates manifest, live instance, load context, diagnostics, and state.
//
// Architecture Notes:
//     Mutable state (State, FaultException) is updated by PluginHost exclusively.
//     Thread-safe reads via volatile for State field.
//
// ==========================================================

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Internal record of a single plugin managed by <see cref="PluginHost"/>.
/// </summary>
internal sealed class PluginEntry
{
    // ── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Parsed plugin manifest.</summary>
    public PluginManifest Manifest { get; }

    // ── Live Instance ────────────────────────────────────────────────────────

    /// <summary>Plugin instance (null until successfully loaded).</summary>
    public IWpfHexEditorPlugin? Instance { get; set; }

    /// <summary>Isolated AssemblyLoadContext for InProcess plugins (null for Sandbox).</summary>
    public PluginLoadContext? LoadContext { get; set; }

    // ── State ────────────────────────────────────────────────────────────────

    private volatile PluginState _state = PluginState.Unloaded;

    /// <summary>Current lifecycle state of the plugin.</summary>
    public PluginState State
    {
        get => _state;
        set => _state = value;
    }

    /// <summary>Exception captured during a Faulted transition (null otherwise).</summary>
    public Exception? FaultException { get; set; }

    // ── Timing ───────────────────────────────────────────────────────────────

    /// <summary>UTC timestamp when the plugin was successfully initialized.</summary>
    public DateTime? LoadedAt { get; set; }

    /// <summary>Time taken by the plugin's InitializeAsync call.</summary>
    public TimeSpan InitDuration { get; set; }

    // ── Diagnostics ──────────────────────────────────────────────────────────

    /// <summary>Rolling performance diagnostics collector for this plugin.</summary>
    public PluginDiagnosticsCollector Diagnostics { get; } = new();

    // ── Constructor ──────────────────────────────────────────────────────────

    public PluginEntry(PluginManifest manifest)
    {
        Manifest = manifest;
    }
}
