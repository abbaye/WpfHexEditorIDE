// ==========================================================
// Project: WpfHexEditor.SDK
// File: IPluginDiagnostics.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Interface exposing real-time performance diagnostics for a plugin.
//     Implemented by PluginDiagnosticsCollector in PluginHost.
//
// Architecture Notes:
//     Read by SlowPluginDetector and PluginManager UI.
//     Plugin authors may optionally implement this on their plugin class to
//     provide self-reported metrics.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Exposes real-time performance diagnostics for a loaded plugin.
/// </summary>
public interface IPluginDiagnostics
{
    /// <summary>Gets the current memory usage attributed to this plugin in bytes.</summary>
    long MemoryBytes { get; }

    /// <summary>Gets the current CPU usage percentage (0.0–100.0) attributed to this plugin.</summary>
    double CpuUsagePercent { get; }

    /// <summary>Gets the rolling average execution time of the last N plugin calls.</summary>
    TimeSpan AverageExecutionTime { get; }

    /// <summary>
    /// Gets whether the plugin is currently responsive
    /// (i.e. not blocked, frozen, or exceeding watchdog threshold).
    /// </summary>
    bool IsResponsive { get; }

    /// <summary>Gets the time elapsed since the plugin was last initialized.</summary>
    TimeSpan Uptime { get; }
}
