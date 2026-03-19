// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlDesignerSplitHost.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-18 — Phase B: IPropertyProviderSource + XamlDesignPropertyProvider.
//                       Phase C2: Arrow key nudge (1px / 10px with Shift).
//                       Phase E4+E5: Rulers/Grid/Snap toggles + Cut/Copy/Paste toolbar + context menu.
//                       Phase F3+F5: Copy/Paste/Duplicate/WrapIn + Ctrl+F element search bar.
// Description:
//     Main document editor for .xaml files.
//     Hosts a CodeEditorSplitHost (code pane) and a ZoomPanCanvas-wrapped
//     DesignCanvas (design pane) side-by-side with a GridSplitter.
//     Provides Code / Split / Design view mode toggle buttons.
//     Auto-preview debounces code changes and re-renders the canvas.
//
// Architecture Notes:
//     Proxy / Delegate Pattern — IDocumentEditor forwarded to CodeEditorSplitHost.
//     Composite — wraps CodeEditorSplitHost + ZoomPanCanvas + DesignCanvas.
//     Observer — DesignCanvas.RenderError drives the error banner.
//     View mode state machine: CodeOnly / Split / DesignOnly.
//     Overkill Undo/Redo — DesignUndoManager replaces raw Stack<DesignOperation>.
//     Phase 1 — DesignInteractionService + undo stack driven by OperationCommitted.
//     Phase 2 — SnapEngineService (wired through DesignInteractionService).
//     Phase 3 — ZoomPanCanvas + ZoomPanViewModel zoom toolbar group.
//     Phase 4 — ToolboxDropService handles AllowDrop + Drop on ZoomPanCanvas.
//     Phase 5 — AlignmentToolbarViewModel alignment group + batch undo support.
//     Phase 9 — DesignTimeXamlPreprocessor filters d:* from preview XAML.
//     Phase 10 — AnimationPreviewService attached after canvas root is known.
//     Phase 11 — Layout Mode Switcher (Design Right/Left/Bottom/Top, Ctrl+Shift+L).
//     Phase B  — IPropertyProviderSource: XamlDesignPropertyProvider wired to F4 panel.
//     Phase C2 — Arrow key nudge on canvas selection.
//     Phase E4 — Rulers / Grid / Snap + Cut/Copy/Paste toolbar buttons.
//     Phase E5 — Context menu extended with Cut/Copy/Paste/Duplicate/Wrap In.
//     Phase F3 — Internal clipboard for XAML element copy/paste.
//     Phase F5 — Ctrl+F element search overlay on the design canvas.
// ==========================================================

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.CodeEditor.Models;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;
using WpfHexEditor.ProjectSystem.Languages;
using WpfHexEditor.SDK.Commands;
// Resolve ambiguity: System.Windows.Controls.Primitives.StatusBarItem vs Editor.Core.StatusBarItem
using CoreStatusBarItem = WpfHexEditor.Editor.Core.StatusBarItem;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Split-pane XAML designer document editor.
/// </summary>
public sealed class XamlDesignerSplitHost : Grid,
    IDocumentEditor,
    IOpenableDocument,
    IEditorPersistable,
    IStatusBarContributor,
    IEditorToolbarContributor,
    IPropertyProviderSource
{
    // ── View mode ─────────────────────────────────────────────────────────────

    private enum ViewMode { CodeOnly, Split, DesignOnly }
    private ViewMode _viewMode = ViewMode.Split;

    private enum SplitLayout
    {
        HorizontalDesignRight,  // code LEFT  | design RIGHT  ← default
        HorizontalDesignLeft,   // design LEFT | code RIGHT
        VerticalDesignBottom,   // code TOP    / design BOTTOM
        VerticalDesignTop,      // design TOP  / code BOTTOM
    }
    private SplitLayout _splitLayout = SplitLayout.HorizontalDesignRight;

    // ── Child controls ────────────────────────────────────────────────────────

    private readonly CodeEditorSplitHost _codeHost;
    private readonly DesignCanvas        _designCanvas;
    private readonly ZoomPanCanvas       _zoomPan;
    private readonly GridSplitter        _splitter;

    // Design pane scroll wrapper (contains _zoomPan + H/V scrollbars).
    private Grid      _designPaneGrid    = null!;
    private ScrollBar _hScrollBar        = null!;
    private ScrollBar _vScrollBar        = null!;
    private bool      _isSyncingScrollBars;

    private readonly ColumnDefinition    _codeColumn     = new() { Width  = new GridLength(1, GridUnitType.Star) };
    private readonly ColumnDefinition    _splitterColumn = new() { Width  = new GridLength(4) };
    private readonly ColumnDefinition    _designColumn   = new() { Width  = new GridLength(1, GridUnitType.Star) };

    // Row definitions for vertical split orientation.
    private readonly RowDefinition       _contentCodeRow   = new() { Height = new GridLength(1, GridUnitType.Star) };
    private readonly RowDefinition       _splitterRow      = new() { Height = new GridLength(4) };
    private readonly RowDefinition       _contentDesignRow = new() { Height = new GridLength(1, GridUnitType.Star) };

    // Toolbar row definition + container reference (managed by UpdateGridLayout).
    private readonly RowDefinition       _toolbarRow = new() { Height = GridLength.Auto };
    private Border?                      _toolbarContainer;

    // Toolbar strip controls
    private readonly ToggleButton _btnCodeOnly;
    private readonly ToggleButton _btnSplit;
    private readonly ToggleButton _btnDesignOnly;
    private readonly ToggleButton _btnAutoPreview;
    private readonly Border       _errorBanner;
    private readonly TextBlock    _errorText;

    // ── Overkill Undo/Redo — DesignUndoManager ────────────────────────────────

    private readonly DesignUndoManager _undoManager = new();
    private Button? _btnUndo;
    private Button? _btnRedo;

    // ── Phase 1 — Design interaction ──────────────────────────────────────────

    private readonly DesignToXamlSyncService     _syncService        = new();
    private readonly DesignInteractionService     _interactionService = new();

    // ── Bidirectional canvas↔code selection sync ──────────────────────────────

    private readonly XamlSourceLocationService _locationService = new();

    /// <summary>
    /// Guards against feedback loops: when we're programmatically driving a sync in one
    /// direction, this flag suppresses the reactive event that would trigger the reverse.
    /// Also set by ApplyXamlToCode to prevent LoadFromString's caret-reset from
    /// launching a spurious Code→Canvas sync.
    /// </summary>
    private bool _isSyncingSelection;

    /// <summary>
    /// Debounce timer for Code→Canvas sync (150ms idle window).
    /// Coalesces rapid CaretMoved + SelectionChanged events so that FindUidAtLine
    /// (which parses the full XAML) is called at most once per idle interval.
    /// </summary>
    private readonly DispatcherTimer _codeToCanvasSyncTimer = new()
        { Interval = TimeSpan.FromMilliseconds(150) };

    // ── Phase 4 — Toolbox drag-drop ───────────────────────────────────────────

    private readonly ToolboxDropService _dropService = new();

    // ── Phase 5 — Alignment ───────────────────────────────────────────────────

    private readonly AlignmentToolbarViewModel _alignVm = new();

    // ── Phase 9 — Design-time data ────────────────────────────────────────────

    private readonly DesignTimeXamlPreprocessor _preprocessor = new();

    // ── Phase 10 — Animation preview ─────────────────────────────────────────

    private readonly AnimationPreviewService _animPreviewService = new();

    // ── Phase 3 — Zoom toolbar ────────────────────────────────────────────────

    private readonly ZoomPanViewModel _zoomVm;

    // ── Phase B — F4 property provider ────────────────────────────────────────

    private readonly XamlDesignPropertyProvider _idePropertyProvider = new();

    // ── Phase F3 — Internal XAML clipboard ────────────────────────────────────

    private string? _clipboardXaml;

    // ── Phase F5 — Element search overlay ─────────────────────────────────────

    private Border?  _searchBar;
    private TextBox? _searchBox;

    // ── Auto-preview debounce ─────────────────────────────────────────────────

    private readonly DispatcherTimer _previewTimer;
    private bool _autoPreviewEnabled = true;

    // ── Document state ────────────────────────────────────────────────────────

    private readonly XamlDocument _document = new();
    private string?  _filePath;

    // ── Status bar ─────────────────────────────────────────────────────────────

    private readonly CoreStatusBarItem _sbElement     = new() { Label = "XAML",  Value = "" };
    private readonly CoreStatusBarItem _sbCoordinates = new() { Label = "Pos",   Value = "" };
    private readonly CoreStatusBarItem _sbZoom        = new() { Label = "Zoom",  Value = "100%" };
    private readonly CoreStatusBarItem _sbViewMode    = new() { Label = "View",   Value = "Split" };
    private readonly CoreStatusBarItem _sbLayout      = new() { Label = "Layout", Value = "Design Right" };

    // ── Toolbar contributor ────────────────────────────────────────────────────

    /// <summary>Contextual toolbar items exposed to the IDE's dynamic toolbar pod.</summary>
    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; } = new();

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired when the selected element changes (used by the plugin to sync panels).</summary>
    public event EventHandler? SelectedElementChanged;

    /// <summary>Fired when the user requests to focus the Property Inspector panel.</summary>
    public event EventHandler? FocusPropertiesPanelRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlDesignerSplitHost()
    {
        // Column/row definitions are declared as fields and managed by UpdateGridLayout().

        // -- Design canvas (Phase 1) -----------------------------------------
        _designCanvas = new DesignCanvas();
        _designCanvas.EnableInteraction(_interactionService);

        // -- ZoomPanCanvas wrapping DesignCanvas (Phase 3) -------------------
        // AdornerDecorator ensures adorners (selection/resize/hover) are rendered
        // inside ZoomPanCanvas.ClipToBounds — not at the Window-level AdornerLayer,
        // which would cause them to bleed outside the designer viewport.
        var designAdornerHost = new AdornerDecorator { Child = _designCanvas };

        // Explicit size so ZoomPanCanvas.FitToContent / ClampOffsets / CenterContent see
        // the natural design canvas width (e.g. 1280) rather than (viewportWidth / zoom).
        // Without this, LayoutTransform on the AdornerDecorator makes ActualWidth =
        // viewportWidth / zoom, causing all zoom/scroll formulas to resolve to viewport size.
        designAdornerHost.Width  = _designCanvas.Width;    // initially 1280
        designAdornerHost.Height = _designCanvas.Height;   // initially 720

        // Sync when DesignCanvas changes its declared size (XAML root parsing sets new W/H).
        _designCanvas.SizeChanged += (_, _) =>
        {
            designAdornerHost.Width  = _designCanvas.Width;
            designAdornerHost.Height = _designCanvas.Height;
        };

        _zoomPan = new ZoomPanCanvas
        {
            Content   = designAdornerHost,
            AllowDrop = true
        };
        _zoomVm = new ZoomPanViewModel(_zoomPan);
        _zoomPan.ZoomChanged += (_, _) => _sbZoom.Value = _zoomVm.ZoomLabel;

        // -- Toolbar strip ---------------------------------------------------
        _toolbarContainer = BuildToolbar(out _btnCodeOnly, out _btnSplit, out _btnDesignOnly,
                                         out _btnAutoPreview, out _errorBanner, out _errorText);
        Children.Add(_toolbarContainer);

        // -- Code pane -------------------------------------------------------
        _codeHost = new CodeEditorSplitHost();
        Children.Add(_codeHost);

        // -- GridSplitter (direction/position configured by UpdateGridLayout) --
        _splitter = new GridSplitter
        {
            ResizeBehavior = GridResizeBehavior.PreviousAndNext
        };
        _splitter.SetResourceReference(BackgroundProperty, "DockSplitterBrush");
        Children.Add(_splitter);

        // -- Design pane wrapper: ZoomPanCanvas + H/V scrollbars -------------
        _hScrollBar = new ScrollBar { Orientation = Orientation.Horizontal };
        _vScrollBar = new ScrollBar { Orientation = Orientation.Vertical   };

        _designPaneGrid = new Grid();
        _designPaneGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _designPaneGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _designPaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _designPaneGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetRow(_zoomPan,    0); Grid.SetColumn(_zoomPan,    0);
        Grid.SetRow(_hScrollBar, 1); Grid.SetColumn(_hScrollBar, 0);
        Grid.SetRow(_vScrollBar, 0); Grid.SetColumn(_vScrollBar, 1);

        // Corner rectangle filling the gap where both scrollbars meet.
        var scrollCorner = new Rectangle { Width = SystemParameters.VerticalScrollBarWidth };
        scrollCorner.SetResourceReference(Rectangle.FillProperty, "DockSplitterBrush");
        Grid.SetRow(scrollCorner, 1); Grid.SetColumn(scrollCorner, 1);

        _designPaneGrid.Children.Add(_zoomPan);
        _designPaneGrid.Children.Add(_hScrollBar);
        _designPaneGrid.Children.Add(_vScrollBar);
        _designPaneGrid.Children.Add(scrollCorner);

        // Scrollbar → canvas: value 0 = content at +ScrollExtraMargin (left/top blank margin visible).
        _hScrollBar.ValueChanged += (_, _) =>
        {
            if (_isSyncingScrollBars) return;
            _zoomPan.OffsetX = ZoomPanCanvas.ScrollExtraMargin - _hScrollBar.Value;
        };
        _vScrollBar.ValueChanged += (_, _) =>
        {
            if (_isSyncingScrollBars) return;
            _zoomPan.OffsetY = ZoomPanCanvas.ScrollExtraMargin - _vScrollBar.Value;
        };

        // Canvas → scrollbars: refresh ranges/values on every zoom, pan, or resize.
        _zoomPan.ZoomChanged += (_, _) => UpdateScrollBars();
        _zoomPan.PanChanged  += (_, _) => UpdateScrollBars();
        _zoomPan.SizeChanged += (_, _) => UpdateScrollBars();

        Children.Add(_designPaneGrid);

        // -- Auto-preview timer ----------------------------------------------
        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _previewTimer.Tick += OnPreviewTimerTick;

        // -- Wire design interaction + undo (Phase 1 / Overkill) ------------
        _interactionService.OperationCommitted += OnDesignOperationCommitted;

        // -- Wire alignment batch undo (Phase 5 / Overkill) -----------------
        _alignVm.OperationsBatch += OnAlignmentOperationsBatch;

        // -- Wire undo manager history changes -------------------------------
        _undoManager.HistoryChanged += OnUndoHistoryChanged;

        // -- Register ApplicationCommands for Ctrl+Z / Ctrl+Y ---------------
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo,
            (_, _) => Undo(),
            (_, e) => { e.CanExecute = CanUndo; e.Handled = true; }));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo,
            (_, _) => Redo(),
            (_, e) => { e.CanExecute = CanRedo; e.Handled = true; }));

        // -- Wire toolbox drop (Phase 4) ------------------------------------
        _zoomPan.Drop  += OnZoomPanDrop;
        _zoomPan.DragOver += (_, e) =>
        {
            if (e.Data.GetDataPresent(ToolboxDropService.DragDropFormat))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        };

        // -- Wire code host events -------------------------------------------
        _codeHost.PrimaryEditor.ModifiedChanged  += OnCodeModified;
        // Code→Canvas debounced sync: coalesce CaretMoved (line changes) and
        // SelectionChanged (same-line clicks) into a single 150ms-idle timer tick.
        _codeToCanvasSyncTimer.Tick              += OnCodeToCanvasSyncTimerTick;
        _codeHost.PrimaryEditor.CaretMoved       += (_, _) => RequestCodeToCanvasSync();
        _codeHost.PrimaryEditor.SelectionChanged += (_, _) => RequestCodeToCanvasSync();
        _designCanvas.RenderError                += OnRenderError;
        _designCanvas.SelectedElementChanged     += OnDesignSelectionChanged;

        // -- Phase B: wire property provider to selection changes ------------
        _designCanvas.SelectedElementChanged += (_, _) => UpdateIdePropertyProvider();

        // -- Phase F5: build search overlay (floats over design pane) --------
        _searchBar = BuildSearchBar(out _searchBox);
        _searchBar.HorizontalAlignment = HorizontalAlignment.Right;
        _searchBar.VerticalAlignment   = VerticalAlignment.Top;
        _searchBar.Margin              = new Thickness(0, 4, 4, 0);
        _searchBar.Visibility          = Visibility.Collapsed;
        Panel.SetZIndex(_searchBar, 100); // appear above splitter and panes
        Children.Add(_searchBar);

        // -- Forward IDocumentEditor events from code pane ------------------
        _codeHost.ModifiedChanged  += (s, e) => ModifiedChanged?.Invoke(this, e);
        _codeHost.CanUndoChanged   += (s, e) => CanUndoChanged?.Invoke(this, e);
        _codeHost.CanRedoChanged   += (s, e) => CanRedoChanged?.Invoke(this, e);
        _codeHost.TitleChanged     += (s, e) => TitleChanged?.Invoke(this, e);
        _codeHost.StatusMessage    += (s, e) => StatusMessage?.Invoke(this, e);
        _codeHost.OutputMessage    += (s, e) => OutputMessage?.Invoke(this, e);
        _codeHost.SelectionChanged += (s, e) => SelectionChanged?.Invoke(this, e);
        _codeHost.OperationStarted   += (s, e) => OperationStarted?.Invoke(this, e);
        _codeHost.OperationProgress  += (s, e) => OperationProgress?.Invoke(this, e);
        _codeHost.OperationCompleted += (s, e) => OperationCompleted?.Invoke(this, e);

        // Apply initial layout + view mode (also triggers UpdateGridLayout).
        ApplySplitLayout(SplitLayout.HorizontalDesignRight);

        // -- StatusBar: view mode choices ------------------------------------
        _sbViewMode.Choices.Add(new StatusBarChoice { DisplayName = "Code Only",   IsActive = false, Command = new RelayCommand(_ => ApplyViewMode(ViewMode.CodeOnly)) });
        _sbViewMode.Choices.Add(new StatusBarChoice { DisplayName = "Split",       IsActive = true,  Command = new RelayCommand(_ => ApplyViewMode(ViewMode.Split)) });
        _sbViewMode.Choices.Add(new StatusBarChoice { DisplayName = "Design Only", IsActive = false, Command = new RelayCommand(_ => ApplyViewMode(ViewMode.DesignOnly)) });

        // -- StatusBar: layout choices --------------------------------------
        _sbLayout.Choices.Add(new StatusBarChoice { DisplayName = "Design Right",  IsActive = true,  Command = new RelayCommand(_ => ApplySplitLayout(SplitLayout.HorizontalDesignRight)) });
        _sbLayout.Choices.Add(new StatusBarChoice { DisplayName = "Design Left",   IsActive = false, Command = new RelayCommand(_ => ApplySplitLayout(SplitLayout.HorizontalDesignLeft)) });
        _sbLayout.Choices.Add(new StatusBarChoice { DisplayName = "Design Bottom", IsActive = false, Command = new RelayCommand(_ => ApplySplitLayout(SplitLayout.VerticalDesignBottom)) });
        _sbLayout.Choices.Add(new StatusBarChoice { DisplayName = "Design Top",    IsActive = false, Command = new RelayCommand(_ => ApplySplitLayout(SplitLayout.VerticalDesignTop)) });

        // -- IDE toolbar pod items -------------------------------------------
        BuildToolbarItems();

        // -- Design canvas context menu -------------------------------------
        _designCanvas.ContextMenu = BuildDesignContextMenu();

        // -- Keyboard shortcuts ----------------------------------------------
        Focusable = true;
        PreviewKeyDown += OnGridKeyDown;

        // -- Initialize UndoCommand / RedoCommand backing fields ------------
        UndoCommand = new RelayCommand(_ => Undo(), _ => CanUndo);
        RedoCommand = new RelayCommand(_ => Redo(), _ => CanRedo);
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>Exposes the design canvas for plugin wiring (selection events, etc.).</summary>
    public DesignCanvas Canvas => _designCanvas;

    /// <summary>Exposes the XAML document model for plugin wiring (outline tree, etc.).</summary>
    public XamlDocument Document => _document;

    /// <summary>Exposes the animation preview service for Phase 10 panel wiring.</summary>
    public AnimationPreviewService AnimationPreviewService => _animPreviewService;

    /// <summary>Exposes the zoom view model for external toolbar binding.</summary>
    public ZoomPanViewModel ZoomViewModel => _zoomVm;

    /// <summary>Exposes alignment commands for external toolbar binding.</summary>
    public AlignmentToolbarViewModel AlignmentViewModel => _alignVm;

    /// <summary>Exposes the undo manager for the History Panel plugin wiring.</summary>
    public DesignUndoManager UndoManager => _undoManager;

    // ── IPropertyProviderSource ───────────────────────────────────────────────

    /// <summary>
    /// Returns the long-lived property provider that reflects the current canvas selection.
    /// Called by the IDE when the active document tab changes.
    /// </summary>
    public IPropertyProvider? GetPropertyProvider() => _idePropertyProvider;

    // ── IOpenableDocument ─────────────────────────────────────────────────────

    async Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
    {
        _filePath = filePath;

        // Unsubscribe from any previous document before loading a new file.
        if (_codeHost.PrimaryEditor.Document is { } prevDoc)
            prevDoc.TextChanged -= OnDocumentTextChanged;

        // Delegate to the code host — it resolves the XAML language highlighter
        // internally via LanguageRegistry.Instance.GetLanguageForFile (CodeEditorSplitHost.OpenAsync).
        await ((IOpenableDocument)_codeHost).OpenAsync(filePath, ct);

        // Subscribe to raw text changes for live auto-preview on every keystroke.
        if (_codeHost.PrimaryEditor.Document is { } doc)
            doc.TextChanged += OnDocumentTextChanged;

        // Trigger initial render after the file is loaded.
        if (_autoPreviewEnabled)
            TriggerPreview();
    }

    // ── IDocumentEditor (proxy to _codeHost) ─────────────────────────────────

    private IDocumentEditor Active => _codeHost;

    public bool     IsDirty    => Active.IsDirty;
    public bool     CanUndo    => Active.CanUndo || _undoManager.CanUndo;
    public bool     CanRedo    => Active.CanRedo || _undoManager.CanRedo;
    public bool     IsReadOnly { get => Active.IsReadOnly; set { Active.IsReadOnly = value; } }
    public string   Title      => Active.Title;
    public bool     IsBusy     => Active.IsBusy;

    public ICommand? UndoCommand      { get; private set; }
    public ICommand? RedoCommand      { get; private set; }
    public ICommand? SaveCommand      => Active.SaveCommand;
    public ICommand? CopyCommand      => Active.CopyCommand;
    public ICommand? CutCommand       => Active.CutCommand;
    public ICommand? PasteCommand     => Active.PasteCommand;
    public ICommand? DeleteCommand    => Active.DeleteCommand;
    public ICommand? SelectAllCommand => Active.SelectAllCommand;

    public void Undo()
    {
        // Design undo stack takes priority when in design/split mode.
        if (_undoManager.CanUndo && _viewMode != ViewMode.CodeOnly)
        {
            ExecuteUndoEntry(_undoManager.PopUndo());
            return;
        }
        Active.Undo();
    }

    public void Redo()
    {
        if (_undoManager.CanRedo && _viewMode != ViewMode.CodeOnly)
        {
            ExecuteRedoEntry(_undoManager.PopRedo());
            return;
        }
        Active.Redo();
    }

    /// <summary>
    /// Jumps to a specific history state by applying the requested number of
    /// undo and redo steps. Called by the Design History Panel code-behind.
    /// </summary>
    public void JumpToHistoryEntry(int undoCount, int redoCount)
    {
        _undoManager.JumpToHistoryEntry(
            undoCount, redoCount,
            entry => ExecuteUndoEntry(entry),
            entry => ExecuteRedoEntry(entry));
    }

    public void Save()          => Active.Save();
    public Task SaveAsync(CancellationToken ct = default)                    => Active.SaveAsync(ct);
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => Active.SaveAsAsync(filePath, ct);
    public void Copy()          => Active.Copy();
    public void Cut()           => Active.Cut();
    public void Paste()         => Active.Paste();
    public void Delete()        => Active.Delete();
    public void SelectAll()     => Active.SelectAll();
    public void CancelOperation() => Active.CancelOperation();

    public void Close()
    {
        _previewTimer.Stop();
        _animPreviewService.Stop();
        ((IDocumentEditor)_codeHost).Close();
    }

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

    // ── IEditorPersistable ────────────────────────────────────────────────────

    public EditorConfigDto GetEditorConfig()
    {
        var dto = ((object)_codeHost is IEditorPersistable p)
            ? p.GetEditorConfig()
            : new EditorConfigDto();

        // Persist XAML designer-specific state in the Extra dictionary.
        dto.Extra ??= new Dictionary<string, string>();
        dto.Extra["xd.view"]    = _viewMode.ToString();
        dto.Extra["xd.layout"]  = _splitLayout.ToString();
        dto.Extra["xd.ratio"]   = GetSplitRatio().ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        dto.Extra["xd.zoom"]    = _zoomPan.ZoomLevel.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
        dto.Extra["xd.selPath"] = _designCanvas.SelectedElement is null
            ? string.Empty
            : GetElementPath(_designCanvas.SelectedElement);

        return dto;
    }

    public void ApplyEditorConfig(EditorConfigDto config)
    {
        if (((object)_codeHost) is IEditorPersistable p)
            p.ApplyEditorConfig(config);

        if (config.Extra is not null)
        {
            if (config.Extra.TryGetValue("xd.layout", out var layoutStr)
                && Enum.TryParse<SplitLayout>(layoutStr, out var sl))
                ApplySplitLayout(sl);

            if (config.Extra.TryGetValue("xd.view", out var viewStr)
                && Enum.TryParse<ViewMode>(viewStr, out var mode))
                ApplyViewMode(mode);

            if (config.Extra.TryGetValue("xd.ratio", out var ratioStr)
                && double.TryParse(ratioStr, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var ratio))
                SetSplitRatio(ratio);

            if (config.Extra.TryGetValue("xd.zoom", out var zoomStr)
                && double.TryParse(zoomStr, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out var zoom))
                _zoomPan.ZoomLevel = zoom;
        }
    }

    public byte[]? GetUnsavedModifications()
        => (((object)_codeHost) as IEditorPersistable)?.GetUnsavedModifications();

    public void ApplyUnsavedModifications(byte[] data)
        => (((object)_codeHost) as IEditorPersistable)?.ApplyUnsavedModifications(data);

    public ChangesetSnapshot GetChangesetSnapshot()
        => (((object)_codeHost) as IEditorPersistable)?.GetChangesetSnapshot() ?? ChangesetSnapshot.Empty;

    public void ApplyChangeset(ChangesetDto changeset)
        => (((object)_codeHost) as IEditorPersistable)?.ApplyChangeset(changeset);

    public void MarkChangesetSaved()
        => (((object)_codeHost) as IEditorPersistable)?.MarkChangesetSaved();

    public IReadOnlyList<BookmarkDto>? GetBookmarks()
        => (((object)_codeHost) as IEditorPersistable)?.GetBookmarks();

    public void ApplyBookmarks(IReadOnlyList<BookmarkDto> bookmarks)
        => (((object)_codeHost) as IEditorPersistable)?.ApplyBookmarks(bookmarks);

    // ── IStatusBarContributor ─────────────────────────────────────────────────

    public ObservableCollection<CoreStatusBarItem> StatusBarItems { get; } = new();

    public void RefreshStatusBarItems()
    {
        if (StatusBarItems.Count == 0)
        {
            StatusBarItems.Add(_sbElement);
            StatusBarItems.Add(_sbCoordinates);
            StatusBarItems.Add(_sbZoom);
            StatusBarItems.Add(_sbViewMode);
            StatusBarItems.Add(_sbLayout);
        }

        var el = _designCanvas.SelectedElement;
        _sbElement.Value = el is null
            ? string.Empty
            : $"{el.GetType().Name}  •  Esc = parent  •  Alt+Click: cycle";
        _sbCoordinates.Value = el is System.Windows.FrameworkElement fe
            ? $"{fe.ActualWidth:F0} × {fe.ActualHeight:F0}"
            : string.Empty;
    }

    // ── Auto-preview ──────────────────────────────────────────────────────────

    private void OnCodeModified(object? sender, EventArgs e)
    {
        if (!_autoPreviewEnabled) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    /// <summary>
    /// Fires on every raw keystroke (document TextChanged), debouncing the preview.
    /// This supplements OnCodeModified which only fires when the "is modified" flag
    /// transitions (true→false or false→true), not on every edit.
    /// </summary>
    private void OnDocumentTextChanged(object? sender, WpfHexEditor.Editor.CodeEditor.Models.TextChangedEventArgs e)
    {
        if (!_autoPreviewEnabled) return;
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        _previewTimer.Stop();
        TriggerPreview();
    }

    private void TriggerPreview()
    {
        var rawText = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        _document.SetXaml(rawText);

        // Phase 9 — strip d:* namespace before rendering.
        string previewText = DesignTimeXamlPreprocessor.HasDesignNamespace(rawText)
            ? _preprocessor.Process(rawText, out _)
            : rawText;

        _designCanvas.XamlSource = previewText;

        // Attach animation preview service to the newly rendered root (Phase 10).
        Dispatcher.InvokeAsync(() =>
        {
            if (_designCanvas.DesignRoot is System.Windows.FrameworkElement root)
                _animPreviewService.Attach(root);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ── Overkill Undo/Redo — undo/redo entry execution ────────────────────────

    /// <summary>
    /// Applies the reverse of a design operation to the code editor (undo direction).
    /// </summary>
    private void ExecuteUndoEntry(IDesignUndoEntry? entry)
    {
        if (entry is null) return;
        var rawXaml = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        var restored = entry switch
        {
            SingleDesignUndoEntry s    => _syncService.ApplyUndo(rawXaml, s.Operation),
            BatchDesignUndoEntry b     => _syncService.ApplyBatchUndo(rawXaml, b.Operations),
            SnapshotDesignUndoEntry sn => sn.BeforeXaml,
            _                          => rawXaml
        };
        ApplyXamlToCode(restored);
    }

    /// <summary>
    /// Re-applies a design operation to the code editor (redo direction).
    /// </summary>
    private void ExecuteRedoEntry(IDesignUndoEntry? entry)
    {
        if (entry is null) return;
        var rawXaml = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        var restored = entry switch
        {
            SingleDesignUndoEntry s    => _syncService.ApplyRedo(rawXaml, s.Operation),
            BatchDesignUndoEntry b     => _syncService.ApplyBatchRedo(rawXaml, b.Operations),
            SnapshotDesignUndoEntry sn => sn.AfterXaml,
            _                          => rawXaml
        };
        ApplyXamlToCode(restored);
    }

    // ── Overkill Undo/Redo — history changed notification ─────────────────────

    private void OnUndoHistoryChanged(object? sender, EventArgs e)
    {
        CanUndoChanged?.Invoke(this, EventArgs.Empty);
        CanRedoChanged?.Invoke(this, EventArgs.Empty);
        CommandManager.InvalidateRequerySuggested();
        UpdateUndoRedoTooltips();
    }

    private void UpdateUndoRedoTooltips()
    {
        if (_btnUndo is not null)
            _btnUndo.ToolTip = _undoManager.CanUndo
                ? $"Undo: {_undoManager.UndoDescription}"
                : "Nothing to undo";

        if (_btnRedo is not null)
            _btnRedo.ToolTip = _undoManager.CanRedo
                ? $"Redo: {_undoManager.RedoDescription}"
                : "Nothing to redo";
    }

    // ── Phase 1 — Design operation committed → undo manager + XAML patch ─────

    private void OnDesignOperationCommitted(object? sender, DesignOperationCommittedEventArgs e)
    {
        var rawXaml = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        // Route through ApplyOperation so structural operations (e.g. Rotate) use the correct patch path.
        var patched  = _syncService.ApplyOperation(rawXaml, e.Operation);
        _undoManager.PushEntry(new SingleDesignUndoEntry(e.Operation));
        ApplyXamlToCode(patched);
    }

    // ── Phase 5 — Alignment batch undo ────────────────────────────────────────

    private void OnAlignmentOperationsBatch(object? sender, IReadOnlyList<AlignmentResult> results)
    {
        if (results.Count == 0) return;

        // Build a batch undo entry from all alignment results.
        var ops  = results.Select(r => r.Operation).ToList();
        var desc = results.Count == 1
            ? results[0].Operation.Description
            : $"Align {results.Count} elements";

        _undoManager.PushEntry(new BatchDesignUndoEntry(ops, desc));

        // Sync code editor — canvas UIElements are already moved by AlignmentService.
        var rawXaml = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        ApplyXamlToCode(_syncService.ApplyBatchRedo(rawXaml, ops));
    }

    // ── Phase 4 — Toolbox drag-drop onto ZoomPanCanvas ───────────────────────

    private void OnZoomPanDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ToolboxDropService.DragDropFormat)) return;
        if (e.Data.GetData(ToolboxDropService.DragDropFormat) is not ToolboxItem item) return;

        var dropPoint  = e.GetPosition(_designCanvas);
        var beforeXaml = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;

        // Determine if top-level element is a Canvas (for Canvas.Left/Top positioning).
        bool isCanvasParent = beforeXaml.Contains("<Canvas");

        var afterXaml = _dropService.InsertItem(beforeXaml, item, dropPoint, isCanvasParent);

        // Snapshot entry captures full XAML before/after to survive UID invalidation.
        _undoManager.PushEntry(new SnapshotDesignUndoEntry(beforeXaml, afterXaml, $"Insert {item.Name}"));
        ApplyXamlToCode(afterXaml);
    }

    // ── Render error ──────────────────────────────────────────────────────────

    private void OnRenderError(object? sender, string? message)
    {
        _errorBanner.Visibility = message is not null ? Visibility.Visible : Visibility.Collapsed;
        _errorText.Text         = message ?? string.Empty;
    }

    // ── Design canvas selection ───────────────────────────────────────────────

    private void OnDesignSelectionChanged(object? sender, EventArgs e)
    {
        RefreshStatusBarItems();
        SelectedElementChanged?.Invoke(this, EventArgs.Empty);

        // Canvas → Code sync: navigate the code editor to the selected element's line.
        if (_isSyncingSelection || !IsDesignVisible()) return;

        int uid = _designCanvas.SelectedElementUid;
        if (uid < 0) return;

        var raw  = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        int line = _locationService.FindElementStartLine(raw, uid);
        if (line < 0) return;

        // Don't steal the caret if it is already on the element's line — avoids jarring
        // caret jumps after the render-restore cycle that silently re-selects an element.
        if (_codeHost.PrimaryEditor.CursorPosition.Line == line) return;

        _isSyncingSelection = true;
        try   { ((INavigableDocument)_codeHost.PrimaryEditor).NavigateTo(line + 1, 1); }
        finally { _isSyncingSelection = false; }
    }

    // ── Code → Canvas sync (debounced) ────────────────────────────────────────

    /// <summary>
    /// Starts / restarts the 150ms Code→Canvas debounce timer.
    /// Called by both CaretMoved (line changes) and SelectionChanged (same-line clicks)
    /// to ensure same-line re-clicks also trigger a canvas sync.
    /// </summary>
    private void RequestCodeToCanvasSync()
    {
        if (_isSyncingSelection || !IsDesignVisible()) return;
        _codeToCanvasSyncTimer.Stop();
        _codeToCanvasSyncTimer.Start();
    }

    /// <summary>
    /// Fired 150ms after the last caret/selection change.
    /// Finds the XAML element at the caret line and selects it on the canvas.
    /// </summary>
    private void OnCodeToCanvasSyncTimerTick(object? sender, EventArgs e)
    {
        _codeToCanvasSyncTimer.Stop();
        if (_isSyncingSelection || !IsDesignVisible()) return;
        if (!_codeHost.PrimaryEditor.Selection.IsEmpty) return;  // ignore text-selection drags

        var raw  = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        int line = _codeHost.PrimaryEditor.CursorPosition.Line;  // 0-based
        if (line < 0) return;
        int uid  = _locationService.FindUidAtLine(raw, line);
        if (uid < 0) return;

        _isSyncingSelection = true;
        try   { _designCanvas.SelectElementByUid(uid); }
        finally { _isSyncingSelection = false; }
    }

    /// <summary>
    /// Returns true when the design canvas is visible (Split or Design-Only mode).
    /// Sync is suppressed in Code-Only mode since the canvas is hidden.
    /// </summary>
    private bool IsDesignVisible()
        => _viewMode is ViewMode.Split or ViewMode.DesignOnly;

    // ── Phase B — Property provider update ────────────────────────────────────

    /// <summary>
    /// Notifies the IDE property provider of the current canvas selection.
    /// Called every time the canvas raises SelectedElementChanged.
    /// </summary>
    private void UpdateIdePropertyProvider()
    {
        var el   = _designCanvas.SelectedElement as System.Windows.DependencyObject;
        var name = el?.GetType().Name ?? "No selection";
        _idePropertyProvider.SetTarget(el, name, PatchPropertyFromProvider);
    }

    /// <summary>
    /// Callback supplied to <see cref="XamlDesignPropertyProvider"/>: applies a
    /// single attribute patch to the code editor and registers a snapshot undo entry.
    /// </summary>
    private void PatchPropertyFromProvider(string propName, string? val)
    {
        var uid = _designCanvas.SelectedElementUid;
        if (uid < 0) return;

        var before = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        var after  = _syncService.PatchElement(before, uid,
                         new Dictionary<string, string?> { [propName] = val });

        if (string.Equals(before, after, StringComparison.Ordinal)) return;

        _undoManager.PushEntry(new SnapshotDesignUndoEntry(before, after, $"Set {propName}"));
        ApplyXamlToCode(after);
    }

    // ── View mode & layout ────────────────────────────────────────────────────

    private void ApplyViewMode(ViewMode mode)
    {
        _viewMode = mode;
        UpdateGridLayout();
    }

    private void ApplySplitLayout(SplitLayout layout)
    {
        _splitLayout = layout;
        UpdateGridLayout();
    }

    /// <summary>
    /// Refreshes H/V scrollbar visibility, range, and thumb position to match
    /// the current ZoomPanCanvas zoom level and pan offset.
    /// Scrollbars are hidden when the design canvas fits entirely inside the viewport.
    /// </summary>
    private void UpdateScrollBars()
    {
        if (_isSyncingScrollBars) return;
        if (_zoomPan.Content is not FrameworkElement content) return;

        double cw    = content.ActualWidth  * _zoomPan.ZoomLevel;
        double ch    = content.ActualHeight * _zoomPan.ZoomLevel;
        double vw    = _zoomPan.ActualWidth;
        double vh    = _zoomPan.ActualHeight;
        double extra = ZoomPanCanvas.ScrollExtraMargin;

        _isSyncingScrollBars = true;
        try
        {
            // Horizontal — total range = content + 2×extra margins - viewport (always enabled).
            // Value 0 → OffsetX = +extra (left blank margin), Value hMax → OffsetX = vw-cw-extra (right blank margin).
            double hMax = Math.Max(0, cw + extra * 2 - vw);
            _hScrollBar.IsEnabled    = hMax > 1;
            _hScrollBar.Minimum      = 0;
            _hScrollBar.Maximum      = Math.Max(1, hMax);
            _hScrollBar.ViewportSize = vw;
            _hScrollBar.LargeChange  = vw;
            _hScrollBar.SmallChange  = 50;
            _hScrollBar.Value        = Math.Clamp(extra - _zoomPan.OffsetX, 0, Math.Max(1, hMax));

            // Vertical
            double vMax = Math.Max(0, ch + extra * 2 - vh);
            _vScrollBar.IsEnabled    = vMax > 1;
            _vScrollBar.Minimum      = 0;
            _vScrollBar.Maximum      = Math.Max(1, vMax);
            _vScrollBar.ViewportSize = vh;
            _vScrollBar.LargeChange  = vh;
            _vScrollBar.SmallChange  = 50;
            _vScrollBar.Value        = Math.Clamp(extra - _zoomPan.OffsetY, 0, Math.Max(1, vMax));
        }
        finally
        {
            _isSyncingScrollBars = false;
        }
    }

    /// <summary>
    /// Rebuilds Grid column/row definitions and repositions all children
    /// based on the current <see cref="_viewMode"/> and <see cref="_splitLayout"/>.
    /// This is the single source-of-truth for all layout state.
    /// </summary>
    private void UpdateGridLayout()
    {
        bool isVertical  = _splitLayout is SplitLayout.VerticalDesignBottom or SplitLayout.VerticalDesignTop;
        bool designFirst = _splitLayout is SplitLayout.HorizontalDesignLeft  or SplitLayout.VerticalDesignTop;

        var starSize   = new GridLength(1, GridUnitType.Star);
        var zeroSize   = new GridLength(0);
        var splitterSz = new GridLength(4);

        bool showCode   = _viewMode != ViewMode.DesignOnly;
        bool showDesign = _viewMode != ViewMode.CodeOnly;
        bool showSplit  = _viewMode == ViewMode.Split;

        // ── Rebuild definitions ───────────────────────────────────────────────
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();
        RowDefinitions.Add(_toolbarRow);

        if (isVertical)
        {
            // Single content column; 4 rows: toolbar | first | splitter | second
            ColumnDefinitions.Add(new ColumnDefinition { Width = starSize });

            var firstRow   = designFirst ? _contentDesignRow : _contentCodeRow;
            var secondRow  = designFirst ? _contentCodeRow   : _contentDesignRow;
            bool showFirst  = designFirst ? showDesign : showCode;
            bool showSecond = designFirst ? showCode   : showDesign;

            if (showSplit)
            {
                // Preserve ratio; restore from zero if coming from a hidden-pane mode.
                if (firstRow.Height  == zeroSize) firstRow.Height  = starSize;
                if (secondRow.Height == zeroSize) secondRow.Height = starSize;
                _splitterRow.Height = splitterSz;
            }
            else
            {
                firstRow.Height     = showFirst  ? starSize : zeroSize;
                secondRow.Height    = showSecond ? starSize : zeroSize;
                _splitterRow.Height = zeroSize;
            }

            RowDefinitions.Add(firstRow);
            RowDefinitions.Add(_splitterRow);
            RowDefinitions.Add(secondRow);
        }
        else
        {
            // Single content row; 3 columns: first | splitter | second
            RowDefinitions.Add(new RowDefinition { Height = starSize });

            var firstCol   = designFirst ? _designColumn  : _codeColumn;
            var secondCol  = designFirst ? _codeColumn    : _designColumn;
            bool showFirst  = designFirst ? showDesign : showCode;
            bool showSecond = designFirst ? showCode   : showDesign;

            if (showSplit)
            {
                // Preserve ratio; restore from zero if coming from a hidden-pane mode.
                if (firstCol.Width  == zeroSize) firstCol.Width  = starSize;
                if (secondCol.Width == zeroSize) secondCol.Width = starSize;
                _splitterColumn.Width = splitterSz;
            }
            else
            {
                firstCol.Width        = showFirst  ? starSize : zeroSize;
                secondCol.Width       = showSecond ? starSize : zeroSize;
                _splitterColumn.Width = zeroSize;
            }

            ColumnDefinitions.Add(firstCol);
            ColumnDefinitions.Add(_splitterColumn);
            ColumnDefinitions.Add(secondCol);
        }

        // ── Reposition children ───────────────────────────────────────────────
        UIElement firstPane  = designFirst ? _designPaneGrid : _codeHost;
        UIElement secondPane = designFirst ? _codeHost       : _designPaneGrid;

        if (isVertical)
        {
            SetRow(_toolbarContainer!, 0);  SetColumn(_toolbarContainer!, 0);  SetColumnSpan(_toolbarContainer!, 1);
            SetRow(firstPane,          1);  SetColumn(firstPane,          0);  SetColumnSpan(firstPane,          1);
            SetRow(_splitter,          2);  SetColumn(_splitter,          0);  SetColumnSpan(_splitter,          1);
            SetRow(secondPane,         3);  SetColumn(secondPane,         0);  SetColumnSpan(secondPane,         1);

            _splitter.Width               = double.NaN;
            _splitter.Height              = 4;
            _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
            _splitter.ResizeDirection     = GridResizeDirection.Rows;
        }
        else
        {
            SetRow(_toolbarContainer!, 0);  SetColumn(_toolbarContainer!, 0);  SetColumnSpan(_toolbarContainer!, 3);
            SetRow(firstPane,          1);  SetColumn(firstPane,          0);  SetColumnSpan(firstPane,          1);
            SetRow(_splitter,          1);  SetColumn(_splitter,          1);  SetColumnSpan(_splitter,          1);
            SetRow(secondPane,         1);  SetColumn(secondPane,         2);  SetColumnSpan(secondPane,         1);

            _splitter.Width               = 4;
            _splitter.Height              = double.NaN;
            _splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
            _splitter.VerticalAlignment   = VerticalAlignment.Stretch;
            _splitter.ResizeDirection     = GridResizeDirection.Columns;
        }

        _splitter.Visibility = showSplit ? Visibility.Visible : Visibility.Collapsed;

        // ── Position search bar over the design pane ──────────────────────────
        if (_searchBar is not null)
        {
            SetRow(_searchBar,        GetRow(_designPaneGrid));
            SetColumn(_searchBar,     GetColumn(_designPaneGrid));
            SetColumnSpan(_searchBar, GetColumnSpan(_designPaneGrid));
        }

        // ── Sync view mode toggle buttons ─────────────────────────────────────
        _btnCodeOnly.IsChecked   = _viewMode == ViewMode.CodeOnly;
        _btnSplit.IsChecked      = _viewMode == ViewMode.Split;
        _btnDesignOnly.IsChecked = _viewMode == ViewMode.DesignOnly;

        var modeLabel = _viewMode switch
        {
            ViewMode.CodeOnly   => "Code Only",
            ViewMode.DesignOnly => "Design Only",
            _                   => "Split"
        };
        _sbViewMode.Value = modeLabel;
        foreach (var choice in _sbViewMode.Choices)
            choice.IsActive = choice.DisplayName == modeLabel;

        // ── Sync layout status bar ────────────────────────────────────────────
        var layoutLabel = _splitLayout switch
        {
            SplitLayout.HorizontalDesignLeft  => "Design Left",
            SplitLayout.VerticalDesignBottom  => "Design Bottom",
            SplitLayout.VerticalDesignTop     => "Design Top",
            _                                  => "Design Right"
        };
        _sbLayout.Value = layoutLabel;
        foreach (var choice in _sbLayout.Choices)
            choice.IsActive = choice.DisplayName == layoutLabel;
    }

    // ── Persistence helpers ───────────────────────────────────────────────────

    private double GetSplitRatio()
    {
        bool vertical = _splitLayout is SplitLayout.VerticalDesignBottom or SplitLayout.VerticalDesignTop;
        if (vertical)
        {
            double total = _contentCodeRow.ActualHeight + _contentDesignRow.ActualHeight;
            return total > 0 ? _contentCodeRow.ActualHeight / total : 0.5;
        }
        double totalW = _codeColumn.ActualWidth + _designColumn.ActualWidth;
        return totalW > 0 ? _codeColumn.ActualWidth / totalW : 0.5;
    }

    private void SetSplitRatio(double ratio)
    {
        if (_viewMode != ViewMode.Split) return;
        bool vertical = _splitLayout is SplitLayout.VerticalDesignBottom or SplitLayout.VerticalDesignTop;
        if (vertical)
        {
            _contentCodeRow.Height   = new GridLength(ratio,       GridUnitType.Star);
            _contentDesignRow.Height = new GridLength(1.0 - ratio, GridUnitType.Star);
        }
        else
        {
            _codeColumn.Width   = new GridLength(ratio,       GridUnitType.Star);
            _designColumn.Width = new GridLength(1.0 - ratio, GridUnitType.Star);
        }
    }

    private static string GetElementPath(UIElement el)
        => el.GetType().Name;

    // ── XAML round-trip helpers ───────────────────────────────────────────────

    /// <summary>
    /// Pushes updated XAML back into the code editor and triggers a preview refresh.
    /// Stops the debounce timer first to prevent a double-render caused by the
    /// document TextChanged event that LoadFromString raises internally.
    /// </summary>
    private void ApplyXamlToCode(string xaml)
    {
        // Set _isSyncingSelection before LoadFromString: LoadFromString resets the
        // caret to (0,0), which fires CaretMoved / SelectionChanged, which would
        // otherwise start the _codeToCanvasSyncTimer and select the root element.
        _isSyncingSelection = true;
        try
        {
            // Stop the debounce timer before writing — we wrote the content ourselves,
            // so the TextChanged event that fires next must not schedule another render.
            _previewTimer.Stop();
            _codeHost.PrimaryEditor.Document?.LoadFromString(xaml);
            if (_autoPreviewEnabled)
                TriggerPreview();
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    // ── Toolbar builder ───────────────────────────────────────────────────────

    private Border BuildToolbar(
        out ToggleButton btnCode, out ToggleButton btnSplit, out ToggleButton btnDesign,
        out ToggleButton btnAuto, out Border errorBanner, out TextBlock errorText)
    {
        ToggleButton MakeToggle(string glyph, string tooltip)
        {
            var btn = new ToggleButton { Content = glyph, ToolTip = tooltip };
            btn.SetResourceReference(StyleProperty, "XD_ToolbarToggleButtonStyle");
            return btn;
        }

        Button MakeIconButton(string glyph, string tooltip)
        {
            var btn = new Button
            {
                Content         = glyph,
                ToolTip         = tooltip,
                Width           = 22,
                Height          = 22,
                Background      = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily      = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize        = 12,
                Cursor          = System.Windows.Input.Cursors.Hand
            };
            btn.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "DockMenuForegroundBrush");
            return btn;
        }

        btnCode   = MakeToggle("\uE8A5", "Code only");
        btnSplit  = MakeToggle("\uE70D", "Split view");
        btnDesign = MakeToggle("\uE769", "Design only");
        btnAuto   = MakeToggle("\uE8EA", "Auto-preview on / off");
        btnAuto.IsChecked = true;

        // Wire view mode toggles.
        var localCode   = btnCode;
        var localSplit  = btnSplit;
        var localDesign = btnDesign;
        localCode.Click   += (_, _) => { if (localCode.IsChecked == true)   ApplyViewMode(ViewMode.CodeOnly); };
        localSplit.Click  += (_, _) => { if (localSplit.IsChecked == true)  ApplyViewMode(ViewMode.Split); };
        localDesign.Click += (_, _) => { if (localDesign.IsChecked == true) ApplyViewMode(ViewMode.DesignOnly); };
        btnAuto.Checked   += (_, _) => _autoPreviewEnabled = true;
        btnAuto.Unchecked += (_, _) => { _autoPreviewEnabled = false; _previewTimer.Stop(); };

        // ── Layout dropdown button ────────────────────────────────────────────
        var btnLayoutLocal = MakeIconButton("\uE8B7", "Split layout (Ctrl+Shift+L)");
        btnLayoutLocal.Click += (_, _) =>
        {
            var ctx = new ContextMenu();
            ctx.SetResourceReference(ContextMenu.BackgroundProperty,  "DockMenuBackgroundBrush");
            ctx.SetResourceReference(ContextMenu.ForegroundProperty,  "DockMenuForegroundBrush");
            ctx.SetResourceReference(ContextMenu.BorderBrushProperty, "DockMenuBorderBrush");

            MenuItem MakeLayoutItem(string glyph, string header, SplitLayout layout)
            {
                var item = new MenuItem { Header = header, Icon = MakeMenuIcon(glyph) };
                item.SetResourceReference(MenuItem.ForegroundProperty, "DockMenuForegroundBrush");
                item.Click += (_, _) => ApplySplitLayout(layout);
                return item;
            }

            ctx.Items.Add(MakeLayoutItem("\uE8D6", "Design Right",  SplitLayout.HorizontalDesignRight));
            ctx.Items.Add(MakeLayoutItem("\uE8D5", "Design Left",   SplitLayout.HorizontalDesignLeft));
            ctx.Items.Add(MakeLayoutItem("\uE8D2", "Design Bottom", SplitLayout.VerticalDesignBottom));
            ctx.Items.Add(MakeLayoutItem("\uE8D4", "Design Top",    SplitLayout.VerticalDesignTop));

            ctx.PlacementTarget = btnLayoutLocal;
            ctx.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            ctx.IsOpen          = true;
        };

        // ── Undo/Redo group (Overkill) ───────────────────────────────────────
        var btnUndoLocal = MakeIconButton("\uE7A7", "Nothing to undo");
        var btnRedoLocal = MakeIconButton("\uE7A6", "Nothing to redo");
        btnUndoLocal.Click += (_, _) => Undo();
        btnRedoLocal.Click += (_, _) => Redo();

        // Store as fields so UpdateUndoRedoTooltips() can update them.
        _btnUndo = btnUndoLocal;
        _btnRedo = btnRedoLocal;

        // ── Zoom group (Phase 3) ─────────────────────────────────────────────
        var btnZoomOut  = MakeIconButton("\uE71F", "Zoom out");
        var btnZoomIn   = MakeIconButton("\uE8A3", "Zoom in");
        var btnZoomReset= MakeIconButton("\uE72B", "Reset zoom (100%)");
        var btnFit      = MakeIconButton("\uE9A6", "Fit to content");

        btnZoomOut.Click   += (_, _) => _zoomVm.ZoomOutCommand.Execute(null);
        btnZoomIn.Click    += (_, _) => _zoomVm.ZoomInCommand.Execute(null);
        btnZoomReset.Click += (_, _) => _zoomVm.ZoomResetCommand.Execute(null);
        btnFit.Click       += (_, _) => _zoomVm.FitToContentCommand.Execute(null);

        // Zoom label (live-updating via ZoomChanged).
        var zoomLabel = new TextBlock
        {
            Width             = 36,
            FontSize          = 10,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment     = TextAlignment.Center
        };
        zoomLabel.SetResourceReference(TextElement.ForegroundProperty, "DockMenuForegroundBrush");
        _zoomPan.ZoomChanged += (_, _) => zoomLabel.Text = _zoomVm.ZoomLabel;
        zoomLabel.Text = _zoomVm.ZoomLabel;

        // ── Alignment group (Phase 5) ────────────────────────────────────────
        var btnAlignLeft   = MakeIconButton("\uE8A2", "Align left");
        var btnAlignRight  = MakeIconButton("\uE8A4", "Align right");
        var btnAlignCenter = MakeIconButton("\uE8A1", "Align center H");
        var btnAlignTop    = MakeIconButton("\uE8A6", "Align top");
        var btnAlignBottom = MakeIconButton("\uE8A7", "Align bottom");

        btnAlignLeft.Click   += (_, _) => _alignVm.AlignLeftCommand.Execute(null);
        btnAlignRight.Click  += (_, _) => _alignVm.AlignRightCommand.Execute(null);
        btnAlignCenter.Click += (_, _) => _alignVm.AlignCenterHCommand.Execute(null);
        btnAlignTop.Click    += (_, _) => _alignVm.AlignTopCommand.Execute(null);
        btnAlignBottom.Click += (_, _) => _alignVm.AlignBottomCommand.Execute(null);

        // ── Phase E4: View options group ─────────────────────────────────────
        var btnRulers = MakeIconButton("\uE8B2", "Toggle rulers (Ctrl+R)");
        var btnGrid   = MakeIconButton("\uE80A", "Toggle grid (Ctrl+G)");
        var btnSnap   = MakeIconButton("\uE8C6", "Toggle snap (Ctrl+Shift+S)");

        btnRulers.Click += (_, _) => { /* TODO Phase E4: toggle ruler overlay */ };
        btnGrid.Click   += (_, _) => { /* TODO Phase E4: toggle design grid  */ };
        btnSnap.Click   += (_, _) => { /* TODO Phase E4: toggle snap engine  */ };

        // ── Phase E4: Edit group ─────────────────────────────────────────────
        var btnCut   = MakeIconButton("\uE8C6", "Cut element (Ctrl+X)");
        var btnCopy  = MakeIconButton("\uE8C8", "Copy element (Ctrl+C)");
        var btnPaste = MakeIconButton("\uE77F", "Paste element (Ctrl+V)");

        btnCut.Click   += (_, _) => CutElement();
        btnCopy.Click  += (_, _) => CopyElement();
        btnPaste.Click += (_, _) => PasteElement();

        // ── Error banner ─────────────────────────────────────────────────────
        errorText = new TextBlock
        {
            FontSize          = 11,
            Margin            = new Thickness(6, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis
        };
        errorText.SetResourceReference(TextElement.ForegroundProperty, "XD_ErrorBannerForeground");

        errorBanner = new Border
        {
            Visibility      = Visibility.Collapsed,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding         = new Thickness(4, 2, 4, 2),
            Child           = errorText
        };
        errorBanner.SetResourceReference(BackgroundProperty,         "XD_ErrorBannerBackground");
        errorBanner.SetResourceReference(Border.BorderBrushProperty, "XD_ErrorBannerBorder");

        // ── Assemble toolbar ─────────────────────────────────────────────────
        var dp = new DockPanel { LastChildFill = true };
        dp.SetResourceReference(BackgroundProperty, "XD_PanelToolbarBrush");

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2, 0, 2) };

        // View mode group
        leftStack.Children.Add(btnCode);
        leftStack.Children.Add(btnSplit);
        leftStack.Children.Add(btnDesign);
        leftStack.Children.Add(MakeSeparator());
        leftStack.Children.Add(btnAuto);
        leftStack.Children.Add(MakeSeparator());
        leftStack.Children.Add(btnLayoutLocal);

        // Undo/Redo group
        leftStack.Children.Add(MakeSeparator());
        leftStack.Children.Add(btnUndoLocal);
        leftStack.Children.Add(btnRedoLocal);

        // Zoom group
        leftStack.Children.Add(MakeSeparator());
        leftStack.Children.Add(btnZoomOut);
        leftStack.Children.Add(zoomLabel);
        leftStack.Children.Add(btnZoomIn);
        leftStack.Children.Add(btnZoomReset);
        leftStack.Children.Add(btnFit);

        // Alignment group
        leftStack.Children.Add(MakeSeparator());
        leftStack.Children.Add(btnAlignLeft);
        leftStack.Children.Add(btnAlignCenter);
        leftStack.Children.Add(btnAlignRight);
        leftStack.Children.Add(btnAlignTop);
        leftStack.Children.Add(btnAlignBottom);

        // View options group (Phase E4)
        leftStack.Children.Add(MakeSeparator());
        leftStack.Children.Add(btnRulers);
        leftStack.Children.Add(btnGrid);
        leftStack.Children.Add(btnSnap);

        // Edit group (Phase E4)
        leftStack.Children.Add(MakeSeparator());
        leftStack.Children.Add(btnCut);
        leftStack.Children.Add(btnCopy);
        leftStack.Children.Add(btnPaste);

        DockPanel.SetDock(leftStack, Dock.Left);
        dp.Children.Add(leftStack);
        dp.Children.Add(errorBanner);

        var container = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child           = dp
        };
        container.SetResourceReference(Border.BorderBrushProperty, "XD_PanelToolbarBorderBrush");

        return container;
    }

    /// <summary>Creates a thin vertical separator for toolbar groups.</summary>
    private UIElement MakeSeparator()
    {
        var sep = new Separator { Width = 1, Margin = new Thickness(4, 2, 4, 2) };
        sep.SetResourceReference(BackgroundProperty, "DockBorderBrush");
        return sep;
    }

    // ── IDE toolbar pod ───────────────────────────────────────────────────────

    /// <summary>
    /// Populates <see cref="ToolbarItems"/> with contextual actions exposed to the IDE toolbar pod.
    /// Layout: [View Mode▾] | [Auto-Preview] | [Undo] [Redo] | [Zoom Out] [Zoom In] [Reset] [Fit] | [Align▾]
    /// </summary>
    private void BuildToolbarItems()
    {
        // View Mode dropdown
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE8A5",
            Tooltip = "View mode (Ctrl+1/2/3)",
            DropdownItems = new ObservableCollection<EditorToolbarItem>
            {
                new() { Icon = "\uE8A5", Label = "Code Only",   Tooltip = "Code Only (Ctrl+1)",   Command = new RelayCommand(_ => ApplyViewMode(ViewMode.CodeOnly)) },
                new() { Icon = "\uE70D", Label = "Split",       Tooltip = "Split view (Ctrl+2)",  Command = new RelayCommand(_ => ApplyViewMode(ViewMode.Split)) },
                new() { Icon = "\uE769", Label = "Design Only", Tooltip = "Design Only (Ctrl+3)", Command = new RelayCommand(_ => ApplyViewMode(ViewMode.DesignOnly)) },
            }
        });

        // Layout dropdown
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE8B7",
            Tooltip = "Split layout (Ctrl+Shift+L)",
            DropdownItems = new ObservableCollection<EditorToolbarItem>
            {
                new() { Icon = "\uE8D6", Label = "Design Right",  Tooltip = "Design Right",  Command = new RelayCommand(_ => ApplySplitLayout(SplitLayout.HorizontalDesignRight)) },
                new() { Icon = "\uE8D5", Label = "Design Left",   Tooltip = "Design Left",   Command = new RelayCommand(_ => ApplySplitLayout(SplitLayout.HorizontalDesignLeft)) },
                new() { Icon = "\uE8D2", Label = "Design Bottom", Tooltip = "Design Bottom", Command = new RelayCommand(_ => ApplySplitLayout(SplitLayout.VerticalDesignBottom)) },
                new() { Icon = "\uE8D4", Label = "Design Top",    Tooltip = "Design Top",    Command = new RelayCommand(_ => ApplySplitLayout(SplitLayout.VerticalDesignTop)) },
            }
        });

        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });

        // Auto-preview toggle
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE8EA",
            Tooltip = "Toggle auto-preview (Ctrl+Shift+P)",
            Command = new RelayCommand(_ =>
            {
                _autoPreviewEnabled   = !_autoPreviewEnabled;
                _btnAutoPreview.IsChecked = _autoPreviewEnabled;
                if (!_autoPreviewEnabled) _previewTimer.Stop();
            })
        });

        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });

        // Zoom controls
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE71F", Tooltip = "Zoom out (Ctrl+-)",    Command = new RelayCommand(_ => _zoomVm.ZoomOutCommand.Execute(null)) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE8A3", Tooltip = "Zoom in (Ctrl++)",     Command = new RelayCommand(_ => _zoomVm.ZoomInCommand.Execute(null)) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE72B", Tooltip = "Reset zoom (Ctrl+0)",  Command = new RelayCommand(_ => _zoomVm.ZoomResetCommand.Execute(null)) });
        ToolbarItems.Add(new EditorToolbarItem { Icon = "\uE9A6", Tooltip = "Fit to content",       Command = new RelayCommand(_ => _zoomVm.FitToContentCommand.Execute(null)) });

        ToolbarItems.Add(new EditorToolbarItem { IsSeparator = true });

        // Alignment dropdown
        ToolbarItems.Add(new EditorToolbarItem
        {
            Icon    = "\uE8A2",
            Tooltip = "Align selected elements",
            DropdownItems = new ObservableCollection<EditorToolbarItem>
            {
                new() { Icon = "\uE8A2", Label = "Align Left",      Command = new RelayCommand(_ => _alignVm.AlignLeftCommand.Execute(null)) },
                new() { Icon = "\uE8A1", Label = "Align Center H",  Command = new RelayCommand(_ => _alignVm.AlignCenterHCommand.Execute(null)) },
                new() { Icon = "\uE8A4", Label = "Align Right",     Command = new RelayCommand(_ => _alignVm.AlignRightCommand.Execute(null)) },
                new() { IsSeparator = true },
                new() { Icon = "\uE8A6", Label = "Align Top",       Command = new RelayCommand(_ => _alignVm.AlignTopCommand.Execute(null)) },
                new() { Icon = "\uE8A7", Label = "Align Bottom",    Command = new RelayCommand(_ => _alignVm.AlignBottomCommand.Execute(null)) },
                new() { IsSeparator = true },
                new() { Icon = "\uE898", Label = "Bring to Front",  Command = new RelayCommand(_ => _alignVm.BringToFrontCommand.Execute(null)) },
                new() { Icon = "\uE896", Label = "Send to Back",    Command = new RelayCommand(_ => _alignVm.SendToBackCommand.Execute(null)) },
            }
        });
    }

    // ── Design canvas context menu ────────────────────────────────────────────

    /// <summary>
    /// Builds the right-click context menu attached to the design canvas.
    /// Items: Delete | Bring to Front / Send to Back | View Code | Properties
    /// </summary>
    private ContextMenu BuildDesignContextMenu()
    {
        MenuItem MakeItem(string glyph, string header, ICommand cmd, string? gesture = null)
        {
            var item = new MenuItem { Header = header, Command = cmd };
            if (gesture is not null)
                item.InputGestureText = gesture;
            item.Icon = MakeMenuIcon(glyph);
            item.SetResourceReference(MenuItem.ForegroundProperty, "DockMenuForegroundBrush");
            return item;
        }

        var menu = new ContextMenu();
        menu.SetResourceReference(ContextMenu.BackgroundProperty,   "DockMenuBackgroundBrush");
        menu.SetResourceReference(ContextMenu.ForegroundProperty,   "DockMenuForegroundBrush");
        menu.SetResourceReference(ContextMenu.BorderBrushProperty,  "DockMenuBorderBrush");

        // "Select Parent" — visible only when there is a selectable parent within the canvas.
        var selectParentItem = new MenuItem { Header = "Select Parent", InputGestureText = "Esc" };
        selectParentItem.SetResourceReference(MenuItem.ForegroundProperty, "DockMenuForegroundBrush");
        selectParentItem.Icon = MakeMenuIcon("\uE898");
        menu.Opened += (_, _) =>
        {
            selectParentItem.IsEnabled = _designCanvas.SelectedElement is not null
                && _designCanvas.FindSelectableParent(_designCanvas.SelectedElement) is not null;
        };
        selectParentItem.Click += (_, _) => _designCanvas.SelectParent();
        menu.Items.Add(selectParentItem);
        menu.Items.Add(new Separator());

        menu.Items.Add(MakeItem("\uE74D", "Delete",         new RelayCommand(_ => DeleteSelectedElement()),                                              "Del"));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE8C6", "Cut",           new RelayCommand(_ => CutElement()),                                                         "Ctrl+X"));
        menu.Items.Add(MakeItem("\uE8C8", "Copy",          new RelayCommand(_ => CopyElement()),                                                         "Ctrl+C"));
        menu.Items.Add(MakeItem("\uE77F", "Paste",         new RelayCommand(_ => PasteElement()),                                                        "Ctrl+V"));
        menu.Items.Add(MakeItem("\uE8A9", "Duplicate",     new RelayCommand(_ => DuplicateElement())));
        menu.Items.Add(new Separator());

        // Wrap In submenu (Phase E5)
        var wrapMenu = new MenuItem { Header = "Wrap in" };
        wrapMenu.SetResourceReference(MenuItem.ForegroundProperty, "DockMenuForegroundBrush");
        foreach (var tag in new[] { "Border", "Grid", "StackPanel", "Canvas", "ScrollViewer" })
        {
            var tagCapture = tag;
            var wrapItem   = new MenuItem { Header = tagCapture };
            wrapItem.SetResourceReference(MenuItem.ForegroundProperty, "DockMenuForegroundBrush");
            wrapItem.Click += (_, _) => WrapSelectedElement(tagCapture);
            wrapMenu.Items.Add(wrapItem);
        }
        menu.Items.Add(wrapMenu);

        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE898", "Bring to Front", new RelayCommand(_ => _alignVm.BringToFrontCommand.Execute(null)),                           "Alt+Home"));
        menu.Items.Add(MakeItem("\uE896", "Send to Back",   new RelayCommand(_ => _alignVm.SendToBackCommand.Execute(null)),                             "Alt+End"));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE8A5", "View Code",      new RelayCommand(_ => ApplyViewMode(ViewMode.CodeOnly)),                                     "F7"));
        menu.Items.Add(MakeItem("\uE946", "Properties",     new RelayCommand(_ => FocusPropertiesPanelRequested?.Invoke(this, EventArgs.Empty)),         "F4"));

        return menu;
    }

    /// <summary>Creates a Segoe MDL2 Assets icon TextBlock for menu items.</summary>
    private static TextBlock MakeMenuIcon(string glyph)
    {
        var tb = new TextBlock
        {
            Text          = glyph,
            FontFamily    = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize      = 12,
            Width         = 14,
            TextAlignment = TextAlignment.Center
        };
        tb.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "DockMenuForegroundBrush");
        return tb;
    }

    // ── Delete element ────────────────────────────────────────────────────────

    /// <summary>
    /// Removes the currently selected canvas element from the XAML source.
    /// Captures a SnapshotDesignUndoEntry so the deletion is fully reversible.
    /// </summary>
    private void DeleteSelectedElement()
    {
        int uid = _designCanvas.SelectedElementUid;
        if (uid < 0) return;

        var elementType = _designCanvas.SelectedElement?.GetType().Name ?? "Element";
        var beforeXaml  = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        var afterXaml   = _syncService.RemoveElement(beforeXaml, uid);

        if (string.Equals(beforeXaml, afterXaml, StringComparison.Ordinal)) return;

        _undoManager.PushEntry(new SnapshotDesignUndoEntry(beforeXaml, afterXaml, $"Delete {elementType}"));
        ApplyXamlToCode(afterXaml);
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    /// <summary>
    /// Handles keyboard shortcuts at the Grid (host) level:
    ///   Ctrl+1/2/3 — view modes | Ctrl+±/0 — zoom | Ctrl+Shift+P — auto-preview | F7 — view code
    /// </summary>
    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl  = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift)   != 0;

        if (ctrl && !shift)
        {
            switch (e.Key)
            {
                case Key.D1:
                    ApplyViewMode(ViewMode.CodeOnly);
                    e.Handled = true;
                    return;
                case Key.D2:
                    ApplyViewMode(ViewMode.Split);
                    e.Handled = true;
                    return;
                case Key.D3:
                    ApplyViewMode(ViewMode.DesignOnly);
                    e.Handled = true;
                    return;
                case Key.Add:
                case Key.OemPlus:
                    _zoomVm.ZoomInCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Subtract:
                case Key.OemMinus:
                    _zoomVm.ZoomOutCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.D0:
                    _zoomVm.ZoomResetCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        if (ctrl && shift && e.Key == Key.P)
        {
            _autoPreviewEnabled       = !_autoPreviewEnabled;
            _btnAutoPreview.IsChecked = _autoPreviewEnabled;
            if (!_autoPreviewEnabled) _previewTimer.Stop();
            e.Handled = true;
            return;
        }

        if (ctrl && shift && e.Key == Key.L)
        {
            ApplySplitLayout((SplitLayout)(((int)_splitLayout + 1) % 4));
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F7)
        {
            ApplyViewMode(ViewMode.CodeOnly);
            e.Handled = true;
            return;
        }

        // Delete selected canvas element when design pane has keyboard focus.
        if (e.Key == Key.Delete && _designCanvas.IsKeyboardFocusWithin)
        {
            DeleteSelectedElement();
            e.Handled = true;
            return;
        }

        // Phase F5 — Ctrl+F opens the element search bar when canvas has focus.
        if (ctrl && !shift && e.Key == Key.F && _designCanvas.IsKeyboardFocusWithin)
        {
            OpenSearch();
            e.Handled = true;
            return;
        }

        // Phase C2 — Arrow key nudge (1px plain, 10px with Shift) on canvas selection.
        if (_designCanvas.IsKeyboardFocusWithin && _designCanvas.SelectedElement is FrameworkElement nudgeEl)
        {
            var step = shift ? 10.0 : 1.0;
            switch (e.Key)
            {
                case Key.Left:  NudgeElement(nudgeEl, -step, 0);   e.Handled = true; return;
                case Key.Right: NudgeElement(nudgeEl,  step, 0);   e.Handled = true; return;
                case Key.Up:    NudgeElement(nudgeEl, 0, -step);   e.Handled = true; return;
                case Key.Down:  NudgeElement(nudgeEl, 0,  step);   e.Handled = true; return;
            }
        }
    }

    // ── Phase C2 — Arrow key nudge ────────────────────────────────────────────

    /// <summary>
    /// Moves the selected element by (dx, dy) pixels by synthesising a
    /// move-start / delta / completed sequence on the interaction service,
    /// which records the operation and generates an undo entry.
    /// </summary>
    private void NudgeElement(FrameworkElement el, double dx, double dy)
    {
        var uid = _designCanvas.SelectedElementUid;
        if (uid < 0) return;

        var origin = new System.Windows.Point(0, 0);
        _interactionService.OnMoveStart(el, origin, uid);
        _interactionService.OnMoveDelta(el, new System.Windows.Point(dx, dy));
        _interactionService.OnMoveCompleted(el);
    }

    // ── Phase F3 — Copy / Paste / Duplicate / Wrap In ─────────────────────────

    /// <summary>Copies the selected element's XAML fragment to the internal clipboard.</summary>
    private void CopyElement()
    {
        if (_designCanvas.SelectedElementUid < 0) return;
        var raw  = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        var xdoc = System.Xml.Linq.XDocument.Parse(raw, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        var el   = FindXElementByUid(xdoc, _designCanvas.SelectedElementUid);
        if (el is null) return;
        _clipboardXaml = el.ToString();
        System.Windows.Clipboard.SetText(_clipboardXaml);
    }

    /// <summary>Cuts the selected element (copy + delete).</summary>
    private void CutElement()
    {
        CopyElement();
        DeleteSelectedElement();
    }

    /// <summary>Pastes the internal clipboard XAML as a new sibling / last child of root.</summary>
    private void PasteElement()
    {
        var text = _clipboardXaml ?? System.Windows.Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return;

        var before = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        var after  = InsertXamlSnippet(before, text);
        if (string.Equals(before, after, StringComparison.Ordinal)) return;

        _undoManager.PushEntry(new SnapshotDesignUndoEntry(before, after, "Paste element"));
        ApplyXamlToCode(after);
    }

    /// <summary>Duplicates the selected element in-place.</summary>
    private void DuplicateElement()
    {
        CopyElement();
        PasteElement();
    }

    /// <summary>
    /// Wraps the selected element in a new container element (e.g. Border, Grid).
    /// </summary>
    private void WrapSelectedElement(string containerTag)
    {
        var uid = _designCanvas.SelectedElementUid;
        if (uid < 0) return;

        var before = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        var after  = _syncService.WrapInContainer(before, uid, containerTag);
        if (string.Equals(before, after, StringComparison.Ordinal)) return;

        _undoManager.PushEntry(new SnapshotDesignUndoEntry(before, after, $"Wrap in {containerTag}"));
        ApplyXamlToCode(after);
    }

    /// <summary>
    /// Inserts <paramref name="snippet"/> as the last child of the root panel element.
    /// Falls back to appending before the root closing tag when no panel is found.
    /// </summary>
    private static string InsertXamlSnippet(string rawXaml, string snippet)
    {
        if (string.IsNullOrWhiteSpace(rawXaml)) return rawXaml;
        try
        {
            var doc  = System.Xml.Linq.XDocument.Parse(rawXaml, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            var root = doc.Root;
            if (root is null) return rawXaml;

            var parsed = System.Xml.Linq.XElement.Parse(snippet, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            root.Add(parsed);

            var sb = new System.Text.StringBuilder();
            using var w = System.Xml.XmlWriter.Create(sb, new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent             = false,
                NewLineHandling    = System.Xml.NewLineHandling.None
            });
            doc.WriteTo(w);
            w.Flush();
            return sb.ToString();
        }
        catch
        {
            return rawXaml;
        }
    }

    /// <summary>
    /// Finds the <see cref="System.Xml.Linq.XElement"/> at the given pre-order UID
    /// (mirrors DesignToXamlSyncService.FindUid but operates on an already-parsed document).
    /// </summary>
    private static System.Xml.Linq.XElement? FindXElementByUid(
        System.Xml.Linq.XDocument doc, int uid)
    {
        if (doc.Root is null) return null;
        int counter = 0;
        return FindXElementByUidCore(doc.Root, uid, ref counter);
    }

    private static System.Xml.Linq.XElement? FindXElementByUidCore(
        System.Xml.Linq.XElement el, int uid, ref int counter)
    {
        if (counter == uid) return el;
        counter++;
        foreach (var child in el.Elements())
        {
            var found = FindXElementByUidCore(child, uid, ref counter);
            if (found is not null) return found;
        }
        return null;
    }

    // ── Phase F5 — Element search bar ─────────────────────────────────────────

    /// <summary>Builds the collapsible search overlay placed at the top-right of the canvas area.</summary>
    private Border BuildSearchBar(out TextBox searchBox)
    {
        searchBox = new TextBox { Width = 200, Height = 24, Margin = new Thickness(4) };
        searchBox.SetResourceReference(TextBox.ForegroundProperty, "DockMenuForegroundBrush");
        searchBox.SetResourceReference(TextBox.BackgroundProperty, "DockBackgroundBrush");

        var localBox = searchBox; // capture for lambda
        searchBox.TextChanged += (_, _) => SearchElements(localBox.Text);
        searchBox.KeyDown     += (_, e) => { if (e.Key == Key.Escape) CloseSearch(); };

        var closeBtn = new Button
        {
            Content         = "\uE711",
            Width           = 22,
            Background      = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontFamily      = new System.Windows.Media.FontFamily("Segoe MDL2 Assets")
        };
        closeBtn.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "DockMenuForegroundBrush");
        closeBtn.Click += (_, _) => CloseSearch();

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(searchBox);
        panel.Children.Add(closeBtn);

        var border = new Border
        {
            CornerRadius    = new CornerRadius(3),
            Padding         = new Thickness(2),
            BorderThickness = new Thickness(1),
            Child           = panel
        };
        border.SetResourceReference(Border.BackgroundProperty,   "DockMenuBackgroundBrush");
        border.SetResourceReference(Border.BorderBrushProperty,  "DockBorderBrush");
        return border;
    }

    /// <summary>Opens the search bar and moves focus to the search text box.</summary>
    private void OpenSearch()
    {
        if (_searchBar is null) return;
        _searchBar.Visibility = Visibility.Visible;
        _searchBox?.Focus();
    }

    /// <summary>Hides the search bar and clears the search query.</summary>
    private void CloseSearch()
    {
        if (_searchBar is null) return;
        _searchBar.Visibility = Visibility.Collapsed;
        if (_searchBox is not null) _searchBox.Text = string.Empty;
    }

    /// <summary>
    /// Selects the first canvas element whose type name or x:Name contains <paramref name="query"/>.
    /// Operates on the parsed XDocument to identify the target UID, then calls SelectElement.
    /// </summary>
    private void SearchElements(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || _designCanvas.DesignRoot is null) return;

        var raw = _codeHost.PrimaryEditor.Document?.SaveToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return;

        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(raw, System.Xml.Linq.LoadOptions.PreserveWhitespace);
            var uid = FindUidByNameQuery(doc, query);
            if (uid < 0) return;

            // Select the matching element on the canvas via UID.
            SelectCanvasElementByUid(uid);
        }
        catch { /* swallow parse errors */ }
    }

    /// <summary>
    /// Traverses the XDocument in pre-order and returns the UID of the first element
    /// whose local name or x:Name attribute contains <paramref name="query"/> (case-insensitive).
    /// </summary>
    private static int FindUidByNameQuery(System.Xml.Linq.XDocument doc, string query)
    {
        if (doc.Root is null) return -1;
        int counter = 0;
        return FindUidByNameQueryCore(doc.Root, query, ref counter);
    }

    private static int FindUidByNameQueryCore(
        System.Xml.Linq.XElement el, string query, ref int counter)
    {
        bool nameMatch = el.Name.LocalName.Contains(query, StringComparison.OrdinalIgnoreCase);
        bool keyMatch  = (el.Attribute("{http://schemas.microsoft.com/winfx/2006/xaml}Name")?.Value
                         ?? string.Empty).Contains(query, StringComparison.OrdinalIgnoreCase);

        if (nameMatch || keyMatch) return counter;
        counter++;

        foreach (var child in el.Elements())
        {
            var found = FindUidByNameQueryCore(child, query, ref counter);
            if (found >= 0) return found;
        }
        return -1;
    }

    /// <summary>
    /// Selects the canvas UIElement that was mapped to the given UID during the last render.
    /// Uses XamlElementMapper (exposed via DesignCanvas) to retrieve the UIElement.
    /// </summary>
    private void SelectCanvasElementByUid(int uid)
    {
        // Re-use the existing hit-test mapper by asking the canvas to select by UID.
        // DesignCanvas.SelectElement(null) clears, but we need the mapper. Walk the
        // visual tree to find an element whose Tag string is "xd_{uid}".
        if (_designCanvas.DesignRoot is null) return;
        var target = FindElementByTagUid(_designCanvas.DesignRoot, uid);
        if (target is not null)
            _designCanvas.SelectElement(target);
    }

    private static UIElement? FindElementByTagUid(UIElement root, int uid)
    {
        var tagKey = $"xd_{uid}";
        return FindElementByTagUidCore(root, tagKey);
    }

    private static UIElement? FindElementByTagUidCore(DependencyObject node, string tagKey)
    {
        if (node is FrameworkElement fe && fe.Tag is string tag && tag == tagKey)
            return fe;

        int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < childCount; i++)
        {
            var child  = System.Windows.Media.VisualTreeHelper.GetChild(node, i);
            var result = FindElementByTagUidCore(child, tagKey);
            if (result is not null) return result;
        }
        return null;
    }
}
