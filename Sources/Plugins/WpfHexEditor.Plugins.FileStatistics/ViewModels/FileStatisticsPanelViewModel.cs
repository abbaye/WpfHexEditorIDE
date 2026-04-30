// ==========================================================
// Project: WpfHexEditor.Plugins.FileStatistics
// File: FileStatisticsPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     ViewModel for FileStatisticsPanel â€” exposes file analysis
//     results: size, entropy, byte composition, health, anomalies.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Plugins.FileStatistics.Properties;

namespace WpfHexEditor.Plugins.FileStatistics.ViewModels;

public sealed class FileStatisticsPanelViewModel : ViewModelBase
{
    private string  _fileName           = string.Empty;
    private string  _filePath           = string.Empty;
    private string  _fileSize           = string.Empty;
    private string  _detectedFormat     = "Unknown";
    private string  _entropy            = string.Empty;
    private string  _compressionRatio   = string.Empty;
    private string  _nullBytePercent    = string.Empty;
    private string  _printablePercent   = string.Empty;
    private string  _controlCharPercent = string.Empty;
    private string  _healthStatus       = "Unknown";
    private string  _statusText         = FileStatisticsResources.FileStats_NoFileLoaded;
    private bool    _isLoading;
    private ObservableCollection<string> _anomalies = new();

    public string FileName           { get => _fileName;           set => SetField(ref _fileName, value); }
    public string FilePath           { get => _filePath;           set => SetField(ref _filePath, value); }
    public string FileSize           { get => _fileSize;           set => SetField(ref _fileSize, value); }
    public string DetectedFormat     { get => _detectedFormat;     set => SetField(ref _detectedFormat, value); }
    public string Entropy            { get => _entropy;            set => SetField(ref _entropy, value); }
    public string CompressionRatio   { get => _compressionRatio;   set => SetField(ref _compressionRatio, value); }
    public string NullBytePercent    { get => _nullBytePercent;    set => SetField(ref _nullBytePercent, value); }
    public string PrintablePercent   { get => _printablePercent;   set => SetField(ref _printablePercent, value); }
    public string ControlCharPercent { get => _controlCharPercent; set => SetField(ref _controlCharPercent, value); }
    public string HealthStatus       { get => _healthStatus;       set => SetField(ref _healthStatus, value); }
    public string StatusText         { get => _statusText;         set => SetField(ref _statusText, value); }
    public bool   IsLoading          { get => _isLoading;          set => SetField(ref _isLoading, value); }

    public ObservableCollection<string> Anomalies
    {
        get => _anomalies;
        set => SetField(ref _anomalies, value);
    }

    public void Clear()
    {
        FileName = FilePath = FileSize = Entropy = CompressionRatio = string.Empty;
        NullBytePercent = PrintablePercent = ControlCharPercent = string.Empty;
        DetectedFormat = "Unknown";
        HealthStatus   = "Unknown";
        StatusText     = FileStatisticsResources.FileStats_NoFileLoaded;
        Anomalies.Clear();
    }


}
