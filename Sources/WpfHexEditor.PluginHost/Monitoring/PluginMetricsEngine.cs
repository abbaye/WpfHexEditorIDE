//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using System.Windows.Threading;

namespace WpfHexEditor.PluginHost.Monitoring;

/// <summary>
/// Central metrics collection engine with dual-mode sampling:
/// - Passive: Periodic timer-based sampling (every 5s)
/// - Active: Event-driven sampling on plugin callbacks
/// </summary>
public sealed class PluginMetricsEngine : IDisposable
{
    private readonly Func<IReadOnlyList<PluginEntry>> _getPlugins;
    private readonly Dispatcher _dispatcher;
    private readonly Action<string> _log;
    private readonly DispatcherTimer _passiveTimer;
    private readonly Channel<MetricsSample> _metricsQueue;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts = new();

    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastCpuTime;
    private double _lastSampledCpuPercent;
    private long _sampleCount;
    private bool _isInitialized;
    private readonly TaskCompletionSource<bool> _initializationTcs = new();

    // Plugin activity tracking for prioritized sampling
    private readonly ConcurrentDictionary<string, DateTime> _pluginLastActivity = new();

    public event EventHandler<MetricsSampledEventArgs>? MetricsSampled;

    /// <summary>Process-level CPU% from last sample.</summary>
    public double LastSampledCpuPercent => _lastSampledCpuPercent;

    /// <summary>Total number of samples collected.</summary>
    public long SampleCount => _sampleCount;

    /// <summary>Whether initial sampling has completed.</summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>Sampling interval for passive mode.</summary>
    public TimeSpan PassiveSamplingInterval
    {
        get => _passiveTimer.Interval;
        set => _passiveTimer.Interval = value;
    }

    public PluginMetricsEngine(
        Func<IReadOnlyList<PluginEntry>> getPlugins,
        Dispatcher dispatcher,
        Action<string>? logger = null)
    {
        _getPlugins = getPlugins ?? throw new ArgumentNullException(nameof(getPlugins));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _log = logger ?? (_ => { });

        var process = Process.GetCurrentProcess();
        _lastCpuTime = process.TotalProcessorTime;
        _lastCpuCheck = DateTime.UtcNow;

        // Unbounded channel for metrics samples
        _metricsQueue = Channel.CreateUnbounded<MetricsSample>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // Background processing task
        _processingTask = Task.Run(ProcessMetricsQueueAsync);

        // Passive sampling timer
        _passiveTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _passiveTimer.Tick += OnPassiveSamplingTick;
    }

    /// <summary>
    /// Performs initial sampling after plugin loading completes.
    /// Should be called after all plugins are loaded.
    /// </summary>
    public async Task InitializeAsync(int delayMs = 150)
    {
        if (_isInitialized) return;

        _log("[MetricsEngine] Waiting for plugin initialization...");
        await Task.Delay(delayMs);

        // Force initial sample
        await EnqueuePassiveSampleAsync();

        _isInitialized = true;
        _initializationTcs.TrySetResult(true);
        _log("[MetricsEngine] Initialization complete");
    }

    /// <summary>
    /// Waits for the metrics engine to complete initialization.
    /// </summary>
    public Task WaitForInitializationAsync()
    {
        return _initializationTcs.Task;
    }

    /// <summary>
    /// Starts passive sampling timer.
    /// </summary>
    public void Start()
    {
        if (!_passiveTimer.IsEnabled)
        {
            _passiveTimer.Start();
            _log("[MetricsEngine] Passive sampling started");
        }
    }

    /// <summary>
    /// Stops passive sampling timer.
    /// </summary>
    public void Stop()
    {
        if (_passiveTimer.IsEnabled)
        {
            _passiveTimer.Stop();
            _log("[MetricsEngine] Passive sampling stopped");
        }
    }

    /// <summary>
    /// Records plugin activity timestamp for prioritized sampling.
    /// Called by TimedHexEditorService when a plugin callback is invoked.
    /// </summary>
    public void RecordPluginActivity(string pluginId)
    {
        _pluginLastActivity[pluginId] = DateTime.UtcNow;
    }

    /// <summary>
    /// Manually triggers an immediate active sample for a specific plugin.
    /// Called by TimedHexEditorService after each timed plugin callback.
    /// </summary>
    public async Task EnqueueActiveSampleAsync(string pluginId, TimeSpan executionTime)
    {
        var sample = new MetricsSample
        {
            Timestamp      = DateTime.UtcNow,
            PluginId       = pluginId,
            ExecutionTime  = executionTime,
            IsActiveSample = true,
        };

        await _metricsQueue.Writer.WriteAsync(sample, _cts.Token);
    }

    /// <summary>
    /// Enqueues an active sample with a precise per-thread CPU% from
    /// <see cref="ThreadCpuSampler"/> (Phase 6 — InProcess plugins).
    /// Provides real per-plugin CPU rather than the process-wide estimate.
    /// </summary>
    public async Task EnqueueThreadSampleAsync(string pluginId, ThreadSample threadSample)
    {
        var sample = new MetricsSample
        {
            Timestamp      = DateTime.UtcNow,
            PluginId       = pluginId,
            ExecutionTime  = threadSample.Elapsed,
            ThreadCpuPct   = threadSample.CpuPercent,
            IsActiveSample = true,
            HasThreadCpu   = true,
        };

        await _metricsQueue.Writer.WriteAsync(sample, _cts.Token);
    }

    /// <summary>
    /// Triggers a passive sample (process-wide metrics).
    /// </summary>
    private async Task EnqueuePassiveSampleAsync()
    {
        var sample = new MetricsSample
        {
            Timestamp = DateTime.UtcNow,
            IsActiveSample = false
        };

        await _metricsQueue.Writer.WriteAsync(sample, _cts.Token);
    }

    private void OnPassiveSamplingTick(object? sender, EventArgs e)
    {
        _ = EnqueuePassiveSampleAsync();
    }

    /// <summary>
    /// Background task that processes metrics samples from the queue.
    /// </summary>
    private async Task ProcessMetricsQueueAsync()
    {
        await foreach (var sample in _metricsQueue.Reader.ReadAllAsync(_cts.Token))
        {
            try
            {
                if (sample.IsActiveSample)
                    ProcessActiveSample(sample);
                else
                    ProcessPassiveSample(sample);

                _sampleCount++;
            }
            catch (Exception ex)
            {
                _log($"[MetricsEngine] Error processing sample: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Processes an active sample (plugin-specific callback execution).
    /// Uses per-thread CPU% when available (Phase 6 — ThreadCpuSampler),
    /// otherwise falls back to last known process-wide CPU%.
    /// </summary>
    private void ProcessActiveSample(MetricsSample sample)
    {
        if (string.IsNullOrEmpty(sample.PluginId)) return;

        var plugins = _getPlugins();
        var plugin = plugins.FirstOrDefault(p => p.Manifest.Id == sample.PluginId);
        if (plugin == null) return;

        var memBytes = GC.GetTotalMemory(forceFullCollection: false);

        // Phase 6: prefer precise per-thread CPU% over the process-wide estimate
        var cpuPct = sample.HasThreadCpu ? sample.ThreadCpuPct : _lastSampledCpuPercent;

        plugin.Diagnostics.Record(cpuPct, memBytes, sample.ExecutionTime);

        _log($"[MetricsEngine] Active sample: {sample.PluginId}, " +
             $"ExecTime={sample.ExecutionTime.TotalMilliseconds:F2}ms, " +
             $"CPU={cpuPct:F2}% ({(sample.HasThreadCpu ? "thread" : "process-est")})");
    }

    /// <summary>
    /// Processes a passive sample (process-wide metrics for all loaded plugins).
    /// </summary>
    private void ProcessPassiveSample(MetricsSample sample)
    {
        var now = sample.Timestamp;
        var wallElapsed = now - _lastCpuCheck;
        if (wallElapsed.TotalMilliseconds < 1) return;

        var process = Process.GetCurrentProcess();
        var cpuNow = process.TotalProcessorTime;
        var cpuDelta = cpuNow - _lastCpuTime;

        double cpuPct = cpuDelta.TotalMilliseconds
            / (wallElapsed.TotalMilliseconds * Environment.ProcessorCount) * 100.0;
        cpuPct = Math.Clamp(cpuPct, 0.0, 100.0);
        _lastSampledCpuPercent = cpuPct;

        _lastCpuTime = cpuNow;
        _lastCpuCheck = now;

        long memBytes = GC.GetTotalMemory(forceFullCollection: false);
        long memMb = memBytes / (1024 * 1024);

        var plugins = _getPlugins();
        var loaded = plugins.Where(p => p.State == SDK.Models.PluginState.Loaded).ToList();

        // Log sample for diagnostics
        _log($"[MetricsEngine] Sample #{_sampleCount}: CPU={cpuPct:F2}%, Mem={memMb}MB, Plugins={loaded.Count}");

        // Record sample for all loaded plugins
        foreach (var entry in loaded)
        {
            // Check if plugin was recently active (within last 2 seconds)
            bool isActive = _pluginLastActivity.TryGetValue(entry.Manifest.Id, out var lastActivity)
                && (now - lastActivity).TotalSeconds < 2;

            // For passive samples, use TimeSpan.Zero as execution time
            // (we're not measuring a specific plugin call)
            entry.Diagnostics.Record(cpuPct, memBytes, TimeSpan.Zero);
        }

        // Raise event for UI updates
        MetricsSampled?.Invoke(this, new MetricsSampledEventArgs
        {
            Timestamp = now,
            CpuPercent = cpuPct,
            MemoryBytes = memBytes,
            LoadedPluginCount = loaded.Count
        });
    }

    /// <summary>
    /// Forces an immediate sample (useful for "Force Sample Now" button).
    /// </summary>
    public async Task ForceSampleNowAsync()
    {
        _log("[MetricsEngine] Forcing immediate sample...");
        await EnqueuePassiveSampleAsync();
    }

    public void Dispose()
    {
        Stop();
        _cts.Cancel();
        _metricsQueue.Writer.Complete();
        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch { /* best effort */ }
        _cts.Dispose();
    }
}

/// <summary>
/// Represents a single metrics sample in the processing queue.
/// </summary>
internal sealed class MetricsSample
{
    public DateTime Timestamp { get; init; }
    public string? PluginId { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public bool IsActiveSample { get; init; }

    // Phase 6: per-thread CPU data (set when HasThreadCpu = true)
    public double ThreadCpuPct { get; init; }
    public bool HasThreadCpu { get; init; }
}

/// <summary>
/// Event args for MetricsSampled event.
/// </summary>
public sealed class MetricsSampledEventArgs : EventArgs
{
    public DateTime Timestamp { get; init; }
    public double CpuPercent { get; init; }
    public long MemoryBytes { get; init; }
    public int LoadedPluginCount { get; init; }
}
