using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TblEditor.Services;
using WpfHexEditor.Editor.TblEditor.ViewModels;

namespace WpfHexEditor.Editor.TblEditor.Controls;

/// <summary>
/// Standalone document-style TBL editor.
/// No embedded toolbar — all editing commands are exposed via <see cref="IDocumentEditor"/>
/// and TBL-specific properties/methods for the host to wire to its own menus.
/// </summary>
public partial class TblEditorControl : UserControl, IDocumentEditor
{
    private readonly TblEditorViewModel _vm;
    private string? _currentFilePath;

    // ── Constructor ────────────────────────────────────────────────────────

    public TblEditorControl()
    {
        InitializeComponent();
        _vm = new TblEditorViewModel();
        DataContext = _vm;

        _vm.DirtyChanged      += (_, _) => { ModifiedChanged?.Invoke(this, EventArgs.Empty); NotifyTitle(); };
        _vm.CanUndoChanged    += (_, _) => CanUndoChanged?.Invoke(this, EventArgs.Empty);
        _vm.CanRedoChanged    += (_, _) => CanRedoChanged?.Invoke(this, EventArgs.Empty);
        _vm.StatisticsChanged += (_, s) => StatusMessage?.Invoke(this, s.ToString());

        // Populate type-filter combo
        TypeFilterCombo.Items.Add(new ComboBoxItem { Content = "(All)", Tag = null });
        foreach (DteType t in Enum.GetValues<DteType>())
            TypeFilterCombo.Items.Add(new ComboBoxItem { Content = t.ToString(), Tag = t });
        TypeFilterCombo.SelectedIndex = 0;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dependency Properties
    // ═══════════════════════════════════════════════════════════════════════

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(TblStream), typeof(TblEditorControl),
            new PropertyMetadata(null, OnSourceChanged));

    /// <summary>TBL stream bound to this editor. Setting it triggers an async reload.</summary>
    public TblStream? Source
    {
        get => (TblStream?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TblEditorControl ctrl && e.NewValue is TblStream tbl)
            _ = ctrl.LoadAsync(tbl);
    }

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TblEditorControl),
            new PropertyMetadata(false, (d, e) => ((TblEditorControl)d)._vm.IsReadOnly = (bool)e.NewValue));

    // ═══════════════════════════════════════════════════════════════════════
    // IDocumentEditor
    // ═══════════════════════════════════════════════════════════════════════

    public bool IsDirty => _vm.IsDirty;
    public bool CanUndo => _vm.CanUndo;
    public bool CanRedo => _vm.CanRedo;

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string Title
    {
        get
        {
            var name = _currentFilePath != null
                ? Path.GetFileName(_currentFilePath)
                : _vm.Source != null ? "untitled.tbl" : "TBL Editor";
            return _vm.IsDirty ? $"{name} *" : name;
        }
    }

    public ICommand UndoCommand => _vm.UndoCommand;
    public ICommand RedoCommand => _vm.RedoCommand;
    public ICommand SaveCommand => new TblRelayCommand(_ => Save());
    public ICommand CopyCommand => new TblRelayCommand(_ => Copy(), _ => EntriesGrid.SelectedItems.Count > 0);
    public ICommand CutCommand => new TblRelayCommand(_ => Cut(), _ => EntriesGrid.SelectedItems.Count > 0 && !IsReadOnly);
    public ICommand PasteCommand => new TblRelayCommand(_ => Paste(), _ => !IsReadOnly && Clipboard.ContainsText());
    ICommand IDocumentEditor.DeleteCommand => new TblRelayCommand(_ => Delete(), _ => EntriesGrid.SelectedItems.Count > 0 && !IsReadOnly);
    public ICommand SelectAllCommand => new TblRelayCommand(_ => SelectAll());

    public void Undo() => _vm.Undo();
    public void Redo() => _vm.Redo();

    public void Save()
    {
        _vm.Save();
        if (!string.IsNullOrEmpty(_currentFilePath))
            PersistToFile(_currentFilePath);
        StatusMessage?.Invoke(this, "Saved");
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        _vm.Save();
        if (!string.IsNullOrEmpty(_currentFilePath))
            await Task.Run(() => PersistToFile(_currentFilePath!), ct);
        StatusMessage?.Invoke(this, "Saved");
    }

    public async Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        if (_vm.Source == null) return;
        _vm.Save();
        await Task.Run(() => PersistToFile(filePath), ct);
        _currentFilePath = filePath;
        NotifyTitle();
        StatusMessage?.Invoke(this, $"Saved: {Path.GetFileName(filePath)}");
    }

    public void Copy()
    {
        if (EntriesGrid.SelectedItems.Count == 0) return;
        ApplicationCommands.Copy.Execute(null, EntriesGrid);
    }

    public void Cut()
    {
        Copy();
        DeleteSelectedEntries();
    }

    public void Paste()
    {
        if (!Clipboard.ContainsText()) return;
        ApplicationCommands.Paste.Execute(null, EntriesGrid);
    }

    public void Delete() => DeleteSelectedEntries();

    public void Close()
    {
        _vm.ClearFilter();
        Source = null;
        _currentFilePath = null;
        NotifyTitle();
    }

    public event EventHandler? ModifiedChanged;
    public event EventHandler? CanUndoChanged;
    public event EventHandler? CanRedoChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;

    // Explicit interface implementation — TblEditorControl already has SelectionChanged<Dte?>
    private EventHandler? _docEditorSelectionChanged;
    event EventHandler? IDocumentEditor.SelectionChanged
    {
        add => _docEditorSelectionChanged += value;
        remove => _docEditorSelectionChanged -= value;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TBL-specific public API
    // ═══════════════════════════════════════════════════════════════════════

    public ICommand AddCommand    => _vm.AddCommand;
    public ICommand DeleteCommand => _vm.DeleteCommand;

    // ── Load ─────────────────────────────────────────────────────────────

    public void Load(TblStream tbl)   => _ = LoadAsync(tbl);
    public void Load(string filePath) => _ = LoadFromFileAsync(filePath);

    public async Task LoadAsync(TblStream tbl, CancellationToken ct = default)
    {
        await _vm.LoadAsync(tbl, ct);
        NotifyTitle();
    }

    // ── Editing ──────────────────────────────────────────────────────────

    public void AddEntry(Dte? template = null) => _vm.AddEntry(template);
    public void DuplicateSelectedEntry()       => _vm.DuplicateSelected();
    public void DeleteSelectedEntries()        => _vm.DeleteSelected();

    // ── Import / Export ───────────────────────────────────────────────────

    public async Task ImportAsync(string filePath)
    {
        if (_vm.Source == null) return;
        var result = await Task.Run(() => new TblImportService().ImportFromFile(filePath));
        if (result.Success)
        {
            foreach (var dte in result.Entries)
                if (dte.IsValid) _vm.Source.Add(dte);
            await _vm.LoadAsync(_vm.Source);
            StatusMessage?.Invoke(this, $"Imported {result.ImportedCount} entries");
        }
        else
        {
            var err = result.Errors.Count > 0 ? result.Errors[0] : "Unknown error";
            StatusMessage?.Invoke(this, $"Import failed: {err}");
        }
    }

    public async Task ExportAsync(string filePath, TblFileFormat format)
    {
        if (_vm.Source == null) return;
        var entries = _vm.Entries.Select(e => e.ToDto()).ToList();
        await Task.Run(() => new TblExportService().ExportToFile(entries, filePath));
        StatusMessage?.Invoke(this, $"Exported to {Path.GetFileName(filePath)}");
    }

    // ── Search / Filter ───────────────────────────────────────────────────

    public void ShowSearch()              { SearchPanel.Visibility = Visibility.Visible; SearchBox.Focus(); }
    public void HideSearch()              { SearchPanel.Visibility = Visibility.Collapsed; _vm.ClearFilter(); }
    public void SetSearch(string text)    { ShowSearch(); _vm.SearchText = text; }
    public void SetTypeFilter(DteType? t) => _vm.TypeFilter = t;
    public void SetConflictsFilter(bool b) => _vm.ShowConflictsOnly = b;
    public void ClearFilter()             => _vm.ClearFilter();

    // ── Navigation ────────────────────────────────────────────────────────

    public void GoToEntry(string hexKey)
    {
        var vm = _vm.FindEntry(hexKey);
        if (vm == null) return;
        EntriesGrid.ScrollIntoView(vm);
        EntriesGrid.SelectedItem = vm;
    }

    public void SelectAll() => EntriesGrid.SelectAll();

    // ── State ─────────────────────────────────────────────────────────────

    public int  EntryCount    => _vm.Entries.Count;
    public Dte? SelectedEntry => _vm.SelectedEntry?.ToDto();

    public event EventHandler<Dte?>?          SelectionChanged;
    public event EventHandler<TblStatistics>? StatisticsChanged
    {
        add    => _vm.StatisticsChanged += value;
        remove => _vm.StatisticsChanged -= value;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // XAML event handlers
    // ═══════════════════════════════════════════════════════════════════════

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        { ShowSearch(); e.Handled = true; }
        else if (e.Key == Key.Escape && SearchPanel.Visibility == Visibility.Visible)
        { HideSearch(); e.Handled = true; }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        { Undo(); e.Handled = true; }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        { Redo(); e.Handled = true; }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)      { HideSearch(); e.Handled = true; }
        else if (e.Key == Key.Return) { EntriesGrid.Focus(); e.Handled = true; }
    }

    private void CloseSearch_Click(object sender, RoutedEventArgs e) => HideSearch();

    private void TypeFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeFilterCombo.SelectedItem is ComboBoxItem item)
            _vm.TypeFilter = item.Tag is DteType t ? t : null;
    }

    private void EntriesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Commit)
            _vm.MarkDirty();
    }

    private void EntriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EntriesGrid.SelectedItem is TblEntryViewModel vm)
        {
            _vm.SelectedEntry = vm;
            SelectionChanged?.Invoke(this, vm.ToDto());
        }
        else SelectionChanged?.Invoke(this, null);

        // Notify IDocumentEditor subscribers
        _docEditorSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Private helpers
    // ═══════════════════════════════════════════════════════════════════════

    private async Task LoadFromFileAsync(string filePath, CancellationToken ct = default)
    {
        var tbl = new TblStream(filePath);
        await Task.Run(() => tbl.Load(), ct);
        _currentFilePath = filePath;
        Source = tbl;
        await _vm.LoadAsync(tbl, ct);
        NotifyTitle();
    }

    /// <summary>Persists the current entries to <paramref name="filePath"/> using <see cref="TblExportService"/>.</summary>
    private void PersistToFile(string filePath)
    {
        if (_vm.Source == null) return;
        var entries = _vm.Entries.Select(e => e.ToDto()).ToList();
        new TblExportService().ExportToTblFile(entries, filePath);
    }

    private void NotifyTitle()
    {
        var name = _currentFilePath != null
            ? Path.GetFileName(_currentFilePath)
            : _vm.Source != null ? "untitled.tbl" : "TBL Editor";
        TitleChanged?.Invoke(this, _vm.IsDirty ? name + " *" : name);
    }
}

// ── File-scoped RelayCommand for TblEditorControl IDocumentEditor commands ────

file sealed class TblRelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public TblRelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
    public void Execute(object? p)    => _execute(p);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
