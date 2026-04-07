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
using WpfHexEditor.Editor.ClassDiagram.Controls.Adorners;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Canvas that hosts a <see cref="DiagramVisualLayer"/> for high-performance rendering
/// and manages selection, hover, drag-move, and context menus via manual hit-testing.
/// </summary>
public sealed class DiagramCanvas : Canvas
{
    // ── Children ──────────────────────────────────────────────────────────────
    private readonly DiagramVisualLayer _layer = new();

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
    private bool _minimapVisible = true;

    // ── Events ────────────────────────────────────────────────────────────────
    public event EventHandler<ClassNode?>?                    SelectedClassChanged;
    public event EventHandler<ClassNode?>?                    HoveredClassChanged;
    public event EventHandler<ClassNode?>?                    AddMemberRequested;
    public event EventHandler<(ClassNode Node, string? NewName)>? RenameNodeRequested;
    public event EventHandler<(ClassNode Node, ClassMember Member)>? DeleteMemberRequested;
    public event EventHandler<(ClassNode Node, ClassMember Member)>? NavigateToMemberRequested;
    public event EventHandler<string>?                        ExportRequested;  // format: "png","plantUml","structurizr","mermaid","svg","csharp"

    // ── Constructor ───────────────────────────────────────────────────────────

    public DiagramCanvas()
    {
        Background = Brushes.Transparent;
        Focusable  = true;
        Loaded    += OnLoaded;

        Children.Add(_layer);
        Canvas.SetLeft(_layer, 0);
        Canvas.SetTop(_layer, 0);

        // Minimap — bottom-left, above main layer.
        Children.Add(_minimap);
        _minimap.ViewportNavigateRequested += OnMinimapNavigate;
        SizeChanged += (_, _) => UpdateMinimapPosition();
        UpdateMinimapPosition();
    }

    // ── Minimap API ───────────────────────────────────────────────────────────

    public bool IsMinimapVisible
    {
        get => _minimapVisible;
        set
        {
            _minimapVisible    = value;
            _minimap.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateMinimapPosition()
    {
        // Position minimap in bottom-left, anchored to visible area.
        // Use the parent ScrollViewer's ViewportHeight when available.
        ScrollViewer? sv = FindAncestorScrollViewer();
        double viewH = sv?.ViewportHeight > 0 ? sv.ViewportHeight
                     : ActualHeight > 0       ? ActualHeight
                     : 400;
        double viewOffY = sv?.VerticalOffset ?? 0;

        Canvas.SetLeft(_minimap, 8);
        Canvas.SetTop(_minimap, viewOffY + viewH - DiagramMinimapControl.MapHeight - 8);
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
        menu.Items.Add(MakeItem("\uE8D3", "Auto Layout",   () => { }));
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
