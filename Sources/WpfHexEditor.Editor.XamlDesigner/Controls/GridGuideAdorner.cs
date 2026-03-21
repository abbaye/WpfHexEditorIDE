// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Controls/GridGuideAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     VS2026-style Grid column/row guide adorner.
//     Placed on the selected Grid element; draws dashed guide lines at
//     each internal column/row boundary and hosts interactive handle chips
//     (above/left of Grid) and invisible boundary grips (drag-to-resize).
//
// Visual layout:
//     ┌──────┬──────┬──────┐   ← column handle chips (above Grid)
//     │      ┆      ┆      │   ← dashed column guide lines
//     ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌┤   ← dashed row guide line
//     │      ┆      ┆      │
//     └──────┴──────┴──────┘
//  ↑ row handle chips (left of Grid)
//
// Architecture Notes:
//     Adorner with VisualCollection: GridHandleChip[] + GridBoundaryGrip[].
//     Guide lines drawn in OnRender. Chips/grips are UIElements → WPF handles
//     hit-testing and layout (including negative-coordinate handles outside Grid bounds).
//     Events bubble: GridGuideAdorner → DesignCanvas → XamlDesignerSplitHost.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

// ── GridHandleChip ─────────────────────────────────────────────────────────────

/// <summary>
/// Small badge drawn above (columns) or to the left (rows) of the Grid.
/// Shows index + size type with a dropdown button (type change) and a × button (delete).
/// </summary>
internal sealed class GridHandleChip : FrameworkElement
{
    // Layout constants
    private const double ChipW   = 58;
    private const double ChipH   = 22;
    private const double BtnW    = 15;   // width of each action button
    private const double Radius  = 3;

    // Hit-test rects (in local coordinates, set in OnRender)
    private Rect _dropdownRect;
    private Rect _deleteRect;

    public GridDefinitionModel Model { get; }

    public event EventHandler?        DeleteClicked;
    public event EventHandler?        DropdownClicked;

    public GridHandleChip(GridDefinitionModel model)
    {
        Model  = model;
        Width  = ChipW;
        Height = ChipH;
        ToolTip = model.IsColumn
            ? $"Column {model.Index}: {model.RawValue}  ({model.ActualPixels:F0} px)"
            : $"Row {model.Index}: {model.RawValue}  ({model.ActualPixels:F0} px)";
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bgBrush = Application.Current?.TryFindResource("XD_GridHandleBackground") as Brush
                      ?? new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F));

        var borderPen = new Pen(
            Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(0x26, 0x7F, 0xCF)), 1.0);
        borderPen.Freeze();

        var textBrush = Brushes.White;
        var dimBrush  = new SolidColorBrush(Color.FromArgb(180, 200, 200, 200));
        dimBrush.Freeze();

        var fullRect = new Rect(0, 0, ChipW, ChipH);

        // Background
        dc.DrawRoundedRectangle(bgBrush, borderPen, fullRect, Radius, Radius);

        // Index + size label
        var labelText = $"{Model.Index}  {Model.DisplayLabel}";
        var ft = MakeText(labelText, 10, textBrush);
        double labelX = 5;
        double labelY = (ChipH - ft.Height) / 2;
        dc.DrawText(ft, new Point(labelX, labelY));

        // Dropdown "▾" button zone
        _dropdownRect = new Rect(ChipW - BtnW * 2, 0, BtnW, ChipH);
        var dropFt = MakeText("▾", 9, dimBrush);
        dc.DrawText(dropFt, new Point(
            _dropdownRect.X + (_dropdownRect.Width - dropFt.Width) / 2,
            (ChipH - dropFt.Height) / 2));

        // Divider before delete button
        dc.DrawLine(borderPen,
            new Point(ChipW - BtnW, 3),
            new Point(ChipW - BtnW, ChipH - 3));

        // Delete "×" button zone
        _deleteRect = new Rect(ChipW - BtnW, 0, BtnW, ChipH);
        var delBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x70, 0x70));
        delBrush.Freeze();
        var delFt = MakeText("×", 10, delBrush);
        dc.DrawText(delFt, new Point(
            _deleteRect.X + (_deleteRect.Width - delFt.Width) / 2,
            (ChipH - delFt.Height) / 2));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (_deleteRect.Contains(pos))
        {
            DeleteClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        else if (_dropdownRect.Contains(pos))
        {
            DropdownClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
        base.OnMouseLeftButtonDown(e);
    }

    private static FormattedText MakeText(string text, double size, Brush brush)
        => new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                brush,
                96);
}

// ── GridBoundaryGrip ────────────────────────────────────────────────────────────

/// <summary>
/// Invisible drag zone positioned at an internal Grid boundary.
/// Width = 8 px for column boundaries; Height = 8 px for row boundaries.
/// Shows the appropriate resize cursor and fires delta events on drag.
/// </summary>
internal sealed class GridBoundaryGrip : FrameworkElement
{
    public bool IsColumn { get; }
    public int  BoundaryAfterIndex { get; }   // boundary is after definitions[this index]

    /// <summary>Fired continuously during a drag with the cumulative pixel delta.</summary>
    public event EventHandler<double>? DragDelta;

    /// <summary>Fired when the mouse is released, carrying the final pixel delta.</summary>
    public event EventHandler<double>? DragCompleted;

    private bool   _isDragging;
    private Point  _dragStart;
    private double _cumulativeDelta;

    public GridBoundaryGrip(bool isColumn, int boundaryAfterIndex)
    {
        IsColumn           = isColumn;
        BoundaryAfterIndex = boundaryAfterIndex;
        Cursor             = isColumn ? Cursors.SizeWE : Cursors.SizeNS;
        IsHitTestVisible   = true;
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => new PointHitTestResult(this, hitTestParameters.HitPoint);

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _isDragging      = true;
        _cumulativeDelta = 0;
        _dragStart       = e.GetPosition(null);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_isDragging) return;
        var current = e.GetPosition(null);
        _cumulativeDelta = IsColumn
            ? current.X - _dragStart.X
            : current.Y - _dragStart.Y;
        DragDelta?.Invoke(this, _cumulativeDelta);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
        DragCompleted?.Invoke(this, _cumulativeDelta);
        _cumulativeDelta = 0;
        e.Handled = true;
    }
}

// ── GridAddButton ──────────────────────────────────────────────────────────────

/// <summary>Small "+" badge shown at the end of the column or row ruler.</summary>
internal sealed class GridAddButton : FrameworkElement
{
    public bool IsColumn { get; }
    public event EventHandler? Clicked;

    public GridAddButton(bool isColumn)
    {
        IsColumn = isColumn;
        Width    = 22;
        Height   = 22;
        Cursor   = Cursors.Hand;
        ToolTip  = isColumn ? "Add column" : "Add row";
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bgBrush = Application.Current?.TryFindResource("XD_GridHandleBackground") as Brush
                      ?? new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F));
        var pen = new Pen(
            Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(0x26, 0x7F, 0xCF)), 1.0);
        pen.Freeze();

        dc.DrawRoundedRectangle(bgBrush, pen, new Rect(0, 0, 22, 22), 3, 3);

        var ft = new FormattedText("+", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.White, 96);
        dc.DrawText(ft, new Point((22 - ft.Width) / 2, (22 - ft.Height) / 2));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}

// ── GridGuideAdorner ──────────────────────────────────────────────────────────

/// <summary>
/// Main adorner that draws Grid column/row guide lines and hosts interactive
/// handle chips, boundary grips, and add-buttons via a VisualCollection.
/// </summary>
public sealed class GridGuideAdorner : Adorner
{
    // ── Visual constants ──────────────────────────────────────────────────────

    private const double ChipGap       = 4.0;   // gap between Grid edge and handle chip
    private const double GripThickness = 8.0;   // hit-zone width of each boundary grip

    // ── Children ──────────────────────────────────────────────────────────────

    private readonly VisualCollection          _visuals;
    private readonly List<GridHandleChip>      _colChips = [];
    private readonly List<GridHandleChip>      _rowChips = [];
    private readonly List<GridBoundaryGrip>    _colGrips = [];
    private readonly List<GridBoundaryGrip>    _rowGrips = [];
    private          GridAddButton?            _addColBtn;
    private          GridAddButton?            _addRowBtn;

    // ── State ─────────────────────────────────────────────────────────────────

    private IReadOnlyList<GridDefinitionModel> _cols = Array.Empty<GridDefinitionModel>();
    private IReadOnlyList<GridDefinitionModel> _rows = Array.Empty<GridDefinitionModel>();

    // Per-boundary drag preview offset (updated on DragDelta, reset on DragCompleted)
    private readonly Dictionary<(bool IsCol, int Idx), double> _dragPreview = [];

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<GridGuideResizedEventArgs>?     GuideResized;
    public event EventHandler<GridGuideAddedEventArgs>?       GuideAdded;
    public event EventHandler<GridGuideRemovedEventArgs>?     GuideRemoved;
    public event EventHandler<GridGuideTypeChangedEventArgs>? GuideTypeChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    public GridGuideAdorner(UIElement adornedElement) : base(adornedElement)
    {
        _visuals = new VisualCollection(this);
        IsHitTestVisible = true;
    }

    // ── VisualCollection plumbing ─────────────────────────────────────────────

    protected override int    VisualChildrenCount    => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds all guide children from the supplied definition snapshot.
    /// Call after every successful canvas re-render.
    /// </summary>
    public void Refresh(
        IReadOnlyList<GridDefinitionModel> cols,
        IReadOnlyList<GridDefinitionModel> rows)
    {
        _cols = cols;
        _rows = rows;
        _dragPreview.Clear();
        RebuildChildren();
        InvalidateMeasure();
        InvalidateVisual();
    }

    // ── Children rebuild ──────────────────────────────────────────────────────

    private void RebuildChildren()
    {
        _visuals.Clear();
        _colChips.Clear();  _rowChips.Clear();
        _colGrips.Clear();  _rowGrips.Clear();
        _addColBtn = null;  _addRowBtn = null;

        // Column handle chips (one per column)
        foreach (var col in _cols)
        {
            var chip = new GridHandleChip(col);
            chip.DeleteClicked   += (_, _) => OnChipDelete(col);
            chip.DropdownClicked += (_, _) => OnChipDropdown(chip, col);
            _colChips.Add(chip);
            _visuals.Add(chip);
        }

        // Row handle chips (one per row)
        foreach (var row in _rows)
        {
            var chip = new GridHandleChip(row);
            chip.DeleteClicked   += (_, _) => OnChipDelete(row);
            chip.DropdownClicked += (_, _) => OnChipDropdown(chip, row);
            _rowChips.Add(chip);
            _visuals.Add(chip);
        }

        // Column boundary grips (between adjacent columns)
        for (int i = 0; i < _cols.Count - 1; i++)
        {
            var grip = new GridBoundaryGrip(true, i);
            grip.DragDelta     += (_, delta) => OnGripDelta(true, i, delta);
            grip.DragCompleted += (_, delta) => OnGripCompleted(true, i, delta);
            _colGrips.Add(grip);
            _visuals.Add(grip);
        }

        // Row boundary grips
        for (int i = 0; i < _rows.Count - 1; i++)
        {
            var grip = new GridBoundaryGrip(false, i);
            grip.DragDelta     += (_, delta) => OnGripDelta(false, i, delta);
            grip.DragCompleted += (_, delta) => OnGripCompleted(false, i, delta);
            _rowGrips.Add(grip);
            _visuals.Add(grip);
        }

        // Add-column button (right of Grid)
        _addColBtn = new GridAddButton(true);
        _addColBtn.Clicked += (_, _) => GuideAdded?.Invoke(this,
            new GridGuideAddedEventArgs { IsColumn = true,  InsertAfter = _cols.Count - 1, Definition = "*" });
        _visuals.Add(_addColBtn);

        // Add-row button (below Grid)
        _addRowBtn = new GridAddButton(false);
        _addRowBtn.Clicked += (_, _) => GuideAdded?.Invoke(this,
            new GridGuideAddedEventArgs { IsColumn = false, InsertAfter = _rows.Count - 1, Definition = "*" });
        _visuals.Add(_addRowBtn);
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size constraint)
    {
        foreach (UIElement child in _visuals)
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return base.MeasureOverride(constraint);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double chipW = _colChips.Count > 0 ? _colChips[0].DesiredSize.Width : 58;
        double chipH = _colChips.Count > 0 ? _colChips[0].DesiredSize.Height : 22;

        // Column chips — above the Grid, centered on each column
        foreach (var chip in _colChips)
        {
            var def = chip.Model;
            double cx = def.CenterOffsetPixels;
            chip.Arrange(new Rect(
                cx - chipW / 2,
                -(chipH + ChipGap),
                chipW,
                chipH));
        }

        // Row chips — to the left of the Grid, centered on each row
        foreach (var chip in _rowChips)
        {
            var def = chip.Model;
            double cy = def.CenterOffsetPixels;
            chip.Arrange(new Rect(
                -(chipW + ChipGap),
                cy - chipH / 2,
                chipW,
                chipH));
        }

        // Column boundary grips — thin vertical strips at each internal column boundary
        for (int i = 0; i < _colGrips.Count; i++)
        {
            double x = _cols[i].EndOffsetPixels;
            _colGrips[i].Arrange(new Rect(
                x - GripThickness / 2, 0,
                GripThickness, finalSize.Height));
        }

        // Row boundary grips — thin horizontal strips at each internal row boundary
        for (int i = 0; i < _rowGrips.Count; i++)
        {
            double y = _rows[i].EndOffsetPixels;
            _rowGrips[i].Arrange(new Rect(
                0, y - GripThickness / 2,
                finalSize.Width, GripThickness));
        }

        // Add-column button — to the right of the Grid, above center
        _addColBtn?.Arrange(new Rect(
            finalSize.Width + ChipGap,
            -(chipH + ChipGap),
            _addColBtn.DesiredSize.Width,
            _addColBtn.DesiredSize.Height));

        // Add-row button — below the Grid, to the left of center
        _addRowBtn?.Arrange(new Rect(
            -(chipW + ChipGap),
            finalSize.Height + ChipGap,
            _addRowBtn.DesiredSize.Width,
            _addRowBtn.DesiredSize.Height));

        return finalSize;
    }

    // ── Guide line rendering ──────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        if (_cols.Count == 0 && _rows.Count == 0) return;

        var guideBrush = Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
                         ?? new SolidColorBrush(Color.FromRgb(0x26, 0x7F, 0xCF));

        var guidePen = new Pen(guideBrush, 1.0)
        {
            DashStyle = new DashStyle(new double[] { 5, 3 }, 0)
        };
        guidePen.Freeze();

        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;

        // Internal column guide lines
        for (int i = 0; i < _cols.Count - 1; i++)
        {
            double x = _cols[i].EndOffsetPixels;

            // Shift line by drag preview if active
            if (_dragPreview.TryGetValue((true, i), out var dx))
                x += dx;

            dc.DrawLine(guidePen, new Point(x, 0), new Point(x, h));
        }

        // Internal row guide lines
        for (int i = 0; i < _rows.Count - 1; i++)
        {
            double y = _rows[i].EndOffsetPixels;

            if (_dragPreview.TryGetValue((false, i), out var dy))
                y += dy;

            dc.DrawLine(guidePen, new Point(0, y), new Point(w, y));
        }
    }

    // ── Drag events ───────────────────────────────────────────────────────────

    private void OnGripDelta(bool isCol, int index, double delta)
    {
        _dragPreview[(isCol, index)] = delta;
        InvalidateVisual();   // redraw guide line at preview position
    }

    private void OnGripCompleted(bool isCol, int index, double delta)
    {
        _dragPreview.Remove((isCol, index));
        InvalidateVisual();

        var def = isCol ? _cols[index] : _rows[index];
        string newValue = ComputeNewValue(def, delta);
        if (string.IsNullOrEmpty(newValue)) return;

        GuideResized?.Invoke(this, new GridGuideResizedEventArgs
        {
            IsColumn    = isCol,
            Index       = index,
            NewRawValue = newValue
        });
    }

    /// <summary>Converts a pixel drag delta into an appropriate new XAML size string.</summary>
    private static string ComputeNewValue(GridDefinitionModel def, double delta)
    {
        return def.SizeType switch
        {
            GridSizeType.Fixed =>
                $"{Math.Max(4, Math.Round(def.FixedPixels + delta, 0)).ToString(CultureInfo.InvariantCulture)}",

            GridSizeType.Star when def.ActualPixels > 0 =>
                $"{Math.Max(0.01, def.StarFactor * (def.ActualPixels + delta) / def.ActualPixels):G4}*",

            GridSizeType.Auto => string.Empty,   // Auto cannot be drag-resized

            _ => string.Empty
        };
    }

    // ── Chip button events ────────────────────────────────────────────────────

    private void OnChipDelete(GridDefinitionModel def)
        => GuideRemoved?.Invoke(this, new GridGuideRemovedEventArgs
           { IsColumn = def.IsColumn, Index = def.Index });

    private void OnChipDropdown(GridHandleChip chip, GridDefinitionModel def)
    {
        var menu = new ContextMenu();
        menu.SetResourceReference(ContextMenu.BackgroundProperty, "DockMenuBackgroundBrush");
        menu.SetResourceReference(ContextMenu.ForegroundProperty, "DockMenuForegroundBrush");

        AddTypeItem(menu, "Star (*)",    def, GridSizeType.Star,  "*");
        AddTypeItem(menu, "Auto",        def, GridSizeType.Auto,  "Auto");
        AddTypeItem(menu, "Fixed (px…)", def, GridSizeType.Fixed, "100");
        menu.PlacementTarget = chip;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void AddTypeItem(ContextMenu menu, string header,
        GridDefinitionModel def, GridSizeType type, string rawValue)
    {
        var item = new MenuItem
        {
            Header    = header,
            IsChecked = def.SizeType == type
        };
        item.SetResourceReference(MenuItem.ForegroundProperty, "DockMenuForegroundBrush");
        item.Click += (_, _) => GuideTypeChanged?.Invoke(this,
            new GridGuideTypeChangedEventArgs
            {
                IsColumn    = def.IsColumn,
                Index       = def.Index,
                NewType     = type,
                NewRawValue = rawValue
            });
        menu.Items.Add(item);
    }
}
