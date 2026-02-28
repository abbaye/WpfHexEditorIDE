using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.TblEditor.Services;

namespace WpfHexEditor.Editor.TblEditor.ViewModels;

/// <summary>
/// Internal ViewModel for TblEditorControl.
/// Manages the entry collection, filtering, undo/redo and async validation.
/// </summary>
internal sealed class TblEditorViewModel : INotifyPropertyChanged, IDisposable
{
    // ── Undo / Redo ────────────────────────────────────────────────────────
    private const int MaxUndoDepth = 100;

    private readonly Stack<ITblCommand> _undoStack = new();
    private readonly Stack<ITblCommand> _redoStack = new();

    // ── Data ───────────────────────────────────────────────────────────────
    private readonly ObservableCollection<TblEntryViewModel> _entries = [];
    private ICollectionView? _filteredEntries;
    private TblStream? _source;

    // ── Filter / Search ────────────────────────────────────────────────────
    private string? _searchText;
    private DteType? _typeFilter;
    private bool _showConflictsOnly;
    private readonly DispatcherTimer _searchDebounce;

    // ── State ──────────────────────────────────────────────────────────────
    private bool _isDirty;
    private bool _isLoading;
    private bool _isAnalyzing;
    private TblStatistics _statistics = new();

    // ── Services (lazy) ────────────────────────────────────────────────────
    private readonly TblConflictAnalyzer _conflictAnalyzer = new();
    private readonly TblValidationService _validationService = new();
    private CancellationTokenSource _analysisCts = new();

    // ── Constructor ────────────────────────────────────────────────────────
    public TblEditorViewModel()
    {
        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); ApplyFilter(); };

        AddCommand    = new RelayCommand(_ => ExecuteAdd(),    _ => !IsReadOnly);
        DeleteCommand = new RelayCommand(_ => ExecuteDelete(), _ => SelectedEntry != null && !IsReadOnly);
        UndoCommand   = new RelayCommand(_ => Undo(),          _ => CanUndo);
        RedoCommand   = new RelayCommand(_ => Redo(),          _ => CanRedo);
    }

    // ── Properties ─────────────────────────────────────────────────────────

    public ICollectionView? FilteredEntries => _filteredEntries;
    public ObservableCollection<TblEntryViewModel> Entries => _entries;

    public TblStream? Source
    {
        get => _source;
        set { _source = value; OnPropertyChanged(); }
    }

    public string? SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }
    }

    public DteType? TypeFilter
    {
        get => _typeFilter;
        set { _typeFilter = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public bool ShowConflictsOnly
    {
        get => _showConflictsOnly;
        set { _showConflictsOnly = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(); DirtyChanged?.Invoke(this, EventArgs.Empty); } }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set { _isAnalyzing = value; OnPropertyChanged(); }
    }

    public TblStatistics Statistics
    {
        get => _statistics;
        private set { _statistics = value; OnPropertyChanged(); StatisticsChanged?.Invoke(this, value); }
    }

    public bool IsReadOnly { get; set; }

    public TblEntryViewModel? SelectedEntry { get; set; }

    /// <summary>Marks the document dirty from external callers (e.g. inline cell edits).</summary>
    internal void MarkDirty() => IsDirty = true;

    // ── Undo / Redo state ──────────────────────────────────────────────────
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    // ── Commands ───────────────────────────────────────────────────────────
    public ICommand AddCommand    { get; }
    public ICommand DeleteCommand { get; }
    public ICommand UndoCommand   { get; }
    public ICommand RedoCommand   { get; }

    // ── Events ─────────────────────────────────────────────────────────────
    public event EventHandler? DirtyChanged;
    public event EventHandler? CanUndoChanged;
    public event EventHandler? CanRedoChanged;
    public event EventHandler<TblStatistics>? StatisticsChanged;

    // ── Load ───────────────────────────────────────────────────────────────

    public async Task LoadAsync(TblStream tbl, CancellationToken ct = default)
    {
        IsLoading = true;
        _entries.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        IsDirty = false;
        Source = tbl;

        try
        {
            var allEntries = await Task.Run(() => tbl.GetAllEntries().ToList(), ct);
            foreach (var block in Chunk(allEntries, 100))
            {
                ct.ThrowIfCancellationRequested();
                foreach (var dte in block)
                    _entries.Add(new TblEntryViewModel(dte));
            }
            RebuildView();
            UpdateStatistics();
            await _validationService.ValidateAllAsync(_entries, ct);
            await RunAnalysisAsync();
        }
        finally { IsLoading = false; }
    }

    // ── Save ───────────────────────────────────────────────────────────────

    public void Save()
    {
        if (_source == null) return;
        var existing = _source.GetAllEntries().ToList();
        foreach (var d in existing) _source.Remove(d);
        foreach (var vm in _entries) { var d = vm.ToDto(); if (d.IsValid) _source.Add(d); }
        IsDirty = false;
    }

    // ── Undo / Redo ────────────────────────────────────────────────────────

    public void Undo()
    {
        if (_undoStack.TryPop(out var cmd))
        {
            cmd.Undo();
            _redoStack.Push(cmd);
            NotifyUndoRedo();
            IsDirty = true;
        }
    }

    public void Redo()
    {
        if (_redoStack.TryPop(out var cmd))
        {
            cmd.Execute();
            _undoStack.Push(cmd);
            NotifyUndoRedo();
            IsDirty = true;
        }
    }

    // ── Entry mutations ────────────────────────────────────────────────────

    public void AddEntry(Dte? template = null)
    {
        if (IsReadOnly) return;
        var dte  = template ?? new Dte("00", "?");
        var vm   = new TblEntryViewModel(dte);
        var cmd  = new AddEntryCommand(_entries, vm);
        cmd.Execute();
        PushUndo(cmd);
        SelectedEntry = vm;
        IsDirty = true;
        UpdateStatistics();
        _ = RunAnalysisAsync();
    }

    public void DuplicateSelected()
    {
        if (IsReadOnly || SelectedEntry == null) return;
        AddEntry(SelectedEntry.ToDto());
    }

    public void DeleteSelected()
    {
        if (IsReadOnly || SelectedEntry == null) return;
        var cmd = new RemoveEntryCommand(_entries, SelectedEntry);
        cmd.Execute();
        PushUndo(cmd);
        IsDirty = true;
        UpdateStatistics();
        _ = RunAnalysisAsync();
    }

    // ── Filter ─────────────────────────────────────────────────────────────

    public void ClearFilter()
    {
        _searchText = null;
        _typeFilter = null;
        _showConflictsOnly = false;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(TypeFilter));
        OnPropertyChanged(nameof(ShowConflictsOnly));
        ApplyFilter();
    }

    // ── GoTo ───────────────────────────────────────────────────────────────

    public TblEntryViewModel? FindEntry(string hexKey)
        => _entries.FirstOrDefault(e => e.Entry.Equals(hexKey, StringComparison.OrdinalIgnoreCase));

    // ── Internals ──────────────────────────────────────────────────────────

    private void ExecuteAdd()    => AddEntry();
    private void ExecuteDelete() => DeleteSelected();

    private void ApplyFilter()
    {
        _filteredEntries?.Refresh();
    }

    private bool EntryMatchesFilter(object obj)
    {
        if (obj is not TblEntryViewModel vm) return false;
        if (_showConflictsOnly && !vm.HasConflict) return false;
        if (_typeFilter.HasValue && vm.Type != _typeFilter.Value) return false;
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var s = _searchText;
            if (!vm.Entry.Contains(s, StringComparison.OrdinalIgnoreCase) &&
                vm.Value?.Contains(s, StringComparison.OrdinalIgnoreCase) != true)
                return false;
        }
        return true;
    }

    private void RebuildView()
    {
        _filteredEntries = CollectionViewSource.GetDefaultView(_entries);
        _filteredEntries.Filter = EntryMatchesFilter;
        _filteredEntries.SortDescriptions.Clear();
        OnPropertyChanged(nameof(FilteredEntries));
    }

    private void UpdateStatistics()
    {
        var stats = new TblStatistics
        {
            TotalCount     = _entries.Count,
            AsciiCount     = _entries.Count(e => e.Type == DteType.Ascii),
            DteCount       = _entries.Count(e => e.Type == DteType.DualTitleEncoding),
            MteCount       = _entries.Count(e => e.Type == DteType.MultipleTitleEncoding),
            Byte2Count     = _entries.Count(e => e.ByteLength == 2),
            Byte3Count     = _entries.Count(e => e.ByteLength == 3),
            Byte4Count     = _entries.Count(e => e.ByteLength == 4),
            Byte5PlusCount = _entries.Count(e => e.ByteLength >= 5),
            JapaneseCount  = _entries.Count(e => e.Type == DteType.Japonais),
            EndBlockCount  = _entries.Count(e => e.Type == DteType.EndBlock),
            EndLineCount   = _entries.Count(e => e.Type == DteType.EndLine),
            ConflictCount  = _entries.Count(e => e.HasConflict),
        };
        var singleByteKeys = _entries.Where(e => e.ByteLength == 1)
            .Select(e => e.Entry.ToUpperInvariant()).Distinct().Count();
        stats.CoveragePercent = singleByteKeys / 256.0 * 100;
        Statistics = stats;
    }

    private async Task RunAnalysisAsync()
    {
        _analysisCts.Cancel();
        _analysisCts.Dispose();
        _analysisCts = new CancellationTokenSource();
        var ct = _analysisCts.Token;
        IsAnalyzing = true;
        try
        {
            var conflicts = await _conflictAnalyzer.AnalyzeConflictsAsync(_entries, ct);
            if (ct.IsCancellationRequested) return;
            var conflictKeys = new HashSet<string>(
                conflicts.SelectMany(c => c.ConflictingEntries).Select(d => d.Entry.ToUpperInvariant()));
            foreach (var vm in _entries)
                vm.HasConflict = conflictKeys.Contains(vm.Entry.ToUpperInvariant());
        }
        catch (OperationCanceledException) { }
        finally { if (!ct.IsCancellationRequested) IsAnalyzing = false; }
    }

    private void PushUndo(ITblCommand cmd)
    {
        _undoStack.Push(cmd);
        if (_undoStack.Count > MaxUndoDepth) TrimStack(_undoStack);
        _redoStack.Clear();
        NotifyUndoRedo();
    }

    private static void TrimStack(Stack<ITblCommand> stack)
    {
        var items = stack.ToArray();
        stack.Clear();
        foreach (var item in items.Take(MaxUndoDepth)) stack.Push(item);
    }

    private void NotifyUndoRedo()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        CanUndoChanged?.Invoke(this, EventArgs.Empty);
        CanRedoChanged?.Invoke(this, EventArgs.Empty);
        (UndoCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RedoCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private static IEnumerable<List<T>> Chunk<T>(List<T> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
    }

    // ── INotifyPropertyChanged ─────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── IDisposable ────────────────────────────────────────────────────────
    public void Dispose()
    {
        _searchDebounce.Stop();
        _analysisCts.Cancel();
        _analysisCts.Dispose();
    }
}

// ── Undo/Redo Command infrastructure ──────────────────────────────────────────

internal interface ITblCommand
{
    void Execute();
    void Undo();
}

internal sealed class AddEntryCommand(ObservableCollection<TblEntryViewModel> entries, TblEntryViewModel vm) : ITblCommand
{
    public void Execute() => entries.Add(vm);
    public void Undo()    => entries.Remove(vm);
}

internal sealed class RemoveEntryCommand(ObservableCollection<TblEntryViewModel> entries, TblEntryViewModel vm) : ITblCommand
{
    private int _index;
    public void Execute() { _index = entries.IndexOf(vm); entries.RemoveAt(_index); }
    public void Undo()    => entries.Insert(_index, vm);
}

internal sealed class EditEntryCommand(TblEntryViewModel vm, string oldEntry, string oldValue, string newEntry, string newValue) : ITblCommand
{
    public void Execute() { vm.Entry = newEntry; vm.Value = newValue; }
    public void Undo()    { vm.Entry = oldEntry; vm.Value = oldValue; }
}

// ── RelayCommand ──────────────────────────────────────────────────────────────

internal sealed class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)    => execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public event EventHandler? CanExecuteChanged;
}
