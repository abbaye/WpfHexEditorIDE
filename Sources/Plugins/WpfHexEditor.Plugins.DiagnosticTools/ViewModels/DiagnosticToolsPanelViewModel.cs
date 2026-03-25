// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: ViewModels/DiagnosticToolsPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     MVVM ViewModel for DiagnosticToolsPanel.
//     Holds ring-buffer data for CPU/memory graphs (120 points each),
//     an observable event list with type-filter, scalar .NET counters,
//     pause/resume state, metric history for CSV export, and export command.
//
// Architecture Notes:
//     All PushXxx / AddXxx methods marshal to the UI thread.
//     FilteredEvents uses ICollectionView so filtering is live without
//     rebuilding the collection.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using WpfHexEditor.Plugins.DiagnosticTools.Options;

namespace WpfHexEditor.Plugins.DiagnosticTools.ViewModels;

// -----------------------------------------------------------------------
// Supporting types
// -----------------------------------------------------------------------

/// <summary>Coarse event category for the Events-tab filter.</summary>
public enum EventFilter { All, GC, Exceptions, ThreadPool }

/// <summary>Single CPU+memory data point recorded for CSV export.</summary>
public sealed record MetricSample(DateTime Time, double CpuPct, double MemMb);

/// <summary>Single entry in the diagnostic event log.</summary>
public sealed record DiagnosticEventEntry(DateTime Time, string Text)
{
    public string TimeText => Time.ToString("HH:mm:ss.fff");
}

// -----------------------------------------------------------------------

/// <summary>
/// Data-source for <see cref="Views.DiagnosticToolsPanel"/>.
/// </summary>
public sealed class DiagnosticToolsPanelViewModel : INotifyPropertyChanged
{
    private static int RingCapacity   => DiagnosticToolsOptions.Instance.RingCapacity;
    private static int EventMaxCount  => DiagnosticToolsOptions.Instance.EventMaxCount;
    private static int MetricMaxCount => DiagnosticToolsOptions.Instance.MetricMaxCount;

    // -----------------------------------------------------------------------
    // Ring-buffer collections
    // -----------------------------------------------------------------------

    /// <summary>CPU % samples, newest last.</summary>
    public ObservableCollection<double> CpuSamples    { get; } = new();

    /// <summary>Working-set memory (MB) samples, newest last.</summary>
    public ObservableCollection<double> MemorySamples { get; } = new();

    // -----------------------------------------------------------------------
    // Event log + filter
    // -----------------------------------------------------------------------

    public ObservableCollection<DiagnosticEventEntry> Events { get; } = new();

    /// <summary>Live-filtered view of <see cref="Events"/> for the Events tab.</summary>
    public ICollectionView FilteredEvents { get; }

    private EventFilter _eventFilter = EventFilter.All;
    public EventFilter EventFilter
    {
        get => _eventFilter;
        set
        {
            if (_eventFilter == value) return;
            _eventFilter = value;
            OnPropertyChanged();
            FilteredEvents.Refresh();
        }
    }

    // -----------------------------------------------------------------------
    // Pause / Resume
    // -----------------------------------------------------------------------

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set { _isPaused = value; OnPropertyChanged(); OnPropertyChanged(nameof(PauseResumeGlyph)); }
    }

    /// <summary>Segoe MDL2 glyph: pause when running, play when paused.</summary>
    public string PauseResumeGlyph => _isPaused ? "\uE768" : "\uE769";

    // -----------------------------------------------------------------------
    // Scalar counters
    // -----------------------------------------------------------------------

    private double _gcHeapMb;
    private int    _threadPoolQueue;
    private int    _threadPoolThreads;
    private string _sessionStatus = "Idle — no process running";
    private string _currentCpu    = "—";
    private string _currentMemory = "—";

    public double GcHeapMb
    {
        get => _gcHeapMb;
        set { _gcHeapMb = value; OnPropertyChanged(); OnPropertyChanged(nameof(GcHeapMbText)); }
    }

    public string GcHeapMbText => $"{_gcHeapMb:F1} MB";

    public int ThreadPoolQueue
    {
        get => _threadPoolQueue;
        set { _threadPoolQueue = value; OnPropertyChanged(); }
    }

    public int ThreadPoolThreads
    {
        get => _threadPoolThreads;
        set { _threadPoolThreads = value; OnPropertyChanged(); }
    }

    public string SessionStatus
    {
        get => _sessionStatus;
        set { _sessionStatus = value; OnPropertyChanged(); }
    }

    public string CurrentCpu
    {
        get => _currentCpu;
        set { _currentCpu = value; OnPropertyChanged(); }
    }

    public string CurrentMemory
    {
        get => _currentMemory;
        set { _currentMemory = value; OnPropertyChanged(); }
    }

    // -----------------------------------------------------------------------
    // Metric history (for CSV export)
    // -----------------------------------------------------------------------

    private readonly List<MetricSample> _metricHistory = new(MetricMaxCount);

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public DiagnosticToolsPanelViewModel()
    {
        FilteredEvents = CollectionViewSource.GetDefaultView(Events);
        FilteredEvents.Filter = obj =>
        {
            if (obj is not DiagnosticEventEntry entry) return false;
            return _eventFilter == EventFilter.All
                || (_eventFilter == EventFilter.GC         && entry.Text.StartsWith("[GC]"))
                || (_eventFilter == EventFilter.Exceptions && entry.Text.StartsWith("[exception]"))
                || (_eventFilter == EventFilter.ThreadPool && entry.Text.StartsWith("[TP]"));
        };
    }

    // -----------------------------------------------------------------------
    // Push methods (called from background threads — marshal to UI)
    // -----------------------------------------------------------------------

    public void PushCpuSample(double pct)
    {
        RunOnUi(() =>
        {
            if (CpuSamples.Count >= RingCapacity) CpuSamples.RemoveAt(0);
            CpuSamples.Add(pct);
            CurrentCpu = $"{pct:F1} %";
        });
    }

    public void PushMemorySample(double mb)
    {
        RunOnUi(() =>
        {
            if (MemorySamples.Count >= RingCapacity) MemorySamples.RemoveAt(0);
            MemorySamples.Add(mb);
            CurrentMemory = $"{mb:F1} MB";

            // Record metric history for CSV export (cap to avoid unbounded growth).
            if (_metricHistory.Count >= MetricMaxCount) _metricHistory.RemoveAt(0);
            double cpuLast = CpuSamples.Count > 0 ? CpuSamples[^1] : 0;
            _metricHistory.Add(new MetricSample(DateTime.Now, cpuLast, mb));
        });
    }

    public void AddEvent(string text)
    {
        RunOnUi(() =>
        {
            Events.Insert(0, new DiagnosticEventEntry(DateTime.Now, text));
            if (Events.Count > EventMaxCount) Events.RemoveAt(Events.Count - 1);
        });
    }

    public void AddGcEvent(string counter, double value)
    {
        string gen = counter switch
        {
            "gen-0-gc-count" => "Gen0",
            "gen-1-gc-count" => "Gen1",
            "gen-2-gc-count" => "Gen2",
            _                => counter
        };
        AddEvent($"[GC] {gen} collected — {value:F0} /s");
    }

    public void Reset()
    {
        RunOnUi(() =>
        {
            CpuSamples.Clear();
            MemorySamples.Clear();
            Events.Clear();
            _metricHistory.Clear();
            GcHeapMb          = 0;
            ThreadPoolQueue   = 0;
            ThreadPoolThreads = 0;
            CurrentCpu        = "—";
            CurrentMemory     = "—";
            SessionStatus     = "Idle — no process running";
            IsPaused          = false;
        });
    }

    // -----------------------------------------------------------------------
    // CSV export
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes a CSV snapshot of all recorded metric samples (and merged events)
    /// to <paramref name="path"/>. Returns the number of rows written.
    /// </summary>
    public async Task<int> ExportCsvAsync(string path)
    {
        // Capture snapshot on UI thread, then write on background thread.
        List<MetricSample>        samples;
        List<DiagnosticEventEntry> events;

        if (Application.Current?.Dispatcher.CheckAccess() == false)
            await Application.Current.Dispatcher.InvokeAsync(() => { /* just switch */ });

        samples = [.. _metricHistory];
        events  = [.. Events];

        return await Task.Run(() =>
        {
            // Merge events into metric rows by closest timestamp.
            var eventByTime = events
                .GroupBy(e => e.Time.Ticks / TimeSpan.TicksPerSecond)
                .ToDictionary(g => g.Key, g => g.First().Text);

            using var writer = new System.IO.StreamWriter(path, append: false,
                encoding: System.Text.Encoding.UTF8);

            writer.WriteLine("Timestamp,CPU%,MemMB,Event");
            foreach (var s in samples)
            {
                long bucket = s.Time.Ticks / TimeSpan.TicksPerSecond;
                string evText = eventByTime.TryGetValue(bucket, out var ev)
                    ? ev.Replace(",", ";")
                    : string.Empty;
                writer.WriteLine(
                    $"{s.Time:O},{s.CpuPct:F2},{s.MemMb:F2},{evText}");
            }

            return samples.Count;
        });
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // -----------------------------------------------------------------------

    private static void RunOnUi(Action action)
    {
        var app = Application.Current;
        if (app is null) return;

        if (app.Dispatcher.CheckAccess())
            action();
        else
            app.Dispatcher.BeginInvoke(action);
    }
}
