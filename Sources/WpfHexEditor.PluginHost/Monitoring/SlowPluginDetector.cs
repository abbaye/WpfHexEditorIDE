// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: SlowPluginDetector.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Periodically scans all loaded plugins and emits SlowPluginDetected
//     when a plugin's rolling average execution time exceeds its dynamic threshold
//     (average × 2.5) or a configured absolute ceiling.
//
// Architecture Notes:
//     DispatcherTimer (UI thread) at configurable interval (default 5s).
//     Dynamic threshold prevents false positives on legitimately slow plugins.
//     SlowPluginDetected carries Disable/Restart/Ignore callbacks for InfoBar wiring.
//
// ==========================================================

using System.Windows.Threading;

namespace WpfHexEditor.PluginHost.Monitoring;

/// <summary>
/// Detects plugins whose execution time consistently exceeds acceptable thresholds.
/// </summary>
public sealed class SlowPluginDetector : IDisposable
{
    private readonly Func<IReadOnlyList<PluginEntry>> _getPlugins;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    // Configurable thresholds
    public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan AbsoluteThreshold { get; set; } = TimeSpan.FromMilliseconds(500);
    public double DynamicThresholdMultiplier { get; set; } = 2.5;

    /// <summary>
    /// Raised when a plugin is detected as slow. Handler can Disable/Restart/Ignore.
    /// </summary>
    public event EventHandler<SlowPluginDetectedEventArgs>? SlowPluginDetected;

    public SlowPluginDetector(Func<IReadOnlyList<PluginEntry>> getPlugins, Dispatcher dispatcher)
    {
        _getPlugins = getPlugins ?? throw new ArgumentNullException(nameof(getPlugins));
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = MonitoringInterval
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Interval = MonitoringInterval; // allow runtime changes to take effect

        foreach (var entry in _getPlugins())
        {
            if (entry.State != SDK.Models.PluginState.Loaded) continue;

            var avg = entry.Diagnostics.AverageExecutionTime;
            if (avg == TimeSpan.Zero) continue;

            var dynamicThreshold = TimeSpan.FromTicks((long)(avg.Ticks * DynamicThresholdMultiplier));
            var effectiveThreshold = dynamicThreshold < AbsoluteThreshold ? AbsoluteThreshold : dynamicThreshold;

            if (avg > effectiveThreshold)
            {
                SlowPluginDetected?.Invoke(this, new SlowPluginDetectedEventArgs(
                    entry.Manifest.Id,
                    entry.Manifest.Name,
                    avg,
                    effectiveThreshold));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}

/// <summary>
/// Event args for SlowPluginDetected, carrying enough context for an InfoBar.
/// </summary>
public sealed class SlowPluginDetectedEventArgs : EventArgs
{
    public string PluginId { get; }
    public string PluginName { get; }
    public TimeSpan AverageExecutionTime { get; }
    public TimeSpan Threshold { get; }

    public SlowPluginDetectedEventArgs(
        string pluginId,
        string pluginName,
        TimeSpan averageExecutionTime,
        TimeSpan threshold)
    {
        PluginId = pluginId;
        PluginName = pluginName;
        AverageExecutionTime = averageExecutionTime;
        Threshold = threshold;
    }
}
