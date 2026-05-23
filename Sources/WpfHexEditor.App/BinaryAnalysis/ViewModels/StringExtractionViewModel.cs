//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    private float _minReadability = 0f;
    private bool _printableOnly;
    private int _totalCount;
    private int _shownCount;
    private string _statusText = string.Empty;
    private OpenedFileItem? _selectedFile;
    private bool _isOutdated;
    private bool _syncCaretToGrid = true;

    private FileSystemWatcher? _watcher;
    private System.Windows.Threading.DispatcherTimer? _rescanTimer;
    private System.Windows.Threading.DispatcherTimer? _filterDebounceTimer;
    private bool _autoRescan;
    public bool AutoRescan
    {
        get => _autoRescan;
        set { _autoRescan = value; OnPropertyChanged(); }
    }

    // Cached from last RunAsync — used by ApplyCodeHighlights without re-reading disk
    private byte[]? _lastBuffer;
    private string? _lastBufferPath;
    private long[]? _lastLineStarts;

    // Entropy map: one byte per 256-byte block, value = (Shannon entropy * 32) clamped to 0-255
    private byte[]? _entropyMap;

    // Offset-sorted index for O(log n) nearest-run lookup on caret sync
    private StringRun[] _offsetIndex = [];

    // ── Collections ──────────────────────────────────────────────────────────

    private readonly List<StringRun> _allResults        = [];  // full scan result list
    private          StringRun[]     _allResultsSnapshot = [];  // stable snapshot for background filter — swapped atomically in RunAsync
    private readonly List<StringRun> _displayList        = [];  // filtered view, replaces CollectionView
    private CancellationTokenSource  _filterCts          = new();

    /// <summary>Filtered + sorted list bound directly to the DataGrid ItemsSource.</summary>
    public List<StringRun> DisplayList => _displayList;

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

    private bool _isDisplayCapped;
    public bool IsDisplayCapped
    {
        get => _isDisplayCapped;
        private set { _isDisplayCapped = value; OnPropertyChanged(); }
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
        set { _minUniqueChars = Math.Clamp(value, 1, 20); OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    public bool UseRegexFilter
    {
        get => _useRegexFilter;
        set { _useRegexFilter = value; OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    public long RangeFrom
    {
        get => _rangeFrom;
        set { _rangeFrom = Math.Max(0, value); OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    public long RangeTo
    {
        get => _rangeTo;
        set { _rangeTo = Math.Max(0, value); OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    public bool ExcludeHighEntropy
    {
        get => _excludeHighEntropy;
        set { _excludeHighEntropy = value; OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    public double EntropyThreshold
    {
        get => _entropyThreshold;
        set { _entropyThreshold = Math.Clamp(value, 0.1, 1.0); OnPropertyChanged(); ScheduleFilterRefresh(); }
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

    public float MinReadability
    {
        get => _minReadability;
        set { _minReadability = Math.Clamp(value, 0f, 1f); OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    public bool PrintableOnly
    {
        get => _printableOnly;
        set { _printableOnly = value; OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    private bool _wordWrap;
    public bool WordWrap
    {
        get => _wordWrap;
        set { _wordWrap = value; OnPropertyChanged(); }
    }

    private bool _groupByEncoding;
    public bool GroupByEncoding
    {
        get => _groupByEncoding;
        set { _groupByEncoding = value; OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    public Dictionary<StringEncoding, int> EncodingCounts { get; } = [];

    private void RefreshEncodingCounts()
    {
        EncodingCounts.Clear();
        foreach (var run in _displayList)
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
        ScheduleFilterRefresh();
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
        ScheduleFilterRefresh();
    }

    public bool IsEncodingActive(StringEncoding enc) => ActiveEncodings.Contains(enc);

    // ── Constructor ───────────────────────────────────────────────────────────

    public StringExtractionViewModel() { }

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

    public void RefreshOpenedFiles()
    {
        if (_context is not null) RebuildOpenedFilesList(_context);
    }

    private void RebuildOpenedFilesList(IIDEHostContext ctx)
    {
        OpenedFiles.Clear();

        void AddIfNew(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (OpenedFiles.All(f => !string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                OpenedFiles.Add(new OpenedFileItem(path));
        }

        // Source 1: active HexEditor (always available when a binary file is open)
        AddIfNew(ctx.HexEditor.CurrentFilePath);

        // Source 2: all materialised document tabs
        foreach (var doc in ctx.DocumentHost.Documents.OpenDocuments)
            AddIfNew(doc.FilePath);

        // Source 3: layout paths for lazy (never-activated) tabs
        foreach (var path in ctx.DocumentHost.GetAllLayoutFilePaths())
            AddIfNew(path);

        // Default selection = active document
        var active = ctx.HexEditor.CurrentFilePath
                  ?? ctx.DocumentHost.Documents.ActiveDocument?.FilePath;
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

    public StringRun? FindNearestRun(long caret)
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
        IsDisplayCapped = false;
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

            var (runs, lineStarts, entropyMap, dupCounts, encCounts) = await Task.Run(() =>
            {
                var r = StringExtractor.Extract(buffer.AsSpan(), _minLength, encodings, tblTable);

                var rk = new StringRun[r.Count];
                if (r.Count >= 500)
                    Parallel.For(0, r.Count, i => rk[i] = r[i] with { Kind = StringPatternDetector.Detect(r[i].Value) });
                else
                    for (int i = 0; i < r.Count; i++) rk[i] = r[i] with { Kind = StringPatternDetector.Detect(r[i].Value) };

                var ls = BuildLineStartsFromBuffer(buffer);
                var em = BuildEntropyMap(buffer);

                var dc = new Dictionary<string, int>(StringComparer.Ordinal);
                var ec = new Dictionary<StringEncoding, int>();
                foreach (var run in rk)
                {
                    dc.TryGetValue(run.Value, out int dv); dc[run.Value] = dv + 1;
                    ec.TryGetValue(run.Encoding, out int ev); ec[run.Encoding] = ev + 1;
                }

                return (rk, ls, em, dc, ec);
            }, _cts.Token);

            _lastBuffer      = buffer;
            _lastBufferPath  = targetPath;
            _lastLineStarts  = lineStarts;
            _entropyMap      = entropyMap;

            ArmFileWatcher(targetPath);

            _duplicateCounts = dupCounts;
            EncodingCounts.Clear();
            foreach (var (enc, cnt) in encCounts) EncodingCounts[enc] = cnt;
            OnPropertyChanged(nameof(EncodingCounts));

            // Stop debounce timer before mutating _allResults — prevents timer tick from
            // racing between Clear() and AddRange(), snapshotting an empty/partial list.
            _filterDebounceTimer?.Stop();
            _allResults.Clear();
            _offsetIndex = runs;
            TotalCount = runs.Length;

            _allResults.AddRange(runs);
            _allResultsSnapshot = runs;   // O(1) ref swap — filter calls use this stable snapshot
            await ApplyFilterAsync();

            StatusText = $"{TotalCount} strings found, {ShownCount} shown — {sw.Elapsed.TotalMilliseconds:F0} ms"
                       + (IsDisplayCapped ? " (display capped at 100 000)" : string.Empty);
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
        OnPropertyChanged(nameof(ActiveEncodings));
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
            ScheduleFilterRefresh();
        }
    }

    // Coalesces rapid filter changes into a single background pass after 300 ms.
    private void ScheduleFilterRefresh()
    {
        if (_filterDebounceTimer is null)
        {
            _filterDebounceTimer = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(300) };
            _filterDebounceTimer.Tick += async (_, _) =>
            {
                _filterDebounceTimer!.Stop();
                await ApplyFilterAsync();
            };
        }
        _filterDebounceTimer.Stop();
        _filterDebounceTimer.Start();
    }

    // Runs the filter predicate on a background thread, then updates DisplayList on UI thread.
    private async Task ApplyFilterAsync()
    {
        _filterCts.Cancel();
        _filterCts.Dispose();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;

        // O(1) ref copy — RunAsync swaps _allResultsSnapshot atomically after scan completes.
        var allResults      = _allResultsSnapshot;
        var pinnedRuns      = _pinnedRuns;
        var activeEncodings = ActiveEncodings;
        var activeKinds     = _activeKinds;
        var rangeFrom       = _rangeFrom;
        var rangeTo         = _rangeTo;
        var minUnique       = _minUniqueChars;
        var exclEntropy     = _excludeHighEntropy;
        var entropyMap      = _entropyMap;
        var entropyThresh   = _entropyThreshold;
        var minRead         = _minReadability;
        var printableOnly   = _printableOnly;
        var showClusters    = _showOnlyClusters;
        var clusterMap      = _clusterMap;
        var filter          = _filter;
        var useRegex        = _useRegexFilter;
        var compiledRegex   = _compiledRegex;
        var groupByEnc      = _groupByEncoding;
        const int DisplayCap = 100_000;

        List<StringRun>? filtered = await Task.Run(() =>
        {
            var result = new List<StringRun>(Math.Min(allResults.Length, DisplayCap));
            foreach (var run in allResults)
            {
                if (token.IsCancellationRequested) return null;

                if (!pinnedRuns.Contains(run))
                {
                    if (!activeEncodings.Contains(run.Encoding)) continue;
                    if (run.Offset < rangeFrom || run.Offset + run.Length > rangeTo) continue;
                    if (run.UniqueCharCount < minUnique) continue;
                    if (exclEntropy && entropyMap is not null)
                    {
                        int block = (int)(run.Offset / 256);
                        if (block < entropyMap.Length && entropyMap[block] / 255.0 >= entropyThresh) continue;
                    }
                    if (minRead > 0f && run.ReadabilityScore < minRead) continue;
                    if (printableOnly && !IsPrintable(run.Value)) continue;
                    if (activeKinds.Count > 0 && !activeKinds.Contains(run.Kind)) continue;
                    if (showClusters && (!clusterMap.TryGetValue(run, out int cid) || cid == 0)) continue;
                    if (!string.IsNullOrEmpty(filter))
                    {
                        if (useRegex) { if (compiledRegex?.IsMatch(run.Value) != true) continue; }
                        else          { if (!run.Value.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue; }
                    }
                }

                result.Add(run);
                if (result.Count >= DisplayCap) break;
            }
            if (groupByEnc && !token.IsCancellationRequested)
                result.Sort(static (a, b) => a.Encoding.CompareTo(b.Encoding));
            return result;
        }, token);

        if (token.IsCancellationRequested || filtered is null) return;

        _displayList.Clear();
        _displayList.AddRange(filtered);
        IsDisplayCapped = _allResults.Count > DisplayCap && filtered.Count >= DisplayCap;
        ShownCount = _displayList.Count;
        OnPropertyChanged(nameof(DisplayList));
    }

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
    /// Highlight the given runs.  Source of truth is the <see cref="DisplayList"/> (filtered).
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

    public long LastBufferLength => _lastBuffer?.Length ?? 0;

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

    // ── Snapshots ─────────────────────────────────────────────────────────────

    private const int MaxSnapshots = 10;

    // Nested to keep it close to the snapshot API; consumed by StringDiffPanel only.
    public sealed record ScanSnapshot(string FileName, DateTime TakenAt, IReadOnlyList<StringRun> Runs)
    {
        public string DisplayName =>
            $"{System.IO.Path.GetFileName(FileName)} — {TakenAt:HH:mm:ss} ({Runs.Count:N0} strings)";
    }

    public ObservableCollection<ScanSnapshot> Snapshots { get; } = [];

    public void TakeSnapshot()
    {
        if (_allResults.Count == 0) return;
        var snap = new ScanSnapshot(
            _lastBufferPath ?? "(unknown)",
            DateTime.Now,
            _allResults.ToList());
        Snapshots.Insert(0, snap);
        while (Snapshots.Count > MaxSnapshots) Snapshots.RemoveAt(Snapshots.Count - 1);
    }

    public async void RestoreSnapshot(ScanSnapshot snapshot)
    {
        _allResults.Clear();
        _allResults.AddRange(snapshot.Runs);
        _offsetIndex = [.. snapshot.Runs];
        RebuildDuplicateCounts();
        TotalCount = _allResults.Count;
        await ApplyFilterAsync();
        RefreshEncodingCounts();
        StatusText = $"Snapshot restored — {snapshot.DisplayName}";
    }

    // ── Kind filter ───────────────────────────────────────────────────────────

    private readonly HashSet<StringKind> _activeKinds = [];

    /// <summary>Kinds to show. Empty = show all (no filter).</summary>
    public HashSet<StringKind> ActiveKinds => _activeKinds;

    public void ToggleKind(StringKind kind)
    {
        if (!_activeKinds.Remove(kind)) _activeKinds.Add(kind);
        OnPropertyChanged(nameof(ActiveKinds));
        ScheduleFilterRefresh();
    }

    public bool IsKindActive(StringKind kind) => _activeKinds.Contains(kind);

    // ── Similarity clustering ─────────────────────────────────────────────────

    private Dictionary<StringRun, int> _clusterMap = [];

    private bool _showOnlyClusters;
    public bool ShowOnlyClusters
    {
        get => _showOnlyClusters;
        set { _showOnlyClusters = value; OnPropertyChanged(); ScheduleFilterRefresh(); }
    }

    public async Task ClusterAsync()
    {
        if (IsBusy || _allResults.Count == 0) return;
        IsBusy = true;
        try
        {
            var snapshot = _allResults.ToList();
            var map = await Task.Run(() => StringSimilarityClusterer.Cluster(snapshot));
            _clusterMap = map;
            await ApplyFilterAsync();
            int groups = map.Values.Where(v => v > 0).Distinct().Count();
            StatusText = $"Clustering done — {groups} groups found";
        }
        finally { IsBusy = false; }
    }

    public int GetClusterId(StringRun run) =>
        _clusterMap.TryGetValue(run, out int id) ? id : 0;

    // ── FileSystemWatcher ─────────────────────────────────────────────────────

    private void ArmFileWatcher(string? path)
    {
        _watcher?.Dispose();
        _watcher = null;

        if (string.IsNullOrEmpty(path)) return;

        var dir  = Path.GetDirectoryName(path)!;
        var name = Path.GetFileName(path);
        _watcher = new FileSystemWatcher(dir, name)
        {
            NotifyFilter        = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
    }

    private async void OnRescanTimerTick(object? sender, EventArgs e)
    {
        _rescanTimer!.Stop();
        await RunAsync();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            IsOutdated = true;
            if (!_autoRescan) return;

            if (_rescanTimer is null)
            {
                _rescanTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _rescanTimer.Tick += OnRescanTimerTick;
            }
            _rescanTimer.Stop();
            _rescanTimer.Start();
        });
    }

    // ── Printable helper ──────────────────────────────────────────────────────

    private static bool IsPrintable(string value)
    {
        foreach (char c in value)
            if (c < 0x20 && c != '\t' && c != '\n' && c != '\r') return false;
        return true;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_context is not null) DetachDocumentEvents(_context);
        _cts?.Cancel();
        _cts?.Dispose();
        _watcher?.Dispose();
        _rescanTimer?.Stop();
        _filterDebounceTimer?.Stop();
    }
}
