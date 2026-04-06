// ==========================================================
// Project: WpfHexEditor.Plugins.PatternAnalysis
// File: PatternAnalysisPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for PatternAnalysisPanel â€” exposes entropy, patterns,
//     anomalies, and histogram data for the binary analysis view.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.PatternAnalysis.ViewModels;

public sealed class PatternAnalysisPanelViewModel : ViewModelBase
{
    private string  _statusText  = "No data loaded";
    private string  _entropy     = string.Empty;
    private string  _fileSize    = string.Empty;
    private bool    _isAnalyzing;
    private ObservableCollection<PatternEntry>  _patterns  = new();
    private ObservableCollection<AnomalyEntry> _anomalies = new();

    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }
    public string Entropy    { get => _entropy;    set => SetField(ref _entropy, value); }
    public string FileSize   { get => _fileSize;   set => SetField(ref _fileSize, value); }
    public bool   IsAnalyzing { get => _isAnalyzing; set => SetField(ref _isAnalyzing, value); }

    public ObservableCollection<PatternEntry> Patterns
    {
        get => _patterns;
        set => SetField(ref _patterns, value);
    }

    public ObservableCollection<AnomalyEntry> Anomalies
    {
        get => _anomalies;
        set => SetField(ref _anomalies, value);
    }

    public void Clear()
    {
        Entropy = FileSize = string.Empty;
        StatusText = "No data loaded";
        Patterns.Clear();
        Anomalies.Clear();
    }


}

public sealed record PatternEntry(string Pattern, int Count, long FirstOffset, string Category);
public sealed record AnomalyEntry(string Description, long Offset, string Severity);
