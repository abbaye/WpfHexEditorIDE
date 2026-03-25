// Project      : WpfHexEditorControl
// File         : ViewModels/DiffHubViewModel.cs
// Description  : ViewModel for DiffHubPanel (launcher panel) — tracks both file paths,
//                runs comparisons, fires CompareCompleted so the plugin can open a
//                DiffViewerDocument tab, and records comparison history.
// Architecture : INPC, no WPF dependency.  Uses DiffEngine from WpfHexEditor.Core.Diff.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.FileComparison.ViewModels;

public sealed class DiffHubViewModel : INotifyPropertyChanged
{
    // ── Backing fields ────────────────────────────────────────────────────────
    private string  _file1Path   = string.Empty;
    private string  _file2Path   = string.Empty;
    private string  _statusText  = "Select two files and click Compare";
    private bool    _isComparing;
    private DiffEngineResult? _lastResult;

    // ── Services ──────────────────────────────────────────────────────────────
    private readonly DiffEngine         _engine  = new();
    private CancellationTokenSource?    _cts;

    // ── Public properties ──────────────────────────────────────────────────────

    public string File1Path
    {
        get => _file1Path;
        set { SetField(ref _file1Path, value); NotifyOf(nameof(File1Name)); }
    }

    public string File2Path
    {
        get => _file2Path;
        set { SetField(ref _file2Path, value); NotifyOf(nameof(File2Name)); }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool IsComparing
    {
        get => _isComparing;
        set => SetField(ref _isComparing, value);
    }

    public DiffEngineResult? LastResult
    {
        get => _lastResult;
        private set { SetField(ref _lastResult, value); NotifyOf(nameof(SimilarityPercent)); }
    }

    public int SimilarityPercent => (int)((_lastResult?.Similarity ?? 0) * 100);

    // ── Computed file names (displayed in history) ─────────────────────────────
    public string File1Name => string.IsNullOrEmpty(_file1Path) ? string.Empty : Path.GetFileName(_file1Path);
    public string File2Name => string.IsNullOrEmpty(_file2Path) ? string.Empty : Path.GetFileName(_file2Path);

    // ── History (from ComparisonSettings) ─────────────────────────────────────
    public ObservableCollection<ComparisonHistoryEntry> History { get; } = [];

    // ── CompareCompleted event ─────────────────────────────────────────────────
    /// <summary>
    /// Raised when a comparison finishes successfully.
    /// The plugin subscribes to open a DiffViewerDocument tab.
    /// </summary>
    public event EventHandler<DiffEngineResult>? CompareCompleted;

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand CompareCommand    { get; }
    public ICommand SwapCommand       { get; }
    public ICommand ClearCommand      { get; }

    // ── Constructor ───────────────────────────────────────────────────────────
    public DiffHubViewModel()
    {
        CompareCommand = new RelayCommand(
            _ => _ = CompareAsync(),
            _ => !string.IsNullOrEmpty(_file1Path) && !string.IsNullOrEmpty(_file2Path) && !_isComparing);

        SwapCommand = new RelayCommand(
            _ => { (File1Path, File2Path) = (_file2Path, _file1Path); },
            _ => !string.IsNullOrEmpty(_file1Path) || !string.IsNullOrEmpty(_file2Path));

        ClearCommand = new RelayCommand(_ => Clear());
    }

    // ── Operations ────────────────────────────────────────────────────────────

    public void SuggestFile1(string path)
    {
        if (!string.IsNullOrEmpty(File1Path)) return;
        File1Path = path;
    }

    public void LoadHistory(IEnumerable<ComparisonHistoryEntry> entries)
    {
        History.Clear();
        foreach (var e in entries)
            History.Add(e);
    }

    public async Task CompareAsync(CancellationToken externalCt = default)
    {
        if (string.IsNullOrEmpty(_file1Path) || string.IsNullOrEmpty(_file2Path)) return;

        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _cts.Token;

        IsComparing = true;
        StatusText  = "Comparing…";
        LastResult  = null;

        try
        {
            var result = await _engine.CompareAsync(_file1Path, _file2Path, ct: ct);
            LastResult = result;

            BuildStatusText(result);
            RecordHistory(result);
            CompareCompleted?.Invoke(this, result);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsComparing = false;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void BuildStatusText(DiffEngineResult result)
    {
        if (result.TextResult is { } text)
            StatusText = $"Text diff — {text.Stats.ModifiedLines} modified, " +
                         $"{text.Stats.InsertedLines} added, {text.Stats.DeletedLines} removed  " +
                         $"({SimilarityPercent}% similar)" +
                         (result.FallbackReason is not null ? $"  [{result.FallbackReason}]" : "");
        else if (result.BinaryResult is { } bin)
            StatusText = $"Binary diff — {bin.Stats.TotalRegions} regions  " +
                         $"({SimilarityPercent}% similar)" +
                         (bin.Truncated ? "  [truncated]" : "");
    }

    private void RecordHistory(DiffEngineResult result)
    {
        var mode = result.TextResult is not null ? "Text" : "Binary";
        // Update existing entry or prepend a new one (cap 20)
        var existing = History.FirstOrDefault(h =>
            string.Equals(h.LeftPath,  _file1Path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(h.RightPath, _file2Path, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.LastUsed = DateTime.Now;
            existing.Mode     = mode;
        }
        else
        {
            History.Insert(0, new ComparisonHistoryEntry
            {
                LeftPath  = _file1Path,
                RightPath = _file2Path,
                Mode      = mode,
                LastUsed  = DateTime.Now
            });
            if (History.Count > 20) History.RemoveAt(History.Count - 1);
        }
    }

    private void Clear()
    {
        File1Path  = File2Path = string.Empty;
        StatusText = "Select two files and click Compare";
        LastResult = null;
    }

    private void NotifyOf(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>One entry in the comparison history list.</summary>
public sealed class ComparisonHistoryEntry
{
    public string   LeftPath     { get; set; } = string.Empty;
    public string   RightPath    { get; set; } = string.Empty;
    public string   Mode         { get; set; } = string.Empty;
    public DateTime LastUsed     { get; set; }

    public string LeftFileName  => string.IsNullOrEmpty(LeftPath)  ? string.Empty : Path.GetFileName(LeftPath);
    public string RightFileName => string.IsNullOrEmpty(RightPath) ? string.Empty : Path.GetFileName(RightPath);
}
