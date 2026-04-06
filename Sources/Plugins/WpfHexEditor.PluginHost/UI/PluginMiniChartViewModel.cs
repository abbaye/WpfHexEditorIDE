// ==========================================================
// Project: WpfHexEditor.PluginHost.UI
// File: PluginMiniChartViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Per-plugin mini-chart ViewModel. Maintains a rolling
//     CPU% and Memory MB history for a single plugin, used to
//     render individual sparkline charts in the Plugin Monitor
//     detail pane and DataGrid inline columns.
//
// Architecture Notes:
//     Observer pattern â€” pushed by PluginMonitoringViewModel
//     on every sampling tick via PushSample().
//     Weighted CPU estimate: process CPU Ã— (plugin AvgExecMs / sum all AvgExecMs).
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.PluginHost.UI;

/// <summary>
/// Maintains a per-plugin rolling history of CPU% and Memory MB samples
/// for rendering individual sparkline charts in the Plugin Monitor.
/// </summary>
public sealed class PluginMiniChartViewModel : ViewModelBase
{
    private const int MaxPoints = 60;

    private double _currentCpu;
    private long   _currentMemoryMb;
    private double _peakCpu;
    private long   _peakMemoryMb;


    public PluginMiniChartViewModel(string pluginId, string pluginName)
    {
        PluginId   = pluginId;
        PluginName = pluginName;
    }

    // -- Identity ----------------------------------------------------------------

    public string PluginId   { get; }
    public string PluginName { get; }

    // -- Live values -------------------------------------------------------------

    /// <summary>Most recently recorded (weighted) CPU% estimate for this plugin.</summary>
    public double CurrentCpu
    {
        get => _currentCpu;
        private set { _currentCpu = value; OnPropertyChanged(); }
    }

    /// <summary>Most recently recorded Memory MB for this plugin.</summary>
    public long CurrentMemoryMb
    {
        get => _currentMemoryMb;
        private set { _currentMemoryMb = value; OnPropertyChanged(); }
    }

    /// <summary>Peak CPU% observed in the rolling window.</summary>
    public double PeakCpu
    {
        get => _peakCpu;
        private set { _peakCpu = value; OnPropertyChanged(); }
    }

    /// <summary>Peak Memory MB observed in the rolling window.</summary>
    public long PeakMemoryMb
    {
        get => _peakMemoryMb;
        private set { _peakMemoryMb = value; OnPropertyChanged(); }
    }

    // -- Chart data --------------------------------------------------------------

    /// <summary>Rolling weighted CPU% history (max 60 points, one per sampling tick).</summary>
    public ObservableCollection<ChartPoint> CpuHistory { get; } = new();

    /// <summary>Rolling Memory MB history (max 60 points, one per sampling tick).</summary>
    public ObservableCollection<ChartPoint> MemoryHistory { get; } = new();

    // -- API ---------------------------------------------------------------------

    /// <summary>
    /// Appends a new sample. Maintains the max-60-point rolling window.
    /// Must be called from the Dispatcher thread (same as the parent ViewModel).
    /// </summary>
    public void PushSample(double cpu, long memoryMb)
    {
        var now = DateTime.UtcNow;
        AppendPoint(CpuHistory,    new ChartPoint(now, cpu));
        AppendPoint(MemoryHistory, new ChartPoint(now, memoryMb));
        CurrentCpu      = cpu;
        CurrentMemoryMb = memoryMb;
        if (cpu      > _peakCpu)      PeakCpu      = cpu;
        if (memoryMb > _peakMemoryMb) PeakMemoryMb = memoryMb;
    }

    /// <summary>Resets all chart history and peak values.</summary>
    public void Reset()
    {
        CpuHistory.Clear();
        MemoryHistory.Clear();
        CurrentCpu      = 0;
        CurrentMemoryMb = 0;
        PeakCpu         = 0;
        PeakMemoryMb    = 0;
    }

    private static void AppendPoint(ObservableCollection<ChartPoint> col, ChartPoint pt)
    {
        while (col.Count >= MaxPoints)
            col.RemoveAt(0);
        col.Add(pt);
    }

}
