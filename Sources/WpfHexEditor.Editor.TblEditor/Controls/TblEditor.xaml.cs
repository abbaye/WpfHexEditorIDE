//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
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
public partial class TblEditor : UserControl, IDocumentEditor, IDiagnosticSource, IPropertyProviderSource, IOpenableDocument, IStatusBarContributor
{
    private readonly TblEditorViewModel _vm;
    private string? _currentFilePath;
    private double _baseFontSize = 12.0;
    private bool _suppressSourceLoad;

    // ── IDiagnosticSource ─────────────────────────────────────────────────
    private List<DiagnosticEntry> _diagnostics = [];
    public event EventHandler? DiagnosticsChanged;

    string IDiagnosticSource.SourceLabel
        => _currentFilePath is not null ? Path.GetFileName(_currentFilePath) : "TBL Editor";

    IReadOnlyList<DiagnosticEntry> IDiagnosticSource.GetDiagnostics() => _diagnostics;

    // ── Constructor ────────────────────────────────────────────────────────

    public TblEditor()
    {
        InitializeComponent();
        _vm = new TblEditorViewModel();
        DataContext = _vm;

        _vm.DirtyChanged      += (_, _) => { ModifiedChanged?.Invoke(this, EventArgs.Empty); NotifyTitle(); };
        _vm.CanUndoChanged    += (_, _) => CanUndoChanged?.Invoke(this, EventArgs.Empty);
        _vm.CanRedoChanged    += (_, _) => CanRedoChanged?.Invoke(this, EventArgs.Empty);
        _vm.StatisticsChanged += (_, s) => { StatusMessage?.Invoke(this, s.ToString()); RefreshStatusBarItems(); };

        // Auto-refresh ErrorPanel on any collection mutation (add/delete/undo/redo/toolbar commands)
        _vm.Entries.CollectionChanged += (_, _) => { if (!_vm.IsLoading) _ = ForceValidationAsync(); };

        // Populate type-filter combo
        TypeFilterCombo.Items.Add(new ComboBoxItem { Content = "(All)", Tag = null });
        foreach (DteType t in Enum.GetValues<DteType>())
            TypeFilterCombo.Items.Add(new ComboBoxItem { Content = t.ToString(), Tag = t });
        TypeFilterCombo.SelectedIndex = 0;

        Loaded += (_, _) => _baseFontSize = EntriesGrid.FontSize;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Dependency Properties
    // ═══════════════════════════════════════════════════════════════════════

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(TblStream), typeof(TblEditor),
            new PropertyMetadata(null, OnSourceChanged));

    /// <summary>
    /// TBL stream bound to this editor. Setting it triggers an async reload.
    /// </summary>
    public TblStream? Source
    {
        get => (TblStream?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TblEditor ctrl && e.NewValue is TblStream tbl && !ctrl._suppressSourceLoad)
            _ = ctrl.LoadAsync(tbl);
    }

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(TblEditor),
            new PropertyMetadata(false, (d, e) => ((TblEditor)d)._vm.IsReadOnly = (bool)e.NewValue));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(int), typeof(TblEditor),
            new PropertyMetadata(100, OnZoomChanged, CoerceZoom));

    /// <summary>Zoom level in percent (50–200). Default is 100.</summary>
    public int Zoom
    {
        get => (int)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    private static object CoerceZoom(DependencyObject d, object baseValue)
        => Math.Clamp((int)baseValue, 50, 200);

    // Base pixel widths for fixed-size columns (index matches DataGrid.Columns order).
    // NaN = star/auto column — not touched.
    private static readonly double[] _baseColWidths = [24, 100, 120, 75, 50, double.NaN];

    private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (TblEditor)d;
        var zoom = (int)e.NewValue;
        ctrl.EntriesGrid.FontSize = ctrl._baseFontSize * zoom / 100.0;
        ctrl.ApplyZoomToColumns(zoom);
        if (ctrl._sbZoom != null)
            ctrl._sbZoom.Value = $"{zoom}%";
    }

    private void ApplyZoomToColumns(int zoom)
    {
        var factor = zoom / 100.0;
        for (int i = 0; i < EntriesGrid.Columns.Count && i < _baseColWidths.Length; i++)
        {
            if (!double.IsNaN(_baseColWidths[i]))
                EntriesGrid.Columns[i].Width = new DataGridLength(_baseColWidths[i] * factor);
        }
    }

    public void ZoomIn()    => Zoom = Math.Min(Zoom + 10, 200);
    public void ZoomOut()   => Zoom = Math.Max(Zoom - 10, 50);
    public void ResetZoom() => Zoom = 100;

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
        _diagnostics.Clear();
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        NotifyTitle();
    }

    public event EventHandler? ModifiedChanged;
    public event EventHandler? CanUndoChanged;
    public event EventHandler? CanRedoChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<string>? OutputMessage;

    // ── Long-running operations (no-op: TblEditor has no async operations) ──
    public bool IsBusy => false;
    public void CancelOperation() { }
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

    // Explicit interface implementation — TblEditor already has SelectionChanged<Dte?>
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

    Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
        => LoadFromFileAsync(filePath, ct);

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

    public void ShowSearch()               { SearchPanel.Visibility = Visibility.Visible; SearchBox.Focus(); }
    public void HideSearch()               { SearchPanel.Visibility = Visibility.Collapsed; _vm.ClearFilter(); }
    public void SetSearch(string text)     { ShowSearch(); _vm.SearchText = text; }
    public void SetTypeFilter(DteType? t)  => _vm.TypeFilter = t;
    public void SetConflictsFilter(bool b) => _vm.ShowConflictsOnly = b;
    public void ClearFilter()              => _vm.ClearFilter();

    // ── Validation ────────────────────────────────────────────────────────

    /// <summary>
    /// Re-runs full validation + conflict analysis on all in-memory entries,
    /// updates the Error Panel diagnostics and reports a summary in the status bar.
    /// </summary>
    public async Task ForceValidationAsync()
    {
        if (_vm.Source == null) return;
        StatusMessage?.Invoke(this, "Validating…");

        await _vm.ForceReanalysisAsync();

        var fileName = _currentFilePath is not null ? Path.GetFileName(_currentFilePath) : null;

        var duplicateKeys = _vm.Entries
            .GroupBy(e => e.Entry.ToUpperInvariant())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        _diagnostics =
        [
            // TBL001 — entrée structurellement invalide (hex malformé, valeur vide…)
            .._vm.Entries
                .Where(e => !e.IsValid)
                .Select(e => new DiagnosticEntry(
                    DiagnosticSeverity.Error, "TBL001",
                    e.ValidationError ?? "Invalid entry",
                    FileName: fileName, FilePath: _currentFilePath)),
            // TBL002 — clé hex en doublon (toutes les copies signalées)
            .._vm.Entries
                .Where(e => duplicateKeys.Contains(e.Entry.ToUpperInvariant()))
                .Select(e => new DiagnosticEntry(
                    DiagnosticSeverity.Error, "TBL002",
                    $"Duplicate hex key \"{e.Entry}\"",
                    FileName: fileName, FilePath: _currentFilePath)),
            // TBL003 — conflit de préfixe uniquement (HasConflict ET pas doublon)
            .._vm.Entries
                .Where(e => e.HasConflict && !duplicateKeys.Contains(e.Entry.ToUpperInvariant()))
                .Select(e => new DiagnosticEntry(
                    DiagnosticSeverity.Warning, "TBL003",
                    $"Prefix conflict on entry \"{e.Entry}\"",
                    FileName: fileName, FilePath: _currentFilePath))
        ];
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);

        var errors   = _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
        var warnings = _diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
        StatusMessage?.Invoke(this, errors == 0 && warnings == 0
            ? "Validation OK — no issues found"
            : $"Validation: {errors} error(s), {warnings} warning(s)");
        RefreshStatusBarItems();
    }

    // ── Navigation ────────────────────────────────────────────────────────

    public void GoToEntry(string hexKey)
    {
        var vm = _vm.FindEntry(hexKey);
        if (vm == null) return;
        EntriesGrid.ScrollIntoView(vm);
        EntriesGrid.SelectedItem = vm;
    }

    /// <summary>
    /// Scrolls to and selects the next entry with a prefix conflict, wrapping around.
    /// </summary>
    public void GoToNextConflict()
    {
        var entries = _vm.Entries.ToList();
        if (entries.Count == 0) return;

        int startIdx = 0;
        if (_vm.SelectedEntry != null)
        {
            var currentIdx = entries.IndexOf(_vm.SelectedEntry);
            if (currentIdx >= 0) startIdx = currentIdx + 1;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var candidate = entries[(startIdx + i) % entries.Count];
            if (!candidate.HasConflict) continue;
            EntriesGrid.ScrollIntoView(candidate);
            EntriesGrid.SelectedItem  = candidate;
            _vm.SelectedEntry         = candidate;
            return;
        }
        StatusMessage?.Invoke(this, "No conflicts found");
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

    // ── IPropertyProviderSource ───────────────────────────────────────────
    private TblEditorPropertyProvider? _propertyProvider;
    public IPropertyProvider? GetPropertyProvider()
        => _propertyProvider ??= new TblEditorPropertyProvider(this);

    // ═══════════════════════════════════════════════════════════════════
    // IStatusBarContributor
    // ═══════════════════════════════════════════════════════════════════

    private ObservableCollection<StatusBarItem>? _statusBarItems;
    private StatusBarItem _sbEntries = null!;
    private StatusBarItem _sbAscii   = null!;
    private StatusBarItem _sbDte     = null!;
    private StatusBarItem _sbMte     = null!;
    private StatusBarItem _sbCov     = null!;
    private StatusBarItem _sbConf    = null!;
    private StatusBarItem _sbZoom    = null!;

    public ObservableCollection<StatusBarItem> StatusBarItems
        => _statusBarItems ??= BuildStatusBarItems();

    private ObservableCollection<StatusBarItem> BuildStatusBarItems()
    {
        _sbEntries = new StatusBarItem { Label = "Entries", Tooltip = "Total TBL entries" };
        _sbAscii   = new StatusBarItem { Label = "ASCII",   Tooltip = "ASCII type entries" };
        _sbDte     = new StatusBarItem { Label = "DTE",     Tooltip = "Dual Title Encoding entries" };
        _sbMte     = new StatusBarItem { Label = "MTE",     Tooltip = "Multiple Title Encoding entries" };
        _sbCov     = new StatusBarItem { Label = "Cov.",    Tooltip = "Single-byte key coverage (0x00–0xFF)" };
        _sbConf    = new StatusBarItem { Label = "⚠",       Tooltip = "Prefix conflicts detected" };
        _sbZoom    = new StatusBarItem { Label = "Zoom",    Tooltip = "Current zoom level (Ctrl+Scroll, Ctrl+0 to reset)" };
        RefreshStatusBarItems();
        return new ObservableCollection<StatusBarItem> { _sbZoom, _sbEntries, _sbAscii, _sbDte, _sbMte, _sbCov, _sbConf };
    }

    internal void RefreshStatusBarItems()
    {
        if (_statusBarItems == null) return;
        var s = _vm.Statistics;
        _sbEntries.Value = s.TotalCount.ToString();
        _sbAscii.Value   = s.AsciiCount.ToString();
        _sbDte.Value     = s.DteCount.ToString();
        _sbMte.Value     = s.MteCount.ToString();
        _sbCov.Value     = $"{s.CoveragePercent:F1}%";
        _sbConf.Value    = s.ConflictCount.ToString();
        if (_sbZoom != null) _sbZoom.Value = $"{Zoom}%";
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
        else if ((e.Key == Key.OemPlus || e.Key == Key.Add) && Keyboard.Modifiers == ModifierKeys.Control)
        { ZoomIn(); e.Handled = true; }
        else if ((e.Key == Key.OemMinus || e.Key == Key.Subtract) && Keyboard.Modifiers == ModifierKeys.Control)
        { ZoomOut(); e.Handled = true; }
        else if ((e.Key == Key.D0 || e.Key == Key.NumPad0) && Keyboard.Modifiers == ModifierKeys.Control)
        { ResetZoom(); e.Handled = true; }
    }

    private void UserControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Delta > 0) ZoomIn(); else ZoomOut();
        e.Handled = true;
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
        {
            _vm.MarkDirty();
            _ = ForceValidationAsync();
        }
    }

    private void EntriesGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        bool hasSource    = _vm.Source != null;
        bool hasSelection = EntriesGrid.SelectedItems.Count > 0;
        bool singleSel    = EntriesGrid.SelectedItems.Count == 1;
        bool notReadOnly  = !IsReadOnly;
        bool hasConflicts = _vm.Entries.Any(en => en.HasConflict);

        CtxAdd.IsEnabled              = hasSource && notReadOnly;
        CtxDuplicate.IsEnabled        = singleSel && notReadOnly;
        CtxDelete.IsEnabled           = hasSelection && notReadOnly;
        CtxCut.IsEnabled              = hasSelection && notReadOnly;
        CtxCopy.IsEnabled             = hasSelection;
        CtxPaste.IsEnabled            = notReadOnly && Clipboard.ContainsText();
        CtxFind.IsEnabled             = hasSource;
        CtxSelectAll.IsEnabled        = hasSource;
        CtxForceValidation.IsEnabled  = hasSource && !_vm.IsAnalyzing;
        CtxShowConflictsOnly.IsEnabled = hasSource;
        CtxShowConflictsOnly.IsChecked = _vm.ShowConflictsOnly;
        CtxNextConflict.IsEnabled     = hasConflicts;
        CtxUndo.IsEnabled             = _vm.CanUndo;
        CtxRedo.IsEnabled             = _vm.CanRedo;
    }

    // ── Context menu Click handlers ────────────────────────────────────────

    private void CtxAdd_Click(object sender, RoutedEventArgs e)              => AddEntry();
    private void CtxDuplicate_Click(object sender, RoutedEventArgs e)        => DuplicateSelectedEntry();
    private void CtxDelete_Click(object sender, RoutedEventArgs e)           => DeleteSelectedEntries();
    private void CtxCut_Click(object sender, RoutedEventArgs e)              => Cut();
    private void CtxCopy_Click(object sender, RoutedEventArgs e)             => Copy();
    private void CtxPaste_Click(object sender, RoutedEventArgs e)            => Paste();
    private void CtxFind_Click(object sender, RoutedEventArgs e)             => ShowSearch();
    private void CtxSelectAll_Click(object sender, RoutedEventArgs e)        => SelectAll();
    private void CtxForceValidation_Click(object sender, RoutedEventArgs e)  => _ = ForceValidationAsync();
    private void CtxShowConflictsOnly_Click(object sender, RoutedEventArgs e) => SetConflictsFilter(CtxShowConflictsOnly.IsChecked);
    private void CtxNextConflict_Click(object sender, RoutedEventArgs e)     => GoToNextConflict();
    private void CtxUndo_Click(object sender, RoutedEventArgs e)             => Undo();
    private void CtxRedo_Click(object sender, RoutedEventArgs e)             => Redo();

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
        var fileName = Path.GetFileName(filePath);
        OutputMessage?.Invoke(this, $"Opening {fileName}…");

        // ── 1. Repair analysis on raw content (produces line-numbered diagnostics)
        var rawContent   = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        var repairResult = await Task.Run(
            () => new TblRepairService().Repair(rawContent, fileName), ct);

        // ── 2. Load via TblStream (already lenient — invalid lines silently skipped)
        var tbl = new TblStream(filePath);
        await Task.Run(() => tbl.Load(), ct);
        _currentFilePath = filePath;

        // Single explicit load — suppress DP callback to avoid a second concurrent LoadAsync
        _suppressSourceLoad = true;
        try { Source = tbl; }
        finally { _suppressSourceLoad = false; }
        await _vm.LoadAsync(tbl, ct);

        // ── 3. Output summary
        OutputMessage?.Invoke(this, $"  {EntryCount} entries loaded.");
        if (repairResult.Diagnostics.Count > 0)
        {
            var errors   = repairResult.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
            var warnings = repairResult.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
            if (errors > 0)
                OutputMessage?.Invoke(this,
                    $"  {errors} error(s) detected — saving will remove invalid lines.");
            if (warnings > 0)
                OutputMessage?.Invoke(this,
                    $"  {warnings} warning(s) — see Error Panel for details.");
        }

        // ── 4. Propagate FilePath into diagnostics and publish to ErrorPanel
        _diagnostics = repairResult.Diagnostics
            .Select(d => d with { FilePath = filePath })
            .ToList();
        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);

        // ── 5. Mark dirty when repairs would change the saved content
        if (repairResult.WasModified)
            _vm.MarkDirty();

        NotifyTitle();
    }

    /// <summary>
    /// Persists the current entries to <paramref name="filePath"/> using <see cref="TblExportService"/>.
    /// </summary>
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

// ── File-scoped RelayCommand for TblEditor IDocumentEditor commands ────

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
