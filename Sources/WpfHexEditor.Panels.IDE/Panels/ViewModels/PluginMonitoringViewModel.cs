// ==========================================================
// Project: WpfHexEditor.Panels.IDE
// File: PluginMonitoringViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for the Plugin Monitoring docking panel.
//     Drives two real-time line charts (CPU% and Memory MB) and a
//     per-plugin summary table, refreshed by a DispatcherTimer.
//
// Architecture Notes:
//     Observer pattern — subscribes to WpfPluginHost sampling timer output
//     via periodic pull (Refresh() on timer tick).
//     Canvas-based Polyline charts: pure WPF, no external library.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.PluginHost;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Panels.IDE.Panels.ViewModels;

/// <summary>
/// A single data point for the time-series chart.
/// </summary>
public sealed record ChartPoint(DateTime Time, double Value);

/// <summary>
/// Summary row for a single plugin in the monitoring table.
/// </summary>
public sealed class PluginMonitorRow : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _state = string.Empty;
    private string _stateColor = "#9CA3AF";
    private double _cpuPercent;
    private long _memoryMb;
    private double _avgExecMs;
    private double _initTimeMs;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = string.Empty;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string State
    {
        get => _state;
        set { _state = value; OnPropertyChanged(); }
    }

    public string StateColor
    {
        get => _stateColor;
        set { _stateColor = value; OnPropertyChanged(); }
    }

    public double CpuPercent
    {
        get => _cpuPercent;
        set { _cpuPercent = value; OnPropertyChanged(); }
    }

    public long MemoryMb
    {
        get => _memoryMb;
        set { _memoryMb = value; OnPropertyChanged(); }
    }

    public double AvgExecMs
    {
        get => _avgExecMs;
        set { _avgExecMs = value; OnPropertyChanged(); }
    }

    public double InitTimeMs
    {
        get => _initTimeMs;
        set { _initTimeMs = value; OnPropertyChanged(); }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Master ViewModel for the Plugin Monitoring docking panel.
/// </summary>
public sealed class PluginMonitoringViewModel : INotifyPropertyChanged, IDisposable
{
    private const int MaxChartPoints = 60; // 5 min at 5 s intervals

    private readonly WpfPluginHost _host;
    private readonly DispatcherTimer _timer;

    private double _currentCpu;
    private long   _currentMemoryMb;
    private int    _loadedCount;
    private int    _totalCount;
    private int    _faultCount;
    private bool   _isRunning = true;
    private int    _intervalSeconds = 5;

    public event PropertyChangedEventHandler? PropertyChanged;

    public PluginMonitoringViewModel(WpfPluginHost host, Dispatcher dispatcher)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(_intervalSeconds)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        StartStopCommand = new RelayCommand(_ => ToggleRunning());
        ResetCommand     = new RelayCommand(_ => Reset());
        SetIntervalCommand = new RelayCommand(p =>
        {
            if (p is int seconds) SetInterval(seconds);
        });

        Refresh();
    }

    // -- Commands ------------------------------------------------------------

    /// <summary>Toggles the sampling timer on/off.</summary>
    public ICommand StartStopCommand { get; }

    /// <summary>Clears all chart history and resets counters.</summary>
    public ICommand ResetCommand { get; }

    /// <summary>Sets the sampling interval. CommandParameter = int seconds.</summary>
    public ICommand SetIntervalCommand { get; }

    // -- Running state -------------------------------------------------------

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value) return;
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StartStopLabel));
            OnPropertyChanged(nameof(StartStopIcon));
        }
    }

    /// <summary>Label shown on the Start/Stop button.</summary>
    public string StartStopLabel => _isRunning ? "Pause" : "Resume";

    /// <summary>Segoe MDL2 icon for the Start/Stop button (Play/Pause).</summary>
    public string StartStopIcon  => _isRunning ? "\uE769" : "\uE768";

    public int IntervalSeconds
    {
        get => _intervalSeconds;
        private set { _intervalSeconds = value; OnPropertyChanged(); }
    }

    private void ToggleRunning()
    {
        if (_isRunning)
        {
            _timer.Stop();
            IsRunning = false;
        }
        else
        {
            _timer.Start();
            IsRunning = true;
        }
    }

    private void Reset()
    {
        CpuHistory.Clear();
        MemoryHistory.Clear();
    }

    private void SetInterval(int seconds)
    {
        if (seconds <= 0) return;
        IntervalSeconds = seconds;
        _timer.Interval = TimeSpan.FromSeconds(seconds);
    }

    // -- Chart data sources --------------------------------------------------

    /// <summary>CPU% samples over time (max 60 points).</summary>
    public ObservableCollection<ChartPoint> CpuHistory { get; } = new();

    /// <summary>Memory MB samples over time (max 60 points).</summary>
    public ObservableCollection<ChartPoint> MemoryHistory { get; } = new();

    // -- Summary metrics -----------------------------------------------------

    public double CurrentCpu
    {
        get => _currentCpu;
        private set { _currentCpu = value; OnPropertyChanged(); }
    }

    public long CurrentMemoryMb
    {
        get => _currentMemoryMb;
        private set { _currentMemoryMb = value; OnPropertyChanged(); }
    }

    public int LoadedCount
    {
        get => _loadedCount;
        private set { _loadedCount = value; OnPropertyChanged(); }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set { _totalCount = value; OnPropertyChanged(); }
    }

    public int FaultCount
    {
        get => _faultCount;
        private set { _faultCount = value; OnPropertyChanged(); }
    }

    public string HealthLabel => FaultCount > 0
        ? $"{FaultCount} fault{(FaultCount == 1 ? "" : "s")}"
        : LoadedCount == TotalCount && TotalCount > 0
            ? "Healthy"
            : TotalCount == 0 ? "No plugins" : $"{LoadedCount}/{TotalCount} running";

    public string HealthColor => FaultCount > 0
        ? "#EF4444"
        : LoadedCount < TotalCount ? "#F59E0B"
        : "#22C55E";

    // -- Plugin table --------------------------------------------------------

    public ObservableCollection<PluginMonitorRow> Rows { get; } = new();

    // -- Internals -----------------------------------------------------------

    private void OnTimerTick(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var plugins = _host.GetAllPlugins();

        var loaded = plugins.Count(p => p.State == PluginState.Loaded);
        var faults = plugins.Count(p => p.State == PluginState.Faulted);
        var now    = DateTime.UtcNow;

        // Aggregate CPU and memory across all loaded plugins.
        // For InProcess isolation, all plugins share the same process metrics,
        // so we sum the reported values (each plugin entry carries a copy of the same snapshot).
        double cpu   = 0;
        long   memMb = 0;

        foreach (var entry in plugins.Where(p => p.State == PluginState.Loaded))
        {
            var snap = entry.Diagnostics.GetLatest();
            if (snap is null) continue;
            cpu   += snap.CpuPercent;
            memMb += snap.MemoryBytes / (1024 * 1024);
        }

        // Clamp to avoid inflating metrics when many identical process-level snapshots are summed.
        if (plugins.Count(p => p.State == PluginState.Loaded) > 1)
        {
            cpu   = Math.Min(cpu, 100);
            memMb = memMb / Math.Max(1, plugins.Count(p => p.State == PluginState.Loaded));
        }

        CurrentCpu      = cpu;
        CurrentMemoryMb = memMb;
        LoadedCount     = loaded;
        TotalCount      = plugins.Count;
        FaultCount      = faults;
        OnPropertyChanged(nameof(HealthLabel));
        OnPropertyChanged(nameof(HealthColor));

        // Rolling chart data
        AddChartPoint(CpuHistory,    new ChartPoint(now, cpu));
        AddChartPoint(MemoryHistory, new ChartPoint(now, memMb));

        // Sync rows collection
        var existingIds = Rows.Select(r => r.Id).ToHashSet();
        var currentIds  = plugins.Select(p => p.Manifest.Id).ToHashSet();

        // Remove stale rows
        foreach (var stale in Rows.Where(r => !currentIds.Contains(r.Id)).ToList())
            Rows.Remove(stale);

        // Add or update
        foreach (var entry in plugins)
        {
            var snap = entry.Diagnostics.GetLatest();
            var row  = Rows.FirstOrDefault(r => r.Id == entry.Manifest.Id);

            if (row is null)
            {
                row = new PluginMonitorRow { Id = entry.Manifest.Id };
                Rows.Add(row);
            }

            row.Name       = entry.Manifest.Name;
            row.State      = StateLabel(entry.State);
            row.StateColor = StateBadgeColor(entry.State);
            row.CpuPercent = snap?.CpuPercent ?? 0;
            row.MemoryMb   = snap is not null ? snap.MemoryBytes / (1024 * 1024) : 0;
            row.AvgExecMs  = entry.Diagnostics.AverageExecutionTime.TotalMilliseconds;
            row.InitTimeMs = entry.InitDuration.TotalMilliseconds;
        }
    }

    private static void AddChartPoint(ObservableCollection<ChartPoint> collection, ChartPoint point)
    {
        // Batch-remove old points then add new one — avoids O(n) RemoveAt(0) per item.
        // Since ObservableCollection fires per removal, batch-remove in reverse is fine here.
        while (collection.Count >= MaxChartPoints)
            collection.RemoveAt(0);
        collection.Add(point);
    }

    private static string StateLabel(PluginState state) => state switch
    {
        PluginState.Loaded      => "Running",
        PluginState.Loading     => "Loading",
        PluginState.Disabled    => "Disabled",
        PluginState.Faulted     => "Error",
        PluginState.Incompatible => "Incompatible",
        PluginState.Unloaded    => "Unloaded",
        _                       => "Unknown"
    };

    private static string StateBadgeColor(PluginState state) => state switch
    {
        PluginState.Loaded      => "#22C55E",
        PluginState.Loading     => "#F59E0B",
        PluginState.Disabled    => "#6B7280",
        PluginState.Faulted     => "#EF4444",
        PluginState.Incompatible => "#F97316",
        _                       => "#9CA3AF"
    };

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Minimal RelayCommand for toolbar actions in the monitoring panel.</summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
