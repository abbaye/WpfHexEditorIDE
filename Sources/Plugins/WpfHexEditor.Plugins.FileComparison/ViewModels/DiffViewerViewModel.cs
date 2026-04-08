// ==========================================================
// Project: WpfHexEditor.Plugins.FileComparison
// File: ViewModels/DiffViewerViewModel.cs
// Description:
//     ViewModel for the DiffViewerDocument tab.
//     Owns the DiffEngineResult, builds parallel left/right row lists,
//     provides navigation between diff blocks, and stats.
//
// Architecture Notes:
//     INPC, no WPF dependency.
//     DiffLineRow + DiffWordSegment are display models defined here.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.Core.Diff.Services;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Services;
using WpfHexEditor.Editor.Core.Helpers;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.Core.ViewModels;

// Alias to avoid ambiguity with System.Windows.Input.ICommand
using DiffEngineService = WpfHexEditor.Core.Diff.Services.DiffEngine;

namespace WpfHexEditor.Plugins.FileComparison.ViewModels;

// ── Display models ─────────────────────────────────────────────────────────

/// <summary>One visual row in the left or right diff pane.</summary>
public sealed class DiffLineRow
{
    public int?                          LineNumber { get; init; }
    public string                        Content    { get; init; } = string.Empty;
    /// <summary>Equal | Modified | DeletedLeft | InsertedRight | Empty</summary>
    public string                        Kind       { get; init; } = "Equal";
    public IReadOnlyList<DiffWordSegment> Segments  { get; init; } = [];
}

/// <summary>A word-level segment within a Modified row.</summary>
public sealed class DiffWordSegment
{
    public string Text      { get; init; } = string.Empty;
    public bool   IsChanged { get; init; }
}

/// <summary>
/// F2 â€” One row in the Structure Diff grid.
/// Represents a matched (or unmatched) field from the whfmt block layout.
/// </summary>
public sealed class StructureDiffRow
{
    public string FieldName    { get; init; } = string.Empty;
    public long   LeftOffset   { get; init; }
    public long   RightOffset  { get; init; }
    public long   LeftLength   { get; init; }
    public long   RightLength  { get; init; }
    public string LeftHex      { get; init; } = string.Empty;
    public string RightHex     { get; init; } = string.Empty;
    /// <summary>True when the field exists on left but not right.</summary>
    public bool   IsOnlyInLeft  { get; init; }
    /// <summary>True when the field exists on right but not left.</summary>
    public bool   IsOnlyInRight { get; init; }
    /// <summary>True when both sides have the field but bytes differ.</summary>
    public bool   IsChanged     { get; init; }
    public bool   IsEqual       => !IsOnlyInLeft && !IsOnlyInRight && !IsChanged;

    public string StatusGlyph => IsOnlyInLeft  ? "âˆ’"
                               : IsOnlyInRight ? "+"
                               : IsChanged     ? "â‰ "
                               : "=";

    public string LeftOffsetHex  => IsOnlyInRight ? "â€”" : $"0x{LeftOffset:X}";
    public string RightOffsetHex => IsOnlyInLeft  ? "â€”" : $"0x{RightOffset:X}";
}

// ── ViewModel ──────────────────────────────────────────────────────────────

public sealed class DiffViewerViewModel : ViewModelBase
{
    // ── State ────────────────────────────────────────────────────────────────

    private DiffEngineResult _result;
    private int              _currentDiffIndex   = -1;
    private bool             _isSideBySide        = true;
    private string           _filterMode          = "All";
    private int              _binaryContextLines  = 3;
    private bool             _useBlockAlignment   = false;
    private bool             _ignoreWhitespace    = false;
    private bool             _isRecomparing       = false;
    private bool             _isLoading           = false;
    private double           _zoomLevel           = 1.0;
    private bool             _isStructureDiffMode = false;
    private CancellationTokenSource? _rebuildCts;

    // ── Services ─────────────────────────────────────────────────────────────

    private readonly DiffEngineService     _engine          = new();
    private readonly FormatDetectionService _formatDetector = new();
    // DiffViewerDataSource adapters available for external consumers (e.g. FormatParsingService)
    // to attach format parsing to individual diff sides without duplicating detection logic.
    private WpfHexEditor.Plugins.FileComparison.Services.DiffViewerDataSource? _leftDataSource;
    private WpfHexEditor.Plugins.FileComparison.Services.DiffViewerDataSource? _rightDataSource;

    // ── Format detection ──────────────────────────────────────────────────────

    private FormatDetectionResult? _leftFormat;
    private FormatDetectionResult? _rightFormat;

    public FormatDetectionResult? LeftFormat
    {
        get => _leftFormat;
        private set { SetField(ref _leftFormat, value); OnPropertyChanged(nameof(LeftFormatBlocks)); }
    }

    public FormatDetectionResult? RightFormat
    {
        get => _rightFormat;
        private set { SetField(ref _rightFormat, value); OnPropertyChanged(nameof(RightFormatBlocks)); }
    }

    /// <summary>Format-field overlay blocks for the left file, sourced from the .whfmt interpreter.</summary>
    public IReadOnlyList<CustomBackgroundBlock>? LeftFormatBlocks  => _leftFormat?.Blocks;
    /// <summary>Format-field overlay blocks for the right file, sourced from the .whfmt interpreter.</summary>
    public IReadOnlyList<CustomBackgroundBlock>? RightFormatBlocks => _rightFormat?.Blocks;

    /// <summary>Format name badge text for the left file (e.g. "PE/EXE Â· 97%").</summary>
    public string LeftFormatBadge  => BuildFormatBadge(_leftFormat);
    /// <summary>Format name badge text for the right file.</summary>
    public string RightFormatBadge => BuildFormatBadge(_rightFormat);

    public bool HasLeftFormat  => _leftFormat is { Success: true };
    public bool HasRightFormat => _rightFormat is { Success: true };

    private static string BuildFormatBadge(FormatDetectionResult? r)
    {
        if (r is not { Success: true, Format: { } fmt }) return string.Empty;
        return $"{fmt.FormatName} Â· {r.Confidence:P0}";
    }

    // ── Stats panel ───────────────────────────────────────────────────────────

    public BinaryStatsPanelViewModel StatsPanel { get; } = new();

    // ── F3 â€” Format overlay toggle ────────────────────────────────────────────

    private bool _showFormatOverlay = true;

    /// <summary>
    /// When <see langword="true"/> the format-field color blocks are drawn on the BinaryDiffCanvas
    /// underneath the diff highlights. Controlled by the Format Overlay toolbar toggle.
    /// </summary>
    public bool ShowFormatOverlay
    {
        get => _showFormatOverlay;
        set
        {
            if (!SetField(ref _showFormatOverlay, value)) return;
            // Expose the active blocks (or null to suppress) so BinaryDiffCanvas binds correctly.
            OnPropertyChanged(nameof(ActiveLeftFormatBlocks));
            OnPropertyChanged(nameof(ActiveRightFormatBlocks));
        }
    }

    /// <summary>Left format blocks, or null when the overlay is toggled off.</summary>
    public IReadOnlyList<CustomBackgroundBlock>? ActiveLeftFormatBlocks
        => _showFormatOverlay ? LeftFormatBlocks : null;

    /// <summary>Right format blocks, or null when the overlay is toggled off.</summary>
    public IReadOnlyList<CustomBackgroundBlock>? ActiveRightFormatBlocks
        => _showFormatOverlay ? RightFormatBlocks : null;

    // ── F2 â€” Structure Diff ───────────────────────────────────────────────────

    /// <summary>Rows for the field-level structure diff grid.</summary>
    public ObservableCollection<StructureDiffRow> StructureDiffRows { get; } = [];

    /// <summary>True when structure diff rows are available (same format detected on both sides).</summary>
    public bool HasStructureDiff => StructureDiffRows.Count > 0;

    /// <summary>True when the Structure Diff tab is shown instead of the hex diff view.</summary>
    public bool IsStructureDiffMode
    {
        get => _isStructureDiffMode;
        set => SetField(ref _isStructureDiffMode, value);
    }

    /// <summary>F2 â€” Toggles between hex diff view and structure diff view.</summary>
    public ICommand ToggleStructureDiffCommand { get; private set; }

    private void BuildStructureDiff(byte[] leftBytes, byte[] rightBytes)
    {
        StructureDiffRows.Clear();

        var leftBlocks  = _leftFormat?.Blocks;
        var rightBlocks = _rightFormat?.Blocks;
        if (leftBlocks is null || rightBlocks is null) return;

        // Index right blocks by description for O(1) lookup
        var rightIndex = new Dictionary<string, CustomBackgroundBlock>(StringComparer.Ordinal);
        foreach (var b in rightBlocks)
            if (!string.IsNullOrEmpty(b.Description) && b.ShowInTooltip)
                rightIndex.TryAdd(b.Description, b);

        var rightSeen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var lb in leftBlocks)
        {
            if (string.IsNullOrEmpty(lb.Description) || !lb.ShowInTooltip) continue;

            if (rightIndex.TryGetValue(lb.Description, out var rb))
            {
                rightSeen.Add(lb.Description);
                var leftSlice  = ExtractHex(leftBytes,  lb.StartOffset, lb.Length);
                var rightSlice = ExtractHex(rightBytes, rb.StartOffset, rb.Length);
                var changed    = leftSlice != rightSlice;

                StructureDiffRows.Add(new StructureDiffRow
                {
                    FieldName    = lb.Description,
                    LeftOffset   = lb.StartOffset,
                    RightOffset  = rb.StartOffset,
                    LeftLength   = lb.Length,
                    RightLength  = rb.Length,
                    LeftHex      = leftSlice,
                    RightHex     = rightSlice,
                    IsChanged    = changed
                });
            }
            else
            {
                StructureDiffRows.Add(new StructureDiffRow
                {
                    FieldName    = lb.Description,
                    LeftOffset   = lb.StartOffset,
                    LeftLength   = lb.Length,
                    LeftHex      = ExtractHex(leftBytes, lb.StartOffset, lb.Length),
                    IsOnlyInLeft = true
                });
            }
        }

        // Emit right-only entries
        foreach (var rb in rightBlocks)
        {
            if (string.IsNullOrEmpty(rb.Description) || !rb.ShowInTooltip) continue;
            if (rightSeen.Contains(rb.Description)) continue;

            StructureDiffRows.Add(new StructureDiffRow
            {
                FieldName     = rb.Description,
                RightOffset   = rb.StartOffset,
                RightLength   = rb.Length,
                RightHex      = ExtractHex(rightBytes, rb.StartOffset, rb.Length),
                IsOnlyInRight = true
            });
        }

        OnPropertyChanged(nameof(HasStructureDiff));
    }

    private static string ExtractHex(byte[] data, long offset, long length)
    {
        const int MaxDisplayBytes = 16;
        if (offset < 0 || offset >= data.Length) return "â€”";
        var count = (int)Math.Min(length, Math.Min(MaxDisplayBytes, data.Length - offset));
        var hex = BitConverter.ToString(data, (int)offset, count).Replace("-", " ");
        return length > MaxDisplayBytes ? hex + " â€¦" : hex;
    }

    // ── F4 â€” Format mismatch banner ───────────────────────────────────────────

    /// <summary>
    /// True when both sides have a detected format but they differ from each other.
    /// Triggers the yellow warning banner in the view.
    /// </summary>
    public bool IsFormatMismatch
        => HasLeftFormat && HasRightFormat
        && _leftFormat!.Format?.FormatName != _rightFormat!.Format?.FormatName;

    /// <summary>Human-readable mismatch message for the banner.</summary>
    public string FormatMismatchMessage
        => IsFormatMismatch
            ? $"âš   Files are different formats: {_leftFormat!.Format?.FormatName} (left)  vs  {_rightFormat!.Format?.FormatName} (right)"
            : string.Empty;

    // ── Diff-block index for navigation ──────────────────────────────────────

    private readonly List<int> _diffBlockStartIndices = [];

    // ── Constructor ──────────────────────────────────────────────────────────

    public DiffViewerViewModel(DiffEngineResult result)
    {
        _result = result;
        BuildRows(result);
        BuildDiffBlockIndex();
        ComputeStats(result);

        PrevDiffCommand   = new RelayCommand(_ => Navigate(-1), _ => CanGoPrev);
        NextDiffCommand   = new RelayCommand(_ => Navigate(+1), _ => CanGoNext);
        ToggleViewCommand = new RelayCommand(_ => IsSideBySide = !IsSideBySide);
        FilterAllCommand      = new RelayCommand(_ => FilterMode = "All");
        FilterModifiedCommand = new RelayCommand(_ => FilterMode = "Modified");
        FilterAddedCommand    = new RelayCommand(_ => FilterMode = "Added");
        FilterRemovedCommand  = new RelayCommand(_ => FilterMode = "Removed");
        ExpandContextCommand  = new RelayCommand(_ => BinaryContextLines = int.MaxValue);
        RecompareCommand      = new RelayCommand(_ => _ = RecompareAsync(), _ => !_isRecomparing);
        ToggleStatsCommand            = new RelayCommand(_ => StatsPanel.IsVisible = !StatsPanel.IsVisible);
        ToggleFormatOverlayCommand    = new RelayCommand(_ => ShowFormatOverlay = !ShowFormatOverlay);
        ToggleStructureDiffCommand    = new RelayCommand(_ => IsStructureDiffMode = !IsStructureDiffMode,
                                                         _ => HasStructureDiff);

        UpdateStatsPanel(_result);
        _ = RunEntropyAsync(_result);
        _ = RunFormatDetectionAsync(_result);
    }

    // ── Result metadata ──────────────────────────────────────────────────────

    public string LeftPath     => _result.LeftPath;
    public string RightPath    => _result.RightPath;
    public string LeftFileName => Path.GetFileName(LeftPath);
    public string RightFileName=> Path.GetFileName(RightPath);
    public string TabTitle     => $"{LeftFileName} \u2194 {RightFileName}";

    // ── Stats ────────────────────────────────────────────────────────────────

    public int    ModifiedCount  { get; private set; }
    public int    AddedCount     { get; private set; }
    public int    RemovedCount   { get; private set; }
    public int    TotalDiffCount => ModifiedCount + AddedCount + RemovedCount;
    public double Similarity     => _result.Similarity;
    public string SimilarityText => $"{Similarity:P0} similar";
    public string EffectiveModeText => _result.EffectiveMode.ToString();

    // ── Row lists ────────────────────────────────────────────────────────────

    public ObservableCollection<DiffLineRow>          LeftRows      { get; } = [];
    public ObservableCollection<DiffLineRow>          RightRows     { get; } = [];
    public BulkObservableCollection<BinaryHexDiffRow> BinaryHexRows { get; } = new();

    /// <summary>True while binary hex rows are being built on a background thread.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    /// <summary>Canvas zoom level (0.5â€“4.0, default 1.0). TwoWay-bound to BinaryDiffCanvas.ZoomLevel.</summary>
    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetField(ref _zoomLevel, Math.Clamp(value, 0.5, 4.0));
    }

    // ── Font (defaults match HexEditor.xaml: Consolas 14) ────────────────────

    private System.Windows.Media.FontFamily _hexFontFamily = new("Consolas");
    private double _hexFontSize = 14.0;

    /// <summary>Font family for the binary diff canvas. Bound from HexEditor defaults.</summary>
    public System.Windows.Media.FontFamily HexFontFamily
    {
        get => _hexFontFamily;
        set => SetField(ref _hexFontFamily, value);
    }

    /// <summary>Font size for the binary diff canvas. Bound from HexEditor defaults.</summary>
    public double HexFontSize
    {
        get => _hexFontSize;
        set => SetField(ref _hexFontSize, value);
    }

    // ── Focused side (left/right) for ParsedFields integration ───────────────

    private bool _isLeftSideFocused = true;

    /// <summary>True when the mouse is over the left pane, false for the right pane.</summary>
    public bool IsLeftSideFocused
    {
        get => _isLeftSideFocused;
        set
        {
            if (!SetField(ref _isLeftSideFocused, value)) return;
            OnPropertyChanged(nameof(ActiveSideFilePath));
        }
    }

    /// <summary>File path of the currently focused side (left or right).</summary>
    public string ActiveSideFilePath => IsLeftSideFocused ? LeftPath : RightPath;

    // ── Binary diff mode ─────────────────────────────────────────────────────

    /// <summary><see langword="true"/> when the result is a binary comparison (hex dump view).</summary>
    public bool IsBinaryMode => _result.EffectiveMode == DiffMode.Binary;

    /// <summary>
    /// Number of equal rows to keep on each side of a diff row in the hex dump view.
    /// Set to <see cref="int.MaxValue"/> to show all rows (no folding).
    /// </summary>
    public int BinaryContextLines
    {
        get => _binaryContextLines;
        set
        {
            if (!SetField(ref _binaryContextLines, value)) return;
            if (_result.BinaryResult is { } bin)
                StartRebuildAsync(bin);
        }
    }

    // ── Navigation ───────────────────────────────────────────────────────────

    public int CurrentDiffIndex
    {
        get => _currentDiffIndex;
        private set
        {
            if (_currentDiffIndex == value) return;
            _currentDiffIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public int  DiffBlockCount => _diffBlockStartIndices.Count;
    public bool CanGoPrev      => _currentDiffIndex > 0;
    public bool CanGoNext      => _currentDiffIndex < DiffBlockCount - 1;

    public ICommand PrevDiffCommand      { get; }
    public ICommand NextDiffCommand      { get; }
    public ICommand ToggleViewCommand    { get; }
    public ICommand FilterAllCommand     { get; }
    public ICommand FilterModifiedCommand{ get; }
    public ICommand FilterAddedCommand   { get; }
    public ICommand FilterRemovedCommand { get; }
    /// <summary>Expands all collapsed context rows (sets <see cref="BinaryContextLines"/> to âˆž).</summary>
    public ICommand ExpandContextCommand { get; }
    /// <summary>Re-runs the binary comparison with the current algorithm setting.</summary>
    public ICommand RecompareCommand     { get; }
    /// <summary>Toggles the stats/entropy panel drawer.</summary>
    public ICommand ToggleStatsCommand   { get; }
    /// <summary>F3 â€” Toggles the whfmt format-field color overlay on the binary diff canvas.</summary>
    public ICommand ToggleFormatOverlayCommand { get; }

    // ── Block-alignment toggle ────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/>, binary comparison uses the block-aligned Rabin-Karp+LCS
    /// algorithm; when <see langword="false"/>, uses the fast byte-scan algorithm.
    /// Toggling automatically triggers a re-compare.
    /// </summary>
    public bool UseBlockAlignment
    {
        get => _useBlockAlignment;
        set
        {
            if (!SetField(ref _useBlockAlignment, value)) return;
            if (IsBinaryMode)
                _ = RecompareAsync();
        }
    }

    /// <summary>
    /// When <see langword="true"/>, whitespace differences are ignored during line comparison.
    /// Toggling triggers a recompare.
    /// </summary>
    public bool IgnoreWhitespace
    {
        get => _ignoreWhitespace;
        set
        {
            if (!SetField(ref _ignoreWhitespace, value)) return;
            _ = RecompareAsync();
        }
    }

    /// <summary>Row index of the current diff block (used by the view to scroll).</summary>
    public int CurrentDiffRowIndex => _diffBlockStartIndices.Count > 0 && _currentDiffIndex >= 0
        ? _diffBlockStartIndices[_currentDiffIndex]
        : -1;

    // ── View mode ────────────────────────────────────────────────────────────

    public bool IsSideBySide
    {
        get => _isSideBySide;
        set { if (SetField(ref _isSideBySide, value)) OnPropertyChanged(nameof(IsSideBySide)); }
    }

    // ── Filter ───────────────────────────────────────────────────────────────

    public string FilterMode
    {
        get => _filterMode;
        set => SetField(ref _filterMode, value);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Replaces the displayed result (used when re-comparing the same pair).</summary>
    public void LoadResult(DiffEngineResult result)
    {
        _result = result;
        LeftRows.Clear();
        RightRows.Clear();
        BinaryHexRows.Clear();
        StructureDiffRows.Clear();
        _diffBlockStartIndices.Clear();
        _currentDiffIndex = -1;
        BuildRows(result);
        BuildDiffBlockIndex();
        ComputeStats(result);
        UpdateStatsPanel(result);
        _ = RunEntropyAsync(result);
        _ = RunFormatDetectionAsync(result);
        OnPropertyChanged(string.Empty);   // refresh all
    }

    // ── Recompare ─────────────────────────────────────────────────────────────

    private void UpdateStatsPanel(DiffEngineResult result)
    {
        StatsPanel.LeftFileName  = Path.GetFileName(result.LeftPath);
        StatsPanel.RightFileName = Path.GetFileName(result.RightPath);
        if (result.BinaryResult is { } bin)
        {
            StatsPanel.LeftFileSize  = bin.Stats.LeftFileSize;
            StatsPanel.RightFileSize = bin.Stats.RightFileSize;
            StatsPanel.Stats         = bin.Stats;
        }
    }

    private async Task RunFormatDetectionAsync(DiffEngineResult result)
    {
        if (result.BinaryResult is not { FullLeftBytes: { } leftBytes, FullRightBytes: { } rightBytes })
            return;

        var leftName  = Path.GetFileName(result.LeftPath);
        var rightName = Path.GetFileName(result.RightPath);

        // Cap to first 4 KB â€” all magic-byte signatures are within the file header.
        const int MaxDetectBytes = 4096;
        var leftHeader  = leftBytes.Length  > MaxDetectBytes ? leftBytes[..MaxDetectBytes]  : leftBytes;
        var rightHeader = rightBytes.Length > MaxDetectBytes ? rightBytes[..MaxDetectBytes] : rightBytes;

        var (lFmt, rFmt) = await Task.Run(() =>
        {
            var l = _formatDetector.DetectFormat(leftHeader,  leftName);
            var r = _formatDetector.DetectFormat(rightHeader, rightName);
            return (l, r);
        }).ConfigureAwait(false);

        LeftFormat  = lFmt;
        RightFormat = rFmt;
        StatsPanel.LeftFormat  = lFmt;
        StatsPanel.RightFormat = rFmt;

        // Create IBinaryDataSource adapters for external consumers
        _leftDataSource?.Dispose();
        _rightDataSource?.Dispose();
        _leftDataSource  = new Services.DiffViewerDataSource(result.LeftPath);
        _rightDataSource = new Services.DiffViewerDataSource(result.RightPath);
        OnPropertyChanged(nameof(LeftFormatBadge));
        OnPropertyChanged(nameof(RightFormatBadge));
        OnPropertyChanged(nameof(HasLeftFormat));
        OnPropertyChanged(nameof(HasRightFormat));
        // F3 â€” propagate active overlay blocks (respects ShowFormatOverlay toggle)
        OnPropertyChanged(nameof(ActiveLeftFormatBlocks));
        OnPropertyChanged(nameof(ActiveRightFormatBlocks));
        // F4 â€” trigger mismatch banner
        OnPropertyChanged(nameof(IsFormatMismatch));
        OnPropertyChanged(nameof(FormatMismatchMessage));

        // F2 â€” build structure diff when both sides share the same format
        if (!IsFormatMismatch
            && result.BinaryResult is { FullLeftBytes: { } lb2, FullRightBytes: { } rb2 })
        {
            BuildStructureDiff(lb2, rb2);
            CommandManager.InvalidateRequerySuggested(); // update ToggleStructureDiffCommand CanExecute
        }
    }

    private async Task RunEntropyAsync(DiffEngineResult result)
    {
        if (result.BinaryResult is not { FullLeftBytes: { } leftBytes, FullRightBytes: { } rightBytes })
            return;

        var analysis = await Task.Run(() =>
            WpfHexEditor.Core.Diff.Services.BinaryEntropyAnalyzer.Analyze(leftBytes, rightBytes))
            .ConfigureAwait(false);

        StatsPanel.Analysis = analysis;
    }

    private async Task RecompareAsync()
    {
        if (_isRecomparing) return;
        _isRecomparing = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            var opts = new BinaryDiffOptions
            {
                RetainFullBytes   = true,
                UseBlockAlignment = _useBlockAlignment,
                BlockSize         = 64
            };
            var compareOpts = new DiffCompareOptions
            {
                IgnoreWhitespace = _ignoreWhitespace
            };
            var result = await _engine.CompareAsync(_result.LeftPath, _result.RightPath,
                binaryOptions: opts, compareOptions: compareOpts).ConfigureAwait(false);
            LoadResult(result);
        }
        finally
        {
            _isRecomparing = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // ── Build logic ───────────────────────────────────────────────────────────

    private void BuildRows(DiffEngineResult result)
    {
        if (result.TextResult is { } text)
            BuildTextRows(text);
        else if (result.BinaryResult is { } bin)
            BuildBinaryRows(bin);
    }

    private void BuildTextRows(TextDiffResult text)
    {
        var visited = new HashSet<TextDiffLine>(ReferenceEqualityComparer.Instance);

        foreach (var line in text.Lines)
        {
            if (visited.Contains(line)) continue;
            visited.Add(line);

            switch (line.Kind)
            {
                case TextLineKind.Equal:
                    LeftRows.Add(new DiffLineRow
                    {
                        LineNumber = line.LeftLineNumber,
                        Content    = line.Content,
                        Kind       = "Equal",
                        Segments   = SingleSegment(line.Content, false)
                    });
                    RightRows.Add(new DiffLineRow
                    {
                        LineNumber = line.RightLineNumber,
                        Content    = line.Content,
                        Kind       = "Equal",
                        Segments   = SingleSegment(line.Content, false)
                    });
                    break;

                case TextLineKind.Modified:
                    var counterpart = line.CounterpartLine;
                    if (counterpart is not null) visited.Add(counterpart);

                    var leftContent  = line.Content;
                    var rightContent = counterpart?.Content ?? string.Empty;
                    var rightNum     = counterpart?.RightLineNumber ?? line.RightLineNumber;

                    LeftRows.Add(new DiffLineRow
                    {
                        LineNumber = line.LeftLineNumber,
                        Content    = leftContent,
                        Kind       = "Modified",
                        Segments   = BuildLeftSegments(leftContent, line.WordEdits)
                    });
                    RightRows.Add(new DiffLineRow
                    {
                        LineNumber = rightNum,
                        Content    = rightContent,
                        Kind       = "Modified",
                        Segments   = BuildRightSegments(rightContent, line.WordEdits)
                    });
                    break;

                case TextLineKind.DeletedLeft:
                    LeftRows.Add(new DiffLineRow
                    {
                        LineNumber = line.LeftLineNumber,
                        Content    = line.Content,
                        Kind       = "DeletedLeft",
                        Segments   = SingleSegment(line.Content, true)
                    });
                    RightRows.Add(new DiffLineRow { Kind = "Empty" });
                    break;

                case TextLineKind.InsertedRight:
                    LeftRows.Add(new DiffLineRow { Kind = "Empty" });
                    RightRows.Add(new DiffLineRow
                    {
                        LineNumber = line.RightLineNumber,
                        Content    = line.Content,
                        Kind       = "InsertedRight",
                        Segments   = SingleSegment(line.Content, true)
                    });
                    break;
            }
        }
    }

    private void BuildBinaryRows(BinaryDiffResult bin)
        => StartRebuildAsync(bin);

    /// <summary>Cancels any in-flight rebuild and starts a new one.</summary>
    private void StartRebuildAsync(BinaryDiffResult bin)
    {
        _rebuildCts?.Cancel();
        _rebuildCts?.Dispose();
        _rebuildCts = new CancellationTokenSource();
        _ = RebuildBinaryHexRowsAsync(bin, _rebuildCts.Token);
    }

    private async Task RebuildBinaryHexRowsAsync(BinaryDiffResult bin, CancellationToken ct = default)
    {
        bool first = TryFirstLoad();
        if (first) IsLoading = true;
        try
        {
            var contextLines = _binaryContextLines;
            var (rows, indices) = await Task.Run(() =>
            {
                var r   = BinaryHexRowBuilder.BuildRows(bin, contextLines);
                var idx = BuildDiffBlockIndicesFromRows(r);
                return (r, idx);
            }, ct).ConfigureAwait(false);

            ct.ThrowIfCancellationRequested();

            BinaryHexRows.ReplaceAll(rows);
            _diffBlockStartIndices.Clear();
            foreach (var i in indices) _diffBlockStartIndices.Add(i);
            _currentDiffIndex = -1;
            OnPropertyChanged(nameof(DiffBlockCount));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CurrentDiffRowIndex));
            CommandManager.InvalidateRequerySuggested();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer rebuild — discard silently.
        }
        finally
        {
            if (first && !ct.IsCancellationRequested)
                IsLoading = false;
        }
    }

    private static List<int> BuildDiffBlockIndicesFromRows(IReadOnlyList<BinaryHexDiffRow> rows)
    {
        var indices = new List<int>();
        bool inBlock = false;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.IsCollapsedContext) { inBlock = false; continue; }
            if (row.HasDiff && !inBlock) { indices.Add(i); inBlock = true; }
            else if (!row.HasDiff)       { inBlock = false; }
        }
        return indices;
    }

    private void BuildDiffBlockIndex()
    {
        // Binary mode builds its own index inside RebuildBinaryHexRows.
        if (IsBinaryMode) return;

        bool inBlock = false;
        for (int i = 0; i < LeftRows.Count; i++)
        {
            bool isDiff = LeftRows[i].Kind != "Equal";
            if (isDiff && !inBlock)
            {
                _diffBlockStartIndices.Add(i);
                inBlock = true;
            }
            else if (!isDiff)
            {
                inBlock = false;
            }
        }
    }

    private void ComputeStats(DiffEngineResult result)
    {
        if (result.TextResult is { } text)
        {
            ModifiedCount = text.Stats.ModifiedLines;
            AddedCount    = text.Stats.InsertedLines;
            RemovedCount  = text.Stats.DeletedLines;
        }
        else if (result.BinaryResult is { } bin)
        {
            ModifiedCount = bin.Stats.ModifiedCount;
            AddedCount    = bin.Stats.InsertedCount;
            RemovedCount  = bin.Stats.DeletedCount;
        }
    }

    private void Navigate(int delta)
    {
        var next = _currentDiffIndex + delta;
        if (next < 0 || next >= _diffBlockStartIndices.Count) return;
        CurrentDiffIndex = next;
    }

    // ── Word segment helpers ──────────────────────────────────────────────────

    private static IReadOnlyList<DiffWordSegment> SingleSegment(string text, bool isChanged)
        => [new DiffWordSegment { Text = text, IsChanged = isChanged }];

    private static IReadOnlyList<DiffWordSegment> BuildLeftSegments(
        string content, IReadOnlyList<DiffEdit> edits)
    {
        if (edits.Count == 0)
            return SingleSegment(content, false);

        var segments = new List<DiffWordSegment>();
        int pos = 0;

        foreach (var edit in edits)
        {
            if (edit.Kind == EditKind.Insert) continue;

            var start = Math.Clamp(edit.LeftStart, 0, content.Length);
            var end   = Math.Clamp(edit.LeftEnd,   0, content.Length);

            if (start > pos)
                segments.Add(new DiffWordSegment
                    { Text = content[pos..start], IsChanged = false });

            if (start < end)
                segments.Add(new DiffWordSegment
                    { Text = content[start..end], IsChanged = edit.Kind == EditKind.Delete });

            pos = end;
        }

        if (pos < content.Length)
            segments.Add(new DiffWordSegment { Text = content[pos..], IsChanged = false });

        return segments.Count > 0 ? segments : SingleSegment(content, false);
    }

    private static IReadOnlyList<DiffWordSegment> BuildRightSegments(
        string content, IReadOnlyList<DiffEdit> edits)
    {
        if (edits.Count == 0)
            return SingleSegment(content, false);

        var segments = new List<DiffWordSegment>();
        int pos = 0;

        foreach (var edit in edits)
        {
            if (edit.Kind == EditKind.Delete) continue;

            var start = Math.Clamp(edit.RightStart, 0, content.Length);
            var end   = Math.Clamp(edit.RightEnd,   0, content.Length);

            if (start > pos)
                segments.Add(new DiffWordSegment
                    { Text = content[pos..start], IsChanged = false });

            if (start < end)
                segments.Add(new DiffWordSegment
                    { Text = content[start..end], IsChanged = edit.Kind == EditKind.Insert });

            pos = end;
        }

        if (pos < content.Length)
            segments.Add(new DiffWordSegment { Text = content[pos..], IsChanged = false });

        return segments.Count > 0 ? segments : SingleSegment(content, false);
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────



    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
