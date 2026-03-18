// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlDesignerSplitHost.cs
// Author: Derek Tremblay
// Created: 2026-03-16
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
using System.Windows.Threading;
using WpfHexEditor.Editor.CodeEditor;
using WpfHexEditor.Editor.CodeEditor.Controls;
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
    IEditorToolbarContributor
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

    private readonly DesignToXamlSyncService  _syncService        = new();
    private readonly DesignInteractionService _interactionService = new();

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
        _zoomPan = new ZoomPanCanvas
        {
            Content    = _designCanvas,
            AllowDrop  = true
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

        // -- Design pane (ZoomPanCanvas) -------------------------------------
        Children.Add(_zoomPan);

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
        _codeHost.PrimaryEditor.ModifiedChanged += OnCodeModified;
        _designCanvas.RenderError               += OnRenderError;
        _designCanvas.SelectedElementChanged    += OnDesignSelectionChanged;

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

    // ── IOpenableDocument ─────────────────────────────────────────────────────

    async Task IOpenableDocument.OpenAsync(string filePath, CancellationToken ct)
    {
        _filePath = filePath;

        // Delegate to the code host — it resolves the XAML language highlighter
        // internally via LanguageRegistry.Instance.GetLanguageForFile (CodeEditorSplitHost.OpenAsync).
        await ((IOpenableDocument)_codeHost).OpenAsync(filePath, ct);

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
        _sbElement.Value     = el?.GetType().Name ?? string.Empty;
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
        var patched  = _syncService.PatchElement(rawXaml, e.Operation.ElementUid, e.Operation.After);
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
        UIElement firstPane  = designFirst ? _zoomPan  : _codeHost;
        UIElement secondPane = designFirst ? _codeHost : _zoomPan;

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
    /// </summary>
    private void ApplyXamlToCode(string xaml)
    {
        // Replace code editor content (triggers ModifiedChanged → preview timer).
        _codeHost.PrimaryEditor.Document?.LoadFromString(xaml);
        if (_autoPreviewEnabled)
            TriggerPreview();
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

        menu.Items.Add(MakeItem("\uE74D", "Delete",         new RelayCommand(_ => DeleteSelectedElement()),                                              "Del"));
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
        }
    }
}
