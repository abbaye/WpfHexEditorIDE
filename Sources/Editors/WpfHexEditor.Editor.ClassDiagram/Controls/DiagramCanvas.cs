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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WpfHexEditor.Editor.ClassDiagram.Controls.Adorners;
using WpfHexEditor.Editor.ClassDiagram.Core.Layout;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
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
    private ClassNode?        _selectedNode;
    private ClassNode?        _hoveredNode;
    private ClassNode?        _dragNode;

    private readonly DiagramCanvasViewModel _vm = new();

    // ── Adorners ──────────────────────────────────────────────────────────────
    private AdornerLayer?          _adornerLayer;
    private ClassBoxSelectAdorner? _selectAdorner;
    private RubberBandAdorner?     _rubberBandAdorner;

    // ── Drag ──────────────────────────────────────────────────────────────────
    private Point  _dragStart;
    private double _dragNodeStartX;
    private double _dragNodeStartY;

    // ── Rubber-band ───────────────────────────────────────────────────────────
    private bool  _isRubberBanding;
    private Point _rubberStart;

    // ── Minimap ───────────────────────────────────────────────────────────────
    private readonly DiagramMinimapControl _minimap = new();
    private bool          _minimapVisible = true;
    private MinimapCorner _minimapCorner  = MinimapCorner.BottomLeft;

    private const double MinimapMargin = 8.0;

    // ── Filter bar (Phase 12) ─────────────────────────────────────────────────
    private readonly DiagramFilterBar _filterBar = new();
    private bool _filterVisible;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<ClassNode?>?                    SelectedClassChanged;
    public event EventHandler<ClassNode?>?                    HoveredClassChanged;
    public event EventHandler<ClassNode?>?                    AddMemberRequested;
    public event EventHandler<(ClassNode Node, string? NewName)>? RenameNodeRequested;
    public event EventHandler<(ClassNode Node, ClassMember Member)>? DeleteMemberRequested;
    public event EventHandler<(ClassNode Node, ClassMember Member)>? NavigateToMemberRequested;
    public event EventHandler<string>?                        ExportRequested;  // format: "png","plantUml","structurizr","mermaid","svg","csharp"
    public event EventHandler<LayoutStrategyKind>?            LayoutStrategyRequested;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DiagramCanvas()
    {
        Background = Brushes.Transparent;
        Focusable  = true;
        Loaded    += OnLoaded;

        Children.Add(_layer);
        Canvas.SetLeft(_layer, 0);
        Canvas.SetTop(_layer, 0);

        // Minimap — bottom-left by default, above main layer.
        Children.Add(_minimap);
        _minimap.ViewportNavigateRequested += OnMinimapNavigate;
        _minimap.CornerChangeRequested     += (_, corner) => SetMinimapCorner(corner);
        _minimap.HideRequested             += (_, _) => IsMinimapVisible = false;
        SizeChanged += (_, _) => UpdateMinimapPosition();
        UpdateMinimapPosition();

        // Filter bar — top-center, hidden by default.
        _filterBar.Visibility  = Visibility.Collapsed;
        _filterBar.FilterChanged  += OnFilterChanged;
        _filterBar.CloseRequested += (_, _) => HideFilterBar();
        Children.Add(_filterBar);
        Panel.SetZIndex(_filterBar, 200);
        SizeChanged += (_, _) => UpdateFilterBarPosition();
    }

    // ── Minimap API ───────────────────────────────────────────────────────────

    public bool IsMinimapVisible
    {
        get => _minimapVisible;
        set
        {
            _minimapVisible     = value;
            _minimap.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public MinimapCorner MinimapCorner
    {
        get => _minimapCorner;
        private set => _minimapCorner = value;
    }

    /// <summary>Moves the minimap to a corner with a smooth animation.</summary>
    public void SetMinimapCorner(MinimapCorner corner)
    {
        _minimapCorner = corner;
        UpdateMinimapPosition(animate: true);
    }

    private void UpdateMinimapPosition(bool animate = false)
    {
        double pw = ActualWidth;
        double ph = ActualHeight;
        if (pw <= 0 || ph <= 0) { Panel.SetZIndex(_minimap, 100); return; }

        double targetLeft = _minimapCorner is MinimapCorner.TopLeft or MinimapCorner.BottomLeft
            ? MinimapMargin
            : pw - DiagramMinimapControl.MapWidth - MinimapMargin;

        double targetTop = _minimapCorner is MinimapCorner.TopLeft or MinimapCorner.TopRight
            ? MinimapMargin
            : ph - DiagramMinimapControl.MapHeight - MinimapMargin;

        if (animate)
        {
            var dur = new Duration(TimeSpan.FromMilliseconds(150));
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var animL = new DoubleAnimation(targetLeft, dur) { EasingFunction = ease };
            var animT = new DoubleAnimation(targetTop,  dur) { EasingFunction = ease };
            _minimap.BeginAnimation(Canvas.LeftProperty, animL);
            _minimap.BeginAnimation(Canvas.TopProperty,  animT);
        }
        else
        {
            Canvas.SetLeft(_minimap, targetLeft);
            Canvas.SetTop(_minimap,  targetTop);
        }
        Panel.SetZIndex(_minimap, 100);
    }

    private void OnMinimapNavigate(object? sender, System.Windows.Point diagPos)
    {
        // Walk up to find a ScrollViewer ancestor and set its scroll offset.
        ScrollViewer? sv = FindAncestorScrollViewer();
        if (sv is null) return;
        sv.ScrollToHorizontalOffset(diagPos.X);
        sv.ScrollToVerticalOffset(diagPos.Y);
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

    /// <summary>Notifies the minimap of a new scroll viewport (call from the parent scroll host).</summary>
    public void SetMinimapViewport(Rect viewportInDiagramCoords) =>
        _minimap.SetViewport(viewportInDiagramCoords);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Rebuilds all visuals from the given document.</summary>
    public void ApplyDocument(DiagramDocument doc)
    {
        _doc          = doc;
        _selectedNode = null;
        _hoveredNode  = null;

        ClearAdorners();
        _layer.RenderAll(doc);
        _minimap.SetDocument(doc);
        SelectedClassChanged?.Invoke(this, null);
    }

    /// <summary>Incremental update: re-renders only the nodes in the patch.</summary>
    public void ApplyPatch(IEnumerable<string> dirtyNodeIds)
    {
        if (_doc is null) return;
        _layer.InvalidateNodes(_doc, dirtyNodeIds, _selectedNode?.Id, _hoveredNode?.Id);
        UpdateSelectAdornerPosition();
    }

    /// <summary>Returns the currently selected class node, or null.</summary>
    public ClassNode? SelectedNode => _selectedNode;

    // ── Loaded ────────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _adornerLayer = AdornerLayer.GetAdornerLayer(this);
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

    private void SelectNode(ClassNode? node)
    {
        if (_selectedNode == node) return;
        _selectedNode = node;
        RemoveSelectAdorner();

        if (_selectedNode is not null)
            AttachSelectAdorner(_selectedNode);

        _layer.UpdateSelection(_selectedNode?.Id, _hoveredNode?.Id);
        SelectedClassChanged?.Invoke(this, _selectedNode);
    }

    private void AttachSelectAdorner(ClassNode node)
    {
        if (_adornerLayer is null) return;
        _selectAdorner = new ClassBoxSelectAdorner(this)
        {
            AdornedBounds = new Rect(node.X, node.Y, node.Width, node.Height)
        };
        _adornerLayer.Add(_selectAdorner);
    }

    private void RemoveSelectAdorner()
    {
        if (_selectAdorner is null || _adornerLayer is null) return;
        _adornerLayer.Remove(_selectAdorner);
        _selectAdorner = null;
    }

    private void UpdateSelectAdornerPosition()
    {
        if (_selectAdorner is null || _selectedNode is null) return;
        _selectAdorner.AdornedBounds =
            new Rect(_selectedNode.X, _selectedNode.Y, _selectedNode.Width, _selectedNode.Height);
    }

    private void ClearAdorners()
    {
        RemoveSelectAdorner();
        RemoveRubberBandAdorner();
    }

    // ── Rubber-band ───────────────────────────────────────────────────────────

    private void AttachRubberBand(Point start)
    {
        if (_adornerLayer is null) return;
        _rubberBandAdorner = new RubberBandAdorner(this) { StartPoint = start, EndPoint = start };
        _adornerLayer.Add(_rubberBandAdorner);
    }

    private void RemoveRubberBandAdorner()
    {
        if (_rubberBandAdorner is null || _adornerLayer is null) return;
        _adornerLayer.Remove(_rubberBandAdorner);
        _rubberBandAdorner = null;
    }

    // ── Mouse – canvas level ──────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();

        Point pt   = e.GetPosition(this);
        var   node = _layer.HitTestNode(pt);

        if (node is not null)
        {
            // Ctrl+Click → navigate to source
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                var member = _layer.HitTestMember(pt, node);
                if (member is not null)
                    NavigateToMemberRequested?.Invoke(this, (node, member));
                else
                    NavigateToMemberRequested?.Invoke(this, (node, node.Members.FirstOrDefault()!));
                e.Handled = true;
                return;
            }

            SelectNode(node);
            _dragNode       = node;
            _dragStart      = pt;
            _dragNodeStartX = node.X;
            _dragNodeStartY = node.Y;
            CaptureMouse();
        }
        else
        {
            SelectNode(null);
            _isRubberBanding = true;
            _rubberStart     = pt;
            AttachRubberBand(_rubberStart);
            CaptureMouse();
        }

        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point pt = e.GetPosition(this);

        if (_dragNode is not null && e.LeftButton == MouseButtonState.Pressed)
        {
            double dx = pt.X - _dragStart.X;
            double dy = pt.Y - _dragStart.Y;

            _dragNode.X = Math.Max(0, _dragNodeStartX + dx);
            _dragNode.Y = Math.Max(0, _dragNodeStartY + dy);

            ApplyPatch([_dragNode.Id]);
        }
        else if (_isRubberBanding && _rubberBandAdorner is not null)
        {
            _rubberBandAdorner.EndPoint = pt;
        }
        else
        {
            // Hover tracking
            var hovered = _layer.HitTestNode(pt);
            if (hovered != _hoveredNode)
            {
                _hoveredNode = hovered;
                _layer.UpdateSelection(_selectedNode?.Id, _hoveredNode?.Id);
                HoveredClassChanged?.Invoke(this, _hoveredNode);
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();

        if (_dragNode is not null)
        {
            _dragNode = null;
        }
        else if (_isRubberBanding)
        {
            _isRubberBanding = false;
            RemoveRubberBandAdorner();
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        Point pt = e.GetPosition(this);

        var node = _layer.HitTestNode(pt);
        if (node is not null)
        {
            BuildNodeContextMenu(node).IsOpen = true;
            e.Handled = true;
            return;
        }

        var rel = _layer.HitTestArrow(pt);
        if (rel is not null)
        {
            BuildArrowContextMenu(rel).IsOpen = true;
            e.Handled = true;
            return;
        }

        BuildEmptyCanvasContextMenu().IsOpen = true;
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveredNode is not null)
        {
            _hoveredNode = null;
            _layer.UpdateSelection(_selectedNode?.Id, null);
            HoveredClassChanged?.Invoke(this, null);
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

    private ContextMenu BuildNodeContextMenu(ClassNode node)
    {
        var menu = StyledMenu();
        menu.Items.Add(MakeItem("\uE70F", "Rename…",   () => RenameNodeRequested?.Invoke(this, (node, null))));
        menu.Items.Add(MakeItem("\uE74D", "Delete",    () => DeleteNode(node)));
        menu.Items.Add(MakeItem("\uE8C8", "Duplicate", () => { }));
        menu.Items.Add(new Separator());

        var addMenu = new MenuItem { Header = "Add Member" };
        addMenu.Items.Add(MakeItem("\uE192", "Field",    () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uE10C", "Property", () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uE8F4", "Method",   () => AddMemberRequested?.Invoke(this, node)));
        addMenu.Items.Add(MakeItem("\uECAD", "Event",    () => AddMemberRequested?.Invoke(this, node)));
        menu.Items.Add(addMenu);

        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE8D4", "Properties", () => { }));
        return menu;
    }

    private ContextMenu BuildArrowContextMenu(ClassRelationship rel)
    {
        var menu = StyledMenu();
        menu.Items.Add(MakeItem("\uE70F", "Edit Label",       () => { }));
        menu.Items.Add(new Separator());

        var changeType = new MenuItem { Header = "Change Type" };
        foreach (RelationshipKind rk in Enum.GetValues<RelationshipKind>())
        {
            var captured = rk;
            changeType.Items.Add(MakeItem("\uE8AB", rk.ToString(), () => ChangeRelationshipType(rel, captured)));
        }
        menu.Items.Add(changeType);
        menu.Items.Add(MakeItem("\uE7A8", "Reverse Direction", () => { }));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE74D", "Delete",            () => DeleteRelationship(rel)));
        return menu;
    }

    private ContextMenu BuildEmptyCanvasContextMenu()
    {
        var menu = StyledMenu();
        menu.Items.Add(MakeItem("\uE710", "Add Class",     () => { }));
        menu.Items.Add(MakeItem("\uE710", "Add Interface", () => { }));
        menu.Items.Add(MakeItem("\uE710", "Add Enum",      () => { }));
        menu.Items.Add(MakeItem("\uE710", "Add Struct",    () => { }));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem("\uE77F", "Paste",         () => { }));
        menu.Items.Add(MakeItem("\uE8B3", "Select All",    () => { }));
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
        menu.Items.Add(MakeItem("\uE904", "Zoom to Fit",   () => { }));
        menu.Items.Add(new Separator());

        var export = new MenuItem { Header = "Export" };
        export.Items.Add(MakeItem("\uEB9F", "Export PNG",        () => ExportRequested?.Invoke(this, "png")));
        export.Items.Add(MakeItem("\uE781", "Export SVG",        () => ExportRequested?.Invoke(this, "svg")));
        export.Items.Add(MakeItem("\uE8A5", "Export C#",         () => ExportRequested?.Invoke(this, "csharp")));
        export.Items.Add(MakeItem("\uE8A5", "Export Mermaid",    () => ExportRequested?.Invoke(this, "mermaid")));
        export.Items.Add(MakeItem("\uE8A5", "Export PlantUML",   () => ExportRequested?.Invoke(this, "plantUml")));
        export.Items.Add(MakeItem("\uE8A5", "Export Structurizr",() => ExportRequested?.Invoke(this, "structurizr")));
        menu.Items.Add(export);

        return menu;
    }

    // ── Delete / change helpers ───────────────────────────────────────────────

    private void DeleteNode(ClassNode node)
    {
        if (_doc is null) return;
        _doc.Classes.Remove(node);
        _doc.Relationships.RemoveAll(r => r.SourceId == node.Id || r.TargetId == node.Id);
        if (_selectedNode == node) SelectNode(null);
        _layer.RenderAll(_doc, _selectedNode?.Id, _hoveredNode?.Id);
    }

    private void DeleteRelationship(ClassRelationship rel)
    {
        if (_doc is null) return;
        _doc.Relationships.Remove(rel);
        _layer.RenderAll(_doc, _selectedNode?.Id, _hoveredNode?.Id);
    }

    private void ChangeRelationshipType(ClassRelationship rel, RelationshipKind newKind)
    {
        if (_doc is null) return;
        int idx = _doc.Relationships.IndexOf(rel);
        if (idx < 0) return;
        _doc.Relationships[idx] = rel with { Kind = newKind };
        _layer.RenderAll(_doc, _selectedNode?.Id, _hoveredNode?.Id);
    }

    // ── Context menu helpers ──────────────────────────────────────────────────

    private ContextMenu StyledMenu()
    {
        var menu = new ContextMenu();
        menu.SetResourceReference(Control.BackgroundProperty, "DockMenuBackgroundBrush");
        menu.SetResourceReference(Control.ForegroundProperty, "DockMenuForegroundBrush");
        return menu;
    }

    private static MenuItem MakeItem(string icon, string header, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon   = new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }
        };
        item.Click += (_, _) => action();
        return item;
    }
}
