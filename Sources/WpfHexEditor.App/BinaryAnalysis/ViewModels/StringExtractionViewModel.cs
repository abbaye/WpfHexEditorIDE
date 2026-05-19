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
    private int _minUniqueChars = 2;
    private string _filter = string.Empty;
    private bool _useRegexFilter;
    private long _rangeFrom;
    private long _rangeTo = long.MaxValue;
    private bool _excludeHighEntropy;
    private double _entropyThreshold = 0.90;
    private int _totalCount;
    private int _shownCount;
    private string _statusText = string.Empty;
    private OpenedFileItem? _selectedFile;
    private bool _isOutdated;
    private bool _syncCaretToGrid = true;

    // Cached from last RunAsync — used by ApplyCodeHighlights without re-reading disk
    private byte[]? _lastBuffer;
    private string? _lastBufferPath;
    private long[]? _lastLineStarts;

    // Entropy map: one byte per 256-byte block, value = (Shannon entropy * 32) clamped to 0-255
    private byte[]? _entropyMap;

    // Offset-sorted index for O(log n) nearest-run lookup on caret sync
    private StringRun[] _offsetIndex = [];

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

    public int MinUniqueChars
    {
        get => _minUniqueChars;
        set { _minUniqueChars = Math.Clamp(value, 1, 20); OnPropertyChanged(); ResultsView.Refresh(); UpdateShownCount(); }
    }

    public bool UseRegexFilter
    {
        get => _useRegexFilter;
        set { _useRegexFilter = value; OnPropertyChanged(); ResultsView.Refresh(); UpdateShownCount(); }
    }

    public long RangeFrom
    {
        get => _rangeFrom;
        set { _rangeFrom = Math.Max(0, value); OnPropertyChanged(); ResultsView.Refresh(); UpdateShownCount(); }
    }

    public long RangeTo
    {
        get => _rangeTo;
        set { _rangeTo = Math.Max(0, value); OnPropertyChanged(); ResultsView.Refresh(); UpdateShownCount(); }
    }

    public bool ExcludeHighEntropy
    {
        get => _excludeHighEntropy;
        set { _excludeHighEntropy = value; OnPropertyChanged(); ResultsView.Refresh(); UpdateShownCount(); }
    }

    public double EntropyThreshold
    {
        get => _entropyThreshold;
        set { _entropyThreshold = Math.Clamp(value, 0.1, 1.0); OnPropertyChanged(); ResultsView.Refresh(); UpdateShownCount(); }
    }

    public bool IsOutdated
    {
        get => _isOutdated;
        private set { _isOutdated = value; OnPropertyChanged(); }
    }

    public bool SyncCaretToGrid
    {
        get => _syncCaretToGrid;
        set { _syncCaretToGrid = value; OnPropertyChanged(); }
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    public Dictionary<StringEncoding, int> EncodingCounts { get; } = [];

    private void RefreshEncodingCounts()
    {
        EncodingCounts.Clear();
        foreach (var run in ResultsView.Cast<StringRun>())
        {
            EncodingCounts.TryGetValue(run.Encoding, out int c);
            EncodingCounts[run.Encoding] = c + 1;
        }
        OnPropertyChanged(nameof(EncodingCounts));
    }

    // ── Pinned runs ───────────────────────────────────────────────────────────

    private readonly HashSet<StringRun> _pinnedRuns = [];

    public bool IsPinned(StringRun run) => _pinnedRuns.Contains(run);

    public void TogglePin(StringRun run)
    {
        if (!_pinnedRuns.Remove(run)) _pinnedRuns.Add(run);
        ResultsView.Refresh();
        UpdateShownCount();
    }

    // ── Duplicate map ─────────────────────────────────────────────────────────

    private Dictionary<string, int> _duplicateCounts = [];

    private void RebuildDuplicateCounts()
    {
        _duplicateCounts = _allResults
            .GroupBy(r => r.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    public int GetDuplicateCount(StringRun run) =>
        _duplicateCounts.TryGetValue(run.Value, out int c) ? c : 1;

    // ── Highlight navigation index ────────────────────────────────────────────

    private int _currentHighlightIdx = -1;
    private List<StringRun> _lastHighlightedRuns = [];

    public void NavigateHighlightNext()  => NavigateHighlight(+1);
    public void NavigateHighlightPrev()  => NavigateHighlight(-1);

    private void NavigateHighlight(int delta)
    {
        if (_lastHighlightedRuns.Count == 0) return;
        _currentHighlightIdx = (_currentHighlightIdx + delta + _lastHighlightedRuns.Count) % _lastHighlightedRuns.Count;
        NavigateToOffset(_lastHighlightedRuns[_currentHighlightIdx]);
        SelectedGridItem = _lastHighlightedRuns[_currentHighlightIdx];
    }

    private StringRun? _selectedGridItem;
    public StringRun? SelectedGridItem
    {
        get => _selectedGridItem;
        set { _selectedGridItem = value; OnPropertyChanged(); }
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
        ctx.DocumentHost.Documents.DocumentRegistered    += OnDocumentRegistered;
        ctx.DocumentHost.Documents.DocumentUnregistered  += OnDocumentUnregistered;
        ctx.DocumentHost.Documents.ActiveDocumentChanged += OnActiveDocumentChanged;
        ctx.HexEditor.SelectionChanged += OnHexEditorSelectionChanged;
    }

    private void DetachDocumentEvents(IIDEHostContext ctx)
    {
        ctx.DocumentHost.Documents.DocumentRegistered    -= OnDocumentRegistered;
        ctx.DocumentHost.Documents.DocumentUnregistered  -= OnDocumentUnregistered;
        ctx.DocumentHost.Documents.ActiveDocumentChanged -= OnActiveDocumentChanged;
        ctx.HexEditor.SelectionChanged -= OnHexEditorSelectionChanged;
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

        if (string.Equals(doc.FilePath, _pendingHighlightPath, StringComparison.OrdinalIgnoreCase))
        {
            _pendingHighlightRuns = null;
            _pendingHighlightPath = null;
        }

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

        var item    = OpenedFiles.FirstOrDefault(f => string.Equals(f.FilePath, doc.FilePath, StringComparison.OrdinalIgnoreCase));
        var hasPending = _pendingHighlightRuns is { Count: > 0 } &&
                         string.Equals(doc.FilePath, _pendingHighlightPath, StringComparison.OrdinalIgnoreCase);

        List<StringRun>? pending = null;
        if (hasPending)
        {
            pending               = _pendingHighlightRuns;
            _pendingHighlightRuns = null;
            _pendingHighlightPath = null;
        }

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            if (item is not null) SelectedFile = item;
            if (pending is not null)
            {
                if (doc.EditorId == WellKnownEditorIds.HexEditor)       ApplyHexHighlights(pending);
                else if (doc.EditorId == WellKnownEditorIds.CodeEditor) ApplyCodeHighlights(pending, doc.FilePath);
            }
        });
    }

    private void OnHexEditorSelectionChanged(object? sender, EventArgs e)
    {
        if (_lastBufferPath is not null &&
            string.Equals(_context?.HexEditor.CurrentFilePath, _lastBufferPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!_isOutdated) IsOutdated = true;
        }

        if (!_syncCaretToGrid) return;

        long caret = _context?.HexEditor.SelectionStart ?? -1;
        if (caret < 0) return;

        var match = FindNearestRun(caret);
        if (match is not null)
            SelectedGridItem = match;
    }

    private StringRun? FindNearestRun(long caret)
    {
        // Check highlighted runs first (small list, O(n) is fine)
        var inHighlight = _lastHighlightedRuns.FirstOrDefault(r => r.Offset <= caret && caret < r.Offset + r.Length);
        if (inHighlight is not null) return inHighlight;

        // Binary search the sorted offset index for the last run whose Offset <= caret
        var idx = _offsetIndex;
        if (idx.Length == 0) return null;

        int lo = 0, hi = idx.Length - 1, best = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (idx[mid].Offset <= caret) { best = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return best >= 0 ? idx[best] : null;
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    public async Task RunAsync()
    {
        if (_context is null || IsBusy) return;

        byte[]? buffer = await LoadBufferAsync();
        if (buffer is null) { StatusText = "No file selected."; return; }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        IsOutdated = false;
        _allResults.Clear();
        _pinnedRuns.Clear();
        TotalCount = 0;
        ShownCount = 0;

        var sw = Stopwatch.StartNew();
        try
        {
            var encodings = BuildEncodingSet();
            var targetPath = SelectedFile?.FilePath;

            ITblDecodeTable? tblTable = (encodings.Contains(StringEncoding.Tbl) ||
                                         encodings.Contains(StringEncoding.TblDte) ||
                                         encodings.Contains(StringEncoding.TblMte))
                                        ? _activeTblTable : null;

            // Run extract + line-start cache + entropy map all on background thread
            var (runs, lineStarts, entropyMap) = await Task.Run(() =>
            {
                var r = StringExtractor.Extract(buffer.AsSpan(), _minLength, encodings, tblTable);
                var ls = BuildLineStartsFromBuffer(buffer);
                var em = BuildEntropyMap(buffer);
                return (r, ls, em);
            }, _cts.Token);

            // Cache for reuse in ApplyCodeHighlights / entropy filter
            _lastBuffer      = buffer;
            _lastBufferPath  = targetPath;
            _lastLineStarts  = lineStarts;
            _entropyMap      = entropyMap;

            foreach (var run in runs)
                _allResults.Add(run);

            _offsetIndex = [.. runs.OrderBy(r => r.Offset)];
            RebuildDuplicateCounts();
            TotalCount = _allResults.Count;
            ResultsView.Refresh();
            RefreshEncodingCounts();
            UpdateShownCount();

            StatusText = $"{TotalCount} strings found, {ShownCount} shown — {sw.Elapsed.TotalMilliseconds:F0} ms";
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

    private System.Text.RegularExpressions.Regex? _compiledRegex;

    public string Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            OnPropertyChanged();
            _compiledRegex = null;
            if (_useRegexFilter && !string.IsNullOrEmpty(value))
            {
                try { _compiledRegex = new System.Text.RegularExpressions.Regex(value, System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
                catch { /* invalid regex — filter shows nothing */ }
            }
            ResultsView.Refresh();
            UpdateShownCount();
        }
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not StringRun run) return false;

        // Pinned runs always visible
        if (_pinnedRuns.Contains(run)) return true;

        if (!ActiveEncodings.Contains(run.Encoding)) return false;

        // Offset range
        if (run.Offset < _rangeFrom || run.Offset + run.Length > _rangeTo) return false;

        if (run.UniqueCharCount < _minUniqueChars) return false;

        // Entropy exclusion
        if (_excludeHighEntropy && _entropyMap is not null)
        {
            int block = (int)(run.Offset / 256);
            if (block < _entropyMap.Length && _entropyMap[block] / 255.0 >= _entropyThreshold)
                return false;
        }

        // Text filter (substring or regex)
        if (!string.IsNullOrEmpty(_filter))
        {
            if (_useRegexFilter)
            {
                if (_compiledRegex is null) return false; // invalid regex
                if (!_compiledRegex.IsMatch(run.Value)) return false;
            }
            else
            {
                if (!run.Value.Contains(_filter, StringComparison.OrdinalIgnoreCase)) return false;
            }
        }

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

    private List<StringRun>? _pendingHighlightRuns;
    private string?          _pendingHighlightPath;

    /// <summary>
    /// Highlight the given runs.  Source of truth is the <see cref="ResultsView"/> (filtered).
    /// Applies immediately if the target file is active; otherwise stores as pending
    /// and applies automatically when <see cref="OnActiveDocumentChanged"/> fires.
    /// </summary>
    public void HighlightRuns(IEnumerable<StringRun> runs)
    {
        if (_context is null) return;

        var runList    = runs.ToList();
        _lastHighlightedRuns   = runList;
        _currentHighlightIdx   = -1;
        var targetPath = SelectedFile?.FilePath;

        bool hexActive = _context.HexEditor.IsActive &&
                         string.Equals(_context.HexEditor.CurrentFilePath, targetPath, StringComparison.OrdinalIgnoreCase);
        if (hexActive || targetPath is null)
        {
            ApplyHexHighlights(runList);
            _pendingHighlightRuns = null;
            return;
        }

        bool codeActive = _context.CodeEditor.IsActive &&
                          string.Equals(_context.CodeEditor.CurrentFilePath, targetPath, StringComparison.OrdinalIgnoreCase);
        if (codeActive)
        {
            ApplyCodeHighlights(runList, targetPath);
            _pendingHighlightRuns = null;
            return;
        }

        _pendingHighlightRuns = runList;
        _pendingHighlightPath = targetPath;
        StatusText = $"Highlights pending — open \"{Path.GetFileName(targetPath)}\" in an editor";
    }

    private void ApplyHexHighlights(List<StringRun> runs)
    {
        _context!.HexEditor.ClearCustomBackgroundBlockByTag(HighlightTag);
        foreach (var (run, i) in runs.Select((r, i) => (r, i)))
        {
            var block = new CustomBackgroundBlock(run.Offset, run.Length, ColorForEncoding(run.Encoding, i))
            {
                Description   = $"[{run.Encoding}] {run.Value}",
                Tag           = HighlightTag,
                Opacity       = 0.35,
                ShowInTooltip = true,
            };
            _context.HexEditor.AddCustomBackgroundBlock(block);
        }
        StatusText = $"{runs.Count} runs highlighted in HexEditor";
    }

    private void ApplyCodeHighlights(List<StringRun> runs, string filePath)
    {
        _context!.CodeEditor.ClearLineHighlightsByTag(HighlightTag);

        // Use cached line starts if available for same file; avoids sync disk read on UI thread
        long[]? lineStarts = string.Equals(filePath, _lastBufferPath, StringComparison.OrdinalIgnoreCase)
            ? _lastLineStarts
            : (_lastBuffer is not null ? BuildLineStartsFromBuffer(_lastBuffer) : null);

        if (lineStarts is null) { StatusText = "Cannot highlight: scan the file first"; return; }

        foreach (var (run, i) in runs.Select((r, i) => (r, i)))
        {
            int line = OffsetToLine(lineStarts, run.Offset);
            _context.CodeEditor.AddLineHighlight(line, ColorForEncoding(run.Encoding, i),
                $"[{run.Encoding}] {run.Value}", HighlightTag);
        }
        StatusText = $"{runs.Count} lines highlighted in CodeEditor";
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

    private static long[] BuildLineStartsFromBuffer(byte[] bytes)
    {
        var starts = new List<long>(capacity: bytes.Length / 40) { 0 };
        for (int i = 0; i < bytes.Length; i++)
            if (bytes[i] == '\n') starts.Add(i + 1);
        return [.. starts];
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

    // ── Entropy map ───────────────────────────────────────────────────────────

    private static byte[] BuildEntropyMap(byte[] data)
    {
        const int blockSize = 256;
        int blocks = (data.Length + blockSize - 1) / blockSize;
        var map = new byte[blocks];
        var freq = new int[256];

        for (int b = 0; b < blocks; b++)
        {
            int start = b * blockSize;
            int len   = Math.Min(blockSize, data.Length - start);
            Array.Clear(freq, 0, 256);
            for (int i = 0; i < len; i++) freq[data[start + i]]++;

            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                if (freq[i] == 0) continue;
                double p = (double)freq[i] / len;
                entropy -= p * Math.Log2(p);
            }
            // Normalise to [0,8] then scale to [0,255]
            map[b] = (byte)Math.Clamp(entropy / 8.0 * 255.0, 0, 255);
        }
        return map;
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

    /// <summary>Last scanned buffer — used by the panel to render context bytes without re-reading disk.</summary>
    public byte[]? LastBuffer => _lastBuffer;

    /// <summary>Notifies the IDE to apply the TBL at <paramref name="filePath"/> to the active HexEditor.</summary>
    public void PublishLoadTbl(string filePath)
    {
        if (_context is null || string.IsNullOrEmpty(filePath)) return;
        _context.IDEEvents.Publish(new LoadTblEvent
        {
            FilePath = filePath,
            Source   = "BinaryAnalysis.StringExtraction",
        });
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_context is not null) DetachDocumentEvents(_context);
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
