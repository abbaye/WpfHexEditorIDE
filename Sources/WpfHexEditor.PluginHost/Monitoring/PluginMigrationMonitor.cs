// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginMigrationMonitor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Background monitor that evaluates InProcess plugins against configurable
//     thresholds (memory, sustained CPU, crash count) and invokes a callback
//     when a migration trigger fires.
//
// Architecture Notes:
//     Observer pattern — called on a DispatcherTimer (10-sec interval).
//     Pure logic layer: no WPF UI dependencies beyond the Dispatcher for
//     marshalling the timer tick to the correct thread.
//     Trigger priority (first match wins): Crashes > Memory > CPU.
//     Already-Sandbox or non-Loaded plugins are always skipped.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.PluginHost.Services;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.Monitoring;

/// <summary>Reason that triggered a migration for a specific plugin.</summary>
public enum MigrationTriggerReason
{
    /// <summary>Plugin has crashed at least <c>CrashCountThreshold</c> times this session.</summary>
    Crashes,

    /// <summary>Plugin memory footprint exceeded the configured threshold.</summary>
    Memory,

    /// <summary>Plugin CPU% stayed above the configured threshold for the sustained window.</summary>
    Cpu
}

/// <summary>
/// Monitors InProcess plugins every 10 seconds and invokes the provided callback
/// when a resource or stability threshold is exceeded.
/// </summary>
internal sealed class PluginMigrationMonitor : IDisposable
{
    // Suppress all migration triggers for this duration after plugin load to avoid
    // false positives caused by the CPU/memory spike during batch initialisation.
    private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(60);

    // Minimum gap between two triggers for the same plugin (prevents rapid re-triggering
    // if the user dismisses a suggestion without migrating).
    private static readonly TimeSpan TriggerCooldown = TimeSpan.FromMinutes(5);

    private readonly Func<IEnumerable<PluginEntry>> _getLoadedEntries;
    private readonly Action<string, MigrationTriggerReason> _onMigrationTriggered;
    private readonly DispatcherTimer _timer;

    // Per-plugin crash counts (reset when plugin is reloaded/migrated).
    private readonly Dictionary<string, int> _crashCounts =
        new(StringComparer.OrdinalIgnoreCase);

    // Timestamp when CPU first exceeded the suggest threshold for each plugin.
    private readonly Dictionary<string, DateTime> _highCpuSince =
        new(StringComparer.OrdinalIgnoreCase);

    // Timestamp of last trigger per plugin (cooldown guard).
    private readonly Dictionary<string, DateTime> _lastTriggered =
        new(StringComparer.OrdinalIgnoreCase);

    private PluginMigrationPolicy _policy;
    private bool _disposed;

    /// <summary>
    /// Initialises the monitor.
    /// </summary>
    /// <param name="getLoadedEntries">Delegate that returns all currently loaded plugin entries.</param>
    /// <param name="policy">Initial migration policy (can be hot-updated via <see cref="UpdatePolicy"/>).</param>
    /// <param name="onMigrationTriggered">
    ///     Callback invoked on the Dispatcher thread when a trigger fires.
    ///     Receives (pluginId, reason). The callback is responsible for deciding whether to
    ///     suggest or auto-migrate based on the current <see cref="PluginMigrationPolicy.Mode"/>.
    /// </param>
    /// <param name="dispatcher">WPF Dispatcher for the timer tick.</param>
    public PluginMigrationMonitor(
        Func<IEnumerable<PluginEntry>> getLoadedEntries,
        PluginMigrationPolicy policy,
        Action<string, MigrationTriggerReason> onMigrationTriggered,
        Dispatcher dispatcher)
    {
        _getLoadedEntries     = getLoadedEntries     ?? throw new ArgumentNullException(nameof(getLoadedEntries));
        _policy               = policy               ?? throw new ArgumentNullException(nameof(policy));
        _onMigrationTriggered = onMigrationTriggered ?? throw new ArgumentNullException(nameof(onMigrationTriggered));

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _timer.Tick += OnTick;
    }

    // -- Public API ---------------------------------------------------------------

    /// <summary>Starts the background evaluation loop.</summary>
    public void Start()
    {
        if (!_disposed) _timer.Start();
    }

    /// <summary>
    /// Replaces the active policy with <paramref name="newPolicy"/>.
    /// Takes effect on the next timer tick.
    /// </summary>
    public void UpdatePolicy(PluginMigrationPolicy newPolicy)
    {
        _policy = newPolicy ?? throw new ArgumentNullException(nameof(newPolicy));
    }

    /// <summary>Increments the crash counter for the given plugin.</summary>
    public void RecordCrash(string pluginId)
    {
        if (string.IsNullOrEmpty(pluginId)) return;
        _crashCounts.TryGetValue(pluginId, out var current);
        _crashCounts[pluginId] = current + 1;
    }

    /// <summary>Resets the crash counter and high-CPU window for the given plugin.</summary>
    public void ResetCrashCount(string pluginId)
    {
        _crashCounts.Remove(pluginId);
        _highCpuSince.Remove(pluginId);
        _lastTriggered.Remove(pluginId);
    }

    /// <summary>Returns the current crash count for the given plugin (0 if never crashed).</summary>
    public int GetCrashCount(string pluginId)
        => _crashCounts.TryGetValue(pluginId, out var count) ? count : 0;

    // -- IDisposable --------------------------------------------------------------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    // -- Internals ----------------------------------------------------------------

    private void OnTick(object? sender, EventArgs e)
    {
        if (_policy.Mode == PluginMigrationMode.Disabled) return;

        var now = DateTime.UtcNow;

        foreach (var entry in _getLoadedEntries())
        {
            // Only monitor InProcess loaded plugins.
            if (entry.State != PluginState.Loaded) continue;
            if (entry.ResolvedIsolationMode == PluginIsolationMode.Sandbox) continue;

            // Startup grace period: skip until the plugin has been running long enough.
            if (entry.Diagnostics.Uptime < StartupGracePeriod) continue;

            // Trigger cooldown: do not re-fire within TriggerCooldown of the last trigger.
            if (_lastTriggered.TryGetValue(entry.Manifest.Id, out var lastTrigger)
                && now - lastTrigger < TriggerCooldown)
                continue;

            var reason = EvaluateEntry(entry, now);
            if (reason is null) continue;

            _lastTriggered[entry.Manifest.Id] = now;
            _onMigrationTriggered(entry.Manifest.Id, reason.Value);
        }
    }

    /// <summary>
    /// Evaluates one plugin entry.  Returns a trigger reason if a threshold is breached,
    /// null otherwise.  Priority: Crashes &gt; Memory &gt; CPU.
    /// </summary>
    private MigrationTriggerReason? EvaluateEntry(PluginEntry entry, DateTime now)
    {
        var id = entry.Manifest.Id;

        // --- 1. Crashes (highest priority) ---
        if (_crashCounts.TryGetValue(id, out var crashes)
            && crashes >= _policy.CrashCountThreshold)
            return MigrationTriggerReason.Crashes;

        var snap = entry.Diagnostics.GetLatest();
        if (snap is null) return null;

        var memMb = snap.MemoryBytes / (1024 * 1024);

        // --- 2. Memory ---
        // Fire on the AutoMigrate threshold in AutoMigrate mode, or on the Suggest threshold
        // in SuggestOnly mode.  Either way the reason is Memory — the caller decides what to do.
        var memThreshold = _policy.Mode == PluginMigrationMode.AutoMigrate
            ? _policy.MemoryAutoMigrateThresholdMb
            : _policy.MemorySuggestThresholdMb;

        if (memMb >= memThreshold)
            return MigrationTriggerReason.Memory;

        // --- 3. CPU (sustained window) ---
        var cpu = Math.Clamp(snap.CpuPercent, 0.0, 100.0);
        var cpuThreshold = _policy.Mode == PluginMigrationMode.AutoMigrate
            ? _policy.CpuAutoMigrateThresholdPercent
            : _policy.CpuSuggestThresholdPercent;

        if (cpu >= cpuThreshold)
        {
            if (!_highCpuSince.ContainsKey(id))
                _highCpuSince[id] = now;

            var sustainedFor = now - _highCpuSince[id];
            if (sustainedFor.TotalSeconds >= _policy.CpuSustainedWindowSeconds)
                return MigrationTriggerReason.Cpu;
        }
        else
        {
            // CPU dropped below threshold — reset the sustained window.
            _highCpuSince.Remove(id);
        }

        return null;
    }
}
