//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.ViewModels;

/// <summary>Item in the opened-files dropdown.</summary>
public sealed record OpenedFileItem(string FilePath)
{
    public string DisplayName => Path.GetFileName(FilePath);
    public override string ToString() => DisplayName;
}

public sealed class StringExtractionViewModel : ViewModelBase, IDisposable
{
    private IIDEHostContext? _context;
    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private int _minLength = 4;
    private string _filter = string.Empty;
    private int _totalCount;
    private int _shownCount;
    private string _statusText = string.Empty;
    private OpenedFileItem? _selectedFile;

    // ── Collections ──────────────────────────────────────────────────────────

    private readonly ObservableCollection<StringRun> _allResults = [];
    public  ICollectionView ResultsView { get; }

    public ObservableCollection<OpenedFileItem> OpenedFiles { get; } = [];

    /// <summary>Encodings selected for the next scan AND shown in the results view.</summary>
    public HashSet<StringEncoding> ActiveEncodings { get; } = [StringEncoding.Ascii, StringEncoding.Utf16Le];

    // ── Properties ───────────────────────────────────────────────────────────

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public int MinLength
    {
        get => _minLength;
        set { _minLength = Math.Clamp(value, 1, 64); OnPropertyChanged(); }
    }

    public string Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            OnPropertyChanged();
            ResultsView.Refresh();
            UpdateShownCount();
        }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set { _totalCount = value; OnPropertyChanged(); UpdateStatusText(); }
    }

    public int ShownCount
    {
        get => _shownCount;
        private set { _shownCount = value; OnPropertyChanged(); UpdateStatusText(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public OpenedFileItem? SelectedFile
    {
        get => _selectedFile;
        set { _selectedFile = value; OnPropertyChanged(); }
    }

    public void ToggleEncoding(StringEncoding enc)
    {
        if (!ActiveEncodings.Remove(enc))
            ActiveEncodings.Add(enc);
        if (ActiveEncodings.Count == 0)
            ActiveEncodings.Add(StringEncoding.Ascii);
        ResultsView.Refresh();
        UpdateShownCount();
    }

    public bool IsEncodingActive(StringEncoding enc) => ActiveEncodings.Contains(enc);

    // ── Constructor ───────────────────────────────────────────────────────────

    public StringExtractionViewModel()
    {
        ResultsView = CollectionViewSource.GetDefaultView(_allResults);
        ResultsView.Filter = FilterPredicate;
    }

    // ── Context ───────────────────────────────────────────────────────────────

    public void SetContext(IIDEHostContext context)
    {
        if (_context is not null) DetachDocumentEvents(_context);
        _context = context;
        AttachDocumentEvents(context);
        RebuildOpenedFilesList(context);
    }

    private void AttachDocumentEvents(IIDEHostContext ctx)
    {
        ctx.DocumentHost.Documents.DocumentRegistered   += OnDocumentRegistered;
        ctx.DocumentHost.Documents.DocumentUnregistered += OnDocumentUnregistered;
        ctx.DocumentHost.Documents.ActiveDocumentChanged += OnActiveDocumentChanged;
    }

    private void DetachDocumentEvents(IIDEHostContext ctx)
    {
        ctx.DocumentHost.Documents.DocumentRegistered   -= OnDocumentRegistered;
        ctx.DocumentHost.Documents.DocumentUnregistered -= OnDocumentUnregistered;
        ctx.DocumentHost.Documents.ActiveDocumentChanged -= OnActiveDocumentChanged;
    }

    // ── Document list maintenance ─────────────────────────────────────────────

    private void RebuildOpenedFilesList(IIDEHostContext ctx)
    {
        OpenedFiles.Clear();
        foreach (var doc in ctx.DocumentHost.Documents.OpenDocuments)
            if (!string.IsNullOrEmpty(doc.FilePath))
                OpenedFiles.Add(new OpenedFileItem(doc.FilePath));

        // Also pick up layout paths not yet materialised
        foreach (var path in ctx.DocumentHost.GetAllLayoutFilePaths())
            if (!string.IsNullOrEmpty(path) && OpenedFiles.All(f => !string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                OpenedFiles.Add(new OpenedFileItem(path));

        // Default selection = active document
        var active = ctx.DocumentHost.Documents.ActiveDocument?.FilePath;
        if (active is not null)
            SelectedFile = OpenedFiles.FirstOrDefault(f => string.Equals(f.FilePath, active, StringComparison.OrdinalIgnoreCase));
        SelectedFile ??= OpenedFiles.FirstOrDefault();
    }

    private void OnDocumentRegistered(object? sender, DocumentModel doc)
    {
        if (string.IsNullOrEmpty(doc.FilePath)) return;
        var item = new OpenedFileItem(doc.FilePath);
        if (OpenedFiles.All(f => !string.Equals(f.FilePath, doc.FilePath, StringComparison.OrdinalIgnoreCase)))
            System.Windows.Application.Current?.Dispatcher.Invoke(() => OpenedFiles.Add(item));
    }

    private void OnDocumentUnregistered(object? sender, DocumentModel doc)
    {
        var item = OpenedFiles.FirstOrDefault(f => string.Equals(f.FilePath, doc.FilePath, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            OpenedFiles.Remove(item);
            if (ReferenceEquals(SelectedFile, item))
                SelectedFile = OpenedFiles.FirstOrDefault();
        });
    }

    private void OnActiveDocumentChanged(object? sender, DocumentModel? doc)
    {
        if (doc?.FilePath is null) return;
        var item = OpenedFiles.FirstOrDefault(f => string.Equals(f.FilePath, doc.FilePath, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
            System.Windows.Application.Current?.Dispatcher.Invoke(() => SelectedFile = item);

        // Apply deferred highlights when the awaited file finally becomes active.
        if (_pendingHighlightRuns is { Count: > 0 } &&
            string.Equals(doc.FilePath, _pendingHighlightPath, StringComparison.OrdinalIgnoreCase))
        {
            var pending = _pendingHighlightRuns;
            _pendingHighlightRuns = null;
            _pendingHighlightPath = null;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                bool isHex  = doc.EditorId == "hex-editor";
                bool isCode = doc.EditorId == "code-editor";
                if (isHex)        ApplyHexHighlights(pending);
                else if (isCode)  ApplyCodeHighlights(pending, doc.FilePath);
            });
        }
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    public async Task RunAsync()
    {
        if (_context is null || IsBusy) return;

        // Determine target: selected file or active HexEditor
        byte[]? buffer = await LoadBufferAsync();
        if (buffer is null) { StatusText = "No file selected."; return; }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        _allResults.Clear();
        TotalCount = 0;
        ShownCount = 0;

        var sw = Stopwatch.StartNew();
        try
        {
            // Build active encoding set
            var encodings = BuildEncodingSet();

            ITblDecodeTable? tblTable = (encodings.Contains(StringEncoding.Tbl) ||
                                         encodings.Contains(StringEncoding.TblDte) ||
                                         encodings.Contains(StringEncoding.TblMte))
                                        ? _activeTblTable : null;

            var runs = await Task.Run(
                () => StringExtractor.Extract(buffer.AsSpan(), _minLength, encodings, tblTable),
                _cts.Token);

            foreach (var run in runs)
                _allResults.Add(run);

            TotalCount = _allResults.Count;
            ResultsView.Refresh();
            UpdateShownCount();

            var elapsed = sw.Elapsed;
            StatusText = $"{TotalCount} strings found, {ShownCount} shown — {elapsed.TotalMilliseconds:F0} ms";
        }
        catch (OperationCanceledException) { StatusText = "Cancelled."; }
        finally { IsBusy = false; }
    }

    private async Task<byte[]?> LoadBufferAsync()
    {
        var selectedPath = SelectedFile?.FilePath;
        var activePath   = _context?.HexEditor.CurrentFilePath;

        // Use HexEditorStream when the selected file is the one open in the active HexEditor.
        // This avoids File.ReadAllBytesAsync failing on a locked file held by the ByteProvider.
        bool isActiveInHexEditor = _context?.HexEditor.IsActive == true &&
            (selectedPath is null ||
             string.Equals(selectedPath, activePath, StringComparison.OrdinalIgnoreCase));

        if (isActiveInHexEditor)
        {
            using var hs = new HexEditorStream(_context!.HexEditor);
            var buf = new byte[hs.Length];
            hs.Position = 0;
            await hs.ReadExactlyAsync(buf, _cts?.Token ?? CancellationToken.None);
            return buf;
        }

        if (selectedPath is null) return null;

        // File not currently open in HexEditor — read from disk with shared access.
        if (!File.Exists(selectedPath)) return null;
        await using var fs = new FileStream(selectedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, useAsync: true);
        var buffer = new byte[fs.Length];
        await fs.ReadExactlyAsync(buffer, _cts?.Token ?? CancellationToken.None);
        return buffer;
    }

    private HashSet<StringEncoding> BuildEncodingSet() =>
        ActiveEncodings.Count > 0 ? new HashSet<StringEncoding>(ActiveEncodings) : [StringEncoding.Ascii];

    private ITblDecodeTable? _activeTblTable;
    public void SetTblTable(ITblDecodeTable? table)
    {
        _activeTblTable = table;
        if (table is not null)
        {
            ActiveEncodings.Add(StringEncoding.Tbl);
            ActiveEncodings.Add(StringEncoding.TblDte);
            ActiveEncodings.Add(StringEncoding.TblMte);
        }
        else
        {
            ActiveEncodings.Remove(StringEncoding.Tbl);
            ActiveEncodings.Remove(StringEncoding.TblDte);
            ActiveEncodings.Remove(StringEncoding.TblMte);
        }
    }

    /// <summary>Adds a file path to the opened-files combo if not already present, then selects it.</summary>
    public void AddFileToCombo(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        var existing = OpenedFiles.FirstOrDefault(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new OpenedFileItem(filePath);
            System.Windows.Application.Current?.Dispatcher.Invoke(() => OpenedFiles.Add(existing));
        }
        SelectedFile = existing;
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private bool FilterPredicate(object obj)
    {
        if (obj is not StringRun run) return false;
        if (!ActiveEncodings.Contains(run.Encoding)) return false;
        if (!string.IsNullOrEmpty(_filter) &&
            !run.Value.Contains(_filter, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private void UpdateShownCount() =>
        ShownCount = ResultsView is System.Collections.ICollection c ? c.Count : ResultsView.Cast<object>().Count();

    private void UpdateStatusText()
    {
        StatusText = _totalCount == 0
            ? string.Empty
            : $"{_totalCount} strings found, {_shownCount} shown";
    }

    // ── Highlight ─────────────────────────────────────────────────────────────

    private const string HighlightTag = "StringExtraction";

    // Pending highlight state — set when the target file isn't active yet.
    private List<StringRun>? _pendingHighlightRuns;
    private string?          _pendingHighlightPath;
    private bool             _pendingIsCodeEditor;

    /// <summary>
    /// Highlight the given runs.  Source of truth is the <see cref="ResultsView"/> (filtered).
    /// Applies immediately if the target file is active; otherwise stores as pending
    /// and applies automatically when <see cref="OnActiveDocumentChanged"/> fires.
    /// </summary>
    public void HighlightRuns(IEnumerable<StringRun> runs)
    {
        if (_context is null) return;

        var runList   = runs.ToList();
        var targetPath = SelectedFile?.FilePath;

        // ── HexEditor path ────────────────────────────────────────────────────
        bool hexActive = _context.HexEditor.IsActive &&
                         string.Equals(_context.HexEditor.CurrentFilePath, targetPath, StringComparison.OrdinalIgnoreCase);
        if (hexActive || targetPath is null)
        {
            ApplyHexHighlights(runList);
            _pendingHighlightRuns = null;
            return;
        }

        // ── CodeEditor path ───────────────────────────────────────────────────
        bool codeActive = _context.CodeEditor.IsActive &&
                          string.Equals(_context.CodeEditor.CurrentFilePath, targetPath, StringComparison.OrdinalIgnoreCase);
        if (codeActive)
        {
            ApplyCodeHighlights(runList, targetPath);
            _pendingHighlightRuns = null;
            return;
        }

        // ── Deferred ──────────────────────────────────────────────────────────
        _pendingHighlightRuns  = runList;
        _pendingHighlightPath  = targetPath;
        StatusText = $"Highlights en attente — ouvre \"{Path.GetFileName(targetPath)}\" dans un éditeur";
    }

    private void ApplyHexHighlights(List<StringRun> runs)
    {
        _context!.HexEditor.ClearCustomBackgroundBlockByTag(HighlightTag);
        int idx = 0;
        foreach (var run in runs)
        {
            var block = new CustomBackgroundBlock(run.Offset, run.Length, ColorForEncoding(run.Encoding, idx))
            {
                Description  = $"[{run.Encoding}] {run.Value}",
                Tag          = HighlightTag,
                Opacity      = 0.35,
                ShowInTooltip = true,
            };
            _context.HexEditor.AddCustomBackgroundBlock(block);
            idx++;
        }
        StatusText = $"{runs.Count} runs highlighted in HexEditor";
    }

    private void ApplyCodeHighlights(List<StringRun> runs, string filePath)
    {
        _context!.CodeEditor.ClearLineHighlightsByTag(HighlightTag);

        // Build a line-start offset lookup from the file content.
        var lineStarts = BuildLineStarts(filePath);
        if (lineStarts is null) { StatusText = "Highlight CodeEditor: impossible de lire le fichier"; return; }

        int idx = 0;
        foreach (var run in runs)
        {
            int line = OffsetToLine(lineStarts, run.Offset); // 1-based
            _context.CodeEditor.AddLineHighlight(line, ColorForEncoding(run.Encoding, idx),
                $"[{run.Encoding}] {run.Value}", HighlightTag);
            idx++;
        }
        StatusText = $"{runs.Count} lignes highlighted in CodeEditor";
    }

    public void ClearHighlights()
    {
        _pendingHighlightRuns = null;
        _pendingHighlightPath = null;
        _context?.HexEditor.ClearCustomBackgroundBlockByTag(HighlightTag);
        _context?.CodeEditor.ClearLineHighlightsByTag(HighlightTag);
        UpdateStatusText();
    }

    // ── Color palette — alternating shades per encoding ──────────────────────

    private static SolidColorBrush ColorForEncoding(StringEncoding enc, int index)
    {
        var (r, g, b) = enc switch
        {
            StringEncoding.Tbl or StringEncoding.TblDte or StringEncoding.TblMte
                                => (0x4C, 0xAF, 0x50),
            StringEncoding.Ascii => (0x42, 0x8B, 0xCA),
            StringEncoding.Utf8 or StringEncoding.Utf16Le or StringEncoding.Utf16Be
                                => (0x00, 0xBC, 0xD4),
            StringEncoding.Ebcdic or StringEncoding.EbcdicNoSpec
                                => (0xFF, 0x98, 0x00),
            StringEncoding.Latin1 => (0xAB, 0x47, 0xBC),
            _                   => (0x90, 0x90, 0x90),
        };
        // Odd indices get a darker shade (~20% darker) to distinguish adjacent same-encoding runs.
        if ((index & 1) == 1)
        {
            r = Math.Max(0, r - 0x28);
            g = Math.Max(0, g - 0x28);
            b = Math.Max(0, b - 0x28);
        }
        return new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
    }

    // ── Offset → line helpers ─────────────────────────────────────────────────

    private static long[]? BuildLineStarts(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var starts = new List<long> { 0 };
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] == '\n') starts.Add(i + 1);
            return [.. starts];
        }
        catch { return null; }
    }

    private static int OffsetToLine(long[] lineStarts, long offset)
    {
        int lo = 0, hi = lineStarts.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (lineStarts[mid] <= offset) lo = mid; else hi = mid - 1;
        }
        return lo + 1; // 1-based
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public void Cancel() => _cts?.Cancel();

    public void NavigateToOffset(StringRun run)
    {
        if (_context is null) return;
        _context.IDEEvents.Publish(new NavigateToOffsetEvent
        {
            Offset = run.Offset,
            Source = "BinaryAnalysis.StringExtraction",
        });
    }

    public IEnumerable<StringRun> GetAllRuns() => _allResults;

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_context is not null) DetachDocumentEvents(_context);
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
