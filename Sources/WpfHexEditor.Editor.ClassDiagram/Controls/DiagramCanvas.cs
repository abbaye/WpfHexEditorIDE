// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramCanvas.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Main diagram canvas. Manages ClassBoxControl and
//     RelationshipArrowControl children, adorner-layer selection
//     and rubber-band selection, and canvas-level context menus.
//
// Architecture Notes:
//     Pattern: Composite + Mediator.
//     ApplyDocument rebuilds all children from the DiagramDocument.
//     Selection state is maintained via ClassBoxSelectAdorner.
//     RubberBandAdorner is used for drag-select on empty space.
//     Context menus use DynamicResource DockMenu* tokens.
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
/// Canvas that hosts class box controls and relationship arrows for a <see cref="DiagramDocument"/>.
/// </summary>
public sealed class DiagramCanvas : Canvas
{
    private readonly Dictionary<string, ClassBoxControl> _classBoxes = [];
    private readonly List<RelationshipArrowControl> _arrows = [];

    private AdornerLayer? _adornerLayer;
    private ClassBoxSelectAdorner? _selectAdorner;
    private RubberBandAdorner? _rubberBandAdorner;

    private ClassBoxControl? _selectedBox;
    private ClassBoxControl? _dragBox;

    private readonly DiagramCanvasViewModel _vm = new();

    // Drag state
    private bool _isDragging;
    private Point _dragStart;
    private double _dragNodeStartX;
    private double _dragNodeStartY;

    // Rubber-band state
    private bool _isRubberBanding;
    private Point _rubberStart;

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public event EventHandler<ClassNode?>? SelectedClassChanged;
    public event EventHandler<ClassNode?>? HoveredClassChanged;
    public event EventHandler<ClassNode?>? AddMemberRequested;

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    public DiagramCanvas()
    {
        Background = Brushes.Transparent; // Required for hit-testing on empty space
        Focusable  = true;
        Loaded    += OnLoaded;
    }

    // ---------------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Rebuilds all children (class boxes and arrows) from the given document.
    /// </summary>
    public void ApplyDocument(DiagramDocument doc)
    {
        ClearAdorners();
        Children.Clear();
        _classBoxes.Clear();
        _arrows.Clear();
        _selectedBox = null;

        foreach (ClassNode node in doc.Classes)
        {
            var box = CreateClassBox(node);
            _classBoxes[node.Id] = box;

            Canvas.SetLeft(box, node.X);
            Canvas.SetTop(box, node.Y);
            Children.Add(box);
        }

        foreach (ClassRelationship rel in doc.Relationships)
        {
            var arrow = CreateArrow(rel, doc);
            _arrows.Add(arrow);
            Children.Add(arrow);
        }

        // Ensure arrows are below boxes in Z-order
        foreach (var arrow in _arrows)
            Panel.SetZIndex(arrow, 0);

        foreach (var box in _classBoxes.Values)
            Panel.SetZIndex(box, 1);
    }

    /// <summary>Returns the currently selected class node, or null.</summary>
    public ClassNode? SelectedNode => _selectedBox?.Node;

    // ---------------------------------------------------------------------------
    // Loaded
    // ---------------------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _adornerLayer = AdornerLayer.GetAdornerLayer(this);
    }

    // ---------------------------------------------------------------------------
    // ClassBoxControl factory
    // ---------------------------------------------------------------------------

    private ClassBoxControl CreateClassBox(ClassNode node)
    {
        var box = new ClassBoxControl { Node = node };

        box.MouseLeftButtonDown += OnBoxMouseLeftButtonDown;
        box.MouseMove           += OnBoxMouseMove;
        box.MouseLeftButtonUp   += OnBoxMouseLeftButtonUp;
        box.HoveredClassChanged += (_, n) => HoveredClassChanged?.Invoke(this, n);
        box.DeleteRequested     += (_, n) => OnDeleteNodeRequested(n);
        box.AddMemberRequested  += (_, t) => AddMemberRequested?.Invoke(this, t.Item1);

        return box;
    }

    // ---------------------------------------------------------------------------
    // RelationshipArrow factory
    // ---------------------------------------------------------------------------

    private RelationshipArrowControl CreateArrow(ClassRelationship rel, DiagramDocument doc)
    {
        var arrow = new RelationshipArrowControl { Relationship = rel };
        UpdateArrowBounds(arrow, rel, doc);
        arrow.DeleteRequested += (_, _) => OnDeleteRelationshipRequested(rel);
        return arrow;
    }

    private static void UpdateArrowBounds(RelationshipArrowControl arrow,
        ClassRelationship rel, DiagramDocument doc)
    {
        ClassNode? srcNode = doc.FindClass(rel.SourceId);
        ClassNode? tgtNode = doc.FindClass(rel.TargetId);

        if (srcNode is not null)
            arrow.SourceBounds = new Rect(srcNode.X, srcNode.Y, srcNode.Width, srcNode.Height);
        if (tgtNode is not null)
            arrow.TargetBounds = new Rect(tgtNode.X, tgtNode.Y, tgtNode.Width, tgtNode.Height);
    }

    // ---------------------------------------------------------------------------
    // Selection management
    // ---------------------------------------------------------------------------

    private void SelectBox(ClassBoxControl? box)
    {
        if (_selectedBox is not null)
        {
            _selectedBox.IsSelected = false;
            RemoveSelectAdorner();
        }

        _selectedBox = box;

        if (_selectedBox is not null)
        {
            _selectedBox.IsSelected = true;
            AttachSelectAdorner(_selectedBox);
        }

        SelectedClassChanged?.Invoke(this, _selectedBox?.Node);
    }

    private void AttachSelectAdorner(ClassBoxControl box)
    {
        if (_adornerLayer is null) return;

        double left = Canvas.GetLeft(box).IfNaN(0);
        double top  = Canvas.GetTop(box).IfNaN(0);
        Rect bounds = new(left, top, box.RenderSize.Width, box.RenderSize.Height);

        _selectAdorner = new ClassBoxSelectAdorner(this) { AdornedBounds = bounds };
        _adornerLayer.Add(_selectAdorner);
    }

    private void RemoveSelectAdorner()
    {
        if (_selectAdorner is null || _adornerLayer is null) return;
        _adornerLayer.Remove(_selectAdorner);
        _selectAdorner = null;
    }

    private void ClearAdorners()
    {
        RemoveSelectAdorner();
        RemoveRubberBandAdorner();
    }

    // ---------------------------------------------------------------------------
    // Rubber-band adorner
    // ---------------------------------------------------------------------------

    private void AttachRubberBand(Point start)
    {
        if (_adornerLayer is null) return;
        _rubberBandAdorner = new RubberBandAdorner(this)
        {
            StartPoint = start,
            EndPoint   = start
        };
        _adornerLayer.Add(_rubberBandAdorner);
    }

    private void RemoveRubberBandAdorner()
    {
        if (_rubberBandAdorner is null || _adornerLayer is null) return;
        _adornerLayer.Remove(_rubberBandAdorner);
        _rubberBandAdorner = null;
    }

    // ---------------------------------------------------------------------------
    // Box mouse handlers
    // ---------------------------------------------------------------------------

    private void OnBoxMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ClassBoxControl box) return;

        SelectBox(box);

        _dragBox = box;
        _isDragging = false;
        _dragStart = e.GetPosition(this);
        _dragNodeStartX = box.Node?.X ?? 0;
        _dragNodeStartY = box.Node?.Y ?? 0;

        box.CaptureMouse();
        e.Handled = true;
    }

    private void OnBoxMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ClassBoxControl box || _dragBox != box) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        _isDragging = true;
        Point pos   = e.GetPosition(this);
        double dx   = pos.X - _dragStart.X;
        double dy   = pos.Y - _dragStart.Y;

        double newX = Math.Max(0, _dragNodeStartX + dx);
        double newY = Math.Max(0, _dragNodeStartY + dy);

        if (box.Node is not null)
        {
            box.Node.X = newX;
            box.Node.Y = newY;
        }

        Canvas.SetLeft(box, newX);
        Canvas.SetTop(box, newY);

        // Update select adorner position
        if (_selectAdorner is not null)
            _selectAdorner.AdornedBounds = new Rect(newX, newY, box.RenderSize.Width, box.RenderSize.Height);

        e.Handled = true;
    }

    private void OnBoxMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ClassBoxControl box || _dragBox != box) return;
        box.ReleaseMouseCapture();
        _dragBox    = null;
        _isDragging = false;
        e.Handled   = true;
    }

    // ---------------------------------------------------------------------------
    // Canvas mouse handlers (rubber-band, deselect, empty context menu)
    // ---------------------------------------------------------------------------

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        // Click on empty canvas — deselect and begin rubber-band
        if (e.OriginalSource == this || e.OriginalSource is DrawingVisual)
        {
            SelectBox(null);
            _isRubberBanding = true;
            _rubberStart     = e.GetPosition(this);
            AttachRubberBand(_rubberStart);
            CaptureMouse();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isRubberBanding || _rubberBandAdorner is null) return;
        _rubberBandAdorner.EndPoint = e.GetPosition(this);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_isRubberBanding) return;

        _isRubberBanding = false;
        RemoveRubberBandAdorner();
        ReleaseMouseCapture();
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);

        if (e.OriginalSource == this)
        {
            BuildEmptyCanvasContextMenu().IsOpen = true;
            e.Handled = true;
        }
    }

    // ---------------------------------------------------------------------------
    // Delete handlers
    // ---------------------------------------------------------------------------

    private void OnDeleteNodeRequested(ClassNode? node)
    {
        if (node is null) return;

        if (_classBoxes.TryGetValue(node.Id, out var box))
        {
            if (_selectedBox == box) SelectBox(null);
            Children.Remove(box);
            _classBoxes.Remove(node.Id);
        }
    }

    private void OnDeleteRelationshipRequested(ClassRelationship rel)
    {
        var arrow = _arrows.FirstOrDefault(a => a.Relationship == rel);
        if (arrow is null) return;
        Children.Remove(arrow);
        _arrows.Remove(arrow);
    }

    // ---------------------------------------------------------------------------
    // Empty canvas context menu
    // ---------------------------------------------------------------------------

    private ContextMenu BuildEmptyCanvasContextMenu()
    {
        var menu = new ContextMenu();
        menu.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "DockMenuBackgroundBrush");
        menu.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "DockMenuForegroundBrush");

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

        var exportMenu = new MenuItem { Header = "Export" };
        exportMenu.Items.Add(MakeItem("\uEB9F", "Export PNG",     () => { }));
        exportMenu.Items.Add(MakeItem("\uE781", "Export SVG",     () => { }));
        exportMenu.Items.Add(MakeItem("\uE8A5", "Export C#",      () => { }));
        exportMenu.Items.Add(MakeItem("\uE8A5", "Export Mermaid", () => { }));
        menu.Items.Add(exportMenu);

        return menu;
    }

    // ---------------------------------------------------------------------------
    // Context menu helper
    // ---------------------------------------------------------------------------

    private static MenuItem MakeItem(string icon, string header, Action action)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon   = new TextBlock
            {
                Text       = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize   = 14
            }
        };
        item.Click += (_, _) => action();
        return item;
    }
}
