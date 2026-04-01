// ==========================================================
// Project: WpfHexEditor.Editor.ResxEditor
// File: Controls/ResxEditor.xaml.cs
// Description:
//     Code-behind for the main RESX editor UserControl.
//     Implements all IDocumentEditor opt-in interfaces and
//     wires them to the ResxEditorViewModel.
//
// Interfaces implemented:
//   IDocumentEditor           — core contract
//   IOpenableDocument         — OpenAsync(filePath)
//   IDiagnosticSource         — Error Panel integration
//   ISearchTarget             — QuickSearchBar integration
//   IEditorToolbarContributor — host toolbar pod
//   IStatusBarContributor     — status bar
//   IPropertyProviderSource   — Property panel
//   IBufferAwareEditor        — shared document buffer (cross-editor sync)
//   IEditorPersistable        — session state restore
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Editor.ResxEditor.Models;
using WpfHexEditor.Editor.ResxEditor.Services;
using WpfHexEditor.Editor.ResxEditor.UndoRedo;
using WpfHexEditor.Editor.ResxEditor.ViewModels;

namespace WpfHexEditor.Editor.ResxEditor.Controls;

/// <summary>
/// Grid-based .resx / .resw editor implementing the full WpfHexEditor
/// document editor contract with IDE pipeline integration.
/// </summary>
public partial class ResxEditor : UserControl,
    IDocumentEditor,
    IOpenableDocument,
    IDiagnosticSource,
    ISearchTarget,
    IEditorToolbarContributor,
    IStatusBarContributor,
    IPropertyProviderSource,
    IBufferAwareEditor,
    IEditorPersistable
{
    // -- Fields -------------------------------------------------------------

    private readonly ResxEditorViewModel _vm;
    private readonly ResxSearchService   _search = new();
    private          ResxDocument?       _document;
    private          string              _filePath = string.Empty;
    private          bool                _suppressBufferSync;
    private          IDocumentBuffer?    _buffer;

    // -- IDiagnosticSource --------------------------------------------------

    private List<DiagnosticEntry> _diagnostics = [];
    public event EventHandler? DiagnosticsChanged;

    string IDiagnosticSource.SourceLabel
        => string.IsNullOrEmpty(_filePath) ? "RESX Editor" : Path.GetFileName(_filePath);

    IReadOnlyList<DiagnosticEntry> IDiagnosticSource.GetDiagnostics() => _diagnostics;

    // -- Constructor --------------------------------------------------------

    public ResxEditor()
    {
        InitializeComponent();

        _vm = new ResxEditorViewModel();
        DataContext = _vm;

        _vm.DirtyChanged      += (_, _) => { ModifiedChanged?.Invoke(this, EventArgs.Empty); UpdateTitle(); };
        _vm.CanUndoChanged    += (_, _) => CanUndoChanged?.Invoke(this, EventArgs.Empty);
        _vm.CanRedoChanged    += (_, _) => CanRedoChanged?.Invoke(this, EventArgs.Empty);
        _vm.StatisticsChanged += (_, s) => { StatusMessage?.Invoke(this, s); RefreshStatusBarItems(); };

        _vm.Entries.CollectionChanged += (_, _) =>
        {
            if (!_vm.IsLoading) _ = RunValidationAsync();
        };

        InitToolbarItems();
        InitStatusBarItems();
    }

    // -----------------------------------------------------------------------
    // IDocumentEditor
    // -----------------------------------------------------------------------

    public bool   IsDirty    => _vm.IsDirty;
    public bool   CanUndo    => _vm.CanUndo;
    public bool   CanRedo    => _vm.CanRedo;
    public bool   IsBusy     { get; private set; }
    public string Title      => BuildTitle();

    public bool IsReadOnly
    {
        get => _vm.IsReadOnly;
        set { _vm.IsReadOnly = value; }
    }

    public ICommand? UndoCommand   => _vm.UndoCommand;
    public ICommand? RedoCommand   => _vm.RedoCommand;
    public ICommand? SaveCommand   { get; } = null;   // wired by host
    public ICommand? CopyCommand   { get; } = null;
    public ICommand? CutCommand    { get; } = null;
    public ICommand? PasteCommand  { get; } = null;
    public ICommand? DeleteCommand => _vm.DeleteCommand;
    public ICommand? SelectAllCommand { get; } = null;

    public event EventHandler?         ModifiedChanged;
    public event EventHandler?         CanUndoChanged;
    public event EventHandler?         CanRedoChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<string>? OutputMessage;
    public event EventHandler?         SelectionChanged;
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;

    public void Undo()                  => _vm.Undo();
    public void Redo()                  => _vm.Redo();
    public void Copy()                  { }
    public void Cut()                   { }
    public void Paste()                 { }
    public void Delete()                => _vm.DeleteCommand.Execute(null);
    public void SelectAll()             { }
    public void CancelOperation()       { }

    public void Save()
    {
        if (string.IsNullOrEmpty(_filePath)) return;
        _ = SaveAsync();
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_document is null || string.IsNullOrEmpty(_filePath)) return;
        IsBusy = true;
        try
        {
            var entries = _vm.GetCurrentEntries();
            await ResxDocumentSerializer.SaveAsync(_document, entries, _filePath, ct);
            _vm.MarkAllSaved();
            UpdateTitle();
            OutputMessage?.Invoke(this, $"Saved: {_filePath}");

            // Notify EventBus via the ResxDesignerGeneratedEvent channel (reuse host wiring)
            PublishSaveEvent(_filePath);
        }
        finally { IsBusy = false; }
    }

    public async Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        _filePath    = filePath;
        _vm.FilePath = filePath;
        await SaveAsync(ct);
    }

    public void Close()
    {
        if (_buffer is not null) DetachBuffer();
    }

    // -----------------------------------------------------------------------
    // IOpenableDocument
    // -----------------------------------------------------------------------

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _filePath    = filePath;
        _vm.FilePath = filePath;
        IsBusy = true;
        try
        {
            _document = await ResxDocumentParser.ParseAsync(filePath, ct);
            _vm.LoadDocument(_document);
            await RunValidationAsync();

            // Locale discovery + publish event
            var localeSet = ResxLocaleDiscovery.Discover(filePath);
            if (localeSet.HasVariants)
                PublishLocaleDiscoveredEvent(localeSet);

            UpdateTitle();
        }
        catch (Exception ex)
        {
            _diagnostics = [new DiagnosticEntry(DiagnosticSeverity.Error, "RESX000",
                $"Failed to parse file: {ex.Message}",
                FileName: Path.GetFileName(filePath), FilePath: filePath)];
            DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            OutputMessage?.Invoke(this, $"[RESX] Error opening {filePath}: {ex.Message}");
        }
        finally { IsBusy = false; }
    }

    // -----------------------------------------------------------------------
    // ISearchTarget
    // -----------------------------------------------------------------------

    public SearchBarCapabilities Capabilities => SearchBarCapabilities.CaseSensitive
                                               | SearchBarCapabilities.Wildcard
                                               | SearchBarCapabilities.Replace;

    public int MatchCount        { get; private set; }
    public int CurrentMatchIndex { get; private set; }

    public event EventHandler? SearchResultsChanged;

    public void Find(string query, SearchTargetOptions options = default)
    {
        var caseSensitive = options.HasFlag(SearchTargetOptions.CaseSensitive);
        MatchCount = _search.Refresh(_vm.Entries, query, caseSensitive, false);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        SelectSearchMatch(_search.FindNext());
    }

    public void FindNext()     => SelectSearchMatch(_search.FindNext());
    public void FindPrevious() => SelectSearchMatch(_search.FindPrevious());
    public void ClearSearch()  { MatchCount = 0; SearchResultsChanged?.Invoke(this, EventArgs.Empty); }

    public void Replace(string replacement)
    {
        _search.Replace(_vm.SelectedEntry, replacement);
    }

    public void ReplaceAll(string replacement)
    {
        var count = _search.ReplaceAll(_vm.Entries, replacement);
        StatusMessage?.Invoke(this, $"Replaced {count} occurrence(s).");
        _ = RunValidationAsync();
    }

    public UIElement? GetCustomFiltersContent() => null;

    private void SelectSearchMatch(ResxEntryViewModel? match)
    {
        if (match is null) return;
        _vm.SelectedEntry = match;
        EntriesGrid.ScrollIntoView(match);
    }

    // -----------------------------------------------------------------------
    // IEditorToolbarContributor
    // -----------------------------------------------------------------------

    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; } = [];

    private void InitToolbarItems()
    {
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE710",   // Add — Segoe MDL2
            Label   = "Add",
            Tooltip = "Add new entry (Ins)",
            Command = new ViewModels.RelayCommand(_ => ExecuteAddAndNavigate(), _ => !_vm.IsReadOnly)
        });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE74D",   // Delete
            Label   = "Delete",
            Tooltip = "Delete selected entry (Del)",
            Command = _vm.DeleteCommand
        });
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE10E",   // Undo
            Tooltip = "Undo (Ctrl+Z)",
            Command = _vm.UndoCommand
        });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE10D",   // Redo
            Tooltip = "Redo (Ctrl+Y)",
            Command = _vm.RedoCommand
        });
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });

        // Export dropdown
        var exportDrop = new ObservableCollection<EditorToolbarItem>
        {
            new() { Label = "Export → CSV",   Command = new ViewModels.RelayCommand(_ => _ = ExportAsync(ResxExportFormat.Csv)) },
            new() { Label = "Export → JSON",  Command = new ViewModels.RelayCommand(_ => _ = ExportAsync(ResxExportFormat.Json)) },
            new() { Label = "Export → XLIFF", Command = new ViewModels.RelayCommand(_ => _ = ExportAsync(ResxExportFormat.Xliff)) },
            new() { Label = "Export → Android strings.xml", Command = new ViewModels.RelayCommand(_ => _ = ExportAsync(ResxExportFormat.AndroidStrings)) },
            new() { Label = "Export → iOS .strings",        Command = new ViewModels.RelayCommand(_ => _ = ExportAsync(ResxExportFormat.IosStrings)) },
        };
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon = "\uE8A5", Label = "Export", Tooltip = "Export entries…",
            DropdownItems = exportDrop
        });

        // Generate Designer.cs
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE943",   // Code
            Label   = "Designer",
            Tooltip = "Generate Designer.cs (Ctrl+Shift+G)",
            Command = new ViewModels.RelayCommand(_ => _ = GenerateDesignerAsync())
        });
    }

    // -----------------------------------------------------------------------
    // IStatusBarContributor
    // -----------------------------------------------------------------------

    public ObservableCollection<StatusBarItem> StatusBarItems { get; } = [];

    private StatusBarItem? _sbEntries;
    private StatusBarItem? _sbType;

    private void InitStatusBarItems()
    {
        _sbEntries = new StatusBarItem { Label = "Entries", Value = "0" };
        _sbType    = new StatusBarItem { Label = "Type",    Value = "All" };
        StatusBarItems.Add(_sbEntries);
        StatusBarItems.Add(_sbType);
    }

    public void RefreshStatusBarItems()
    {
        if (_sbEntries is not null) _sbEntries.Value = _vm.Entries.Count.ToString();
        if (_sbType    is not null) _sbType.Value    = _vm.EntryTypeFilter?.ToString() ?? "All";
    }

    // -----------------------------------------------------------------------
    // IPropertyProviderSource
    // -----------------------------------------------------------------------

    public IPropertyProvider? GetPropertyProvider() => null;  // Future: ResxPropertyProvider

    // -----------------------------------------------------------------------
    // IBufferAwareEditor
    // -----------------------------------------------------------------------

    public void AttachBuffer(IDocumentBuffer buffer)
    {
        _buffer = buffer;
        // Push current content to buffer
        if (_document is not null)
        {
            _suppressBufferSync = true;
            try
            {
                var xml = ResxDocumentSerializer.Serialize(_document, _vm.GetCurrentEntries());
                _buffer.SetText(xml, this);
            }
            finally { _suppressBufferSync = false; }
        }
        buffer.Changed += OnBufferChanged;
    }

    public void DetachBuffer()
    {
        if (_buffer is not null)
        {
            _buffer.Changed -= OnBufferChanged;
            _buffer = null;
        }
    }

    private void OnBufferChanged(object? sender, DocumentBufferChangedEventArgs e)
    {
        if (_suppressBufferSync) return;
        if (e.Source == this)    return;

        Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                var doc = ResxDocumentParser.ParseXml(_filePath, e.NewText);
                _suppressBufferSync = true;
                _document = doc;
                _vm.LoadDocument(doc);
                await RunValidationAsync();
            }
            catch { /* malformed XML from peer editor — ignore */ }
            finally { _suppressBufferSync = false; }
        });
    }

    // -----------------------------------------------------------------------
    // IEditorPersistable
    // -----------------------------------------------------------------------

    public EditorConfigDto GetEditorConfig()
    {
        var state = new ResxEditorState
        {
            ScrollOffset  = GetGridScrollOffset(),
            SelectedKey   = _vm.SelectedEntry?.Name,
            TypeFilter    = _vm.EntryTypeFilter?.ToString(),
            SearchText    = _vm.SearchText,
            SortColumn    = "Name",
            SortDirection = "Ascending",
            ColumnWidths  = GetColumnWidths()
        };

        var config = new EditorConfigDto();
        (config.Extra ??= [])["ResxState"] = JsonSerializer.Serialize(state);
        return config;
    }

    public void ApplyEditorConfig(EditorConfigDto config)
    {
        if (config.Extra?.TryGetValue("ResxState", out var json) != true) return;
        try
        {
            var state = JsonSerializer.Deserialize<ResxEditorState>(json);
            if (state is null) return;

            _vm.SearchText = state.SearchText ?? string.Empty;

            if (state.TypeFilter is not null && Enum.TryParse<ResxEntryType>(state.TypeFilter, out var t))
                _vm.EntryTypeFilter = t;

            if (state.ColumnWidths is not null)
                ApplyColumnWidths(state.ColumnWidths);

            if (state.SelectedKey is not null)
            {
                var target = _vm.Entries.FirstOrDefault(e => e.Name == state.SelectedKey);
                if (target is not null) { _vm.SelectedEntry = target; EntriesGrid.ScrollIntoView(target); }
            }
        }
        catch { /* ignore stale state */ }
    }

    public byte[]?              GetUnsavedModifications()          => null;
    public void                 ApplyUnsavedModifications(byte[] _) { }
    public ChangesetSnapshot    GetChangesetSnapshot()             => ChangesetSnapshot.Empty;
    public void                 ApplyChangeset(ChangesetDto _)     { }
    public void                 MarkChangesetSaved()               { }
    public IReadOnlyList<BookmarkDto>? GetBookmarks()              => null;
    public void                 ApplyBookmarks(IReadOnlyList<BookmarkDto> _) { }

    // -----------------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------------

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { _vm.Undo(); e.Handled = true; }
        if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) { _vm.Redo(); e.Handled = true; }
        if (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None) { ExecuteAddAndNavigate(); e.Handled = true; }
        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None && _vm.SelectedEntry is not null) { _vm.DeleteCommand.Execute(null); e.Handled = true; }
        if (e.Key == Key.G && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { _ = GenerateDesignerAsync(); e.Handled = true; }
    }

    private void EntriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => SelectionChanged?.Invoke(this, EventArgs.Empty);

    private void EntriesGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;
        if (e.Row.Item is not ResxEntryViewModel vm) return;

        var colHeader = e.Column.Header?.ToString() ?? string.Empty;

        if (colHeader is "Name" or "Value")
        {
            if (e.EditingElement is not TextBox tb) return;
            var newVal = tb.Text;
            var field  = colHeader == "Name" ? nameof(ResxEntryViewModel.Name) : nameof(ResxEntryViewModel.Value);
            var oldVal = field == nameof(ResxEntryViewModel.Name) ? vm.SourceEntry.Name : vm.SourceEntry.Value;
            if (newVal != oldVal)
                _vm.Push(new ResxEditEntryAction(vm, field, oldVal, newVal));
        }
        else if (colHeader == "Comment")
        {
            var tb = e.EditingElement as TextBox
                  ?? FindVisualChild<TextBox>(e.EditingElement);
            if (tb is null) return;
            var newComment = tb.Text;
            var oldComment = vm.Comment;
            if (newComment == oldComment) return;
            vm.SetCommentSilent(oldComment);
            _vm.Push(new ResxEditEntryAction(vm, nameof(ResxEntryViewModel.Comment),
                                             oldComment, newComment));
        }
    }

    private void ExecuteAddAndNavigate()
    {
        _vm.AddCommand.Execute(null);
        var entry = _vm.SelectedEntry;
        if (entry is null) return;
        EntriesGrid.ScrollIntoView(entry);
        EntriesGrid.Dispatcher.InvokeAsync(() =>
        {
            var row = EntriesGrid.ItemContainerGenerator.ContainerFromItem(entry) as DataGridRow;
            if (row is null) return;
            EntriesGrid.CurrentCell = new DataGridCellInfo(entry, EntriesGrid.Columns[0]);
            EntriesGrid.BeginEdit();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null) return null;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result is not null) return result;
        }
        return null;
    }

    private void EntriesGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        // Tooltip with validation message
        if (e.Row.Item is ResxEntryViewModel vm && vm.HasValidationError)
            e.Row.ToolTip = vm.ValidationMessage;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private string BuildTitle()
    {
        var name = string.IsNullOrEmpty(_filePath) ? "Untitled.resx" : Path.GetFileName(_filePath);
        return _vm.IsDirty ? $"{name} *" : name;
    }

    private void UpdateTitle() => TitleChanged?.Invoke(this, BuildTitle());

    private async Task RunValidationAsync()
    {
        if (_vm.IsLoading) return;
        var entries = _vm.GetCurrentEntries();
        _diagnostics = ResxValidationService.Validate(entries, _filePath);

        // Flag rows with errors
        var duplicateKeys = _diagnostics
            .Where(d => d.Code == "RESX002")
            .Select(d => d.Tag as string)
            .Where(k => k is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var vm in _vm.Entries)
        {
            vm.HasValidationError = duplicateKeys.Contains(vm.Name)
                || _diagnostics.Any(d => d.Line.HasValue
                    && d.Line.Value - 1 == _vm.Entries.IndexOf(vm));
        }

        DiagnosticsChanged?.Invoke(this, EventArgs.Empty);

        // Push XML to buffer
        if (_buffer is not null && _document is not null)
        {
            _suppressBufferSync = true;
            try
            {
                var xml = ResxDocumentSerializer.Serialize(_document, entries);
                _buffer.SetText(xml, this);
            }
            finally { _suppressBufferSync = false; }
        }

        // Publish entry-changed events for each dirty entry
        await Task.CompletedTask;
    }

    private async Task ExportAsync(ResxExportFormat format)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = format switch
            {
                ResxExportFormat.Csv            => "CSV files|*.csv",
                ResxExportFormat.Json           => "JSON files|*.json",
                ResxExportFormat.Xliff          => "XLIFF files|*.xlf",
                ResxExportFormat.AndroidStrings => "Android strings|*.xml",
                ResxExportFormat.IosStrings     => "iOS strings|*.strings",
                _                               => "All files|*.*"
            }
        };
        if (dlg.ShowDialog() != true) return;
        await ResxExportService.ExportAsync(_vm.GetCurrentEntries(), format, dlg.FileName);
        StatusMessage?.Invoke(this, $"Exported to {dlg.FileName}");
    }

    private async Task GenerateDesignerAsync()
    {
        if (string.IsNullOrEmpty(_filePath)) return;

        var dir       = Path.GetDirectoryName(_filePath) ?? string.Empty;
        var baseName  = Path.GetFileNameWithoutExtension(_filePath);
        var outPath   = Path.Combine(dir, baseName + ".Designer.cs");

        var ns  = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "Resources";
        var opt = new ResxDesignerOptions(ns, baseName, IsPublic: false);
        await ResxDesignerGenerator.GenerateAndSaveAsync(_vm.GetCurrentEntries(), opt, outPath);
        StatusMessage?.Invoke(this, $"Designer.cs generated: {outPath}");

        // EventBus publish — handled by factory's RegisterCommands wiring
        PublishDesignerGeneratedEvent(_filePath, outPath);
    }

    private double GetGridScrollOffset()
    {
        if (EntriesGrid.Template?.FindName("DG_ScrollViewer", EntriesGrid) is ScrollViewer sv)
            return sv.VerticalOffset;
        return 0;
    }

    private Dictionary<string, double> GetColumnWidths()
    {
        var result = new Dictionary<string, double>();
        foreach (var col in EntriesGrid.Columns)
            if (col.Header is string header)
                result[header] = col.ActualWidth;
        return result;
    }

    private void ApplyColumnWidths(Dictionary<string, double> widths)
    {
        foreach (var col in EntriesGrid.Columns)
            if (col.Header is string header && widths.TryGetValue(header, out var w) && w > 0)
                col.Width = new DataGridLength(w);
    }

    // -- EventBus publish stubs (wired by ResxEditorFactory) ----------------

    /// <summary>Called by factory after registration to set the publish delegate.</summary>
    internal Action<string>? OnSavePublish             { get; set; }
    internal Action<ResxLocaleSet>? OnLocalePublish    { get; set; }
    internal Action<string, string>? OnDesignerPublish { get; set; }

    private void PublishSaveEvent(string filePath)
        => OnSavePublish?.Invoke(filePath);

    private void PublishLocaleDiscoveredEvent(ResxLocaleSet set)
        => OnLocalePublish?.Invoke(set);

    private void PublishDesignerGeneratedEvent(string filePath, string designerPath)
        => OnDesignerPublish?.Invoke(filePath, designerPath);
}

// -- Serializable state for IEditorPersistable --------------------------------

internal sealed class ResxEditorState
{
    public double?                    ScrollOffset  { get; set; }
    public string?                    SelectedKey   { get; set; }
    public string?                    TypeFilter    { get; set; }
    public string?                    SearchText    { get; set; }
    public string?                    SortColumn    { get; set; }
    public string?                    SortDirection { get; set; }
    public Dictionary<string, double>? ColumnWidths { get; set; }
}
