// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: ViewModels/DiagnosticToolsPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     MVVM ViewModel for DiagnosticToolsPanel.
//     Holds ring-buffer data for CPU/memory graphs (120 points each),
//     an observable event list, and scalar .NET runtime counters.
//
// Architecture Notes:
//     All PushXxx / AddXxx methods marshal to the UI thread so graph controls
//     can data-bind without cross-thread exceptions.
//     Ring-buffer is a fixed-capacity queue (dequeue oldest when full).
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WpfHexEditor.Plugins.DiagnosticTools.ViewModels;

/// <summary>
/// Data-source for <see cref="Views.DiagnosticToolsPanel"/>.
/// </summary>
public sealed class DiagnosticToolsPanelViewModel : INotifyPropertyChanged
{
    private const int RingCapacity = 120;

    // -----------------------------------------------------------------------
    // Ring-buffer collections (exposed as read-only for graph controls)
    // -----------------------------------------------------------------------

    /// <summary>CPU % samples, newest last.</summary>
    public ObservableCollection<double> CpuSamples { get; } = new();

    /// <summary>Working-set memory (MB) samples, newest last.</summary>
    public ObservableCollection<double> MemorySamples { get; } = new();

    // -----------------------------------------------------------------------
    // Observable event log
    // -----------------------------------------------------------------------

    public ObservableCollection<DiagnosticEventEntry> Events { get; } = new();

    // -----------------------------------------------------------------------
    // Scalar counters (data-bound labels)
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
        });
    }

    public void AddEvent(string text)
    {
        RunOnUi(() =>
        {
            Events.Insert(0, new DiagnosticEventEntry(DateTime.Now, text));
            if (Events.Count > 500) Events.RemoveAt(Events.Count - 1);
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
            GcHeapMb          = 0;
            ThreadPoolQueue   = 0;
            ThreadPoolThreads = 0;
            CurrentCpu        = "—";
            CurrentMemory     = "—";
            SessionStatus     = "Idle — no process running";
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

/// <summary>Single entry in the diagnostic event log.</summary>
public sealed record DiagnosticEventEntry(DateTime Time, string Text)
{
    public string TimeText => Time.ToString("HH:mm:ss.fff");
}
