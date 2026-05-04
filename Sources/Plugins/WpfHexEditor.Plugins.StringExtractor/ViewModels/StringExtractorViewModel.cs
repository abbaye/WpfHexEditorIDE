// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: ViewModels/StringExtractorViewModel.cs
// Description: MVVM ViewModel for the String Extractor panel.
//              Drives extraction, filtering, status, and navigation.
// Architecture Notes:
//     Standalone-safe: _navigateTo is null when no IDE context is available.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using WpfHexEditor.Plugins.StringExtractor.Models;
using WpfHexEditor.Plugins.StringExtractor.Services;

namespace WpfHexEditor.Plugins.StringExtractor.ViewModels;

internal sealed class StringExtractorViewModel : INotifyPropertyChanged
{
    private readonly StringExtractorService           _service    = new();
    private readonly ObservableCollection<ExtractedString> _allResults = [];
    private Action<long>?                             _navigateTo;
    private CancellationTokenSource?                  _extractCts;
    private System.Windows.Threading.DispatcherTimer? _filterDebounce;

    // ICollectionView used by the ListView — avoids re-creating ObservableCollection on each filter.
    private readonly ListCollectionView _resultsView;
    public ICollectionView Results => _resultsView;

    public StringExtractorViewModel()
    {
        _resultsView = (ListCollectionView)CollectionViewSource.GetDefaultView(_allResults);
    }

    private string _filterText = string.Empty;
    public  string  FilterText
    {
        get => _filterText;
        set
        {
            _filterText = value;
            OnPropertyChanged();
            ScheduleFilter();
        }
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

    // HasResults is derived — no backing field needed.
    public bool HasResults => _resultsView.Count > 0;

    private bool _hasFile;
    public  bool  HasFile
    {
        get => _hasFile;
        set { _hasFile = value; OnPropertyChanged(); }
    }

    public StringExtractionOptions Options { get; } = new();

    public void SetNavigateCallback(Action<long>? navigateTo) => _navigateTo = navigateTo;

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
        _resultsView.Filter = null;
        IsExtracting = true;
        Progress     = 0;
        StatusText   = Properties.StringExtractorResources.StringExtractor_Extracting;

        try
        {
            var progress = new Progress<double>(p => Progress = p * 100);
            var results  = await _service.ExtractAsync(data, Options, progress, ct);

            foreach (var s in results)
                _allResults.Add(s);

            ApplyFilter();
            OnPropertyChanged(nameof(HasResults));
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
        _filterText  = string.Empty;
        OnPropertyChanged(nameof(FilterText));
        HasFile      = false;
        StatusText   = string.Empty;
        IsExtracting = false;
        _resultsView.Filter = null;
        OnPropertyChanged(nameof(HasResults));
    }

    // ── Export (file I/O) ─────────────────────────────────────────────────────

    public void ExportToCsv(string path)
    {
        using var w = new System.IO.StreamWriter(path, append: false, System.Text.Encoding.UTF8);
        w.WriteLine("Offset,Length,Encoding,Value");
        foreach (ExtractedString s in _resultsView)
            w.WriteLine($"{s.OffsetHex},{s.Length},{s.Encoding},\"{s.Value.Replace("\"", "\"\"")}\"");
    }

    public void ExportToTxt(string path)
    {
        using var w = new System.IO.StreamWriter(path, append: false, System.Text.Encoding.UTF8);
        foreach (ExtractedString s in _resultsView)
            w.WriteLine($"{s.OffsetHex}  [{s.Encoding}]  {s.Value}");
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void ScheduleFilter()
    {
        _filterDebounce?.Stop();
        _filterDebounce = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _filterDebounce.Tick += (_, _) => { _filterDebounce.Stop(); ApplyFilter(); };
        _filterDebounce.Start();
    }

    private void ApplyFilter()
    {
        var filter = _filterText.Trim();

        _resultsView.Filter = string.IsNullOrEmpty(filter)
            ? null
            : (object obj) =>
            {
                var s = (ExtractedString)obj;
                return s.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || s.OffsetHex.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || s.Encoding.Contains(filter, StringComparison.OrdinalIgnoreCase);
            };

        OnPropertyChanged(nameof(HasResults));

        StatusText = string.IsNullOrEmpty(filter)
            ? string.Format(Properties.StringExtractorResources.StringExtractor_ResultCount, _allResults.Count)
            : string.Format(Properties.StringExtractorResources.StringExtractor_FilteredCount,
                            _resultsView.Count, _allResults.Count);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
