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
using System.Windows.Input;
using WpfHexEditor.Core.Diff.Models;
using WpfHexEditor.SDK.Commands;

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

// ── ViewModel ──────────────────────────────────────────────────────────────

public sealed class DiffViewerViewModel : INotifyPropertyChanged
{
    // ── State ────────────────────────────────────────────────────────────────

    private DiffEngineResult _result;
    private int              _currentDiffIndex = -1;
    private bool             _isSideBySide     = true;
    private string           _filterMode       = "All";

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

    public ObservableCollection<DiffLineRow> LeftRows  { get; } = [];
    public ObservableCollection<DiffLineRow> RightRows { get; } = [];

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

    public ICommand PrevDiffCommand   { get; }
    public ICommand NextDiffCommand   { get; }
    public ICommand ToggleViewCommand { get; }
    public ICommand FilterAllCommand      { get; }
    public ICommand FilterModifiedCommand { get; }
    public ICommand FilterAddedCommand    { get; }
    public ICommand FilterRemovedCommand  { get; }

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
        _diffBlockStartIndices.Clear();
        _currentDiffIndex = -1;
        BuildRows(result);
        BuildDiffBlockIndex();
        ComputeStats(result);
        OnPropertyChanged(string.Empty);   // refresh all
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
    {
        foreach (var region in bin.Regions)
        {
            var kind = region.Kind.ToString();
            var leftContent  = $"0x{region.LeftOffset:X8}";
            var rightContent = $"0x{region.RightOffset:X8}";

            LeftRows.Add(new DiffLineRow
            {
                Content  = leftContent,
                Kind     = kind,
                Segments = SingleSegment(leftContent, kind != "Equal")
            });
            RightRows.Add(new DiffLineRow
            {
                Content  = rightContent,
                Kind     = kind,
                Segments = SingleSegment(rightContent, kind != "Equal")
            });
        }
    }

    private void BuildDiffBlockIndex()
    {
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
            ModifiedCount = bin.Stats.TotalRegions;
            AddedCount    = 0;
            RemovedCount  = 0;
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
