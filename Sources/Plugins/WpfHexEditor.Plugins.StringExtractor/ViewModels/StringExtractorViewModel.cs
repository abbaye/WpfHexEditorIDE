// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: ViewModels/StringExtractorViewModel.cs
// Description: MVVM ViewModel for the String Extractor panel.
//              Drives extraction, filtering, status, and navigation.
// Architecture Notes:
//     Standalone-safe: _navigateTo is null when no IDE context is available.
//     All commands guard against null state; no crashes in standalone mode.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Plugins.StringExtractor.Models;
using WpfHexEditor.Plugins.StringExtractor.Services;

namespace WpfHexEditor.Plugins.StringExtractor.ViewModels;

internal sealed class StringExtractorViewModel : INotifyPropertyChanged
{
    private readonly StringExtractorService         _service    = new();
    private readonly List<ExtractedString>          _allResults = [];
    private Action<long>?                           _navigateTo;
    private CancellationTokenSource?                _extractCts;

    // ── Bindable state ────────────────────────────────────────────────────────

    private ObservableCollection<ExtractedString> _results = [];
    public  ObservableCollection<ExtractedString>  Results
    {
        get => _results;
        private set { _results = value; OnPropertyChanged(); }
    }

    private string _filterText = string.Empty;
    public  string  FilterText
    {
        get => _filterText;
        set { _filterText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    private string _statusText = string.Empty;
    public  string  StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private double _progress;
    public  double  Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    private bool _isExtracting;
    public  bool  IsExtracting
    {
        get => _isExtracting;
        set { _isExtracting = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !_isExtracting;

    private bool _hasResults;
    public  bool  HasResults
    {
        get => _hasResults;
        private set { _hasResults = value; OnPropertyChanged(); }
    }

    private bool _hasFile;
    public  bool  HasFile
    {
        get => _hasFile;
        set { _hasFile = value; OnPropertyChanged(); }
    }

    // ── Options ───────────────────────────────────────────────────────────────

    public StringExtractionOptions Options { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────

    public void SetNavigateCallback(Action<long>? navigateTo)
        => _navigateTo = navigateTo;

    public void NavigateTo(ExtractedString? item)
    {
        if (item is not null) _navigateTo?.Invoke(item.Offset);
    }

    public async Task ExtractAsync(byte[] data)
    {
        _extractCts?.Cancel();
        _extractCts?.Dispose();
        _extractCts = new CancellationTokenSource();
        var ct = _extractCts.Token;

        _allResults.Clear();
        Results      = [];
        IsExtracting = true;
        Progress     = 0;
        StatusText   = Properties.StringExtractorResources.StringExtractor_Extracting;

        try
        {
            var progress = new Progress<double>(p => Progress = p * 100);
            await foreach (var s in _service.ExtractAsync(data, Options, progress, ct))
                _allResults.Add(s);

            ApplyFilter();
            StatusText = string.Format(
                Properties.StringExtractorResources.StringExtractor_ResultCount,
                _allResults.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = string.Empty;
        }
        finally
        {
            IsExtracting = false;
            Progress     = 0;
        }
    }

    public void Clear()
    {
        _extractCts?.Cancel();
        _allResults.Clear();
        Results      = [];
        FilterText   = string.Empty;
        HasFile      = false;
        HasResults   = false;
        StatusText   = string.Empty;
        IsExtracting = false;
    }

    private void ApplyFilter()
    {
        var filter   = _filterText.Trim();
        var filtered = string.IsNullOrEmpty(filter)
            ? _allResults
            : _allResults.Where(s => s.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                  || s.OffsetHex.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                  || s.Encoding.Contains(filter, StringComparison.OrdinalIgnoreCase))
                         .ToList();

        Results    = new ObservableCollection<ExtractedString>(filtered);
        HasResults = Results.Count > 0;

        StatusText = string.IsNullOrEmpty(filter)
            ? string.Format(Properties.StringExtractorResources.StringExtractor_ResultCount, _allResults.Count)
            : string.Format(Properties.StringExtractorResources.StringExtractor_FilteredCount, filtered.Count, _allResults.Count);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public void ExportToCsv(string path)
    {
        using var w = new System.IO.StreamWriter(path, append: false, System.Text.Encoding.UTF8);
        w.WriteLine("Offset,Length,Encoding,Value");
        foreach (var s in Results)
            w.WriteLine($"{s.OffsetHex},{s.Length},{s.Encoding},\"{s.Value.Replace("\"", "\"\"")}\"");
    }

    public void ExportToTxt(string path)
    {
        using var w = new System.IO.StreamWriter(path, append: false, System.Text.Encoding.UTF8);
        foreach (var s in Results)
            w.WriteLine($"{s.OffsetHex}  [{s.Encoding}]  {s.Value}");
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
