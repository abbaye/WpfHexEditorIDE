// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramCanvas.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Main diagram canvas. Uses DiagramVisualLayer for high-performance
//     rendering of all class nodes and relationship arrows in a single
//     DrawingVisual-backed pass. Handles mouse interaction, selection,
//     rubber-band, drag-move, and context menus.
//
// Architecture Notes:
//     Pattern: Composite + Mediator.
//     DiagramVisualLayer is the only child of this Canvas — zero per-node
//     FrameworkElement overhead. Hit-testing is manual coordinate math.
//     Selection adorner is retained on the AdornerLayer for clear visual
//     feedback without polluting the visual layer render pass.
//     ApplyPatch is exposed for Phase 3 live-sync incremental updates.
// ==========================================================

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfHexEditor.Editor.ClassDiagram.Controls.Adorners;
using WpfHexEditor.Editor.ClassDiagram.Core.Layout;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.Services;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>Specifies which corner of the canvas the minimap is anchored to.</summary>
public enum MinimapCorner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// Canvas that hosts a <see cref="DiagramVisualLayer"/> for high-performance rendering
/// and manages selection, hover, drag-move, and context menus via manual hit-testing.
/// </summary>
public sealed class DiagramCanvas : Canvas
{
    // ── Children ──────────────────────────────────────────────────────────────
    private readonly DiagramVisualLayer _layer = new();

    // ── Grid rendering (B1) ───────────────────────────────────────────────────
    private const double GridSpacing = 24.0;  // dot-grid spacing in logical px

    // ── State ─────────────────────────────────────────────────────────────────
    private DiagramDocument?  _doc;
    private ClassNode?        _hoveredNode;
    private ClassNode?        _dragNode;

    // ── Member hover + selection ──────────────────────────────────────────────
    private ClassNode?   _hoveredMemberNode;
    private ClassMember? _hoveredMember;
    private ClassNode?   _selectedMemberNode;
    private ClassMember? _selectedMember;

    // ── Multi-selection ───────────────────────────────────────────────────────
    // _selectedIds = full selection set; _primarySelected = last explicit click target
    private readonly HashSet<string> _selectedIds = new(StringComparer.Ordinal);
    private ClassNode? _primarySelected;
    // Compat shim: external code that reads _selectedNode gets the primary node
    private ClassNode? _selectedNode => _primarySelected;

    private readonly DiagramCanvasViewModel _vm = new();

    // ── Adorners (rubber-band only — selection rect drawn by _layer directly) ──
    private AdornerLayer?      _adornerLayer;
    private RubberBandAdorner? _rubberBandAdorner;

    // ── Drag ──────────────────────────────────────────────────────────────────
    private Point  _dragStart;
    private double _dragNodeStartX;
    private double _dragNodeStartY;
    private Dictionary<string, Point> _dragStartPositions = []; // for multi-node drag

    // ── Resize gripper ────────────────────────────────────────────────────────
    private ClassNode? _resizingNode;
    private double     _resizeStartY;
    private double     _resizeStartHeight;

    // ── Swimlane group drag ──────────────────────────────────────────────────
    private string? _draggingGroupNs;

    // ── Rubber-band ───────────────────────────────────────────────────────────
    private bool  _isRubberBanding;
    private Point _rubberStart;

    // ── Undo manager (injected by ClassDiagramSplitHost) ─────────────────────
    private ClassDiagramUndoManager? _undoManager;

    // ── Snap engine (injected by ClassDiagramSplitHost) ──────────────────────
    private ClassSnapEngineService? _snapEngine;

    // ── Last right-click canvas position (diagram coordinates) ───────────────
    private Point _lastMenuPoint;

    // ── Performance: viewport culling ────────────────────────────────────────
    private ZoomPanCanvas? _zoomPanCanvas;

    // ── Minimap ───────────────────────────────────────────────────────────────
    // Owned by ClassDiagramSplitHost (overlay canvas) — not a child of DiagramCanvas.
    internal readonly DiagramMinimapControl _minimap = new();

    // ── Filter bar (Phase 12) ─────────────────────────────────────────────────
    private readonly DiagramFilterBar _filterBar = new();
    private bool _filterVisible;

    // ── Hover tooltip ─────────────────────────────────────────────────────────
    private readonly DispatcherTimer _tooltipTimer;
    private Popup?    _tooltipPopup;
    private ClassNode? _tooltipNode;
    public  int TooltipDelayMs { get; set; } = 400;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<ClassNode?>?                    SelectedClassChanged;
    public event EventHandler<ClassNode?>?                    HoveredClassChanged;
    public event EventHandler<ClassNode?>?                    AddMemberRequested;
    public event EventHandler<(ClassNode Node, string? NewName)>? RenameNodeRequested;
    public event EventHandler<(ClassNode Node, ClassMember Member)>? DeleteMemberRequested;
    public event EventHandler<(ClassNode Node, ClassMember Member)>? NavigateToMemberRequested;
    public event EventHandler<string>?                        ExportRequested;  // format: "png","plantUml","structurizr","mermaid","svg","csharp"
    public event EventHandler<LayoutStrategyKind>?            LayoutStrategyRequested;
    public event EventHandler<ClassNode>?                     ZoomToNodeRequested;
    public event EventHandler?                                FitToContentRequested;
    /// <summary>Fired when the user clicks "Properties" on a node's context menu.</summary>
    public event EventHandler<ClassNode>?                     ShowPropertiesRequested;
    /// <summary>Fired when the user clicks "Find References" on a node header context menu.</summary>
    public event EventHandler<ClassNode>?                     FindReferencesRequested;
    /// <summary>Fired when the user clicks "Show Metrics" on a node header context menu.</summary>
    public event EventHandler<ClassNode>?                     ShowMetricsRequested;
    /// <summary>Fired when the user clicks "Change Color…" on a node header context menu.</summary>
    public event EventHandler<ClassNode>?                     ChangeNodeColorRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DiagramCanvas()
    {
        Background = Brushes.Transparent;
        Focusable  = true;
        AllowDrop  = true;
        Loaded    += OnLoaded;

        // Tooltip timer
        _tooltipTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TooltipDelayMs) };
        _tooltipTimer.Tick += (_, _) => { _tooltipTimer.Stop(); ShowHoverTooltip(); };

        Children.Add(_layer);
        Canvas.SetLeft(_layer, 0);
        Canvas.SetTop(_layer, 0);

        // Filter bar — top-center, hidden by default.
        _filterBar.Visibility  = Visibility.Collapsed;
        _filterBar.FilterChanged  += OnFilterChanged;
        _filterBar.CloseRequested += (_, _) => HideFilterBar();
        Children.Add(_filterBar);
        Panel.SetZIndex(_filterBar, 200);
        SizeChanged += (_, _) => UpdateFilterBarPosition();
    }

    // ── Minimap API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Show/hide the minimap. ClassDiagramSplitHost owns the minimap's parent canvas;
    /// this property simply toggles Visibility so the session-save code still works.
    /// </summary>
    public bool IsMinimapVisible
    {
        get => _minimap.Visibility == Visibility.Visible;
        set => _minimap.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Injects the undo manager so drag/resize/delete ops are undoable.</summary>
    public void SetUndoManager(ClassDiagramUndoManager um) => _undoManager = um;

    /// <summary>Injects the snap engine so drag-move snaps to grid when enabled.</summary>
    public void SetSnapEngine(ClassSnapEngineService snap) => _snapEngine = snap;

    /// <summary>Rebuilds all visuals from the given document.</summary>
    public void ApplyDocument(DiagramDocument doc)
    {
        _doc              = doc;
        _selectedIds.Clear();
        _primarySelected  = null;
        _hoveredNode      = null;
        _hoveredMember    = null; _hoveredMemberNode = null;
        _selectedMember   = null; _selectedMemberNode = null;

        ClearAdorners();

        // Update LOD zoom before rendering
        if (_zoomPanCanvas is not null)
            _layer.CurrentZoom = _zoomPanCanvas.ZoomFactor;

        // Set culling viewport so RenderAll can skip off-screen nodes
        _layer.RenderAll(doc);

        // After full render, activate viewport culling for subsequent pan/zoom
        var vp = GetViewportRect();
        if (vp.Width < 1e8) // only if we have real viewport info
            _layer.InvalidateViewport(doc, vp);

        _minimap.SetDocument(doc);
        SelectedClassChanged?.Invoke(this, null);
    }

    /// <summary>Forces a full re-render of all nodes and arrows using the current document.</summary>
    public void InvalidateAndRender()
    {
        if (_doc is null) return;
        _layer.RenderAll(_doc, _selectedNode?.Id, _hoveredNode?.Id);
    }

    /// <summary>
    /// Incremental update: re-renders only the nodes in the patch.
    /// Pass <paramref name="refreshArrows"/> = false during live drag to skip
    /// repainting all arrows every frame — call <see cref="DiagramVisualLayer.RefreshArrows"/>
    /// once when the drag ends.
    /// </summary>
    public void ApplyPatch(IEnumerable<string> dirtyNodeIds, bool refreshArrows = true)
    {
        if (_doc is null) return;
        _layer.InvalidateNodes(_doc, dirtyNodeIds, _selectedNode?.Id, _hoveredNode?.Id, refreshArrows);
        UpdateSelectAdornerPosition();
    }

    /// <summary>Returns the primary selected class node, or null.</summary>
    public ClassNode? SelectedNode => _primarySelected;

    /// <summary>The document currently rendered by this canvas.</summary>
    public DiagramDocument? Document => _doc;

    /// <summary>Deletes the primary selected node (with undo entry). No-op when nothing selected.</summary>
    public void DeleteSelectedNode()
    {
        if (_primarySelected is not null)
            DeleteNode(_primarySelected);
    }

    /// <summary>Returns all currently selected node IDs.</summary>
    public IReadOnlyCollection<string> SelectedIds => _selectedIds;

    /// <summary>Clears the entire selection.</summary>
    public void ClearSelection()
    {
        _selectedIds.Clear();
        _primarySelected = null;
        _layer.ClearPreSelection();
        _layer.ClearSelection();
        _layer.SetMultiSelection(_selectedIds);
        _layer.UpdateSelection(null, _hoveredNode?.Id);
        SelectedClassChanged?.Invoke(this, null);
    }

    // ── Rubber-band API (called by ZoomPanCanvas in diagram-space coordinates) ──

    /// <summary>
    /// Begins a rubber-band lasso drag at <paramref name="diagramPt"/>.
    /// Called by ZoomPanCanvas when a left-button-down lands on empty viewport area.
    /// Coordinates must be in diagram (logical, unscaled) space.
    /// </summary>
    public void StartRubberBandAt(Point diagramPt)
    {
        if (_selectedIds.Count > 0) ClearSelection();
        _isRubberBanding = true;
        _rubberStart     = diagramPt;
    }

    /// <summary>
    /// Updates the rubber-band lasso to <paramref name="diagramPt"/> and refreshes the
    /// Explorer-style live pre-selection. Called by ZoomPanCanvas on every MouseMove.
    /// </summary>
    public void UpdateRubberBandAt(Point diagramPt)
    {
        if (!_isRubberBanding) return;

        // Only draw lasso once drag exceeds platform drag-threshold (avoids 1-pixel flicker on click)
        double dx = diagramPt.X - _rubberStart.X;
        double dy = diagramPt.Y - _rubberStart.Y;
        if (Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _layer.DrawRubberBand(_rubberStart, diagramPt);

        if (_doc is not null)
        {
            var lasso = DiagramVisualLayer.GetRubberBandRect(_rubberStart, diagramPt);
            var hits  = new HashSet<string>(StringComparer.Ordinal);
            if (lasso.Width > 2 && lasso.Height > 2)
                foreach (var n in _doc.Classes)
                {
                    var nb = new Rect(n.X, n.Y, n.Width, _layer.ComputeNodeHeight(n));
                    if (lasso.IntersectsWith(nb)) hits.Add(n.Id);
                }
            _layer.SetPreSelection(hits);
        }
    }

    /// <summary>
    /// Finishes the rubber-band lasso at <paramref name="diagramPt"/>, promotes pre-selection
    /// to the real selection set, and clears the lasso visuals.
    /// </summary>
    public void FinishRubberBandAt(Point diagramPt)
    {
        if (!_isRubberBanding) return;
        _isRubberBanding = false;

        Rect selRect = DiagramVisualLayer.GetRubberBandRect(_rubberStart, diagramPt);
        _layer.ClearRubberBand();
        _layer.ClearPreSelection();

        if (_doc is not null && selRect.Width > 2 && selRect.Height > 2)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                _selectedIds.Clear();

            foreach (var n in _doc.Classes)
            {
                var nb = new Rect(n.X, n.Y, n.Width, _layer.ComputeNodeHeight(n));
                if (selRect.IntersectsWith(nb)) _selectedIds.Add(n.Id);
            }
            _primarySelected = _selectedIds.Count == 1
                ? _doc.Classes.FirstOrDefault(n => _selectedIds.Contains(n.Id))
                : null;
            _layer.SetMultiSelection(_selectedIds);
            RedrawSelectionVisual();
            _layer.UpdateSelection(_primarySelected?.Id, _hoveredNode?.Id);
            SelectedClassChanged?.Invoke(this, _primarySelected);
        }
    }

    /// <summary>Cancels an in-progress rubber-band without updating selection.</summary>
    public void CancelRubberBand()
    {
        if (!_isRubberBanding) return;
        _isRubberBanding = false;
        _layer.ClearRubberBand();
        _layer.ClearPreSelection();
    }

    /// <summary>Selects the node with the given Id; no-op if not found.</summary>
    public void SelectNodeById(string nodeId)
    {
        if (_doc is null) return;
        var node = _doc.Classes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null) SelectSingleNode(node);
    }

    /// <summary>Selects all nodes (Ctrl+A).</summary>
    public void SelectAll()
    {
        if (_doc is null) return;
        _selectedIds.UnionWith(_doc.Classes.Select(n => n.Id));
        _primarySelected = null;
        _layer.SetMultiSelection(_selectedIds);
        RedrawSelectionVisual();
        _layer.UpdateSelection(null, _hoveredNode?.Id);
        SelectedClassChanged?.Invoke(this, null);
    }

    /// <summary>
    /// Returns the bounding rectangle of all diagram nodes in logical coordinates,
    /// with 40px padding. Used by ZoomPanCanvas.GetContentBounds() for scrollbar sizing.
    /// </summary>
    public Rect GetDiagramBounds()
    {
        if (_doc is null || _doc.Classes.Count == 0) return new Rect(0, 0, 800, 600);
        double minX = _doc.Classes.Min(n => n.X);
        double minY = _doc.Classes.Min(n => n.Y);
        double maxX = _doc.Classes.Max(n => n.X + n.Width);
        double maxY = _doc.Classes.Max(n => n.Y + _layer.ComputeNodeHeight(n));
        return new Rect(minX - 40, minY - 40, maxX - minX + 80, maxY - minY + 80);
    }

    /// <summary>
    /// Highlights the given relationship arrow (accent pen, thicker stroke).
    /// Pass null to clear the current highlight.
    /// </summary>
    public void HighlightRelationship(string? relId) => _layer.HighlightRelationship(relId);

    /// <summary>Exposes the visual layer for .whcd persistence (same assembly only).</summary>
    internal DiagramVisualLayer VisualLayer => _layer;

    /// <summary>Gets or sets whether namespace swimlane backgrounds are rendered.</summary>
    public bool ShowSwimLanes
    {
        get => _layer.ShowSwimLanes;
        set
        {
            _layer.ShowSwimLanes = value;
            if (_doc is not null)
                _layer.RenderAll(_doc, _selectedNode?.Id, _hoveredNode?.Id);
        }
    }

    // ── Loaded ────────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _adornerLayer = AdornerLayer.GetAdornerLayer(this);

        // Hook ZoomPanCanvas so we can update viewport culling on pan/zoom
        _zoomPanCanvas = FindZoomPanParent();
        if (_zoomPanCanvas is not null)
            _zoomPanCanvas.TransformChanged += OnZoomPanTransformChanged;
    }

    // ── Viewport culling helpers ──────────────────────────────────────────────

    private ZoomPanCanvas? FindZoomPanParent()
    {
        DependencyObject? el = VisualTreeHelper.GetParent(this);
        while (el is not null)
        {
            if (el is ZoomPanCanvas zpc) return zpc;
            el = VisualTreeHelper.GetParent(el);
        }
        return null;
    }

    /// <summary>
    /// Returns the current visible area in diagram (logical, unscaled) coordinates.
    /// Returns an infinitely-large rect when zoom info is unavailable (= no culling).
    /// </summary>
    internal Rect GetViewportRect()
    {
        if (_zoomPanCanvas is null) return new Rect(0, 0, 1e9, 1e9);
        double zoom    = _zoomPanCanvas.ZoomFactor;
        double offsetX = _zoomPanCanvas.OffsetX;
        double offsetY = _zoomPanCanvas.OffsetY;
        double vpW     = _zoomPanCanvas.ActualWidth;
        double vpH     = _zoomPanCanvas.ActualHeight;
        if (vpW < 1 || vpH < 1 || zoom < 0.001) return new Rect(0, 0, 1e9, 1e9);
        // diagram_coord = (screen_coord - offset) / zoom
        return new Rect(-offsetX / zoom, -offsetY / zoom, vpW / zoom, vpH / zoom);
    }

    private void OnZoomPanTransformChanged(object? sender, EventArgs e)
    {
        if (_doc is null) return;

        double newZoom = _zoomPanCanvas?.ZoomFactor ?? 1.0;
        bool lodWas    = _layer.CurrentZoom < DiagramVisualLayer.LodThreshold;
        bool lodNow    = newZoom             < DiagramVisualLayer.LodThreshold;

        _layer.CurrentZoom = newZoom;

        // When zoom crosses the LOD boundary, all visible nodes must be re-rendered
        // so their detail level is consistent (avoids stale full-detail nodes shown
        // at low zoom, or stale LOD pills shown after zooming back in).
        if (lodWas != lodNow)
            _layer.RenderAll(_doc, _selectedNode?.Id, _hoveredNode?.Id);
        else
            _layer.InvalidateViewport(_doc, GetViewportRect());
    }

    /// <summary>
    /// Async variant of <see cref="ApplyDocument"/>: renders in-viewport nodes
    /// immediately, then fills offscreen nodes in background <see cref="DispatcherPriority.Background"/>
    /// batches. The UI remains responsive during the background phase.
    /// </summary>
    public async Task ApplyDocumentAsync(DiagramDocument doc)
    {
        _doc             = doc;
        _selectedIds.Clear();
        _primarySelected = null;
        _hoveredNode     = null;
        _hoveredMember   = null; _hoveredMemberNode = null;
        _selectedMember  = null; _selectedMemberNode = null;

        ClearAdorners();
        _minimap.SetDocument(doc);
        SelectedClassChanged?.Invoke(this, null);

        // Update LOD zoom
        if (_zoomPanCanvas is not null)
            _layer.CurrentZoom = _zoomPanCanvas.ZoomFactor;

        // Allocate DrawingVisual slots without rendering (O(n) alloc, no draw)
        _layer.PrepareVisualSlots(doc);

        // Partition nodes into in-viewport (render immediately) vs offscreen (render lazily)
        var viewport = GetViewportRect();
        var inView   = new List<ClassNode>();
        var offScreen = new List<ClassNode>();
        foreach (var n in doc.Classes)
        {
            double w = n.Width > 4 ? n.Width : 180;
            var nb = new Rect(n.X, n.Y, w, 100);
            if (viewport.Width > 1e8 || nb.IntersectsWith(viewport)) inView.Add(n);
            else offScreen.Add(n);
        }

        // Render visible nodes synchronously (immediate feedback to user)
        foreach (var node in inView)
            _layer.RenderNode(node);
        _layer.RefreshArrows();
        _layer.RenderSwimLanesPublic();

        // Update culling viewport so subsequent pan/zoom works correctly
        _layer.InvalidateViewport(doc, viewport);

        // Render offscreen nodes in background batches (100 per frame slot)
        const int BatchSize = 100;
        for (int i = 0; i < offScreen.Count; i += BatchSize)
        {
            int end   = Math.Min(i + BatchSize, offScreen.Count);
            var batch = offScreen.GetRange(i, end - i);
            await Dispatcher.InvokeAsync(() =>
            {
                foreach (var n in batch)
                    _layer.RenderNode(n);
            }, DispatcherPriority.Background);
        }
    }

    // B1 — Dot-grid background rendered in OnRender (only redraws when Canvas is invalidated)
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Canvas background from theme token (fallback to neutral dark)
        Brush bgBrush = TryFindResource("CD_CanvasBackground") as Brush
                     ?? new SolidColorBrush(Color.FromRgb(26, 27, 38));
        dc.DrawRectangle(bgBrush, null, new Rect(0, 0, ActualWidth, ActualHeight));

        if (ActualWidth < 1 || ActualHeight < 1) return;

        // Dot-grid: tiny circles spaced GridSpacing apart
        Color gridColor = (TryFindResource("CD_CanvasGridLineBrush") as SolidColorBrush)?.Color
                       ?? Color.FromArgb(80, 80, 85, 120);
        var dotBrush = new SolidColorBrush(Color.FromArgb(gridColor.A, gridColor.R, gridColor.G, gridColor.B));

        for (double x = GridSpacing; x < ActualWidth; x += GridSpacing)
            for (double y = GridSpacing; y < ActualHeight; y += GridSpacing)
                dc.DrawEllipse(dotBrush, null, new Point(x, y), 1.0, 1.0);
    }

    // ── Filter bar (Phase 12) ─────────────────────────────────────────────────

    /// <summary>Shows the inline filter bar and focuses the input.</summary>
    public void ShowFilterBar()
    {
        _filterVisible            = true;
        _filterBar.Visibility     = Visibility.Visible;
        UpdateFilterBarPosition();
        _filterBar.FocusInput();
    }

    /// <summary>Hides the filter bar and clears focus mode.</summary>
    public void HideFilterBar()
    {
        _filterVisible        = false;
        _filterBar.Visibility = Visibility.Collapsed;
        _filterBar.Clear();
        _layer.SetFocusNodes(null);
        _filterBar.SetMatchCount(0, 0);
        Focus();
    }

    private ScrollViewer? FindAncestorScrollViewer()
    {
        DependencyObject? el = VisualTreeHelper.GetParent(this);
        while (el is not null)
        {
            if (el is ScrollViewer sv) return sv;
            el = VisualTreeHelper.GetParent(el);
        }
        return null;
    }

    private void UpdateFilterBarPosition()
    {
        _filterBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double barW = _filterBar.DesiredSize.Width > 0 ? _filterBar.DesiredSize.Width : 420;
        ScrollViewer? sv = FindAncestorScrollViewer();
        double viewOffX = sv?.HorizontalOffset ?? 0;
        double viewW    = sv?.ViewportWidth > 0 ? sv.ViewportWidth : ActualWidth;
        Canvas.SetLeft(_filterBar, viewOffX + (viewW - barW) / 2);
        Canvas.SetTop(_filterBar, (sv?.VerticalOffset ?? 0) + 8);
    }

    private void OnFilterChanged(object? sender, DiagramFilterArgs args)
    {
        if (_doc is null) return;

        if (string.IsNullOrEmpty(args.Text))
        {
            _layer.SetFocusNodes(null);
            _filterBar.SetMatchCount(0, 0);
            return;
        }

        string term = args.Text;
        var matched = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in _doc.Classes)
        {
            if (node.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
             || node.Namespace.Contains(term, StringComparison.OrdinalIgnoreCase)
             || node.Members.Any(m =>
                    m.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                 || m.TypeName.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                matched.Add(node.Id);
            }
        }

        _filterBar.SetMatchCount(matched.Count, _doc.Classes.Count);
        _layer.SetFocusNodes(args.FocusMode && matched.Count > 0 ? matched : null);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    /// <summary>Selects exactly one node, clearing the multi-selection set.</summary>
    private void SelectSingleNode(ClassNode? node)
    {
        _selectedIds.Clear();
        if (node is not null) _selectedIds.Add(node.Id);
        _primarySelected = node;
        _layer.SetMultiSelection(_selectedIds);
        RedrawSelectionVisual();
        _layer.UpdateSelection(_primarySelected?.Id, _hoveredNode?.Id);
        SelectedClassChanged?.Invoke(this, _primarySelected);
    }

    /// <summary>Toggles a node in the multi-selection set (Ctrl+Click).</summary>
    private void ToggleNodeSelection(ClassNode node)
    {
        if (_selectedIds.Contains(node.Id))
            _selectedIds.Remove(node.Id);
        else
            _selectedIds.Add(node.Id);

        _primarySelected = _selectedIds.Count == 1
            ? _doc!.Classes.FirstOrDefault(n => _selectedIds.Contains(n.Id))
            : null;
        _layer.SetMultiSelection(_selectedIds);
        RedrawSelectionVisual();
        _layer.UpdateSelection(_primarySelected?.Id, _hoveredNode?.Id);
        SelectedClassChanged?.Invoke(this, _primarySelected);
    }

    /// <summary>Redraws the selection visual for all currently selected nodes.</summary>
    private void RedrawSelectionVisual()
    {
        if (_selectedIds.Count == 0) { _layer.ClearSelection(); return; }

        // During resize use the resizing node bounds
        if (_resizingNode is not null)
        {
            double rh = _layer.ComputeNodeHeight(_resizingNode);
            _layer.DrawSelection(new Rect(_resizingNode.X, _resizingNode.Y, _resizingNode.Width, rh));
            return;
        }

        var rects = _doc!.Classes
            .Where(n => _selectedIds.Contains(n.Id))
            .Select(n => new Rect(n.X, n.Y, n.Width, _layer.ComputeNodeHeight(n)));
        _layer.DrawSelectionSet(rects);
    }

    // Keep compat name used in resize/drag paths
    private void UpdateSelectAdornerPosition() => RedrawSelectionVisual();

    private void ClearAdorners()
    {
        _layer.ClearSelection();
        RemoveRubberBandAdorner();
    }

    // ── Rubber-band ───────────────────────────────────────────────────────────
    // Drawn as a DrawingVisual in _layer (diagram coords) — no AdornerLayer needed.

    private void RemoveRubberBandAdorner()
    {
        // Legacy: also clean up old adorner if somehow present
        if (_rubberBandAdorner is not null && _adornerLayer is not null)
        {
            _adornerLayer.Remove(_rubberBandAdorner);
            _rubberBandAdorner = null;
        }
        _layer.ClearRubberBand();
    }

    // ── Mouse – canvas level ──────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // Don't steal mouse capture from the filter bar when it originated the click
        if (e.OriginalSource is DependencyObject origSrc
            && (_filterBar.IsAncestorOf(origSrc) || _filterBar == origSrc))
        {
            e.Handled = true;
            return;
        }

        Focus();

        Point pt   = e.GetPosition(this);
        var   node = _layer.HitTestNode(pt);

        // Double-click on any node → toggle collapse
        if (e.ClickCount == 2 && _doc is not null)
        {
            var dblNode = _layer.HitTestNode(pt);
            if (dblNode is not null)
            {
                _layer.ToggleCollapsed(dblNode.Id);
                _layer.RenderAll(_doc!, _selectedNode?.Id, _hoveredNode?.Id);
                UpdateSelectAdornerPosition();
                e.Handled = true;
                return;
            }
        }

        // Resize gripper — bottom edge of any node
        if (_doc is not null)
        {
            var gripNode = _layer.IsGripperHit(_doc.Classes, pt);
            if (gripNode is not null)
            {
                _resizingNode      = gripNode;
                _resizeStartY      = pt.Y;
                _resizeStartHeight = _layer.ComputeNodeHeight(gripNode);
                UpdateSelectAdornerPosition();
                CaptureMouse();
                e.Handled = true;
                return;
            }
        }

        // Check "N more" footer hit BEFORE node-level checks
        if (_doc is not null)
        {
            var footerNode = _layer.HitTestMoreFooter(_doc.Classes, pt);
            if (footerNode is not null)
            {
                _layer.ToggleExpanded(footerNode.Id);
                _layer.RenderAll(_doc, _selectedNode?.Id, _hoveredNode?.Id);
                e.Handled = true;
                return;
            }
        }

        if (node is not null)
        {
            // Check section header hit first (collapse/expand toggle)
            string? sectionHit = _layer.HitTestSectionHeader(pt, node);
            if (sectionHit is not null)
            {
                _layer.ToggleSection(node.Id, sectionHit);
                e.Handled = true;
                return;
            }

            // Double-click → navigate to source
            if (e.ClickCount == 2)
            {
                var member = _layer.HitTestMember(pt, node);
                NavigateToMemberRequested?.Invoke(this, (node, member ?? node.Members.FirstOrDefault()!));
                e.Handled = true;
                return;
            }

            // Ctrl+Click → toggle multi-selection (no navigate)
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                ToggleNodeSelection(node);
                e.Handled = true;
                return;
            }

            // Member selection: single-click on a member row selects it
            {
                var clickedMember = _layer.HitTestMember(pt, node);
                if (clickedMember != _selectedMember || node != _selectedMemberNode)
                {
                    _selectedMember     = clickedMember;
                    _selectedMemberNode = clickedMember is not null ? node : null;
                    _layer.SetSelectedMember(_selectedMemberNode?.Id, _selectedMember?.Name);
                }
            }

            // Plain click: if node already in selection, keep the set and just start drag;
            // otherwise collapse to single selection.
            if (!_selectedIds.Contains(node.Id))
                SelectSingleNode(node);

            // Capture start positions of all selected nodes for multi-drag
            HideHoverTooltip();
            _dragNode  = node;
            _dragStart = pt;
            _dragStartPositions.Clear();
            if (_doc is not null)
                foreach (var n in _doc.Classes.Where(n => _selectedIds.Contains(n.Id)))
                    _dragStartPositions[n.Id] = new Point(n.X, n.Y);
            _dragNodeStartX = node.X;
            _dragNodeStartY = node.Y;
            CaptureMouse();
        }
        else
        {
            // Swimlane group drag: click on swimlane background (not on a node)
            var laneNs = _layer.HitTestSwimLane(pt);
            if (laneNs is not null)
            {
                _draggingGroupNs = laneNs;
                _dragStart = pt;
                _dragStartPositions.Clear();
                foreach (var n in _layer.GetSwimLaneNodes(laneNs))
                    _dragStartPositions[n.Id] = new Point(n.X, n.Y);
                CaptureMouse();
            }
            else
            {
                // Click on empty area — rubber-band is handled by ZoomPanCanvas.
                if (_selectedIds.Count > 0) ClearSelection();
            }
        }

        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point pt = e.GetPosition(this);

        // Swimlane group drag in progress
        if (_draggingGroupNs is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            double dx = pt.X - _dragStart.X;
            double dy = pt.Y - _dragStart.Y;
            foreach (var n in _doc!.Classes.Where(n => _dragStartPositions.ContainsKey(n.Id)))
            {
                double rawX = Math.Max(0, _dragStartPositions[n.Id].X + dx);
                double rawY = Math.Max(0, _dragStartPositions[n.Id].Y + dy);
                if (_snapEngine is { SnapToGrid: true })
                {
                    var snapped = _snapEngine.SnapPoint(rawX, rawY, []);
                    n.X = snapped.X; n.Y = snapped.Y;
                }
                else { n.X = rawX; n.Y = rawY; }
            }
            ApplyPatch(_dragStartPositions.Keys, refreshArrows: false);
            e.Handled = true;
            return;
        }

        // Node resize in progress
        if (_resizingNode is not null)
        {
            double dy = pt.Y - _resizeStartY;
            _layer.SetCustomHeight(_resizingNode.Id, Math.Max(_resizeStartHeight + dy, 40));
            _layer.RenderAll(_doc!, _selectedNode?.Id, _hoveredNode?.Id);
            UpdateSelectAdornerPosition();
            e.Handled = true;
            return;
        }

        if (_dragNode is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            double dx = pt.X - _dragStart.X;
            double dy = pt.Y - _dragStart.Y;

            if (_selectedIds.Count > 1 && _dragStartPositions.Count > 0)
            {
                // Move all selected nodes together
                foreach (var n in _doc!.Classes.Where(n => _dragStartPositions.ContainsKey(n.Id)))
                {
                    double rawX = Math.Max(0, _dragStartPositions[n.Id].X + dx);
                    double rawY = Math.Max(0, _dragStartPositions[n.Id].Y + dy);
                    if (_snapEngine is { SnapToGrid: true })
                    {
                        var snapped = _snapEngine.SnapPoint(rawX, rawY, []);
                        n.X = snapped.X; n.Y = snapped.Y;
                    }
                    else { n.X = rawX; n.Y = rawY; }
                }
                ApplyPatch(_selectedIds, refreshArrows: false);
            }
            else
            {
                double rawX = Math.Max(0, _dragNodeStartX + dx);
                double rawY = Math.Max(0, _dragNodeStartY + dy);
                if (_snapEngine is { SnapToGrid: true })
                {
                    var snapped = _snapEngine.SnapPoint(rawX, rawY, []);
                    _dragNode.X = snapped.X; _dragNode.Y = snapped.Y;
                }
                else { _dragNode.X = rawX; _dragNode.Y = rawY; }
                ApplyPatch([_dragNode.Id], refreshArrows: false);
            }
        }
        else if (_isRubberBanding)
        {
            // Rubber-band move is driven by ZoomPanCanvas (which owns the capture).
            // UpdateRubberBandAt() is called directly from there with diagram-space coords.
        }
        else
        {
            // Hover tracking + resize gripper cursor
            var hovered = _layer.HitTestNode(pt);
            if (hovered != _hoveredNode)
            {
                _hoveredNode = hovered;
                _layer.UpdateSelection(_selectedNode?.Id, _hoveredNode?.Id);
                HoveredClassChanged?.Invoke(this, _hoveredNode);

                // Clear member hover when leaving a node
                if (hovered is null && _hoveredMember is not null)
                {
                    _hoveredMember = null; _hoveredMemberNode = null;
                    _layer.SetHoveredMember(null, null);
                }

                // Reset tooltip timer
                HideHoverTooltip();
                if (hovered is not null)
                {
                    _tooltipNode = hovered;
                    _tooltipTimer.Interval = TimeSpan.FromMilliseconds(TooltipDelayMs);
                    _tooltipTimer.Start();
                }
            }

            // Member hover tracking
            if (_hoveredNode is not null)
            {
                var member = _layer.HitTestMember(pt, _hoveredNode);
                if (member != _hoveredMember)
                {
                    _hoveredMember     = member;
                    _hoveredMemberNode = member is not null ? _hoveredNode : null;
                    _layer.SetHoveredMember(_hoveredMemberNode?.Id, _hoveredMember?.Name);
                }
            }

            // Show SizeNS cursor when hovering over a node's bottom-edge gripper,
            // SizeAll cursor when hovering a swimlane background (group drag hint)
            bool onGripper = _doc is not null && _layer.IsGripperHit(_doc.Classes, pt) is not null;
            if (onGripper)
                Cursor = Cursors.SizeNS;
            else if (_hoveredNode is null && _layer.HitTestSwimLane(pt) is not null)
                Cursor = Cursors.SizeAll;
            else
                Cursor = null;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();

        if (_draggingGroupNs is not null)
        {
            // Commit swimlane group drag undo entry
            if (_undoManager is not null && _doc is not null && _dragStartPositions.Count > 0)
            {
                var dragDoc = _doc;
                var moves = _dragStartPositions
                    .Select(kv =>
                    {
                        var n = dragDoc.Classes.FirstOrDefault(x => x.Id == kv.Key);
                        return n is null ? ((ClassNode?)null, kv.Value, new Point()) : (n, kv.Value, new Point(n.X, n.Y));
                    })
                    .Where(t => t.Item1 is not null && (Math.Abs(t.Item3.X - t.Item2.X) > 0.5 || Math.Abs(t.Item3.Y - t.Item2.Y) > 0.5))
                    .Select(t => (Node: t.Item1!, From: t.Item2, To: t.Item3))
                    .ToList();
                if (moves.Count > 0)
                {
                    _undoManager.Push(new SingleClassDiagramUndoEntry(
                        Description: $"Move group «{_draggingGroupNs}» ({moves.Count} nodes)",
                        UndoAction: () => { foreach (var m in moves) { m.Node.X = m.From.X; m.Node.Y = m.From.Y; } _layer.RenderAll(dragDoc, _selectedNode?.Id, _hoveredNode?.Id); },
                        RedoAction: () => { foreach (var m in moves) { m.Node.X = m.To.X;   m.Node.Y = m.To.Y;   } _layer.RenderAll(dragDoc, _selectedNode?.Id, _hoveredNode?.Id); }));
                }
            }
            _draggingGroupNs = null;
            _dragStartPositions.Clear();
            _layer.RefreshArrows();
            Cursor = null;
            e.Handled = true;
            return;
        }

        if (_resizingNode is not null)
        {
            // Commit resize undo entry
            if (_undoManager is not null && _doc is not null)
            {
                double finalH  = _layer.ComputeNodeHeight(_resizingNode);
                double startH  = _resizeStartHeight;
                var    rNode   = _resizingNode;
                var    rDoc    = _doc;   // capture current doc
                if (Math.Abs(finalH - startH) > 0.5)
                {
                    _undoManager.Push(new SingleClassDiagramUndoEntry(
                        Description: $"Resize {rNode.Name}",
                        UndoAction: () => { _layer.SetCustomHeight(rNode.Id, startH); _layer.RenderAll(rDoc, _selectedNode?.Id, _hoveredNode?.Id); },
                        RedoAction: () => { _layer.SetCustomHeight(rNode.Id, finalH); _layer.RenderAll(rDoc, _selectedNode?.Id, _hoveredNode?.Id); }));
                }
            }
            _resizingNode = null;
            Cursor = null;
            e.Handled = true;
            return;
        }

        if (_dragNode is not null)
        {
            // Commit drag undo entry for all moved nodes
            if (_undoManager is not null && _doc is not null && _dragStartPositions.Count > 0)
            {
                var dragDoc = _doc;   // capture current doc
                var moves = _dragStartPositions
                    .Select(kv =>
                    {
                        var n = dragDoc.Classes.FirstOrDefault(x => x.Id == kv.Key);
                        return n is null ? ((ClassNode?)null, kv.Value, new Point()) : (n, kv.Value, new Point(n.X, n.Y));
                    })
                    .Where(t => t.Item1 is not null && (Math.Abs(t.Item3.X - t.Item2.X) > 0.5 || Math.Abs(t.Item3.Y - t.Item2.Y) > 0.5))
                    .Select(t => (Node: t.Item1!, From: t.Item2, To: t.Item3))
                    .ToList();
                if (moves.Count > 0)
                {
                    string desc = moves.Count == 1 ? $"Move {moves[0].Node.Name}" : $"Move {moves.Count} nodes";
                    _undoManager.Push(new SingleClassDiagramUndoEntry(
                        Description: desc,
                        UndoAction: () => { foreach (var m in moves) { m.Node.X = m.From.X; m.Node.Y = m.From.Y; } _layer.RenderAll(dragDoc, _selectedNode?.Id, _hoveredNode?.Id); },
                        RedoAction: () => { foreach (var m in moves) { m.Node.X = m.To.X;   m.Node.Y = m.To.Y;   } _layer.RenderAll(dragDoc, _selectedNode?.Id, _hoveredNode?.Id); }));
                }
            }
            _dragNode = null;
            _dragStartPositions.Clear();
            // Repaint arrows once after drag ends (skipped during drag for perf)
            _layer.RefreshArrows();
        }
        // Rubber-band release is handled by ZoomPanCanvas via FinishRubberBandAt().
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragNode = null;
        _draggingGroupNs = null;
        _dragStartPositions.Clear();
        _resizingNode    = null;
        _isRubberBanding = false;
        Cursor = null;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        HandleEmptyAreaRightClick(e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>
    /// Performs a hit-test at <paramref name="pt"/> (in DiagramCanvas local coordinates)
    /// and opens the appropriate context menu. Called by ZoomPanCanvas when the right-click
    /// lands on an empty area outside the DiagramCanvas visual bounds.
    /// </summary>
    internal void HandleEmptyAreaRightClick(Point pt)
    {
        _lastMenuPoint = pt;
        var node = _layer.HitTestNode(pt);
        if (node is not null)
        {
            // Determine if click is on the header or the member area
            double relY = pt.Y - node.Y;
            double headerH = _layer.ComputeHeaderHeight(node);
            if (relY <= headerH)
                BuildHeaderContextMenu(node).IsOpen = true;
            else
                BuildMemberContextMenu(node, pt).IsOpen = true;
            return;
        }

        var rel = _layer.HitTestArrow(pt);
        if (rel is not null)
        {
            BuildArrowContextMenu(rel).IsOpen = true;
            return;
        }

        BuildEmptyCanvasContextMenu().IsOpen = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        HideHoverTooltip();
        if (_hoveredNode is not null)
        {
            _hoveredNode = null;
            _layer.UpdateSelection(_primarySelected?.Id, null);
            HoveredClassChanged?.Invoke(this, null);
        }
    }

    // ── Toolbox drag-drop ──────────────────────────────────────────────────────

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        if (_doc is null) return;
        if (!e.Data.GetDataPresent("ClassDiagramToolboxEntry")) return;

        if (e.Data.GetData("ClassDiagramToolboxEntry") is not ToolboxEntry entry) return;

        Point pt   = e.GetPosition(this);
        var   node = new ClassNode
        {
            Id      = Guid.NewGuid().ToString("N"),
            Name    = entry.Name,
            Kind    = entry.Kind,
            X       = Math.Max(0, pt.X - 80),
            Y       = Math.Max(0, pt.Y - 20),
            Width   = 160,
            Members = entry.DefaultMembers.ToList()
        };
        var dropDoc = _doc;   // capture current doc
        dropDoc.Classes.Add(node);
        _layer.RenderAll(dropDoc, _selectedNode?.Id, _hoveredNode?.Id);

        _undoManager?.Push(new SingleClassDiagramUndoEntry(
            Description: $"Add {node.Name}",
            UndoAction: () => { dropDoc.Classes.Remove(node); _layer.RenderAll(dropDoc, _selectedNode?.Id, _hoveredNode?.Id); },
            RedoAction: () => { dropDoc.Classes.Add(node);    _layer.RenderAll(dropDoc, _selectedNode?.Id, _hoveredNode?.Id); }));
        e.Handled = true;
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            CancelRubberBand();   // Cancel in-progress lasso first
            ClearSelection();
            e.Handled = true;
        }
    }

    // ── Hover tooltip ─────────────────────────────────────────────────────────

    private void ShowHoverTooltip()
    {
        if (_tooltipNode is null) return;
        HideHoverTooltip();

        var node = _tooltipNode;

        // Build content
        var panel = new StackPanel { Margin = new Thickness(8) };

        // Name row
        var nameBlock = new TextBlock
        {
            Text       = node.Name,
            FontWeight = FontWeights.Bold,
            FontSize   = 13,
            Foreground = (Brush?)TryFindResource("CD_ClassNameForeground")
                         ?? Brushes.White
        };
        panel.Children.Add(nameBlock);

        // Namespace (if present)
        if (!string.IsNullOrEmpty(node.Namespace))
            panel.Children.Add(new TextBlock
            {
                Text      = node.Namespace,
                FontSize  = 10,
                Opacity   = 0.7,
                Foreground= (Brush?)TryFindResource("CD_ClassNameForeground") ?? Brushes.LightGray
            });

        // XmlDoc summary
        if (!string.IsNullOrEmpty(node.XmlDocSummary))
        {
            panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4), Opacity = 0.3 });
            panel.Children.Add(new TextBlock
            {
                Text        = node.XmlDocSummary,
                TextWrapping= TextWrapping.Wrap,
                MaxWidth    = 300,
                FontSize    = 11,
                Foreground  = (Brush?)TryFindResource("DockMenuForegroundBrush") ?? Brushes.White
            });
        }

        // Metrics row
        panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4), Opacity = 0.3 });
        int memberCount = node.Members.Count;
        var m = node.Metrics;
        var metricsBlock = new TextBlock
        {
            FontSize   = 10,
            Foreground = (Brush?)TryFindResource("DockMenuForegroundBrush") ?? Brushes.LightGray
        };
        metricsBlock.Inlines.Add(new System.Windows.Documents.Run($"Members: {memberCount}   "));
        metricsBlock.Inlines.Add(new System.Windows.Documents.Run($"Ce: {m.EfferentCoupling}   Ca: {m.AfferentCoupling}   "));
        metricsBlock.Inlines.Add(new System.Windows.Documents.Run($"I: {m.Instability:F2}")
        {
            Foreground = m.Instability > 0.7 ? Brushes.OrangeRed
                       : m.Instability > 0.4 ? Brushes.Gold
                       : Brushes.LightGreen
        });
        panel.Children.Add(metricsBlock);

        var border = new Border
        {
            Background      = (Brush?)TryFindResource("CD_ClassBoxBackground") ?? new SolidColorBrush(Color.FromRgb(30, 30, 40)),
            BorderBrush     = (Brush?)TryFindResource("CD_ClassBoxBorderBrush") ?? Brushes.DimGray,
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Child           = panel,
            Effect          = new System.Windows.Media.Effects.DropShadowEffect
                              { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.5 }
        };

        _tooltipPopup = new Popup
        {
            Child            = border,
            Placement        = PlacementMode.Mouse,
            AllowsTransparency= true,
            IsOpen           = true,
            StaysOpen        = false
        };
    }

    private void HideHoverTooltip()
    {
        _tooltipTimer.Stop();
        if (_tooltipPopup is not null)
        {
            _tooltipPopup.IsOpen = false;
            _tooltipPopup = null;
        }
    }

    // ── Keyboard (Ctrl+F → filter bar) ───────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (_filterVisible) HideFilterBar();
            else ShowFilterBar();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.M && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            IsMinimapVisible = !IsMinimapVisible;
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    // ── Context menus ─────────────────────────────────────────────────────────

    /// <summary>Context menu for right-click on the class box header area.</summary>
    private ContextMenu BuildHeaderContextMenu(ClassNode node)
    {
        var menu = StyledMenu();
        menu.Items.Add(MakeItem("\uE70F", "Rename…",            () => RenameNodeRequested?.Invoke(this, (node, null))));
        menu.Items.Add(MakeItem("\uE16C", "Copy Name",          () => Clipboard.SetText(node.Name)));
        string fullName = string.IsNullOrEmpty(node.Namespace)
            ? node.Name
            : $"{node.Namespace}.{node.Name}";
        menu.Items.Add(MakeItem("\uE16C", "Copy Full Name",     () => Clipboard.SetText(fullName)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE7C5", "Navigate to Source", () => NavigateToMemberRequested?.Invoke(this, (node, node.Members.FirstOrDefault()!))));
        menu.Items.Add(MakeItem("\uE721", "Find References",    () => FindReferencesRequested?.Invoke(this, node)));
        menu.Items.Add(new Separator());

        bool isCollapsed = _layer.IsCollapsed(node.Id);
        menu.Items.Add(MakeItem(isCollapsed ? "\uE8A4" : "\uE89A",
            isCollapsed ? "Expand" : "Collapse",
            () =>
            {
                _layer.ToggleCollapsed(node.Id);
                _layer.RenderAll(_doc!, _selectedNode?.Id, _hoveredNode?.Id);
                UpdateSelectAdornerPosition();
            }));

        var addMenu = new MenuItem { Header = "Add Member" };
        addMenu.Items.Add(MakeItem("\uE192", "Field",    () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uE10C", "Property", () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uE8F4", "Method",   () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uECAD", "Event",    () => AddMemberRequested?.Invoke(this, node)));
        menu.Items.Add(addMenu);

        menu.Items.Add(MakeItem("\uE790", "Change Color…",  () => ChangeNodeColorRequested?.Invoke(this, node)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE9D9", "Show Metrics",   () => ShowMetricsRequested?.Invoke(this, node)));
        menu.Items.Add(MakeItem("\uE74D", "Delete",          () => DeleteNode(node)));
        return menu;
    }

    /// <summary>Context menu for right-click on a member row within a class box.</summary>
    private ContextMenu BuildMemberContextMenu(ClassNode node, Point pt)
    {
        var menu = StyledMenu();
        var member = _layer.HitTestMember(pt, node);

        if (member is not null)
        {
            menu.Items.Add(MakeItem("\uE70F", "Rename…",            () => RenameNodeRequested?.Invoke(this, (node, null))));
            menu.Items.Add(MakeItem("\uE74D", "Delete",             () => DeleteMemberRequested?.Invoke(this, (node, member))));
            menu.Items.Add(MakeItem("\uE8C8", "Duplicate",          () => DuplicateNode(node)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MakeItem("\uE16C", "Copy Name",          () => Clipboard.SetText(member.DisplayLabel)));
            menu.Items.Add(MakeItem("\uE7C5", "Navigate to Source", () => NavigateToMemberRequested?.Invoke(this, (node, member))));
            menu.Items.Add(new Separator());
        }

        var addMenu = new MenuItem { Header = "Add Member" };
        addMenu.Items.Add(MakeItem("\uE192", "Field",    () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uE10C", "Property", () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uE8F4", "Method",   () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uECAD", "Event",    () => AddMemberRequested?.Invoke(this, node)));
        menu.Items.Add(addMenu);

        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE8D4", "Properties",  () => ShowPropertiesRequested?.Invoke(this, node)));
        return menu;
    }

    private ContextMenu BuildArrowContextMenu(ClassRelationship rel)
    {
        var menu = StyledMenu();
        menu.Items.Add(MakeItem("\uE70F", "Edit Label",       () => EditRelationshipLabel(rel)));
        menu.Items.Add(new Separator());

        var changeType = new MenuItem { Header = "Change Type" };
        foreach (RelationshipKind rk in Enum.GetValues<RelationshipKind>())
        {
            var captured = rk;
            changeType.Items.Add(MakeItem("\uE8AB", rk.ToString(), () => ChangeRelationshipType(rel, captured)));
        }
        menu.Items.Add(changeType);
        menu.Items.Add(MakeItem("\uE7A8", "Reverse Direction", () => ReverseRelationshipDirection(rel)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE74D", "Delete",            () => DeleteRelationship(rel)));
        return menu;
    }

    private ContextMenu BuildEmptyCanvasContextMenu()
    {
        var menu = StyledMenu();
        menu.Items.Add(MakeItem("\uE71C", "Collapse All",
            () =>
            {
                _layer.CollapseAll(_doc!.Classes);
                _layer.RenderAll(_doc!, _selectedNode?.Id, _hoveredNode?.Id);
            }));
        menu.Items.Add(MakeItem("\uE740", "Expand All",
            () =>
            {
                _layer.ExpandAll();
                _layer.RenderAll(_doc!, _selectedNode?.Id, _hoveredNode?.Id);
            }));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE710", "Add Class",     () => AddNodeAtMenuPoint(ClassKind.Class)));
        menu.Items.Add(MakeItem("\uE710", "Add Interface", () => AddNodeAtMenuPoint(ClassKind.Interface)));
        menu.Items.Add(MakeItem("\uE710", "Add Enum",      () => AddNodeAtMenuPoint(ClassKind.Enum)));
        menu.Items.Add(MakeItem("\uE710", "Add Struct",    () => AddNodeAtMenuPoint(ClassKind.Struct)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE8B3", "Select All",    () => SelectAll()));
        menu.Items.Add(new Separator());
        var layoutSub = new MenuItem { Header = "Auto Layout" };
        layoutSub.Icon = new System.Windows.Controls.TextBlock
            { Text = "\uE947", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12 };
        layoutSub.Items.Add(MakeItem("\uE947", "Force-Directed",
            () => LayoutStrategyRequested?.Invoke(this, LayoutStrategyKind.ForceDirected)));
        layoutSub.Items.Add(MakeItem("\uE947", "Hierarchical",
            () => LayoutStrategyRequested?.Invoke(this, LayoutStrategyKind.Hierarchical)));
        layoutSub.Items.Add(MakeItem("\uE947", "Sugiyama",
            () => LayoutStrategyRequested?.Invoke(this, LayoutStrategyKind.Sugiyama)));
        menu.Items.Add(layoutSub);
        menu.Items.Add(MakeItem("\uE904", "Zoom to Fit",   () => FitToContentRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new Separator());

        var export = new MenuItem { Header = "Export" };
        export.Items.Add(MakeItem("\uEB9F", "Export PNG",           () => ExportRequested?.Invoke(this, "png")));
        export.Items.Add(MakeItem("\uE781", "Export SVG",           () => ExportRequested?.Invoke(this, "svg")));
        export.Items.Add(MakeItem("\uE8A5", "Export C#",            () => ExportRequested?.Invoke(this, "csharp")));
        export.Items.Add(MakeItem("\uE8A5", "Export Mermaid",       () => ExportRequested?.Invoke(this, "mermaid")));
        export.Items.Add(MakeItem("\uE8A5", "Export PlantUML",      () => ExportRequested?.Invoke(this, "plantUml")));
        export.Items.Add(MakeItem("\uE8A5", "Export Structurizr",   () => ExportRequested?.Invoke(this, "structurizr")));
        export.Items.Add(new Separator());
        export.Items.Add(MakeItem("\uE16C", "Copy as PNG",          () => ExportRequested?.Invoke(this, "clipboard-png")));
        export.Items.Add(MakeItem("\uE16C", "Copy PlantUML",        () => ExportRequested?.Invoke(this, "clipboard-plantUml")));
        export.Items.Add(MakeItem("\uE16C", "Copy Mermaid",         () => ExportRequested?.Invoke(this, "clipboard-mermaid")));
        menu.Items.Add(export);

        return menu;
    }

    // ── Delete / change helpers ───────────────────────────────────────────────

    private void DeleteNode(ClassNode node)
    {
        if (_doc is null) return;
        var doc        = _doc;   // capture current doc — live-sync may swap _doc later
        var removedRels = doc.Relationships.Where(r => r.SourceId == node.Id || r.TargetId == node.Id).ToList();
        doc.Classes.Remove(node);
        doc.Relationships.RemoveAll(r => r.SourceId == node.Id || r.TargetId == node.Id);
        if (_selectedNode == node) ClearSelection();
        _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id);
        _undoManager?.Push(new SingleClassDiagramUndoEntry(
            Description: $"Delete {node.Name}",
            UndoAction: () => { doc.Classes.Add(node); foreach (var r in removedRels) doc.Relationships.Add(r); _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); },
            RedoAction: () => { doc.Classes.Remove(node); foreach (var r in removedRels) doc.Relationships.Remove(r); _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); }));
    }

    private void DeleteRelationship(ClassRelationship rel)
    {
        if (_doc is null) return;
        var doc = _doc;   // capture current doc
        doc.Relationships.Remove(rel);
        _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id);
        _undoManager?.Push(new SingleClassDiagramUndoEntry(
            Description: "Delete relationship",
            UndoAction: () => { doc.Relationships.Add(rel); _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); },
            RedoAction: () => { doc.Relationships.Remove(rel); _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); }));
    }

    private void ChangeRelationshipType(ClassRelationship rel, RelationshipKind newKind)
    {
        if (_doc is null) return;
        var doc = _doc;   // capture current doc
        int idx = doc.Relationships.IndexOf(rel);
        if (idx < 0) return;
        var newRel = rel with { Kind = newKind };
        doc.Relationships[idx] = newRel;
        _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id);
        _undoManager?.Push(new SingleClassDiagramUndoEntry(
            Description: "Change relationship kind",
            UndoAction: () => { int i = doc.Relationships.IndexOf(newRel); if (i >= 0) { doc.Relationships[i] = rel; _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); } },
            RedoAction: () => { int i = doc.Relationships.IndexOf(rel);    if (i >= 0) { doc.Relationships[i] = newRel; _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); } }));
    }

    // ── Phase 1A — Duplicate node ────────────────────────────────────────────

    private void DuplicateNode(ClassNode node)
    {
        if (_doc is null) return;
        var doc  = _doc;
        var copy = node.DeepClone();
        copy.Id  = Guid.NewGuid().ToString();
        copy.X  += 30;
        copy.Y  += 30;
        doc.Classes.Add(copy);
        _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id);
        _undoManager?.Push(new SingleClassDiagramUndoEntry(
            Description: $"Duplicate {node.Name}",
            UndoAction: () => { doc.Classes.Remove(copy); _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); },
            RedoAction: () => { doc.Classes.Add(copy);    _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); }));
        SelectSingleNode(copy);
        RenameNodeRequested?.Invoke(this, (copy, null));
    }

    // ── Phase 1C — Edit relationship label (inline Popup TextBox) ───────────

    private void EditRelationshipLabel(ClassRelationship rel)
    {
        if (_doc is null) return;
        var doc = _doc;

        var tb = new TextBox
        {
            Text   = rel.Label ?? string.Empty,
            MinWidth  = 140,
            Padding   = new Thickness(4),
            BorderThickness = new Thickness(1)
        };
        tb.SetResourceReference(TextBox.BackgroundProperty, "DockBackgroundBrush");
        tb.SetResourceReference(TextBox.ForegroundProperty, "DockForegroundBrush");

        var popup = new Popup
        {
            Child              = new Border { Child = tb, Padding = new Thickness(2) },
            Placement          = PlacementMode.Mouse,
            AllowsTransparency = true,
            IsOpen             = true,
            StaysOpen          = true
        };
        tb.SelectAll();
        tb.Focus();

        void Commit()
        {
            if (!popup.IsOpen) return;
            popup.IsOpen = false;
            int idx = doc.Relationships.IndexOf(rel);
            if (idx < 0) return;
            string newLabel = tb.Text.Trim();
            if ((rel.Label ?? string.Empty) == newLabel) return;
            var newRel = rel with { Label = newLabel };
            doc.Relationships[idx] = newRel;
            _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id);
            _undoManager?.Push(new SingleClassDiagramUndoEntry(
                Description: "Edit relationship label",
                UndoAction: () =>
                {
                    int i = doc.Relationships.IndexOf(newRel);
                    if (i >= 0) { doc.Relationships[i] = rel; _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); }
                },
                RedoAction: () =>
                {
                    int i = doc.Relationships.IndexOf(rel);
                    if (i >= 0) { doc.Relationships[i] = newRel; _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); }
                }));
        }

        tb.KeyDown   += (_, ke) => { if (ke.Key == Key.Return) { Commit(); ke.Handled = true; } if (ke.Key == Key.Escape) { popup.IsOpen = false; ke.Handled = true; } };
        tb.LostFocus += (_, _)  => Commit();
    }

    // ── Phase 1D — Reverse relationship direction ────────────────────────────

    private void ReverseRelationshipDirection(ClassRelationship rel)
    {
        if (_doc is null) return;
        var doc = _doc;
        int idx = doc.Relationships.IndexOf(rel);
        if (idx < 0) return;
        var newRel = rel with { SourceId = rel.TargetId, TargetId = rel.SourceId };
        doc.Relationships[idx] = newRel;
        _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id);
        _undoManager?.Push(new SingleClassDiagramUndoEntry(
            Description: "Reverse relationship direction",
            UndoAction: () =>
            {
                int i = doc.Relationships.IndexOf(newRel);
                if (i >= 0) { doc.Relationships[i] = rel; _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); }
            },
            RedoAction: () =>
            {
                int i = doc.Relationships.IndexOf(rel);
                if (i >= 0) { doc.Relationships[i] = newRel; _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); }
            }));
    }

    // ── Phase 1E — Add new node at canvas right-click position ───────────────

    private void AddNodeAtMenuPoint(ClassKind kind)
    {
        if (_doc is null) return;
        var doc = _doc;
        string typeName = kind switch
        {
            ClassKind.Interface => "NewInterface",
            ClassKind.Enum      => "NewEnum",
            ClassKind.Struct    => "NewStruct",
            _                   => "NewClass"
        };
        var node = new ClassNode { Name = typeName, Id = Guid.NewGuid().ToString(), Kind = kind };
        node.X = Math.Max(0, _lastMenuPoint.X - node.Width / 2);
        node.Y = Math.Max(0, _lastMenuPoint.Y - 40);
        doc.Classes.Add(node);
        _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id);
        _undoManager?.Push(new SingleClassDiagramUndoEntry(
            Description: $"Add {kind} {typeName}",
            UndoAction: () => { doc.Classes.Remove(node); _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); },
            RedoAction: () => { doc.Classes.Add(node);    _layer.RenderAll(doc, _selectedNode?.Id, _hoveredNode?.Id); }));
        SelectSingleNode(node);
        RenameNodeRequested?.Invoke(this, (node, null));
    }

    // ── Context menu helpers (delegates to shared DiagramMenuHelpers) ─────────

    private ContextMenu StyledMenu() => DiagramMenuHelpers.StyledMenu(this);

    private static MenuItem MakeItem(string icon, string header, Action action)
        => DiagramMenuHelpers.MakeItem(icon, header, action);
}
