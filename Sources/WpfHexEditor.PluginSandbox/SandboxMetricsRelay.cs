//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginSandbox
// File: SandboxMetricsRelay.cs
// Created: 2026-03-15
// Description:
//     Periodically measures CPU and memory of the sandbox process and
//     pushes MetricsPushPayload to the IDE via IpcChannel.
//     Provides real, per-process performance data — replacing the estimated
//     weight-based metrics used for InProcess plugins.
//
// Architecture Notes:
//     - Pattern: Background timer + push telemetry
//     - CPU is measured as sandbox process TotalProcessorTime delta / wall time.
//     - Memory is taken from Process.PrivateMemorySize64 (OS-level) and
//       GC.GetTotalMemory (managed heap).
//     - Push interval: 5 seconds (matches IDE passive sampling interval).
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.SDK.Sandbox;

namespace WpfHexEditor.PluginSandbox;

/// <summary>
/// Measures sandbox process metrics and pushes them to the IDE host every 5 seconds.
/// </summary>
internal sealed class SandboxMetricsRelay
{
    private const int PushIntervalMs = 5_000;

    private readonly IpcChannel _channel;
    private readonly Process _self;

    private string _pluginId = string.Empty;
    private CancellationTokenSource _cts = new();
    private Task? _relayTask;

    // Rolling execution-time tracking for AvgExecMs
    private long _callCount;
    private double _totalExecMs;
    private readonly object _execLock = new();

    // Last-known CPU sample
    private TimeSpan _lastCpuTime;
    private DateTime _lastCpuCheck;

    // ─────────────────────────────────────────────────────────────────────────
    public SandboxMetricsRelay(IpcChannel channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _self = Process.GetCurrentProcess();
        _lastCpuTime = _self.TotalProcessorTime;
        _lastCpuCheck = DateTime.UtcNow;
    }

    /// <summary>Sets the plugin ID used in the MetricsPushPayload.</summary>
    public void SetPluginId(string pluginId) => _pluginId = pluginId;

    /// <summary>
    /// Records a single plugin callback execution time so AvgExecMs is accurate.
    /// Called by SandboxedPluginRunner after timing each plugin method call.
    /// </summary>
    public void RecordExecution(TimeSpan elapsed)
    {
        lock (_execLock)
        {
            _callCount++;
            _totalExecMs += elapsed.TotalMilliseconds;
        }
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _relayTask = Task.Run(() => RelayLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _relayTask?.Wait(TimeSpan.FromSeconds(2)); }
        catch { /* best-effort */ }
    }

    // ── Background loop ───────────────────────────────────────────────────────

    private async Task RelayLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PushIntervalMs, ct).ConfigureAwait(false);
                await PushOnceAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch { /* never crash the relay */ }
        }
    }

    private async Task PushOnceAsync(CancellationToken ct)
    {
        _self.Refresh();

        // CPU delta
        var now = DateTime.UtcNow;
        var wall = now - _lastCpuCheck;
        var cpu = _self.TotalProcessorTime;
        var cpuDelta = cpu - _lastCpuTime;
        var cpuPct = wall.TotalMilliseconds > 0
            ? Math.Clamp(
                cpuDelta.TotalMilliseconds / (wall.TotalMilliseconds * Environment.ProcessorCount) * 100.0,
                0.0, 100.0)
            : 0.0;
        _lastCpuTime = cpu;
        _lastCpuCheck = now;

        // Memory
        var privateMem = _self.PrivateMemorySize64;
        var gcMem = GC.GetTotalMemory(forceFullCollection: false);

        // Avg exec time
        double avgExecMs;
        lock (_execLock)
        {
            avgExecMs = _callCount > 0 ? _totalExecMs / _callCount : 0.0;
            // Reset rolling window
            _callCount = 0;
            _totalExecMs = 0.0;
        }

        var payload = new MetricsPushPayload
        {
            PluginId          = _pluginId,
            CpuPercent        = cpuPct,
            PrivateMemoryBytes = privateMem,
            GcMemoryBytes     = gcMem,
            AvgExecMs         = avgExecMs,
            Timestamp         = now,
        };

        await _channel.SendAsync(new SandboxEnvelope
        {
            Kind    = SandboxMessageKind.MetricsPush,
            Payload = System.Text.Json.JsonSerializer.Serialize(payload),
        }, ct).ConfigureAwait(false);
    }
}
