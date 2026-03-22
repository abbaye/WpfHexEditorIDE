// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/ClassDiagramSplitHost.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Main host Grid for the class diagram editor. Implements
//     IDocumentEditor, IOpenableDocument, IEditorPersistable,
//     IStatusBarContributor, IEditorToolbarContributor.
//     Provides bidirectional DSL↔canvas sync, toolbar, status bar,
//     keyboard shortcuts, and view-mode/split-layout switching.
//
// Architecture Notes:
//     Pattern: Facade + Mediator.
//     UpdateGridLayout() is the single source of truth for all layout changes.
//     DSL pane is a plain TextBox in a Border (upgradeable to CodeEditor).
//     300ms debounce: canvas→code (ClassToSyncService).
//     500ms debounce: code→canvas (ClassToSyncService).
//     Undo max 200 entries via ClassDiagramUndoManager.
//     All WPF brushes use SetResourceReference — no hardcoded colors.
//     Theme tokens: CD_* (see Themes/Generic.xaml for full list).
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.Parser;
using WpfHexEditor.Editor.ClassDiagram.Core.Serializer;
using WpfHexEditor.Editor.ClassDiagram.Services;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;
using WpfHexEditor.Editor.Core;
using EditorStatusBarItem = WpfHexEditor.Editor.Core.StatusBarItem;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------

public enum CdViewMode   { DslOnly, Split, DiagramOnly }
public enum CdSplitLayout{ SplitRight, SplitLeft, SplitBottom, SplitTop }

// ---------------------------------------------------------------------------
// Main host
// ---------------------------------------------------------------------------

/// <summary>
/// Top-level host control for the class diagram editor.
/// Implements all IDE editor contracts and manages layout, sync, undo, and export.
/// </summary>
public sealed class ClassDiagramSplitHost : Grid,
    IDocumentEditor,
    IOpenableDocument,
    IEditorPersistable,
    IStatusBarContributor,
    IEditorToolbarContributor
{
    // ---------------------------------------------------------------------------
    // Core controls
    // ---------------------------------------------------------------------------

    private readonly DiagramCanvas _canvas;
    private readonly ZoomPanCanvas _zoomPan;
    private readonly TextBox       _dslTextBox;
    private readonly Border        _codeHost;
    private readonly Border        _diagramHost;
    private readonly GridSplitter  _splitter;
    private readonly Border        _toolbarContainer;
    private readonly StackPanel    _toolbarPanel;

    // ---------------------------------------------------------------------------
    // View state
    // ---------------------------------------------------------------------------

    private CdViewMode    _viewMode = CdViewMode.Split;
    private CdSplitLayout _layout   = CdSplitLayout.SplitRight;

    // ---------------------------------------------------------------------------
    // Domain
    // ---------------------------------------------------------------------------

    private DiagramDocument _document = new();
    private string? _filePath;
    private bool _isDirty;
    private bool _suppressCodeSync;  // Prevents re-entrant sync loops

    // ---------------------------------------------------------------------------
    // Services
    // ---------------------------------------------------------------------------

    private readonly ClassDiagramUndoManager   _undoManager;
    private readonly ClassSnapEngineService    _snap;
    private readonly ClassInteractionService   _interactionService;
    private readonly ClassToSyncService        _syncService;
    private readonly ClassDiagramExportService _exportService;

    // ---------------------------------------------------------------------------
    // Toolbar items
    // ---------------------------------------------------------------------------

    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; } = [];
    private EditorToolbarItem? _podUndoItem;
    private EditorToolbarItem? _podRedoItem;

    // ---------------------------------------------------------------------------
    // Status bar items
    // ---------------------------------------------------------------------------

    public ObservableCollection<EditorStatusBarItem> StatusBarItems { get; } = [];
    private EditorStatusBarItem? _sbMode;
    private EditorStatusBarItem? _sbZoom;
    private EditorStatusBarItem? _sbSelected;
    private EditorStatusBarItem? _sbStats;
    private EditorStatusBarItem? _sbLayout;

    // ---------------------------------------------------------------------------
    // IDocumentEditor events
    // ---------------------------------------------------------------------------

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

    // ---------------------------------------------------------------------------
    // Plugin-facing events
    // ---------------------------------------------------------------------------

    public event EventHandler<ClassNode?>? SelectedClassChanged;
    public event EventHandler?            DiagramChanged;

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    public ClassDiagramSplitHost()
    {
        // Services
        _undoManager        = new ClassDiagramUndoManager();
        _snap               = new ClassSnapEngineService();
        _interactionService = new ClassInteractionService(_undoManager, _snap);
        _syncService        = new ClassToSyncService();
        _exportService      = new ClassDiagramExportService();

        // Canvas
        _canvas  = new DiagramCanvas();
        _zoomPan = new ZoomPanCanvas();
        _zoomPan.Children.Add(_canvas);

        _canvas.SelectedClassChanged += OnCanvasSelectedClassChanged;
        _canvas.HoveredClassChanged  += (_, _) => { };

        // DSL pane
        _dslTextBox = new TextBox();
        _dslTextBox.SetResourceReference(TextBox.BackgroundProperty, "CD_DslEditorBackground");
        _dslTextBox.SetResourceReference(TextBox.ForegroundProperty, "CD_DslEditorForeground");
        _dslTextBox.SetResourceReference(TextBox.CaretBrushProperty, "CD_DslEditorForeground");
        _dslTextBox.FontFamily              = new FontFamily("Consolas, Courier New");
        _dslTextBox.FontSize                = 13;
        _dslTextBox.AcceptsReturn           = true;
        _dslTextBox.AcceptsTab              = true;
        _dslTextBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _dslTextBox.VerticalScrollBarVisibility   = ScrollBarVisibility.Auto;
        _dslTextBox.BorderThickness         = new Thickness(0);
        _dslTextBox.Padding                 = new Thickness(8, 6, 8, 6);
        _dslTextBox.TextChanged            += OnDslTextChanged;

        _codeHost = new Border { Child = _dslTextBox };
        _codeHost.SetResourceReference(Border.BackgroundProperty, "CD_DslEditorBackground");

        _diagramHost = new Border { Child = _zoomPan };
        _diagramHost.SetResourceReference(Border.BackgroundProperty, "CD_CanvasBackground");

        // GridSplitter
        _splitter = new GridSplitter
        {
            ShowsPreview       = false,
            ResizeBehavior     = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
        };
        _splitter.SetResourceReference(BackgroundProperty, "DockToolBarBorderBrush");

        // Toolbar
        _toolbarPanel     = new StackPanel { Orientation = Orientation.Horizontal };
        _toolbarContainer = new Border { Child = _toolbarPanel, Padding = new Thickness(4, 2, 4, 2) };
        _toolbarContainer.SetResourceReference(Border.BackgroundProperty, "CD_ToolbarBackground");
        _toolbarContainer.SetResourceReference(Border.BorderBrushProperty, "DockToolBarBorderBrush");
        _toolbarContainer.BorderThickness = new Thickness(0, 0, 0, 1);

        // Wire services
        _syncService.CodeUpdateReady   += OnCodeUpdateReady;
        _syncService.CanvasUpdateReady += OnCanvasUpdateReady;
        _undoManager.HistoryChanged    += OnHistoryChanged;

        // Keyboard
        PreviewKeyDown += OnGridKeyDown;

        // Build toolbar + status bar
        BuildToolbarItems();
        BuildStatusBar();

        // Initial layout
        UpdateGridLayout();
    }

    // ---------------------------------------------------------------------------
    // IDocumentEditor
    // ---------------------------------------------------------------------------

    public bool   IsDirty    => _isDirty;
    public bool   CanUndo    => _undoManager.CanUndo;
    public bool   CanRedo    => _undoManager.CanRedo;
    public bool   IsReadOnly { get; set; }
    public int    UndoCount  => _undoManager.UndoCount;
    public int    RedoCount  => _undoManager.RedoCount;
    public bool   IsBusy     => false;

    public string Title
    {
        get
        {
            string name = string.IsNullOrEmpty(_filePath)
                ? "Untitled.classdiagram"
                : Path.GetFileName(_filePath);
            return _isDirty ? $"{name} *" : name;
        }
    }

    public ICommand? UndoCommand      => new RelayCommand(Undo,    () => CanUndo);
    public ICommand? RedoCommand      => new RelayCommand(Redo,    () => CanRedo);
    public ICommand? SaveCommand      => new RelayCommand(() => Save());
    public ICommand? CopyCommand      => null;
    public ICommand? CutCommand       => null;
    public ICommand? PasteCommand     => null;
    public ICommand? DeleteCommand    => null;
    public ICommand? SelectAllCommand => null;

    public void Undo()
    {
        _undoManager.Undo();
        RaiseCanUndoRedo();
    }

    public void Redo()
    {
        _undoManager.Redo();
        RaiseCanUndoRedo();
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_filePath)) return;
        string dsl = ClassDiagramSerializer.Serialize(_document);
        File.WriteAllText(_filePath, dsl);
        SetDirty(false);
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_filePath)) return;
        string dsl = ClassDiagramSerializer.Serialize(_document);
        await File.WriteAllTextAsync(_filePath, dsl, ct).ConfigureAwait(false);
        Dispatcher.Invoke(() => SetDirty(false));
    }

    public async Task SaveAsAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;
        await SaveAsync(ct).ConfigureAwait(false);
        Dispatcher.Invoke(() => TitleChanged?.Invoke(this, Title));
    }

    public void Copy()       { }
    public void Cut()        { }
    public void Paste()      { }
    public void Delete()     { DeleteSelected(); }
    public void SelectAll()  { }
    public void CancelOperation() { }

    public void Close()
    {
        _syncService.Stop();
        _undoManager.HistoryChanged -= OnHistoryChanged;
    }

    // ---------------------------------------------------------------------------
    // IOpenableDocument
    // ---------------------------------------------------------------------------

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _filePath = filePath;

        string dsl = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);

        Dispatcher.Invoke(() =>
        {
            var result = ClassDiagramParser.Parse(dsl);
            _document  = result.Document;
            _document.FilePath = filePath;

            _suppressCodeSync = true;
            _dslTextBox.Text  = dsl;
            _suppressCodeSync = false;

            _canvas.ApplyDocument(_document);
            _undoManager.Clear();
            SetDirty(false);
            TitleChanged?.Invoke(this, Title);
            UpdateStatusBar();
            DiagramChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    // ---------------------------------------------------------------------------
    // IEditorPersistable
    // ---------------------------------------------------------------------------

    public EditorConfigDto GetEditorConfig()
    {
        var dto = new EditorConfigDto();
        dto.Extra ??= [];
        dto.Extra["cd.viewMode"] = _viewMode.ToString();
        dto.Extra["cd.layout"]   = _layout.ToString();
        dto.Extra["cd.zoom"]     = _zoomPan.ZoomFactor.ToString("F2");
        return dto;
    }

    public void ApplyEditorConfig(EditorConfigDto config)
    {
        if (config.Extra is null) return;

        if (config.Extra.TryGetValue("cd.viewMode", out string? vmStr)
         && Enum.TryParse(vmStr, out CdViewMode vm))
            SetViewMode(vm);

        if (config.Extra.TryGetValue("cd.layout", out string? layoutStr)
         && Enum.TryParse(layoutStr, out CdSplitLayout layout))
            SetSplitLayout(layout);

        if (config.Extra.TryGetValue("cd.zoom", out string? zoomStr)
         && double.TryParse(zoomStr, out double zoom))
            _zoomPan.ZoomFactor = zoom;
    }

    public byte[]? GetUnsavedModifications() => null;
    public void ApplyUnsavedModifications(byte[] data) { }
    public ChangesetSnapshot GetChangesetSnapshot() => ChangesetSnapshot.Empty;
    public void ApplyChangeset(ChangesetDto changeset) { }
    public void MarkChangesetSaved() { }
    public IReadOnlyList<BookmarkDto>? GetBookmarks() => null;
    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks) { }

    // ---------------------------------------------------------------------------
    // IStatusBarContributor
    // ---------------------------------------------------------------------------

    public void RefreshStatusBarItems() => UpdateStatusBar();

    // ---------------------------------------------------------------------------
    // IEditorToolbarContributor — already exposed via ToolbarItems property
    // (declared above, populated by BuildToolbarItems)
    // ---------------------------------------------------------------------------

    // ---------------------------------------------------------------------------
    // Public API for plugin wiring
    // ---------------------------------------------------------------------------

    public DiagramDocument Document       => _document;
    public ClassDiagramUndoManager UndoManager => _undoManager;

    /// <summary>
    /// Loads a pre-analyzed <see cref="DiagramDocument"/> directly into the editor.
    /// No file I/O — called by the plugin when opening from source file/folder/solution analysis.
    /// </summary>
    public void LoadDocument(DiagramDocument doc, string title)
    {
        _document  = doc;
        string dsl = ClassDiagramSerializer.Serialize(doc);

        _suppressCodeSync = true;
        _dslTextBox.Text  = dsl;
        _suppressCodeSync = false;

        _canvas.ApplyDocument(_document);
        _undoManager.Clear();
        SetDirty(false);
        TitleChanged?.Invoke(this, title);
        UpdateStatusBar();
        DiagramChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------------------------------------------------
    // View mode / layout
    // ---------------------------------------------------------------------------

    public void SetViewMode(CdViewMode mode)
    {
        _viewMode = mode;
        UpdateGridLayout();
        UpdateModeStatusBar();
    }

    public void SetSplitLayout(CdSplitLayout layout)
    {
        _layout = layout;
        UpdateGridLayout();
        UpdateLayoutStatusBar();
    }

    // ---------------------------------------------------------------------------
    // UpdateGridLayout — single source of truth for all layout changes
    // ---------------------------------------------------------------------------

    private void UpdateGridLayout()
    {
        RowDefinitions.Clear();
        ColumnDefinitions.Clear();
        Children.Clear();

        switch (_viewMode)
        {
            case CdViewMode.DslOnly:
                BuildDslOnlyLayout();
                break;

            case CdViewMode.DiagramOnly:
                BuildDiagramOnlyLayout();
                break;

            case CdViewMode.Split:
                BuildSplitLayout();
                break;
        }
    }

    private void BuildDslOnlyLayout()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        AddToGrid(_toolbarContainer, 0, 0);
        AddToGrid(_codeHost,         1, 0);
        _codeHost.Visibility    = Visibility.Visible;
        _diagramHost.Visibility = Visibility.Collapsed;
        _splitter.Visibility    = Visibility.Collapsed;
    }

    private void BuildDiagramOnlyLayout()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        AddToGrid(_toolbarContainer, 0, 0);
        AddToGrid(_diagramHost,      1, 0);
        _codeHost.Visibility    = Visibility.Collapsed;
        _diagramHost.Visibility = Visibility.Visible;
        _splitter.Visibility    = Visibility.Collapsed;
    }

    private void BuildSplitLayout()
    {
        _codeHost.Visibility    = Visibility.Visible;
        _diagramHost.Visibility = Visibility.Visible;
        _splitter.Visibility    = Visibility.Visible;

        switch (_layout)
        {
            case CdSplitLayout.SplitRight:
                // Toolbar row + [code | splitter | diagram]
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                SetColumnSpan(_toolbarContainer, 3);
                AddToGrid(_toolbarContainer, 0, 0);
                AddToGrid(_codeHost,         1, 0);
                AddToGrid(_splitter,         1, 1);
                AddToGrid(_diagramHost,      1, 2);

                _splitter.ResizeDirection   = GridResizeDirection.Columns;
                _splitter.Width             = 5;
                _splitter.Height            = double.NaN;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
                break;

            case CdSplitLayout.SplitLeft:
                // Toolbar row + [diagram | splitter | code]
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                SetColumnSpan(_toolbarContainer, 3);
                AddToGrid(_toolbarContainer, 0, 0);
                AddToGrid(_diagramHost,      1, 0);
                AddToGrid(_splitter,         1, 1);
                AddToGrid(_codeHost,         1, 2);

                _splitter.ResizeDirection   = GridResizeDirection.Columns;
                _splitter.Width             = 5;
                _splitter.Height            = double.NaN;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
                break;

            case CdSplitLayout.SplitBottom:
                // Toolbar row + code row + splitter + diagram row
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                AddToGrid(_toolbarContainer, 0, 0);
                AddToGrid(_codeHost,         1, 0);
                AddToGrid(_splitter,         2, 0);
                AddToGrid(_diagramHost,      3, 0);

                _splitter.ResizeDirection   = GridResizeDirection.Rows;
                _splitter.Width             = double.NaN;
                _splitter.Height            = 5;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
                break;

            case CdSplitLayout.SplitTop:
                // Toolbar row + diagram row + splitter + code row
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                AddToGrid(_toolbarContainer, 0, 0);
                AddToGrid(_diagramHost,      1, 0);
                AddToGrid(_splitter,         2, 0);
                AddToGrid(_codeHost,         3, 0);

                _splitter.ResizeDirection   = GridResizeDirection.Rows;
                _splitter.Width             = double.NaN;
                _splitter.Height            = 5;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
                break;
        }
    }

    // ---------------------------------------------------------------------------
    // Grid helpers
    // ---------------------------------------------------------------------------

    private void AddToGrid(UIElement element, int row, int col)
    {
        SetRow(element, row);
        SetColumn(element, col);
        Children.Add(element);
    }

    // ---------------------------------------------------------------------------
    // Toolbar construction
    // ---------------------------------------------------------------------------

    private void BuildToolbarItems()
    {
        ToolbarItems.Clear();

        // View mode toggles
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE8A5",
            Label   = "DSL",
            Tooltip = "Show DSL only (Ctrl+1)",
            Command = new RelayCommand(() => SetViewMode(CdViewMode.DslOnly))
        });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE8A5",
            Label   = "Split",
            Tooltip = "Split view (Ctrl+2)",
            Command = new RelayCommand(() => SetViewMode(CdViewMode.Split))
        });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE92F",
            Label   = "Diagram",
            Tooltip = "Show diagram only (Ctrl+3)",
            Command = new RelayCommand(() => SetViewMode(CdViewMode.DiagramOnly))
        });

        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });

        // Undo / Redo
        _podUndoItem = new EditorToolbarItem
        {
            Icon    = "\uE7A7",
            Tooltip = "Undo",
            Command = new RelayCommand(Undo, () => CanUndo)
        };
        _podRedoItem = new EditorToolbarItem
        {
            Icon    = "\uE7A6",
            Tooltip = "Redo",
            Command = new RelayCommand(Redo, () => CanRedo)
        };
        ToolbarItems.Add(_podUndoItem);
        ToolbarItems.Add(_podRedoItem);

        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });

        // Export dropdown
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE898",
            Label   = "Export",
            Tooltip = "Export diagram",
            DropdownItems = new ObservableCollection<EditorToolbarItem>
            {
                new() { Icon = "\uEB9F", Label = "Export PNG",     Command = new RelayCommand(() => ExportPng()) },
                new() { Icon = "\uE781", Label = "Export SVG",     Command = new RelayCommand(() => ExportSvg()) },
                new() { Icon = "\uE8A5", Label = "Export C#",      Command = new RelayCommand(() => ExportCSharp()) },
                new() { Icon = "\uE8A5", Label = "Export Mermaid", Command = new RelayCommand(() => ExportMermaid()) }
            }
        });

        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });

        // Zoom controls
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE8A3",
            Tooltip = "Zoom in (Ctrl+=)",
            Command = new RelayCommand(() => _zoomPan.ZoomFactor += 0.1)
        });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE71F",
            Tooltip = "Zoom out (Ctrl+-)",
            Command = new RelayCommand(() => _zoomPan.ZoomFactor -= 0.1)
        });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE904",
            Tooltip = "Fit to content (Ctrl+Shift+F)",
            Command = new RelayCommand(() => _zoomPan.FitToContent())
        });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE8A4",
            Tooltip = "Reset zoom (Ctrl+0)",
            Command = new RelayCommand(() => _zoomPan.ZoomFactor = 1.0)
        });

        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });

        // Snap toggle
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon     = "\uE81C",
            Tooltip  = "Snap to grid",
            IsToggle = true,
            IsChecked = true,
            Command  = new RelayCommand(() => _snap.SnapToGrid = !_snap.SnapToGrid)
        });

        // Grid toggle
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon     = "\uE80A",
            Tooltip  = "Show grid",
            IsToggle = true,
            IsChecked = true
        });

        // Layout dropdown
        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon  = "\uE8A9",
            Label = "Layout",
            Tooltip = "Split layout (Ctrl+Shift+L)",
            DropdownItems = new ObservableCollection<EditorToolbarItem>
            {
                new() { Label = "Split Right",  Command = new RelayCommand(() => SetSplitLayout(CdSplitLayout.SplitRight))  },
                new() { Label = "Split Left",   Command = new RelayCommand(() => SetSplitLayout(CdSplitLayout.SplitLeft))   },
                new() { Label = "Split Bottom", Command = new RelayCommand(() => SetSplitLayout(CdSplitLayout.SplitBottom)) },
                new() { Label = "Split Top",    Command = new RelayCommand(() => SetSplitLayout(CdSplitLayout.SplitTop))    }
            }
        });
    }

    // ---------------------------------------------------------------------------
    // Status bar
    // ---------------------------------------------------------------------------

    private void BuildStatusBar()
    {
        _sbMode = new EditorStatusBarItem { Label = "Mode", Value = _viewMode.ToString() };
        _sbZoom = new EditorStatusBarItem { Label = "Zoom", Value = "100%" };
        _sbSelected = new EditorStatusBarItem { Label = "Selected", Value = "None" };
        _sbStats = new EditorStatusBarItem { Label = "Classes", Value = "0" };
        _sbLayout = new EditorStatusBarItem { Label = "Layout", Value = _layout.ToString() };

        StatusBarItems.Add(_sbMode);
        StatusBarItems.Add(_sbZoom);
        StatusBarItems.Add(_sbSelected);
        StatusBarItems.Add(_sbStats);
        StatusBarItems.Add(_sbLayout);
    }

    private void UpdateStatusBar()
    {
        UpdateModeStatusBar();
        UpdateLayoutStatusBar();

        if (_sbZoom is not null)
            _sbZoom.Value = $"{(int)(_zoomPan.ZoomFactor * 100)}%";

        if (_sbStats is not null)
            _sbStats.Value = $"{_document.Classes.Count} classes, {_document.Relationships.Count} relationships";
    }

    private void UpdateModeStatusBar()
    {
        if (_sbMode is not null) _sbMode.Value = _viewMode.ToString();
    }

    private void UpdateLayoutStatusBar()
    {
        if (_sbLayout is not null) _sbLayout.Value = _layout.ToString();
    }

    // ---------------------------------------------------------------------------
    // Keyboard shortcuts
    // ---------------------------------------------------------------------------

    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl      = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift     = (Keyboard.Modifiers & ModifierKeys.Shift)   != 0;
        bool ctrlShift = ctrl && shift;

        if (ctrl && !shift)
        {
            switch (e.Key)
            {
                case Key.Z: Undo();                               e.Handled = true; return;
                case Key.Y: Redo();                               e.Handled = true; return;
                case Key.D1: SetViewMode(CdViewMode.DslOnly);     e.Handled = true; return;
                case Key.D2: SetViewMode(CdViewMode.Split);       e.Handled = true; return;
                case Key.D3: SetViewMode(CdViewMode.DiagramOnly); e.Handled = true; return;
                case Key.OemPlus:  _zoomPan.ZoomFactor += 0.1;   e.Handled = true; return;
                case Key.OemMinus: _zoomPan.ZoomFactor -= 0.1;   e.Handled = true; return;
                case Key.D0: _zoomPan.ZoomFactor = 1.0;          e.Handled = true; return;
                case Key.S: Save();                               e.Handled = true; return;
                case Key.D: DuplicateSelected();                  e.Handled = true; return;
                case Key.A: /* Select all */                      e.Handled = true; return;
                case Key.G: AddNewClass(ClassKind.Class);         e.Handled = true; return;
                case Key.I: AddNewClass(ClassKind.Interface);     e.Handled = true; return;
                case Key.E: AddNewClass(ClassKind.Enum);          e.Handled = true; return;
            }
        }

        if (ctrlShift)
        {
            switch (e.Key)
            {
                case Key.Z: Redo();                               e.Handled = true; return;
                case Key.L: CycleSplitLayout();                   e.Handled = true; return;
                case Key.F: _zoomPan.FitToContent();              e.Handled = true; return;
                case Key.E: ShowExportDialog();                   e.Handled = true; return;
            }
        }

        switch (e.Key)
        {
            case Key.Delete: DeleteSelected();  e.Handled = true; break;
            case Key.F2:     BeginRename();     e.Handled = true; break;
            case Key.Escape: ClearSelection();  e.Handled = true; break;
        }
    }

    // ---------------------------------------------------------------------------
    // Canvas event handlers
    // ---------------------------------------------------------------------------

    private void OnCanvasSelectedClassChanged(object? sender, ClassNode? node)
    {
        if (_sbSelected is not null)
            _sbSelected.Value = node?.Name ?? "None";

        SelectedClassChanged?.Invoke(this, node);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------------------------------------------------
    // DSL sync
    // ---------------------------------------------------------------------------

    private void OnDslTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_suppressCodeSync) return;

        string dsl = _dslTextBox.Text;
        var result = ClassDiagramParser.Parse(dsl);
        _document  = result.Document;

        // Schedule canvas refresh (500ms debounce)
        _syncService.ScheduleCodeToCanvas(_document);
        SetDirty(true);
    }

    private void OnCodeUpdateReady(object? sender, string dsl)
    {
        _suppressCodeSync  = true;
        _dslTextBox.Text   = dsl;
        _suppressCodeSync  = false;
    }

    private void OnCanvasUpdateReady(object? sender, DiagramDocument doc)
    {
        _document = doc;
        _canvas.ApplyDocument(_document);
        UpdateStatusBar();
        DiagramChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------------------------------------------------------------------------
    // Undo/Redo helpers
    // ---------------------------------------------------------------------------

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        RaiseCanUndoRedo();
        UpdateUndoRedoTooltips();
    }

    private void RaiseCanUndoRedo()
    {
        CanUndoChanged?.Invoke(this, EventArgs.Empty);
        CanRedoChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateUndoRedoTooltips()
    {
        if (_podUndoItem is not null)
        {
            _podUndoItem.IsEnabled = CanUndo;
            _podUndoItem.Tooltip   = CanUndo
                ? $"Undo: {_undoManager.Entries[_undoManager.UndoCount - 1].Description}"
                : "Nothing to undo";
        }

        if (_podRedoItem is not null)
        {
            _podRedoItem.IsEnabled = CanRedo;
            _podRedoItem.Tooltip   = CanRedo
                ? $"Redo: {_undoManager.Entries[_undoManager.UndoCount].Description}"
                : "Nothing to redo";
        }
    }

    // ---------------------------------------------------------------------------
    // Dirty state
    // ---------------------------------------------------------------------------

    private void SetDirty(bool dirty)
    {
        if (_isDirty == dirty) return;
        _isDirty = dirty;
        ModifiedChanged?.Invoke(this, EventArgs.Empty);
        TitleChanged?.Invoke(this, Title);
    }

    // ---------------------------------------------------------------------------
    // Actions
    // ---------------------------------------------------------------------------

    private void DeleteSelected()
    {
        if (_canvas.SelectedNode is null) return;

        ClassNode node = _canvas.SelectedNode;
        string beforeDsl = ClassDiagramSerializer.Serialize(_document);

        _document.Classes.Remove(node);
        string afterDsl = ClassDiagramSerializer.Serialize(_document);

        _undoManager.Push(new SnapshotClassDiagramUndoEntry(
            BeforeDsl:  beforeDsl,
            AfterDsl:   afterDsl,
            Description: $"Delete {node.Name}",
            ApplyDsl:   ApplyDslSnapshot));

        _canvas.ApplyDocument(_document);
        SyncDslPane();
        SetDirty(true);
    }

    private void DuplicateSelected()
    {
        if (_canvas.SelectedNode is null) return;

        ClassNode original = _canvas.SelectedNode;
        string beforeDsl   = ClassDiagramSerializer.Serialize(_document);

        var copy = ClassNode.Create(original.Name + "_Copy", original.Kind);
        copy.X = original.X + 20;
        copy.Y = original.Y + 20;
        foreach (var m in original.Members) copy.Members.Add(m);
        _document.Classes.Add(copy);

        string afterDsl = ClassDiagramSerializer.Serialize(_document);
        _undoManager.Push(new SnapshotClassDiagramUndoEntry(beforeDsl, afterDsl,
            $"Duplicate {original.Name}", ApplyDslSnapshot));

        _canvas.ApplyDocument(_document);
        SyncDslPane();
        SetDirty(true);
    }

    private void AddNewClass(ClassKind kind)
    {
        string beforeDsl = ClassDiagramSerializer.Serialize(_document);
        string kindLabel = kind.ToString();
        string name = $"New{kindLabel}{_document.Classes.Count + 1}";

        var node = ClassNode.Create(name, kind);
        node.X = 50 + (_document.Classes.Count % 5) * 210.0;
        node.Y = 50 + (_document.Classes.Count / 5) * 120.0;
        _document.Classes.Add(node);

        string afterDsl = ClassDiagramSerializer.Serialize(_document);
        _undoManager.Push(new SnapshotClassDiagramUndoEntry(beforeDsl, afterDsl,
            $"Add {kindLabel} {name}", ApplyDslSnapshot));

        _canvas.ApplyDocument(_document);
        SyncDslPane();
        SetDirty(true);
    }

    private void BeginRename() { /* Inline rename via ClassBoxControl */ }

    private void ClearSelection()
    {
        // Force deselect by re-applying document (or implement canvas.ClearSelection)
        if (_sbSelected is not null) _sbSelected.Value = "None";
    }

    private void CycleSplitLayout()
    {
        CdSplitLayout next = _layout switch
        {
            CdSplitLayout.SplitRight  => CdSplitLayout.SplitLeft,
            CdSplitLayout.SplitLeft   => CdSplitLayout.SplitBottom,
            CdSplitLayout.SplitBottom => CdSplitLayout.SplitTop,
            CdSplitLayout.SplitTop    => CdSplitLayout.SplitRight,
            _                          => CdSplitLayout.SplitRight
        };
        SetSplitLayout(next);
    }

    // ---------------------------------------------------------------------------
    // Export actions
    // ---------------------------------------------------------------------------

    private void ExportPng()
    {
        _ = _exportService.ExportPngAsync(_document,
            Path.ChangeExtension(_filePath ?? "diagram", ".png"));
    }

    private void ExportSvg()
    {
        _ = _exportService.ExportSvgAsync(_document,
            Path.ChangeExtension(_filePath ?? "diagram", ".svg"));
    }

    private void ExportCSharp()
    {
        _ = _exportService.ExportCSharpAsync(_document,
            Path.ChangeExtension(_filePath ?? "diagram", ".cs"));
    }

    private void ExportMermaid()
    {
        _ = _exportService.ExportMermaidAsync(_document,
            Path.ChangeExtension(_filePath ?? "diagram", ".md"));
    }

    private void ShowExportDialog()
    {
        // Export dialog — launches a simple MessageBox for now
        StatusMessage?.Invoke(this, "Export: use File menu or toolbar Export dropdown.");
    }

    // ---------------------------------------------------------------------------
    // DSL snapshot helper
    // ---------------------------------------------------------------------------

    private void ApplyDslSnapshot(string dsl)
    {
        var result = ClassDiagramParser.Parse(dsl);
        _document  = result.Document;
        _canvas.ApplyDocument(_document);
        SyncDslPane();
        UpdateStatusBar();
    }

    private void SyncDslPane()
    {
        string dsl        = ClassDiagramSerializer.Serialize(_document);
        _suppressCodeSync = true;
        _dslTextBox.Text  = dsl;
        _suppressCodeSync = false;
    }
}

// ---------------------------------------------------------------------------
// Internal RelayCommand — avoids cross-project dependency
// ---------------------------------------------------------------------------

file sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => _execute();

    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
