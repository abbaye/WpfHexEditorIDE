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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfHexEditor.Editor.ClassDiagram.Core.Layout;
using WpfHexEditor.Editor.ClassDiagram.Options;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Core.Parser;
using WpfHexEditor.Editor.ClassDiagram.Core.Serializer;
using WpfHexEditor.Editor.ClassDiagram.Services;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.Commands;
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
    private readonly CodeEditorSplitHost _dslEditor;
    private readonly Border              _codeHost;
    private readonly Border              _diagramHost;
    private readonly Border              _diagramBorder;   // 1px separator + scroll overlay container
    private readonly GridSplitter        _splitter;
    private readonly Border              _toolbarContainer;
    private readonly StackPanel          _toolbarPanel;

    // Scrollbars overlaid on the diagram viewport
    private readonly System.Windows.Controls.Primitives.ScrollBar _hScroll
        = new() { Orientation = Orientation.Horizontal, Height = 12, Visibility = Visibility.Collapsed };
    private readonly System.Windows.Controls.Primitives.ScrollBar _vScroll
        = new() { Orientation = Orientation.Vertical,   Width  = 12, Visibility = Visibility.Collapsed };
    private bool   _syncingScrollBars;
    private double _scrollContentLeft;  // b.Left * zoom — used in ValueChanged to compute OffsetX
    private double _scrollContentTop;   // b.Top  * zoom

    // ---------------------------------------------------------------------------
    // View state
    // ---------------------------------------------------------------------------

    private CdViewMode         _viewMode         = CdViewMode.DiagramOnly;
    private CdSplitLayout      _layout           = CdSplitLayout.SplitRight;
    private LayoutStrategyKind _layoutStrategy   = LayoutStrategyKind.ForceDirected;

    // Persisted split ratios so dragging the splitter survives layout rebuilds
    private double _splitColRatio = 0.35;   // code / (code+diagram) for H splits
    private double _splitRowRatio = 0.35;   // code / (code+diagram) for V splits

    // Minimap overlay (owned here, not in DiagramCanvas)
    private MinimapCorner _minimapCorner = MinimapCorner.BottomLeft;
    private Canvas?       _minimapOverlay;

    // ---------------------------------------------------------------------------
    // Navigation bar + scope controls
    // ---------------------------------------------------------------------------

    private readonly DiagramNavBar     _navBar;
    private readonly DiagramScopeChips _scopeChips;
    private readonly DiagramQuickJump  _quickJump;
    private DiagramDocument?           _fullDocument;       // unfiltered source-of-truth for scope switching
    private ScopeFilter                _activeScope = ScopeFilter.All;
    private DiagramScopeChooser?       _scopeChooserOverlay;

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
    private readonly List<EditorToolbarItem> _layoutStrategyItems = [];

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
        this.SetResourceReference(BackgroundProperty, "DockBackgroundBrush");
        Focusable = true;
        PreviewKeyDown += OnPreviewKeyDown;

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

        _canvas.SetUndoManager(_undoManager);
        _canvas.SetSnapEngine(_snap);
        _canvas.SelectedClassChanged     += OnCanvasSelectedClassChanged;
        _canvas.HoveredClassChanged      += OnCanvasHoveredClassChanged;
        _canvas.RenameNodeRequested      += (_, args) => BeginRenameNode(args.Node);
        _canvas.ExportRequested          += OnCanvasExportRequested;
        _canvas.LayoutStrategyRequested  += (_, strategy) => _ = ApplyLayoutAsync(strategy);
        _canvas.FitToContentRequested    += (_, _) => _zoomPan.FitToContent();
        _canvas.ZoomToNodeRequested      += (_, node) =>
            _zoomPan.ZoomToRect(new Rect(node.X, node.Y, node.Width, node.Height), 40);

        // Navigation bar + scope controls
        _navBar     = new DiagramNavBar();
        _scopeChips = new DiagramScopeChips();
        _quickJump  = new DiagramQuickJump();

        _navBar.ZoomToNodeRequested   += (_, node) => { if (node is not null) ZoomToNode(node); };
        _navBar.ZoomToMemberRequested += (_, args)  => { ZoomToNode(args.Node); };
        _navBar.ScopeFilterChanged    += OnNavBarScopeFilterChanged;
        _scopeChips.ScopeChanged      += OnScopeChipsChanged;
        _quickJump.NodeChosen         += (_, node) => { ZoomToNode(node); HideQuickJump(); };
        _quickJump.Closed             += (_, _)    => HideQuickJump();

        // DSL pane — full CodeEditor with bidirectional sync.
        // Canvas → DSL: scheduled via ClassToSyncService (300ms debounce).
        // DSL → Canvas: text changes parsed + scheduled via ClassToSyncService (500ms debounce).
        _dslEditor = new CodeEditorSplitHost { IsReadOnly = false };

        // Apply classdiagram language definition for syntax coloring (deferred so
        // LanguageRegistry is fully populated before we query it).
        // Also wire DSL → Canvas text-change handler here (after editor is ready).
        Loaded += (_, _) =>
        {
            var lang = LanguageRegistry.Instance.FindByExtension(".classdiagram");
            if (lang is not null)
                _dslEditor.SetLanguage(lang);

            // Bidirectional DSL: CanUndoChanged fires after every edit operation,
            // making it a reliable proxy for "user typed something" events.
            _dslEditor.PrimaryEditor.CanUndoChanged += OnDslEditorTextChanged;
        };

        _codeHost = new Border { Child = _dslEditor };
        _codeHost.SetResourceReference(Border.BackgroundProperty, "CD_DslEditorBackground");

        // AdornerDecorator scopes the adorner layer to the diagram area so selection adorners
        // are clipped to the diagram viewport by the Border's ClipToBounds=true.
        var diagramAdornerScope = new System.Windows.Documents.AdornerDecorator { Child = _zoomPan };
        _diagramHost = new Border { Child = diagramAdornerScope, ClipToBounds = true };
        _diagramHost.SetResourceReference(Border.BackgroundProperty, "CD_CanvasBackground");

        // Build scroll overlay: zoomPan + h/v scrollbars in a Grid
        var scrollGrid = new Grid { ClipToBounds = true };
        scrollGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        scrollGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        scrollGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        scrollGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(_diagramHost, 0); Grid.SetColumn(_diagramHost, 0);
        Grid.SetRow(_vScroll, 0);     Grid.SetColumn(_vScroll, 1);
        Grid.SetRow(_hScroll, 1);     Grid.SetColumn(_hScroll, 0);
        scrollGrid.Children.Add(_diagramHost);
        scrollGrid.Children.Add(_vScroll);
        scrollGrid.Children.Add(_hScroll);

        // Minimap overlay — lives in screen/viewport coordinates, ABOVE the ZoomPanCanvas
        // transform chain. This is the correct parent so zoom/pan never affect the minimap position.
        var minimapOverlay = new Canvas { IsHitTestVisible = true, ClipToBounds = true };
        Grid.SetRow(minimapOverlay, 0); Grid.SetColumn(minimapOverlay, 0);
        Panel.SetZIndex(minimapOverlay, 200);
        scrollGrid.Children.Add(minimapOverlay);
        WireMinimapOverlay(_canvas._minimap, minimapOverlay);

        // _diagramBorder adds a 1px left separator + wraps the scroll overlay; ClipToBounds
        // prevents diagram content from bleeding into the adjacent DSL editor column.
        _diagramBorder = new Border { Child = scrollGrid, BorderThickness = new Thickness(1, 0, 0, 0), ClipToBounds = true };
        _diagramBorder.SetResourceReference(Border.BorderBrushProperty, "DockToolBarBorderBrush");

        // Wire scrollbars
        _hScroll.ValueChanged += (_, e) =>
        {
            if (!_syncingScrollBars) _zoomPan.OffsetX = -(e.NewValue + _scrollContentLeft);
        };
        _vScroll.ValueChanged += (_, e) =>
        {
            if (!_syncingScrollBars) _zoomPan.OffsetY = -(e.NewValue + _scrollContentTop);
        };

        // Update scrollbars + minimap viewport when viewport size, zoom, or pan changes
        _zoomPan.SizeChanged      += (_, _) => { UpdateScrollBars(); UpdateMinimapViewport(); };
        _zoomPan.TransformChanged += (_, _) => { UpdateScrollBars(); UpdateMinimapViewport(); };
        DiagramChanged            += (_, _) => UpdateScrollBars();

        // GridSplitter — 6px wide, uses DockSplitterBrush for a visible handle
        _splitter = new GridSplitter
        {
            ShowsPreview        = false,
            ResizeBehavior      = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
            Cursor              = Cursors.SizeWE
        };
        _splitter.SetResourceReference(BackgroundProperty, "DockSplitterBrush");
        _splitter.DragCompleted += (_, _) => SaveSplitRatio();

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
    public ICommand? CopyCommand      => new RelayCommand(CopySelected, () => _canvas.SelectedIds.Count > 0);
    public ICommand? CutCommand       => new RelayCommand(CutSelected,  () => _canvas.SelectedIds.Count > 0);
    public ICommand? PasteCommand     => new RelayCommand(PasteFromClipboard, () =>
    {
        try { return Clipboard.ContainsText() && Clipboard.GetText().TrimStart().StartsWith("// classdiagram-clip"); }
        catch { return false; }
    });
    public ICommand? DeleteCommand    => new RelayCommand(() => { foreach (var id in _canvas.SelectedIds.ToList()) { var n = _document.Classes.FirstOrDefault(c => c.Id == id); if (n is not null) _canvas.DeleteSelectedNode(); } });
    public ICommand? SelectAllCommand => new RelayCommand(() => _canvas.SelectAll());

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

    public void Copy()  => CopySelected();
    public void Cut()   => CutSelected();
    public void Paste() => PasteFromClipboard();
    public void Delete()     { DeleteSelected(); }
    public void SelectAll()  => _canvas.SelectAll();
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
            _dslEditor.PrimaryEditor.LoadText(dsl);
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

    /// <summary>
    /// Serializes the current diagram DSL to UTF-8 bytes for session-suspend/restore.
    /// Returns null when the document is empty (no unsaved state to preserve).
    /// </summary>
    public byte[]? GetUnsavedModifications()
    {
        if (_document.Classes.Count == 0) return null;
        string dsl = ClassDiagramSerializer.Serialize(_document);
        return System.Text.Encoding.UTF8.GetBytes(dsl);
    }

    /// <summary>
    /// Restores a diagram state previously captured by <see cref="GetUnsavedModifications"/>.
    /// Parses the UTF-8 DSL bytes and applies the document to the canvas.
    /// </summary>
    public void ApplyUnsavedModifications(byte[] data)
    {
        if (data is null || data.Length == 0) return;
        string dsl = System.Text.Encoding.UTF8.GetString(data);
        var result = ClassDiagramParser.Parse(dsl);
        if (!result.IsValid) return;
        _document = result.Document;
        if (!string.IsNullOrEmpty(_filePath))
            _document.FilePath = _filePath;
        _suppressCodeSync = true;
        _dslEditor.PrimaryEditor.LoadText(dsl);
        _suppressCodeSync = false;
        _canvas.ApplyDocument(_document);
        SetDirty(true);
        DiagramChanged?.Invoke(this, EventArgs.Empty);
    }

    public ChangesetSnapshot GetChangesetSnapshot() => ChangesetSnapshot.Empty;  // byte-level; N/A for diagrams
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
    /// Forces a full re-render of the diagram visual layer.
    /// Call after theme changes so DrawingVisual brush tokens are re-resolved.
    /// </summary>
    public void RefreshDiagramVisuals()
    {
        if (_document.Classes.Count == 0) return;
        _canvas.ApplyDocument(_document);
        // Minimap uses TryFindResource brushes resolved at OnRender time — just invalidate.
        _canvas._minimap.InvalidateVisual();
    }

    /// <summary>Selects a single node on the canvas and fires SelectedClassChanged.</summary>
    public void SelectNode(ClassNode node) => _canvas.SelectNodeById(node.Id);

    /// <summary>
    /// Highlights the relationship arrow whose SourceId matches <paramref name="relId"/>.
    /// Pass null to clear the highlight.
    /// </summary>
    public void HighlightRelationship(string? relId) => _canvas.HighlightRelationship(relId);

    /// <summary>Zooms the viewport to show the given node.</summary>
    public void ZoomToNode(ClassNode node) =>
        _zoomPan.ZoomToRect(new Rect(node.X, node.Y, node.Width, _canvas.GetDiagramBounds().Height > 0 ? node.Height : 120), 80);

    /// <summary>Forces a full re-render of all nodes and arrows.</summary>
    public void InvalidateAndRender() => _canvas.InvalidateAndRender();

    /// <summary>Forwarded from DiagramCanvas — fires when Ctrl+Click on a member.</summary>
    public event EventHandler<(ClassNode Node, ClassMember Member)>? NavigateToMemberRequested
    {
        add    => _canvas.NavigateToMemberRequested += value;
        remove => _canvas.NavigateToMemberRequested -= value;
    }

    /// <summary>Forwarded from DiagramCanvas — fires when "Rename…" is chosen.</summary>
    public event EventHandler<(ClassNode Node, string? NewName)>? RenameNodeRequested
    {
        add    => _canvas.RenameNodeRequested += value;
        remove => _canvas.RenameNodeRequested -= value;
    }

    /// <summary>Forwarded from DiagramCanvas — fires when "Properties" is chosen on a node.</summary>
    public event EventHandler<ClassNode>? ShowPropertiesRequested
    {
        add    => _canvas.ShowPropertiesRequested += value;
        remove => _canvas.ShowPropertiesRequested -= value;
    }

    /// <summary>Forwarded from DiagramCanvas — fires when "Find References" is chosen on a node header.</summary>
    public event EventHandler<ClassNode>? FindReferencesRequested
    {
        add    => _canvas.FindReferencesRequested += value;
        remove => _canvas.FindReferencesRequested -= value;
    }

    /// <summary>Forwarded from DiagramCanvas — fires when "Show Metrics" is chosen on a node header.</summary>
    public event EventHandler<ClassNode>? ShowMetricsRequested
    {
        add    => _canvas.ShowMetricsRequested += value;
        remove => _canvas.ShowMetricsRequested -= value;
    }

    /// <summary>Forwarded from DiagramCanvas — fires when "Change Color…" is chosen on a node header.</summary>
    public event EventHandler<ClassNode>? ChangeNodeColorRequested
    {
        add    => _canvas.ChangeNodeColorRequested += value;
        remove => _canvas.ChangeNodeColorRequested -= value;
    }

    /// <summary>
    /// Loads a pre-analyzed <see cref="DiagramDocument"/> directly into the editor.
    /// No file I/O — called by the plugin when opening from source file/folder/solution analysis.
    /// </summary>
    /// <summary>
    /// Loads a pre-analyzed <see cref="DiagramDocument"/> directly into the editor.
    /// No file I/O — called by the plugin when opening from source file/folder/solution analysis.
    /// For large diagrams (> 200 types) the Scope Chooser overlay is shown first so the user
    /// can limit rendering before the full diagram is applied.
    /// </summary>
    public void LoadDocument(DiagramDocument doc, string title)
    {
        _fullDocument = doc;   // keep unfiltered source-of-truth
        _activeScope  = ScopeFilter.All;

        // Wire nav bar with the full groups list (populated by plugin via DiagramProjectGroup)
        _navBar.SetDocument(doc, doc.ProjectGroups);
        _scopeChips.SetScope(_activeScope);

        // For very large diagrams show the scope chooser; otherwise apply directly.
        const int LargeThreshold = 200;
        if (doc.Classes.Count > LargeThreshold)
        {
            ShowScopeChooser(doc, title);
            return;
        }

        ApplyDocumentInternal(doc, title, fitToContent: true);
    }

    private void ApplyDocumentInternal(DiagramDocument doc, string title, bool fitToContent)
    {
        _document = doc;
        string dsl = ClassDiagramSerializer.Serialize(doc);

        _suppressCodeSync = true;
        _dslEditor.PrimaryEditor.LoadText(dsl);
        _suppressCodeSync = false;

        _canvas.ApplyDocument(_document);
        _undoManager.Clear();
        SetDirty(false);
        TitleChanged?.Invoke(this, title ?? Title);
        UpdateStatusBar();
        DiagramChanged?.Invoke(this, EventArgs.Empty);

        if (fitToContent)
            Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                () => _zoomPan.FitToContent());
    }

    // ── Session state (save/restore zoom-pan-selection-minimap) ─────────────

    /// <summary>
    /// Returns the current view state as a flat dictionary suitable for
    /// serialization by the plugin (which owns the serializer).
    /// </summary>
    public Dictionary<string, string> GetViewSnapshot()
    {
        var d = new Dictionary<string, string>
        {
            ["zoom"]      = _zoomPan.ZoomFactor.ToString("R"),
            ["offsetX"]   = _zoomPan.OffsetX.ToString("R"),
            ["offsetY"]   = _zoomPan.OffsetY.ToString("R"),
            ["selected"]  = _canvas.SelectedNode?.Id ?? string.Empty,
            ["minimap"]   = _canvas.IsMinimapVisible ? "1" : "0",
            // Version 2 view state (no node-level data — that belongs in .whcd only)
            ["swimlanes"] = _canvas.ShowSwimLanes   ? "1" : "0",
            ["snapGrid"]  = _snap.SnapToGrid        ? "1" : "0",
            ["viewMode"]  = _viewMode.ToString(),
            ["layout"]    = _layout.ToString(),
            ["splitCol"]  = _splitColRatio.ToString("R"),
            ["splitRow"]  = _splitRowRatio.ToString("R")
        };
        return d;
    }

    /// <summary>
    /// Restores a view snapshot previously produced by <see cref="GetViewSnapshot"/>.
    /// Call after <see cref="LoadDocument"/> to suppress the auto-fit.
    /// </summary>
    public void ApplyViewSnapshot(Dictionary<string, string> snap)
    {
        if (snap is null) return;

        Application.Current?.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                if (snap.TryGetValue("zoom", out string? z) && double.TryParse(z, out double zoom))
                    _zoomPan.ZoomFactor = zoom;
                if (snap.TryGetValue("offsetX", out string? ox) && double.TryParse(ox, out double offX))
                    _zoomPan.OffsetX = offX;
                if (snap.TryGetValue("offsetY", out string? oy) && double.TryParse(oy, out double offY))
                    _zoomPan.OffsetY = offY;
                if (snap.TryGetValue("minimap", out string? mm))
                    _canvas.IsMinimapVisible = mm == "1";
                if (snap.TryGetValue("selected", out string? sel) && !string.IsNullOrEmpty(sel))
                    _canvas.SelectNodeById(sel);

                // Version 2 view state
                if (snap.TryGetValue("swimlanes", out string? sw))
                    _canvas.ShowSwimLanes = sw == "1";
                if (snap.TryGetValue("snapGrid", out string? sg))
                    _snap.SnapToGrid = sg == "1";
                if (snap.TryGetValue("viewMode", out string? vmStr)
                 && Enum.TryParse<CdViewMode>(vmStr, out var vm))
                    SetViewMode(vm);
                if (snap.TryGetValue("layout", out string? layStr)
                 && Enum.TryParse<CdSplitLayout>(layStr, out var sl))
                {
                    double col = snap.TryGetValue("splitCol", out string? sc)
                        && double.TryParse(sc, out double cv) ? cv : _splitColRatio;
                    double row = snap.TryGetValue("splitRow", out string? sr)
                        && double.TryParse(sr, out double rv) ? rv : _splitRowRatio;
                    ApplySplitLayout(sl, col, row);
                }
            });
    }

    // ── .whcd twin-file state (positions + view) ─────────────────────────────

    /// <summary>
    /// Captures the complete visual state (node positions, zoom, pan, minimap, selection,
    /// swimlanes, snap, view-mode, split layout, collapsed sections, custom heights)
    /// into a <see cref="WhcdDocument"/> ready for serialization by the plugin.
    /// </summary>
    public WhcdDocument GetWhcdState(IEnumerable<string> sourceFiles)
    {
        var layer = _canvas.VisualLayer;

        // Flatten collapsed sections → "nodeId:SectionName"
        var collapsed = new List<string>();
        foreach (var (nodeId, sections) in layer.CollapsedSectionMap)
            foreach (var sec in sections)
                collapsed.Add($"{nodeId}:{sec}");

        return new WhcdDocument
        {
            SourceFiles       = sourceFiles.ToList(),
            Zoom              = _zoomPan.ZoomFactor,
            OffsetX           = _zoomPan.OffsetX,
            OffsetY           = _zoomPan.OffsetY,
            MinimapVisible    = _canvas.IsMinimapVisible,
            SelectedNodeId    = _canvas.SelectedNode?.Id,
            ShowSwimLanes     = _canvas.ShowSwimLanes,
            SnapToGrid        = _snap.SnapToGrid,
            ViewMode          = _viewMode.ToString(),
            SplitLayout       = _layout.ToString(),
            SplitColRatio     = _splitColRatio,
            SplitRowRatio     = _splitRowRatio,
            CollapsedSections = collapsed,
            Nodes             = _document.Classes
                .Select(n =>
                {
                    layer.CustomHeights.TryGetValue(n.Id, out double h);
                    return new WhcdNodePosition
                    {
                        Id     = n.Id,
                        X      = n.X,
                        Y      = n.Y,
                        Width  = n.Width,
                        Height = h   // 0 when no custom height override
                    };
                })
                .ToList()
        };
    }

    /// <summary>
    /// Restores node positions and view state from a <see cref="WhcdDocument"/>.
    /// Call AFTER <see cref="LoadDocument"/> so the live document is in place.
    /// Nodes not found in <paramref name="state"/> keep their current position (graceful).
    /// </summary>
    public void ApplyWhcdState(WhcdDocument state)
    {
        if (state is null) return;

        var layer = _canvas.VisualLayer;

        // 1. Patch positions into live node objects (matched by ID)
        var posById   = state.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        bool anyMoved = false;
        foreach (var node in _document.Classes)
        {
            if (!posById.TryGetValue(node.Id, out var pos)) continue;
            node.X     = pos.X;
            node.Y     = pos.Y;
            node.Width = pos.Width;
            anyMoved   = true;
        }

        // 1b. Restore custom heights (Version 2+)
        foreach (var pos in state.Nodes.Where(p => p.Height > 0))
            layer.SetCustomHeight(pos.Id, pos.Height);

        // 1c. Restore collapsed sections (Version 2+)
        var sectionMap = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in state.CollapsedSections)
        {
            int colon = entry.IndexOf(':');
            if (colon <= 0) continue;
            string nodeId  = entry[..colon];
            string section = entry[(colon + 1)..];
            if (!sectionMap.TryGetValue(nodeId, out var set))
                sectionMap[nodeId] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(section);
        }
        layer.RestoreCollapsedSections(sectionMap);

        // 2. Re-render with restored positions (skip default FitToContent)
        if (anyMoved)
            _canvas.ApplyDocument(_document);

        // 3. Restore view at Loaded priority (canvas must be measured/arranged first)
        Application.Current?.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                _zoomPan.ZoomFactor      = state.Zoom;
                _zoomPan.OffsetX         = state.OffsetX;
                _zoomPan.OffsetY         = state.OffsetY;
                _canvas.IsMinimapVisible = state.MinimapVisible;
                if (state.SelectedNodeId is { Length: > 0 } sid)
                    _canvas.SelectNodeById(sid);

                // Version 2 view state
                _canvas.ShowSwimLanes = state.ShowSwimLanes;
                _snap.SnapToGrid      = state.SnapToGrid;
                if (Enum.TryParse<CdViewMode>(state.ViewMode, out var vm))
                    SetViewMode(vm);
                if (Enum.TryParse<CdSplitLayout>(state.SplitLayout, out var sl))
                    ApplySplitLayout(sl, state.SplitColRatio, state.SplitRowRatio);
            });
    }

    /// <summary>
    /// Applies a split layout with explicit ratio values without triggering the
    /// default ratio reset that <see cref="SetSplitLayout"/> performs.
    /// </summary>
    private void ApplySplitLayout(CdSplitLayout layout, double colRatio, double rowRatio)
    {
        _splitColRatio = Math.Clamp(colRatio, 0.1, 0.9);
        _splitRowRatio = Math.Clamp(rowRatio, 0.1, 0.9);
        _layout        = layout;
        UpdateGridLayout();
    }

    /// <summary>
    /// Applies an incremental <see cref="DiagramPatch"/> produced by the live-sync
    /// service. Added/removed nodes are merged into <see cref="Document"/>; the canvas
    /// is repainted only for dirty nodes.
    /// </summary>
    public void ApplyPatch(WpfHexEditor.Editor.ClassDiagram.Core.Model.DiagramPatch patch,
                           WpfHexEditor.Editor.ClassDiagram.Core.Model.DiagramDocument next)
    {
        _document = next;

        // Keep DSL pane in sync (suppress canvas re-trigger).
        _suppressCodeSync = true;
        _dslEditor.PrimaryEditor.LoadText(ClassDiagramSerializer.Serialize(_document));
        _suppressCodeSync = false;

        // Repaint only the dirty nodes.
        var dirtyIds = patch.ModifiedNodes.Select(n => n.Id)
            .Concat(patch.AddedNodes.Select(n => n.Id))
            .Concat(patch.RemovedNodeIds)
            .ToHashSet(StringComparer.Ordinal);

        if (dirtyIds.Count > 0)
            _canvas.ApplyPatch(dirtyIds);
        else
            _canvas.ApplyDocument(_document);

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
        _diagramBorder.Visibility = Visibility.Collapsed;
        _splitter.Visibility    = Visibility.Collapsed;
    }

    private void BuildDiagramOnlyLayout()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // toolbar
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // nav bar
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // scope chips
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        AddToGrid(_toolbarContainer, 0, 0);
        AddToGrid(_navBar,           1, 0);
        AddToGrid(_scopeChips,       2, 0);
        AddToGrid(_diagramBorder,    3, 0);
        _codeHost.Visibility      = Visibility.Collapsed;
        _diagramBorder.Visibility = Visibility.Visible;
        _diagramBorder.BorderThickness = new Thickness(0);   // no separator in full-diagram mode
        _splitter.Visibility      = Visibility.Collapsed;
    }

    private void BuildSplitLayout()
    {
        _codeHost.Visibility      = Visibility.Visible;
        _diagramBorder.Visibility = Visibility.Visible;
        _splitter.Visibility      = Visibility.Visible;

        switch (_layout)
        {
            case CdSplitLayout.SplitRight:
            {
                // Toolbar row + NavBar + Chips + [code | splitter | diagram]
                _diagramBorder.BorderThickness = new Thickness(1, 0, 0, 0);
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 0 toolbar
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 1 nav bar
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 2 chips
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 3

                var colCode    = new ColumnDefinition { Width = new GridLength(_splitColRatio,         GridUnitType.Star) };
                var colSplitter= new ColumnDefinition { Width = new GridLength(6) };
                var colDiagram = new ColumnDefinition { Width = new GridLength(1 - _splitColRatio,     GridUnitType.Star) };
                ColumnDefinitions.Add(colCode);
                ColumnDefinitions.Add(colSplitter);
                ColumnDefinitions.Add(colDiagram);

                SetColumnSpan(_toolbarContainer, 3);
                SetColumnSpan(_navBar,           3);
                SetColumnSpan(_scopeChips,       3);
                AddToGrid(_toolbarContainer, 0, 0);
                AddToGrid(_navBar,           1, 0);
                AddToGrid(_scopeChips,       2, 0);
                AddToGrid(_codeHost,         3, 0);
                AddToGrid(_splitter,         3, 1);
                AddToGrid(_diagramBorder,    3, 2);

                _splitter.ResizeDirection     = GridResizeDirection.Columns;
                _splitter.Width               = double.NaN;
                _splitter.Height              = double.NaN;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
                _splitter.Cursor              = Cursors.SizeWE;
                break;
            }

            case CdSplitLayout.SplitLeft:
            {
                // Toolbar row + NavBar + Chips + [diagram | splitter | code]
                _diagramBorder.BorderThickness = new Thickness(0, 0, 1, 0);
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 0 toolbar
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 1 nav bar
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 2 chips
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 3

                var colDiagram = new ColumnDefinition { Width = new GridLength(1 - _splitColRatio,     GridUnitType.Star) };
                var colSplitter= new ColumnDefinition { Width = new GridLength(6) };
                var colCode    = new ColumnDefinition { Width = new GridLength(_splitColRatio,         GridUnitType.Star) };
                ColumnDefinitions.Add(colDiagram);
                ColumnDefinitions.Add(colSplitter);
                ColumnDefinitions.Add(colCode);

                SetColumnSpan(_toolbarContainer, 3);
                SetColumnSpan(_navBar,           3);
                SetColumnSpan(_scopeChips,       3);
                AddToGrid(_toolbarContainer, 0, 0);
                AddToGrid(_navBar,           1, 0);
                AddToGrid(_scopeChips,       2, 0);
                AddToGrid(_diagramBorder,    3, 0);
                AddToGrid(_splitter,         3, 1);
                AddToGrid(_codeHost,         3, 2);

                _splitter.ResizeDirection     = GridResizeDirection.Columns;
                _splitter.Width               = double.NaN;
                _splitter.Height              = double.NaN;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
                _splitter.Cursor              = Cursors.SizeWE;
                break;
            }

            case CdSplitLayout.SplitBottom:
            {
                // Toolbar + NavBar + Chips + code + splitter + diagram
                _diagramBorder.BorderThickness = new Thickness(0, 1, 0, 0);
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 0 toolbar
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 1 nav bar
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 2 chips
                var rowCode    = new RowDefinition { Height = new GridLength(_splitRowRatio,         GridUnitType.Star) };
                var rowSplitter= new RowDefinition { Height = new GridLength(6) };
                var rowDiagram = new RowDefinition { Height = new GridLength(1 - _splitRowRatio,     GridUnitType.Star) };
                RowDefinitions.Add(rowCode);     // 3
                RowDefinitions.Add(rowSplitter); // 4
                RowDefinitions.Add(rowDiagram);  // 5

                AddToGrid(_toolbarContainer, 0, 0);
                AddToGrid(_navBar,           1, 0);
                AddToGrid(_scopeChips,       2, 0);
                AddToGrid(_codeHost,         3, 0);
                AddToGrid(_splitter,         4, 0);
                AddToGrid(_diagramBorder,    5, 0);

                _splitter.ResizeDirection     = GridResizeDirection.Rows;
                _splitter.Width               = double.NaN;
                _splitter.Height              = double.NaN;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
                _splitter.Cursor              = Cursors.SizeNS;
                break;
            }

            case CdSplitLayout.SplitTop:
            {
                // Toolbar + NavBar + Chips + diagram + splitter + code
                _diagramBorder.BorderThickness = new Thickness(0, 0, 0, 1);
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 0 toolbar
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 1 nav bar
                RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // 2 chips
                var rowDiagram = new RowDefinition { Height = new GridLength(1 - _splitRowRatio,     GridUnitType.Star) };
                var rowSplitter= new RowDefinition { Height = new GridLength(6) };
                var rowCode    = new RowDefinition { Height = new GridLength(_splitRowRatio,         GridUnitType.Star) };
                RowDefinitions.Add(rowDiagram);  // 3
                RowDefinitions.Add(rowSplitter); // 4
                RowDefinitions.Add(rowCode);     // 5

                AddToGrid(_toolbarContainer, 0, 0);
                AddToGrid(_navBar,           1, 0);
                AddToGrid(_scopeChips,       2, 0);
                AddToGrid(_diagramBorder,    3, 0);
                AddToGrid(_splitter,         4, 0);
                AddToGrid(_codeHost,         5, 0);

                _splitter.ResizeDirection     = GridResizeDirection.Rows;
                _splitter.Width               = double.NaN;
                _splitter.Height              = double.NaN;
                _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
                _splitter.Cursor              = Cursors.SizeNS;
                break;
            }
        }
    }

    private void SaveSplitRatio()
    {
        if (_layout is CdSplitLayout.SplitRight or CdSplitLayout.SplitLeft)
        {
            // Column 0 = code (SplitRight) or diagram (SplitLeft), column 2 = other
            if (ColumnDefinitions.Count >= 3)
            {
                double codeW  = _layout == CdSplitLayout.SplitRight
                    ? ColumnDefinitions[0].ActualWidth
                    : ColumnDefinitions[2].ActualWidth;
                double total  = ColumnDefinitions[0].ActualWidth + ColumnDefinitions[2].ActualWidth;
                if (total > 0) _splitColRatio = Math.Clamp(codeW / total, 0.1, 0.9);
            }
        }
        else
        {
            // Row 3 = code (SplitBottom) or diagram (SplitTop), row 5 = other
            // (rows 0=toolbar, 1=navBar, 2=chips, 3=content1, 4=splitter, 5=content2)
            if (RowDefinitions.Count >= 6)
            {
                double codeH  = _layout == CdSplitLayout.SplitBottom
                    ? RowDefinitions[3].ActualHeight
                    : RowDefinitions[5].ActualHeight;
                double total  = RowDefinitions[3].ActualHeight + RowDefinitions[5].ActualHeight;
                if (total > 0) _splitRowRatio = Math.Clamp(codeH / total, 0.1, 0.9);
            }
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
                new() { Icon = "\uEB9F", Label = "Export PNG",     Command = new RelayCommand(() => _ = ExportPngAsync()) },
                new() { Icon = "\uE781", Label = "Export SVG",     Command = new RelayCommand(() => _ = ExportSvgAsync()) },
                new() { Icon = "\uE943", Label = "Export C#",      Command = new RelayCommand(() => _ = ExportCSharpAsync()) },
                new() { Icon = "\uE728", Label = "Export Mermaid", Command = new RelayCommand(() => _ = ExportMermaidAsync()) }
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
            Icon      = "\uE80A",
            Tooltip   = "Show grid",
            IsToggle  = true,
            IsChecked = true,
            Command   = new RelayCommand(() => _canvas.ShowGrid = !_canvas.ShowGrid)
        });

        // Minimap toggle
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon      = "\uE8A4",
            Tooltip   = "Toggle minimap (Ctrl+M)",
            IsToggle  = true,
            IsChecked = true,
            Command   = new RelayCommand(() => _canvas.IsMinimapVisible = !_canvas.IsMinimapVisible)
        });

        // Swimlanes toggle
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon      = "\uE7C9",
            Tooltip   = "Show namespace swimlanes",
            IsToggle  = true,
            IsChecked = false,
            Command   = new RelayCommand(() => _canvas.ShowSwimLanes = !_canvas.ShowSwimLanes)
        });

        // Auto-layout strategy dropdown — mutually exclusive radio-style toggles
        _layoutStrategyItems.Clear();
        EditorToolbarItem MakeStrategyItem(string label, LayoutStrategyKind kind)
        {
            EditorToolbarItem item = null!;
            item = new EditorToolbarItem
            {
                Label     = label,
                IsToggle  = true,
                IsChecked = _layoutStrategy == kind,
                Command   = new RelayCommand(() =>
                {
                    _layoutStrategy = kind;
                    foreach (var mi in _layoutStrategyItems)
                        mi.IsChecked = ReferenceEquals(mi, item);
                    ApplyLayout(kind);
                })
            };
            _layoutStrategyItems.Add(item);
            return item;
        }

        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon  = "\uE947",
            Label = "Auto Layout",
            Tooltip = "Apply auto-layout strategy",
            DropdownItems = new ObservableCollection<EditorToolbarItem>
            {
                MakeStrategyItem("Force-Directed", LayoutStrategyKind.ForceDirected),
                MakeStrategyItem("Hierarchical",   LayoutStrategyKind.Hierarchical),
                MakeStrategyItem("Sugiyama",       LayoutStrategyKind.Sugiyama)
            }
        });

        // Split layout dropdown
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

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _canvas.ClearSelection();
            e.Handled = true;
        }
    }

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
                case Key.A: _canvas.SelectAll();                  e.Handled = true; return;
                case Key.G: ShowQuickJump();                      e.Handled = true; return;
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
            case Key.Escape: _canvas.ClearSelection(); e.Handled = true; break;
        }
    }

    // ---------------------------------------------------------------------------
    // Canvas event handlers
    // ---------------------------------------------------------------------------

    private void OnCanvasSelectedClassChanged(object? sender, ClassNode? node)
    {
        if (_sbSelected is not null)
            _sbSelected.Value = node?.Name ?? "None";

        _navBar.SetSelectedNode(node);
        SelectedClassChanged?.Invoke(this, node);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCanvasHoveredClassChanged(object? sender, ClassNode? node)
    {
        if (_sbSelected is null) return;
        // Show hovered node name in status bar when nothing is selected;
        // keep the selected name when something IS selected so it doesn't flicker.
        if (_canvas.SelectedNode is null)
            _sbSelected.Value = node is not null ? $"↑ {node.Name}" : "None";
    }

    // ---------------------------------------------------------------------------
    // DSL sync (canvas → DSL only; DSL pane is read-only)
    // ---------------------------------------------------------------------------

    private void OnCodeUpdateReady(object? sender, string dsl)
    {
        _suppressCodeSync = true;
        _dslEditor.PrimaryEditor.LoadText(dsl);
        _suppressCodeSync = false;
    }

    private void OnCanvasUpdateReady(object? sender, DiagramDocument doc)
    {
        _document = doc;
        _canvas.ApplyDocument(_document);
        SetDirty(true);
        UpdateStatusBar();
        DiagramChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles live typing in the DSL code pane (DSL → Canvas direction).
    /// Parses the text and schedules a canvas update via the sync service.
    /// Ignored when <see cref="_suppressCodeSync"/> is set (canvas-initiated update).
    /// </summary>
    private void OnDslEditorTextChanged(object? sender, EventArgs e)
    {
        if (_suppressCodeSync) return;

        string dsl = _dslEditor.PrimaryEditor.GetText();
        var result = ClassDiagramParser.Parse(dsl);
        if (result.IsValid)
        {
            _syncService.ScheduleCodeToCanvas(result.Document);
            StatusMessage?.Invoke(this, string.Empty);
        }
        else
        {
            // Show parse error count in status bar so the user knows the DSL is invalid.
            StatusMessage?.Invoke(this, $"DSL: {result.Errors.Count} parse error(s) — canvas not updated");
        }
    }

    // ---------------------------------------------------------------------------
    // Undo/Redo helpers
    // ---------------------------------------------------------------------------

    private void OnHistoryChanged(object? sender, EventArgs e)
    {
        RaiseCanUndoRedo();
        UpdateUndoRedoTooltips();
        // Any undo-stack mutation (drag end, resize, delete, add…) means positions changed.
        // Raise DiagramChanged so the plugin's debounced .whcd auto-save fires.
        DiagramChanged?.Invoke(this, EventArgs.Empty);
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

        // Delegate to DiagramCanvas.DeleteNode which pushes a position-preserving
        // SingleClassDiagramUndoEntry. SnapshotClassDiagramUndoEntry must NOT be
        // used here — ApplyDslSnapshot parses DSL without positions → X=0/Y=0 on undo.
        _canvas.DeleteSelectedNode();

        // Keep _document aligned with what the canvas is now showing.
        if (_canvas.Document is not null) _document = _canvas.Document;
        SyncDslPane();
        SetDirty(true);
    }

    private const string ClipboardMimeTag = "// classdiagram-clip\n";

    private void CopySelected()
    {
        var selected = _canvas.SelectedIds
            .Select(id => _document.Classes.FirstOrDefault(n => n.Id == id))
            .Where(n => n is not null)
            .Cast<ClassNode>()
            .ToList();
        if (selected.Count == 0) return;

        // Serialize only the selected nodes + relationships between them to DSL.
        var selectedIds = selected.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var subRels = _document.Relationships
            .Where(r => selectedIds.Contains(r.SourceId) && selectedIds.Contains(r.TargetId))
            .ToList();
        var subDoc = new DiagramDocument();
        foreach (var n in selected) subDoc.Classes.Add(n);
        subDoc.Relationships.AddRange(subRels);

        string dsl = ClipboardMimeTag + ClassDiagramSerializer.Serialize(subDoc);
        Clipboard.SetText(dsl, TextDataFormat.UnicodeText);
    }

    private void CutSelected()
    {
        CopySelected();
        foreach (var id in _canvas.SelectedIds.ToList())
        {
            var node = _document.Classes.FirstOrDefault(n => n.Id == id);
            if (node is not null) _canvas.DeleteSelectedNode();
        }
        if (_canvas.Document is not null) _document = _canvas.Document;
        SyncDslPane();
        SetDirty(true);
    }

    private void PasteFromClipboard()
    {
        if (!Clipboard.ContainsText()) return;
        string text = Clipboard.GetText();
        if (!text.StartsWith(ClipboardMimeTag)) return;

        string dsl = text[ClipboardMimeTag.Length..];
        var result = ClassDiagramParser.Parse(dsl);
        if (!result.IsValid || result.Document.Classes.Count == 0) return;

        // Offset pasted nodes and build old→new ID map for relationship remapping.
        const double Offset = 40;
        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in result.Document.Classes)
        {
            string oldId = node.Id;
            string newId = Guid.NewGuid().ToString();
            idMap[oldId] = newId;
            node.Id  = newId;
            node.X  += Offset;
            node.Y  += Offset;
            _document.Classes.Add(node);
        }

        // Remap relationship IDs so pasted arrows connect the new copies, not the originals.
        var pastedRels = new List<ClassRelationship>();
        foreach (var rel in result.Document.Relationships)
        {
            if (idMap.TryGetValue(rel.SourceId, out string? newSrc) &&
                idMap.TryGetValue(rel.TargetId, out string? newTgt))
            {
                var remapped = rel with { SourceId = newSrc, TargetId = newTgt };
                _document.Relationships.Add(remapped);
                pastedRels.Add(remapped);
            }
        }

        _canvas.ApplyDocument(_document);
        SyncDslPane();
        SetDirty(true);

        var pastedNodes = result.Document.Classes.ToList();
        _undoManager.Push(new SingleClassDiagramUndoEntry(
            Description: $"Paste {pastedNodes.Count} node(s)",
            UndoAction: () =>
            {
                foreach (var n  in pastedNodes) _document.Classes.Remove(n);
                foreach (var r  in pastedRels)  _document.Relationships.Remove(r);
                _canvas.ApplyDocument(_document);
                SyncDslPane();
            },
            RedoAction: () =>
            {
                foreach (var n  in pastedNodes) _document.Classes.Add(n);
                foreach (var r  in pastedRels)  _document.Relationships.Add(r);
                _canvas.ApplyDocument(_document);
                SyncDslPane();
            }));
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

    private void BeginRename() => BeginRenameNode(_canvas.SelectedNode);

    /// <summary>
    /// Shows an inline TextBox popup positioned over the node header so the user
    /// can rename the node in-place. Confirms on Enter/focus-loss, cancels on Escape.
    /// </summary>
    private void BeginRenameNode(ClassNode? node)
    {
        if (node is null) return;

        // Convert the node header's diagram-space top-left to screen coords.
        double zoom    = _zoomPan.ZoomFactor;
        double offsetX = _zoomPan.OffsetX;
        double offsetY = _zoomPan.OffsetY;

        // screen_pt = diagram_pt * zoom + offset (ZoomPanCanvas internal transform)
        var screenOrigin = _zoomPan.TranslatePoint(
            new Point(node.X * zoom + offsetX, node.Y * zoom + offsetY),
            this);

        double popupW = Math.Max(node.Width * zoom, 160);

        var tb = new TextBox
        {
            Text            = node.Name,
            MinWidth        = popupW,
            MaxWidth        = 320,
            FontWeight      = FontWeights.Bold,
            FontSize        = 13,
            Padding         = new Thickness(6, 4, 6, 4),
            BorderThickness = new Thickness(1),
            SelectionStart  = 0,
            SelectionLength = node.Name.Length
        };
        tb.SetResourceReference(TextBox.BackgroundProperty,  "CD_ClassBoxHeaderBackground");
        tb.SetResourceReference(TextBox.ForegroundProperty,  "CD_ClassNameForeground");
        tb.SetResourceReference(TextBox.BorderBrushProperty, "CD_ClassBoxSelectedBorderBrush");

        var popup = new System.Windows.Controls.Primitives.Popup
        {
            Child              = new Border { Child = tb, CornerRadius = new CornerRadius(3), Padding = new Thickness(0) },
            Placement          = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint,
            HorizontalOffset   = screenOrigin.X,
            VerticalOffset     = screenOrigin.Y,
            AllowsTransparency = true,
            StaysOpen          = true,
            IsOpen             = true
        };
        tb.Focus();
        tb.SelectAll();

        void Commit()
        {
            if (!popup.IsOpen) return;
            popup.IsOpen = false;

            string newName = tb.Text.Trim();
            if (newName.Length == 0 || newName == node.Name) return;

            string beforeDsl = ClassDiagramSerializer.Serialize(_document);

            // ClassNode.Name is init-only — replace with a cloned node carrying the new name.
            var renamed = node.DeepClone();
            renamed.Id = node.Id; // preserve stable ID so relationships remain valid
            // patch Name by creating a new instance (init-only property)
            var renamedFinal = new ClassNode
            {
                Name               = newName,
                Kind               = renamed.Kind,
                IsAbstract         = renamed.IsAbstract,
                IsPartial          = renamed.IsPartial,
                IsRecord           = renamed.IsRecord,
                IsSealed           = renamed.IsSealed,
                Namespace          = renamed.Namespace,
                XmlDocSummary      = renamed.XmlDocSummary,
                SourceFilePath     = renamed.SourceFilePath,
                SourceLineOneBased = renamed.SourceLineOneBased,
                Metrics            = renamed.Metrics,
                X                  = renamed.X,
                Y                  = renamed.Y,
                Width              = renamed.Width,
                Height             = renamed.Height,
                CustomColor        = renamed.CustomColor
            };
            renamedFinal.Id = node.Id;
            renamedFinal.Members.AddRange(renamed.Members);
            renamedFinal.Attributes.AddRange(renamed.Attributes);

            int idx = _document.Classes.IndexOf(node);
            if (idx >= 0) _document.Classes[idx] = renamedFinal;

            string afterDsl = ClassDiagramSerializer.Serialize(_document);
            _undoManager.Push(new SnapshotClassDiagramUndoEntry(beforeDsl, afterDsl,
                $"Rename → {newName}", ApplyDslSnapshot));

            _canvas.ApplyDocument(_document);
            SyncDslPane();
            SetDirty(true);
        }

        tb.KeyDown   += (_, ke) =>
        {
            if (ke.Key == Key.Return)  { Commit();              ke.Handled = true; }
            if (ke.Key == Key.Escape)  { popup.IsOpen = false;  ke.Handled = true; }
        };
        tb.LostFocus += (_, _) => Commit();
    }

    // ---------------------------------------------------------------------------
    // Quick Jump (Ctrl+G)
    // ---------------------------------------------------------------------------

    private void ShowQuickJump()
    {
        if (_fullDocument is null && _document.Classes.Count == 0) return;
        var doc = _fullDocument ?? _document;
        _quickJump.SetDocument(doc, doc.ProjectGroups);

        // Add as overlay inside the scrollGrid (which contains _diagramHost, _vScroll, _hScroll)
        if (!IsQuickJumpMounted())
            AddOverlayToDiagramHost(_quickJump);

        _quickJump.Visibility = Visibility.Visible;
        _quickJump.FocusSearch();
    }

    private bool IsQuickJumpMounted()
        => _diagramBorder.Child is Grid sg && sg.Children.Contains(_quickJump);

    private void HideQuickJump()
    {
        _quickJump.Visibility = Visibility.Collapsed;
    }

    // ---------------------------------------------------------------------------
    // Scope Chooser
    // ---------------------------------------------------------------------------

    private void ShowScopeChooser(DiagramDocument doc, string title)
    {
        _scopeChooserOverlay = new DiagramScopeChooser();
        _scopeChooserOverlay.SetDocument(doc, doc.ProjectGroups);

        _scopeChooserOverlay.ScopeChosen += (_, result) =>
        {
            RemoveOverlayFromDiagramHost(_scopeChooserOverlay!);

            _scopeChooserOverlay = null;
            ApplyScopeChoice(result, title);
        };

        _scopeChooserOverlay.Cancelled += (_, _) =>
        {
            RemoveOverlayFromDiagramHost(_scopeChooserOverlay!);
            _scopeChooserOverlay = null;
            // Apply all types if user cancelled (graceful fallback)
            ApplyDocumentInternal(doc, title, fitToContent: true);
        };

        AddOverlayToDiagramHost(_scopeChooserOverlay);
    }

    private void AddOverlayToDiagramHost(UIElement overlay)
    {
        // The scrollGrid is inside _diagramBorder.Child
        if (_diagramBorder.Child is Grid scrollGrid)
        {
            Panel.SetZIndex(overlay, 300);
            Grid.SetRowSpan(overlay, scrollGrid.RowDefinitions.Count > 0 ? scrollGrid.RowDefinitions.Count : 1);
            Grid.SetColumnSpan(overlay, scrollGrid.ColumnDefinitions.Count > 0 ? scrollGrid.ColumnDefinitions.Count : 1);
            scrollGrid.Children.Add(overlay);
        }
    }

    private void RemoveOverlayFromDiagramHost(UIElement overlay)
    {
        if (_diagramBorder.Child is Grid scrollGrid)
            scrollGrid.Children.Remove(overlay);
    }

    private void ApplyScopeChoice(ScopeChoiceResult result, string title)
    {
        if (_fullDocument is null) return;

        DiagramDocument filtered = FilterDocument(_fullDocument, result);
        _activeScope = result.Mode switch
        {
            ScopeMode.Project   => new ScopeFilter { ProjectName = result.ProjectName },
            ScopeMode.Namespace => new ScopeFilter { Namespace   = result.Namespace   },
            _                   => ScopeFilter.All
        };

        _navBar.SetDocument(filtered, filtered.ProjectGroups);
        _scopeChips.SetScope(_activeScope);
        ApplyDocumentInternal(filtered, title, fitToContent: true);
    }

    private void OnNavBarScopeFilterChanged(object? sender, ScopeFilter filter)
    {
        if (_fullDocument is null) return;
        _activeScope = filter;
        _scopeChips.SetScope(filter);

        var result = new ScopeChoiceResult
        {
            Mode        = filter.ProjectName is not null ? ScopeMode.Project :
                          filter.Namespace   is not null ? ScopeMode.Namespace : ScopeMode.All,
            ProjectName = filter.ProjectName,
            Namespace   = filter.Namespace
        };
        DiagramDocument filtered = FilterDocument(_fullDocument, result);
        ApplyDocumentInternal(filtered, Title, fitToContent: true);
    }

    private void OnScopeChipsChanged(object? sender, ScopeFilter filter)
    {
        if (_fullDocument is null) return;
        _activeScope = filter;
        _navBar.SetDocument(_fullDocument, _fullDocument.ProjectGroups);
        // Sync nav bar to new scope (no-event guard handled inside SetDocument)
        var result = new ScopeChoiceResult
        {
            Mode        = filter.ProjectName is not null ? ScopeMode.Project :
                          filter.Namespace   is not null ? ScopeMode.Namespace : ScopeMode.All,
            ProjectName = filter.ProjectName,
            Namespace   = filter.Namespace
        };
        DiagramDocument filtered = FilterDocument(_fullDocument, result);
        _scopeChips.SetScope(filter);
        ApplyDocumentInternal(filtered, Title, fitToContent: true);
    }

    private DiagramDocument FilterDocument(DiagramDocument full, ScopeChoiceResult scope)
    {
        IEnumerable<ClassNode> keep = scope.Mode switch
        {
            ScopeMode.Project   => full.Classes.Where(n =>
                full.ProjectGroups.FirstOrDefault(g => g.ClassIds.Contains(n.Id))?.ProjectName == scope.ProjectName),
            ScopeMode.Namespace => full.Classes.Where(n => n.Namespace == scope.Namespace),
            ScopeMode.Selection => full.Classes.Where(n =>
                scope.SelectedProjects.Contains(
                    full.ProjectGroups.FirstOrDefault(g => g.ClassIds.Contains(n.Id))?.ProjectName ?? string.Empty)),
            _                   => full.Classes.AsEnumerable()
        };

        var keepList  = keep.ToList();
        var keepIds   = keepList.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var keepGroups = full.ProjectGroups
            .Where(g => g.ClassIds.Any(id => keepIds.Contains(id)))
            .ToList();

        var result2 = new DiagramDocument { FilePath = full.FilePath };
        result2.Classes.AddRange(keepList);
        result2.Relationships.AddRange(full.Relationships.Where(r => keepIds.Contains(r.SourceId) && keepIds.Contains(r.TargetId)));
        result2.ProjectGroups.AddRange(keepGroups);
        return result2;
    }

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
    // Layout strategy
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Applies the given layout strategy to the current document and fits the canvas.
    /// Runs the layout engine on a background thread (for force-directed perf).
    /// Wraps the operation in an undo entry that restores node positions.
    /// </summary>
    public async Task ApplyLayoutAsync(LayoutStrategyKind strategy)
    {
        if (_document.Classes.Count == 0) return;
        var opts = new LayoutOptions { Strategy = strategy, ColSpacing = 60, RowSpacing = 80, CanvasPadding = 40 };
        var engine = LayoutStrategyFactory.Create(strategy);

        // Capture before positions for undo
        var beforePositions = _document.Classes.ToDictionary(n => n.Id, n => (n.X, n.Y));

        StatusMessage?.Invoke(this, "Applying layout…");
        await Task.Run(() => engine.Layout(_document, opts));

        // Capture after positions for redo
        var afterPositions = _document.Classes.ToDictionary(n => n.Id, n => (n.X, n.Y));

        _undoManager.Push(new SingleClassDiagramUndoEntry(
            Description: $"Auto Layout ({strategy})",
            UndoAction: () =>
            {
                foreach (var n in _document.Classes)
                    if (beforePositions.TryGetValue(n.Id, out var p)) { n.X = p.X; n.Y = p.Y; }
                _canvas.ApplyDocument(_document);
                _zoomPan.FitToContent();
            },
            RedoAction: () =>
            {
                foreach (var n in _document.Classes)
                    if (afterPositions.TryGetValue(n.Id, out var p)) { n.X = p.X; n.Y = p.Y; }
                _canvas.ApplyDocument(_document);
                _zoomPan.FitToContent();
            }));

        _canvas.ApplyDocument(_document);
        _zoomPan.FitToContent();
        SetDirty(true);
        StatusMessage?.Invoke(this, "Ready");
    }

    /// <summary>Synchronous wrapper for backward compat (called from tests / old code).</summary>
    public void ApplyLayout(LayoutStrategyKind strategy) => _ = ApplyLayoutAsync(strategy);

    // Export actions
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Always returns the live canvas document, falling back to the last parsed snapshot.
    /// Keeps exports consistent with what the user sees on screen.
    /// </summary>
    private DiagramDocument CurrentDocument => _canvas.Document ?? _document;

    private async Task ExportPngAsync()
    {
        if (CurrentDocument.Classes.Count == 0) { StatusMessage?.Invoke(this, "Nothing to export."); return; }
        var doc = CurrentDocument;
        string? path = PickSavePath("PNG Image|*.png", ".png", "png");
        if (path is null) return;
        try
        {
            await _exportService.ExportPngAsync(doc, path);
            string msg = $"PNG exported → {path}";
            StatusMessage?.Invoke(this, $"PNG exported → {Path.GetFileName(path)}");
            OutputMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Export PNG failed: {ex.Message}");
            OutputMessage?.Invoke(this, $"[Export] PNG failed: {ex.Message}");
        }
    }

    private async Task ExportSvgAsync()
    {
        if (CurrentDocument.Classes.Count == 0) { StatusMessage?.Invoke(this, "Nothing to export."); return; }
        var doc = CurrentDocument;
        string? path = PickSavePath("SVG Vector|*.svg", ".svg", "svg");
        if (path is null) return;
        try
        {
            await _exportService.ExportSvgAsync(doc, path);
            string msg = $"SVG exported → {path}";
            StatusMessage?.Invoke(this, $"SVG exported → {Path.GetFileName(path)}");
            OutputMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Export SVG failed: {ex.Message}");
            OutputMessage?.Invoke(this, $"[Export] SVG failed: {ex.Message}");
        }
    }

    private async Task ExportCSharpAsync()
    {
        if (CurrentDocument.Classes.Count == 0) { StatusMessage?.Invoke(this, "Nothing to export."); return; }
        var doc = CurrentDocument;
        string? path = PickSavePath("C# Source|*.cs", ".cs", "cs");
        if (path is null) return;
        try
        {
            await _exportService.ExportCSharpAsync(doc, path);
            string msg = $"C# skeleton exported → {path}";
            StatusMessage?.Invoke(this, $"C# skeleton exported → {Path.GetFileName(path)}");
            OutputMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Export C# failed: {ex.Message}");
            OutputMessage?.Invoke(this, $"[Export] C# failed: {ex.Message}");
        }
    }

    private async Task ExportMermaidAsync()
    {
        if (CurrentDocument.Classes.Count == 0) { StatusMessage?.Invoke(this, "Nothing to export."); return; }
        var doc = CurrentDocument;
        string? path = PickSavePath("Mermaid Diagram|*.mmd;*.md", ".mmd", "mmd");
        if (path is null) return;
        try
        {
            await _exportService.ExportMermaidAsync(doc, path);
            string msg = $"Mermaid exported → {path}";
            StatusMessage?.Invoke(this, $"Mermaid exported → {Path.GetFileName(path)}");
            OutputMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Export Mermaid failed: {ex.Message}");
            OutputMessage?.Invoke(this, $"[Export] Mermaid failed: {ex.Message}");
        }
    }

    private async Task ExportPlantUmlAsync()
    {
        if (CurrentDocument.Classes.Count == 0) { StatusMessage?.Invoke(this, "Nothing to export."); return; }
        var doc = CurrentDocument;
        string? path = PickSavePath("PlantUML|*.puml;*.pu", ".puml", "puml");
        if (path is null) return;
        try
        {
            await _exportService.ExportPlantUmlAsync(doc, path);
            string msg = $"PlantUML exported → {path}";
            StatusMessage?.Invoke(this, $"PlantUML exported → {Path.GetFileName(path)}");
            OutputMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Export PlantUML failed: {ex.Message}");
            OutputMessage?.Invoke(this, $"[Export] PlantUML failed: {ex.Message}");
        }
    }

    private async Task ExportStructurizrAsync()
    {
        if (CurrentDocument.Classes.Count == 0) { StatusMessage?.Invoke(this, "Nothing to export."); return; }
        var doc = CurrentDocument;
        string? path = PickSavePath("Structurizr DSL|*.dsl", ".dsl", "dsl");
        if (path is null) return;
        try
        {
            await _exportService.ExportStructurizrAsync(doc, path);
            string msg = $"Structurizr DSL exported → {path}";
            StatusMessage?.Invoke(this, $"Structurizr DSL exported → {Path.GetFileName(path)}");
            OutputMessage?.Invoke(this, msg);
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke(this, $"Export Structurizr failed: {ex.Message}");
            OutputMessage?.Invoke(this, $"[Export] Structurizr failed: {ex.Message}");
        }
    }

    /// <summary>Opens a SaveFileDialog pre-filled with the diagram file name.</summary>
    private string? PickSavePath(string filter, string defaultExt, string ext)
    {
        string baseName = string.IsNullOrEmpty(_filePath)
            ? "diagram"
            : Path.GetFileNameWithoutExtension(_filePath);

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title       = "Export Diagram",
            Filter      = filter,
            DefaultExt  = defaultExt,
            FileName    = $"{baseName}.{ext}",
            InitialDirectory = string.IsNullOrEmpty(_filePath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : Path.GetDirectoryName(_filePath)
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private void OnCanvasExportRequested(object? sender, string format)
    {
        switch (format)
        {
            case "png":               _ = ExportPngAsync();           break;
            case "svg":               _ = ExportSvgAsync();           break;
            case "csharp":            _ = ExportCSharpAsync();        break;
            case "mermaid":           _ = ExportMermaidAsync();       break;
            case "plantUml":          _ = ExportPlantUmlAsync();      break;
            case "structurizr":       _ = ExportStructurizrAsync();   break;
            case "clipboard-mermaid": _ = CopyMermaidToClipboard();   break;
            case "clipboard-plantUml":_ = CopyPlantUmlToClipboard();  break;
            case "clipboard-png":     _ = CopyPngToClipboard();       break;
        }
    }

    private async Task CopyMermaidToClipboard()
    {
        string text = await _exportService.ExportMermaidAsync(CurrentDocument);
        Clipboard.SetText(text);
        StatusMessage?.Invoke(this, "Mermaid copied to clipboard.");
    }

    private async Task CopyPlantUmlToClipboard()
    {
        string text = await _exportService.ExportPlantUmlAsync(CurrentDocument);
        Clipboard.SetText(text);
        StatusMessage?.Invoke(this, "PlantUML copied to clipboard.");
    }

    private async Task CopyPngToClipboard()
    {
        string tmp = Path.GetTempFileName() + ".png";
        try
        {
            await _exportService.ExportPngAsync(CurrentDocument, tmp);
            if (!File.Exists(tmp)) return;
            // OnLoad forces full decode before we delete the temp file.
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource        = new Uri(tmp);
            bmp.CacheOption      = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            Clipboard.SetImage(bmp);
            StatusMessage?.Invoke(this, "PNG copied to clipboard.");
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
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
        _dslEditor.PrimaryEditor.LoadText(dsl);
        _suppressCodeSync = false;
    }

    // ---------------------------------------------------------------------------
    // Scrollbar sync
    // ---------------------------------------------------------------------------

    // ── Minimap overlay ───────────────────────────────────────────────────────

    private void WireMinimapOverlay(DiagramMinimapControl minimap, Canvas overlay)
    {
        _minimapOverlay = overlay;
        overlay.Children.Add(minimap);

        minimap.PositionDeltaRequested += (_, delta) =>
        {
            double left = Canvas.GetLeft(minimap) + delta.X;
            double top  = Canvas.GetTop(minimap)  + delta.Y;
            left = Math.Clamp(left, 0, Math.Max(0, overlay.ActualWidth  - minimap.ActualWidth));
            top  = Math.Clamp(top,  0, Math.Max(0, overlay.ActualHeight - minimap.ActualHeight));
            minimap.BeginAnimation(Canvas.LeftProperty, null);   // cancel any running snap animation
            minimap.BeginAnimation(Canvas.TopProperty,  null);
            Canvas.SetLeft(minimap, left);
            Canvas.SetTop(minimap, top);
        };

        minimap.CornerChangeRequested += (_, corner) => SnapMinimapToCorner(corner, animate: true);

        minimap.ViewportNavigateRequested += (_, diagPt) =>
        {
            double z = _zoomPan.ZoomFactor;
            double vw = overlay.ActualWidth;
            double vh = overlay.ActualHeight;
            _zoomPan.OffsetX = -(diagPt.X * z) + vw / 2;
            _zoomPan.OffsetY = -(diagPt.Y * z) + vh / 2;
        };

        minimap.HideRequested += (_, _) => _canvas.IsMinimapVisible = false;

        overlay.SizeChanged += (_, _) => SnapMinimapToCorner(_minimapCorner, animate: false);
    }

    private void SnapMinimapToCorner(MinimapCorner corner, bool animate)
    {
        _minimapCorner = corner;
        var minimap = _canvas._minimap;
        var overlay = _minimapOverlay;
        if (overlay is null) return;

        double pw = overlay.ActualWidth;
        double ph = overlay.ActualHeight;
        if (pw <= 0 || ph <= 0) return;

        const double margin = 8.0;
        double targetLeft = corner is MinimapCorner.TopLeft or MinimapCorner.BottomLeft
            ? margin : pw - DiagramMinimapControl.MapWidth  - margin;
        double targetTop  = corner is MinimapCorner.TopLeft or MinimapCorner.TopRight
            ? margin : ph - DiagramMinimapControl.MapHeight - margin;

        if (animate)
        {
            var dur  = new Duration(TimeSpan.FromMilliseconds(150));
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            minimap.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(targetLeft, dur) { EasingFunction = ease });
            minimap.BeginAnimation(Canvas.TopProperty,  new DoubleAnimation(targetTop,  dur) { EasingFunction = ease });
        }
        else
        {
            Canvas.SetLeft(minimap, targetLeft);
            Canvas.SetTop(minimap,  targetTop);
        }
        Panel.SetZIndex(minimap, 10);
    }

    private void UpdateMinimapViewport()
    {
        var overlay = _minimapOverlay;
        if (overlay is null) return;
        double z  = _zoomPan.ZoomFactor;
        double vw = overlay.ActualWidth;
        double vh = overlay.ActualHeight;
        if (z <= 0 || vw <= 0 || vh <= 0) return;

        double diagX = -_zoomPan.OffsetX / z;
        double diagY = -_zoomPan.OffsetY / z;
        _canvas._minimap.SetViewport(new Rect(diagX, diagY, vw / z, vh / z));
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateScrollBars()
    {
        if (_zoomPan.ActualWidth < 1 || _zoomPan.ActualHeight < 1) return;

        var    b  = _zoomPan.GetContentBounds();
        double z  = _zoomPan.ZoomFactor;
        double vw = _zoomPan.ActualWidth;
        double vh = _zoomPan.ActualHeight;

        // Scaled content origin — needed to map OffsetX ↔ scroll value correctly.
        // OffsetX = -(scrollValue + contentLeft * z)  →  scrollValue = -OffsetX - contentLeft*z
        _scrollContentLeft = b.Left * z;
        _scrollContentTop  = b.Top  * z;

        _syncingScrollBars = true;
        try
        {
            // Total scaled extent vs viewport.
            // Extend the range to cover the current viewport offset so scrollbars never
            // disappear after an unconstrained middle-mouse pan beyond the content bounds.
            double rawScrollX = -_zoomPan.OffsetX - _scrollContentLeft;
            double hMax   = Math.Max(0, Math.Max(b.Width  * z - vw, rawScrollX + 1));
            double scrollX = Math.Clamp(rawScrollX, 0, hMax);
            _hScroll.Minimum      = 0;
            _hScroll.Maximum      = hMax;
            _hScroll.ViewportSize = vw;
            _hScroll.Value        = scrollX;
            _hScroll.Visibility   = hMax > 1 ? Visibility.Visible : Visibility.Collapsed;

            double rawScrollY = -_zoomPan.OffsetY - _scrollContentTop;
            double vMax   = Math.Max(0, Math.Max(b.Height * z - vh, rawScrollY + 1));
            double scrollY = Math.Clamp(rawScrollY, 0, vMax);
            _vScroll.Minimum      = 0;
            _vScroll.Maximum      = vMax;
            _vScroll.ViewportSize = vh;
            _vScroll.Value        = scrollY;
            _vScroll.Visibility   = vMax > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _syncingScrollBars = false;
        }
    }
}

