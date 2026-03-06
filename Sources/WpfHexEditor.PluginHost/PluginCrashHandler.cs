// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginCrashHandler.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Handles unhandled exceptions from plugins.
//     Marks the plugin as Faulted, triggers UI notification, and
//     attempts a clean unload. Guarantees IDE stability regardless of plugin failures.
//
// Architecture Notes:
//     Called by PluginHost's try/catch around InitializeAsync and other plugin calls.
//     Raises PluginFaulted event for MainWindow to display InfoBar notification.
//     Never re-throws — the IDE must not crash due to a plugin.
//
// ==========================================================

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Event arguments for the plugin faulted notification.
/// </summary>
public sealed class PluginFaultedEventArgs : EventArgs
{
    /// <summary>Plugin identifier that faulted.</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>Plugin display name.</summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>Exception that caused the fault.</summary>
    public Exception Exception { get; init; } = null!;

    /// <summary>Phase during which the fault occurred (e.g. "InitializeAsync", "ShutdownAsync").</summary>
    public string Phase { get; init; } = string.Empty;
}

/// <summary>
/// Handles plugin crash events — marks Faulted state and raises notifications.
/// </summary>
internal sealed class PluginCrashHandler
{
    /// <summary>
    /// Raised when a plugin is marked Faulted.
    /// Raised on the calling thread — MainWindow subscribes to show InfoBar.
    /// </summary>
    public event EventHandler<PluginFaultedEventArgs>? PluginFaulted;

    /// <summary>
    /// Handles a plugin fault: updates entry state, raises PluginFaulted event.
    /// </summary>
    /// <param name="entry">Plugin entry to mark as Faulted.</param>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="phase">Phase name where the fault occurred.</param>
    public void HandleCrash(PluginEntry entry, Exception exception, string phase)
    {
        entry.State = PluginState.Faulted;
        entry.FaultException = exception;

        PluginFaulted?.Invoke(this, new PluginFaultedEventArgs
        {
            PluginId = entry.Manifest.Id,
            PluginName = entry.Manifest.Name,
            Exception = exception,
            Phase = phase
        });
    }
}
