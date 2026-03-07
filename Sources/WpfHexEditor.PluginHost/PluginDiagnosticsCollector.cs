//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Single diagnostics snapshot captured at a point in time.
/// </summary>
internal sealed record DiagnosticsSnapshot(
    DateTime Timestamp,
    double CpuPercent,
    long MemoryBytes,
    TimeSpan LastExecutionTime);

/// <summary>
/// Collects rolling performance diagnostics for a plugin.
/// Implements <see cref="IPluginDiagnostics"/> for exposure via SDK.
/// </summary>
internal sealed class PluginDiagnosticsCollector : IPluginDiagnostics
{
    private readonly int _capacity;
    private readonly Queue<DiagnosticsSnapshot> _buffer;
    private readonly object _lock = new();
    private DateTime _loadedAt = DateTime.UtcNow;

    public PluginDiagnosticsCollector(int capacity = 100)
    {
        _capacity = capacity;
        _buffer = new Queue<DiagnosticsSnapshot>(capacity);
    }

    public void SetLoadTime(DateTime loadedAt) => _loadedAt = loadedAt;

    /// <summary>Records a new diagnostics snapshot.</summary>
    public void Record(double cpuPercent, long memoryBytes, TimeSpan executionTime)
    {
        lock (_lock)
        {
            if (_buffer.Count >= _capacity)
                _buffer.Dequeue();

            _buffer.Enqueue(new DiagnosticsSnapshot(
                DateTime.UtcNow, cpuPercent, memoryBytes, executionTime));
        }
    }

    /// <summary>
    /// Gets the most recent snapshot, or null if no data has been recorded.
    /// </summary>
    public DiagnosticsSnapshot? GetLatest()
    {
        lock (_lock)
        {
            return _buffer.Count > 0 ? _buffer.Last() : null;
        }
    }

    /// <summary>Returns all snapshots in the rolling buffer (oldest first).</summary>
    public IReadOnlyList<DiagnosticsSnapshot> GetHistory()
    {
        lock (_lock) { return [.. _buffer]; }
    }

    // -- IPluginDiagnostics ----------------------------------------------------

    public long MemoryBytes => GetLatest()?.MemoryBytes ?? 0;
    public double CpuUsagePercent => GetLatest()?.CpuPercent ?? 0.0;
    public bool IsResponsive { get; internal set; } = true;
    public TimeSpan Uptime => DateTime.UtcNow - _loadedAt;

    public TimeSpan AverageExecutionTime
    {
        get
        {
            lock (_lock)
            {
                if (_buffer.Count == 0) return TimeSpan.Zero;
                double avgMs = _buffer.Average(s => s.LastExecutionTime.TotalMilliseconds);
                return TimeSpan.FromMilliseconds(avgMs);
            }
        }
    }

    /// <summary>Returns the peak CPU% observed in the rolling window.</summary>
    public double PeakCpu()
    {
        lock (_lock)
        {
            return _buffer.Count > 0 ? _buffer.Max(s => s.CpuPercent) : 0.0;
        }
    }

    /// <summary>Returns the rolling average CPU% across all recorded snapshots.</summary>
    public double AverageCpu()
    {
        lock (_lock)
        {
            return _buffer.Count > 0 ? _buffer.Average(s => s.CpuPercent) : 0.0;
        }
    }
}
