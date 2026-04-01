// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: ViewModels/ResxEditorViewModel.cs
// Description:
//     Central ViewModel for the RESX editor.  Owns the entry
//     collection, filtering/sorting, undo/redo stack, and all
//     commands.  The control's code-behind wires its events
//     to IDocumentEditor and IDE pipeline callbacks.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WpfHexEditor.Editor.ResxEditor.Models;
using WpfHexEditor.Editor.ResxEditor.UndoRedo;

namespace WpfHexEditor.Editor.ResxEditor.ViewModels;

/// <summary>Main ViewModel for the RESX grid editor.</summary>
public sealed class ResxEditorViewModel : INotifyPropertyChanged
{
    // -- Fields -------------------------------------------------------------

    private readonly ResxUndoStack     _undoStack = new();
    private readonly DispatcherTimer   _searchDebounce;
    private          ResxEntryType?    _entryTypeFilter;
    private          string            _searchText      = string.Empty;
    private          bool              _showOnlyDirty;
    private          bool              _isLoading;
    private          bool              _isReadOnly;
    private          string            _filePath        = string.Empty;

    // Command backing fields (RelayCommand) — exposed as ICommand via public properties
    private RelayCommand _addCmd    = null!;
    private RelayCommand _deleteCmd = null!;
    private RelayCommand _undoCmd   = null!;
    private RelayCommand _redoCmd   = null!;

    // -- Constructor --------------------------------------------------------

    public ResxEditorViewModel()
    {
        Entries        = [];
        FilteredEntries = CollectionViewSource.GetDefaultView(Entries);
        FilteredEntries.Filter = ApplyFilter;

        _searchDebounce = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); FilteredEntries.Refresh(); };

        _undoStack.CanUndoChanged += (_, _) => { CanUndoChanged?.Invoke(this, EventArgs.Empty); RaiseCommands(); };
        _undoStack.CanRedoChanged += (_, _) => { CanRedoChanged?.Invoke(this, EventArgs.Empty); RaiseCommands(); };

        _addCmd    = new RelayCommand(_ => ExecuteAdd(),    _ => !IsReadOnly);
        _deleteCmd = new RelayCommand(_ => ExecuteDelete(), _ => SelectedEntry != null && !IsReadOnly);
        _undoCmd   = new RelayCommand(_ => Undo(),          _ => CanUndo);
        _redoCmd   = new RelayCommand(_ => Redo(),          _ => CanRedo);

        AddCommand    = _addCmd;
        DeleteCommand = _deleteCmd;
        UndoCommand   = _undoCmd;
        RedoCommand   = _redoCmd;
    }

    // -- Collections --------------------------------------------------------

    public ObservableCollection<ResxEntryViewModel> Entries        { get; }
    public ICollectionView                          FilteredEntries { get; }

    // -- Selection ----------------------------------------------------------

    private ResxEntryViewModel? _selectedEntry;
    public ResxEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set { _selectedEntry = value; OnPropertyChanged(); RaiseCommands(); }
    }

    // -- Filter properties --------------------------------------------------

    public ResxEntryType? EntryTypeFilter
    {
        get => _entryTypeFilter;
        set { _entryTypeFilter = value; OnPropertyChanged(); FilteredEntries.Refresh(); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }
    }

    public bool ShowOnlyDirty
    {
        get => _showOnlyDirty;
        set { _showOnlyDirty = value; OnPropertyChanged(); FilteredEntries.Refresh(); }
    }

    // -- State flags --------------------------------------------------------

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public bool IsReadOnly
    {
        get => _isReadOnly;
        set { _isReadOnly = value; OnPropertyChanged(); RaiseCommands(); }
    }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    public bool IsDirty => Entries.Any(e => e.IsDirty);
    public bool CanUndo  => _undoStack.CanUndo;
    public bool CanRedo  => _undoStack.CanRedo;

    // -- Commands -----------------------------------------------------------

    public ICommand AddCommand    { get; }
    public ICommand DeleteCommand { get; }
    public ICommand UndoCommand   { get; }
    public ICommand RedoCommand   { get; }

    // -- Events -------------------------------------------------------------

    public event EventHandler? DirtyChanged;
    public event EventHandler? CanUndoChanged;
    public event EventHandler? CanRedoChanged;
    public event EventHandler<string>? StatisticsChanged;

    // -- Public operations --------------------------------------------------

    /// <summary>
    /// Loads entries from a parsed document.
    /// Clears undo stack and dirty flags.
    /// </summary>
    public void LoadDocument(ResxDocument document)
    {
        IsLoading = true;
        try
        {
            Entries.Clear();
            foreach (var entry in document.Entries)
                Entries.Add(new ResxEntryViewModel(entry));

            _undoStack.Clear();
            FilePath = document.FilePath;
            FireStatistics();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Returns current entries as a list of <see cref="ResxEntry"/> records.</summary>
    public IReadOnlyList<ResxEntry> GetCurrentEntries()
        => Entries.Select(e => e.ToEntry()).ToList();

    /// <summary>Marks all entries as saved (resets dirty flags).</summary>
    public void MarkAllSaved()
    {
        foreach (var e in Entries) e.MarkSaved();
        DirtyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo() => _undoStack.Undo(Entries);
    public void Redo() => _undoStack.Redo(Entries);

    /// <summary>Pushes an action onto the undo stack and fires dirty changed.</summary>
    public void Push(IResxUndoAction action)
    {
        _undoStack.Push(action, Entries);
        OnDirtyChanged();
        FireStatistics();
    }

    // -- Filter predicate ---------------------------------------------------

    private bool ApplyFilter(object obj)
    {
        if (obj is not ResxEntryViewModel vm) return false;

        if (_showOnlyDirty && !vm.IsDirty) return false;
        if (_entryTypeFilter.HasValue && vm.EntryType != _entryTypeFilter.Value) return false;

        if (!string.IsNullOrEmpty(_searchText))
        {
            var q = _searchText;
            if (!vm.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
             && !vm.Value.Contains(q, StringComparison.OrdinalIgnoreCase)
             && !vm.Comment.Contains(q, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    // -- Private helpers ----------------------------------------------------

    private void ExecuteAdd()
    {
        var newEntry = new ResxEntryViewModel(
            new ResxEntry("NewKey", string.Empty, string.Empty, null, null, "preserve"));
        Push(new ResxAddEntryAction(newEntry));
        SelectedEntry = newEntry;
    }

    private void ExecuteDelete()
    {
        if (SelectedEntry is null) return;
        var index = Entries.IndexOf(SelectedEntry);
        Push(new ResxDeleteEntryAction(SelectedEntry, index));
    }

    private void OnDirtyChanged()
        => DirtyChanged?.Invoke(this, EventArgs.Empty);

    private void FireStatistics()
    {
        var strings  = Entries.Count(e => e.EntryType == ResxEntryType.String);
        var images   = Entries.Count(e => e.EntryType == ResxEntryType.Image);
        var binaries = Entries.Count(e => e.EntryType == ResxEntryType.Binary);
        var fileRefs = Entries.Count(e => e.EntryType == ResxEntryType.FileRef);
        StatisticsChanged?.Invoke(this,
            $"{Entries.Count} entries | {strings} strings | {images} images | {binaries} binary | {fileRefs} file refs");
    }

    private void RaiseCommands()
    {
        _addCmd?.RaiseCanExecuteChanged();
        _deleteCmd?.RaiseCanExecuteChanged();
        _undoCmd?.RaiseCanExecuteChanged();
        _redoCmd?.RaiseCanExecuteChanged();
    }

    // -- INotifyPropertyChanged ---------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// -- RelayCommand -----------------------------------------------------------

/// <summary>Minimal ICommand implementation used within the RESX editor project.</summary>
internal sealed class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : System.Windows.Input.ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)    => execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public event EventHandler? CanExecuteChanged;
}
