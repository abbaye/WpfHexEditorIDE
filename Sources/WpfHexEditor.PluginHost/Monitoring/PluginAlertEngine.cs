// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginAlertEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Evaluates loaded plugins against configurable thresholds
//     and raises AlertTriggered when a threshold is breached.
//     Debounced per plugin to prevent log spam.
//
// Architecture Notes:
//     Observer pattern — called by PluginMonitoringViewModel.Refresh()
//     on every sampling tick. Stateless between ticks except cooldown map.
//     All thresholds are hot-configurable via public properties.
// ==========================================================

namespace WpfHexEditor.PluginHost.Monitoring;

/// <summary>Kind of resource threshold that was breached.</summary>
public enum PluginAlertKind { Cpu, Memory, ExecTime }

/// <summary>Event args carrying context for a triggered plugin alert.</summary>
public sealed class PluginAlertEventArgs : EventArgs
{
    public string         PluginId       { get; init; } = string.Empty;
    public string         PluginName     { get; init; } = string.Empty;
    public PluginAlertKind Kind          { get; init; }
    public double         CurrentValue   { get; init; }
    public double         ThresholdValue { get; init; }
    public string         Message        { get; init; } = string.Empty;
}

/// <summary>
/// Monitors per-plugin metrics against configurable CPU, memory, and
/// execution-time thresholds and raises <see cref="AlertTriggered"/>
/// when a threshold is breached.
/// </summary>
public sealed class PluginAlertEngine
{
    // One alert per plugin per cooldown period (default: 60 s).
    private static readonly TimeSpan AlertCooldown = TimeSpan.FromSeconds(60);

    // Suppress all alerts for this duration after a plugin is loaded.
    // Prevents false positives from the CPU spike that occurs during batch startup.
    private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, DateTime> _lastAlertTime =
        new(StringComparer.OrdinalIgnoreCase);

    // -- Configurable thresholds (hot-configurable, no restart required) ---------

    /// <summary>CPU% threshold. Raises a Cpu alert when any plugin exceeds this.</summary>
    public double CpuAlertThreshold { get; set; } = 50.0;

    /// <summary>Memory threshold in MB. Raises a Memory alert when any plugin exceeds this.</summary>
    public long MemoryAlertThresholdMb { get; set; } = 200;

    /// <summary>Execution-time threshold. Raises an ExecTime alert when average exceeds this.</summary>
    public TimeSpan ExecTimeAlertThreshold { get; set; } = TimeSpan.FromSeconds(1);

    // -- Event -------------------------------------------------------------------

    /// <summary>
    /// Raised (at most once per cooldown window per plugin) when a threshold is breached.
    /// Always raised on the caller's thread — PluginMonitoringViewModel calls this
    /// from its Dispatcher-thread Refresh(), so no additional marshalling is needed.
    /// </summary>
    public event EventHandler<PluginAlertEventArgs>? AlertTriggered;

    // -- API ---------------------------------------------------------------------

    /// <summary>
    /// Evaluates all entries against the configured thresholds.
    /// Call once per diagnostics sampling tick from the Dispatcher thread.
    /// </summary>
    public void Evaluate(IEnumerable<PluginEntry> plugins)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in plugins)
        {
            if (entry.State != SDK.Models.PluginState.Loaded) continue;

            var snap = entry.Diagnostics.GetLatest();
            if (snap is null) continue;

            // Suppress alerts during the startup grace period to avoid false positives
            // caused by the process-wide CPU spike that occurs during batch plugin init.
            if (entry.Diagnostics.Uptime < StartupGracePeriod) continue;

            // Enforce cooldown — one alert per plugin per window.
            if (_lastAlertTime.TryGetValue(entry.Manifest.Id, out var lastAlert)
                && now - lastAlert < AlertCooldown) continue;

            var alert = EvaluateEntry(entry, snap);
            if (alert is null) continue;

            _lastAlertTime[entry.Manifest.Id] = now;
            AlertTriggered?.Invoke(this, alert);
        }
    }

    /// <summary>Clears the per-plugin cooldown map (e.g., when the user resets the monitor).</summary>
    public void ResetCooldowns() => _lastAlertTime.Clear();

    // -- Internals ---------------------------------------------------------------

    private PluginAlertEventArgs? EvaluateEntry(PluginEntry entry, DiagnosticsSnapshot snap)
    {
        // Defensively clamp CPU % to 0–100 regardless of how the sample was computed.
        var cpu = Math.Clamp(snap.CpuPercent, 0.0, 100.0);

        if (cpu > CpuAlertThreshold)
        {
            return new PluginAlertEventArgs
            {
                PluginId       = entry.Manifest.Id,
                PluginName     = entry.Manifest.Name,
                Kind           = PluginAlertKind.Cpu,
                CurrentValue   = cpu,
                ThresholdValue = CpuAlertThreshold,
                Message        = $"CPU {cpu:F1}% exceeds threshold {CpuAlertThreshold:F0}%"
            };
        }

        var memMb = snap.MemoryBytes / (1024 * 1024);
        if (memMb > MemoryAlertThresholdMb)
        {
            return new PluginAlertEventArgs
            {
                PluginId       = entry.Manifest.Id,
                PluginName     = entry.Manifest.Name,
                Kind           = PluginAlertKind.Memory,
                CurrentValue   = memMb,
                ThresholdValue = MemoryAlertThresholdMb,
                Message        = $"Memory {memMb} MB exceeds threshold {MemoryAlertThresholdMb} MB"
            };
        }

        var avg = entry.Diagnostics.AverageExecutionTime;
        if (avg > ExecTimeAlertThreshold)
        {
            return new PluginAlertEventArgs
            {
                PluginId       = entry.Manifest.Id,
                PluginName     = entry.Manifest.Name,
                Kind           = PluginAlertKind.ExecTime,
                CurrentValue   = avg.TotalMilliseconds,
                ThresholdValue = ExecTimeAlertThreshold.TotalMilliseconds,
                Message        = $"Avg exec {avg.TotalMilliseconds:F0} ms exceeds threshold {ExecTimeAlertThreshold.TotalMilliseconds:F0} ms"
            };
        }

        return null;
    }
}
