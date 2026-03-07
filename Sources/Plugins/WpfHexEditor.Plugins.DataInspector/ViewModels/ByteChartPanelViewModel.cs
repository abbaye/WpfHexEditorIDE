// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: ByteChartPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for ByteChartPanel — exposes histogram data and
//     display options for the byte-distribution bar chart.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Plugins.DataInspector.ViewModels;

public sealed class ByteChartPanelViewModel : INotifyPropertyChanged
{
    private ObservableCollection<ByteFrequency> _frequencies = new();
    private string  _chartTitle    = "Byte Distribution";
    private string  _statusText    = "No data loaded";
    private bool    _isLoading;
    private bool    _showGrid      = true;
    private string  _colorScheme   = "Default";

    public ObservableCollection<ByteFrequency> Frequencies
    {
        get => _frequencies;
        set => SetField(ref _frequencies, value);
    }

    public string ChartTitle   { get => _chartTitle;   set => SetField(ref _chartTitle, value); }
    public string StatusText   { get => _statusText;   set => SetField(ref _statusText, value); }
    public bool   IsLoading    { get => _isLoading;    set => SetField(ref _isLoading, value); }
    public bool   ShowGrid     { get => _showGrid;     set => SetField(ref _showGrid, value); }
    public string ColorScheme  { get => _colorScheme;  set => SetField(ref _colorScheme, value); }

    public void Clear()
    {
        Frequencies.Clear();
        StatusText = "No data loaded";
        ChartTitle = "Byte Distribution";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed record ByteFrequency(byte Value, long Count, double Percentage);
