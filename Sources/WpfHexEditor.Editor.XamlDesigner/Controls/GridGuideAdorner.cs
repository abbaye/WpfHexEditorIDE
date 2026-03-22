// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Controls/GridGuideAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Updated: 2026-03-18 — Full rewrite: all drawing in OnRender, all mouse
//     interaction dispatched via HitRegion/DragRegion tables on the adorner.
//     Root cause: FrameworkElement children drawn via OnRender are not
//     hit-testable; events never fired. Fix: single adorner owns everything.
// ==========================================================
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

public sealed class GridGuideAdorner : Adorner
{
    private const double ChipW   = 60.0;
    private const double ChipH   = 20.0;
    private const double BtnW    = 16.0;
    private const double GripW   = 8.0;
    private const double AddSz   = 20.0;
    private const double EdgeOff = 4.0;

    private sealed record HitRegion(Rect Bounds, Action OnClick);
    private sealed record DragRegion(Rect Bounds, bool IsColumn, int Index);

    private readonly List<HitRegion>  _hits  = [];
    private readonly List<DragRegion> _drags = [];

    private IReadOnlyList<GridDefinitionModel> _cols = Array.Empty<GridDefinitionModel>();
    private IReadOnlyList<GridDefinitionModel> _rows = Array.Empty<GridDefinitionModel>();

    private bool        _dragging;
    private DragRegion  _activeDrag;
    private Point       _dragOrigin;
    private double      _dragDelta;

    // Hover state: bounds of the hit/drag region currently under the pointer.
    private Rect        _hoveredBounds = Rect.Empty;

    public event EventHandler<GridGuideResizedEventArgs>?     GuideResized;
    public event EventHandler<GridGuideAddedEventArgs>?       GuideAdded;
    public event EventHandler<GridGuideRemovedEventArgs>?     GuideRemoved;
    public event EventHandler<GridGuideTypeChangedEventArgs>? GuideTypeChanged;

    public GridGuideAdorner(UIElement adornedElement) : base(adornedElement)
        => IsHitTestVisible = true;

    protected override HitTestResult HitTestCore(PointHitTestParameters p)
    {
        var b = new Rect(AdornedElement.RenderSize);
        return b.Contains(p.HitPoint) ? new PointHitTestResult(this, p.HitPoint) : null!;
    }

    public void Refresh(IReadOnlyList<GridDefinitionModel> cols, IReadOnlyList<GridDefinitionModel> rows)
    {
        _cols = cols; _rows = rows; _dragging = false; _dragDelta = 0;
        _hoveredBounds = Rect.Empty;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        _hits.Clear(); _drags.Clear();
        if (_cols.Count == 0 && _rows.Count == 0) return;

        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;

        // Guide lines
        var gp = GuidePen();
        for (int i = 0; i < _cols.Count - 1; i++)
        {
            double x = _cols[i].EndOffsetPixels + (_dragging && _activeDrag.IsColumn && _activeDrag.Index == i ? _dragDelta : 0);
            dc.DrawLine(gp, new Point(x, 0), new Point(x, h));
        }
        for (int i = 0; i < _rows.Count - 1; i++)
        {
            double y = _rows[i].EndOffsetPixels + (_dragging && !_activeDrag.IsColumn && _activeDrag.Index == i ? _dragDelta : 0);
            dc.DrawLine(gp, new Point(0, y), new Point(w, y));
        }

        // Column chips
        foreach (var col in _cols)
        {
            double cx = Math.Max(0, Math.Min(w - ChipW, col.CenterOffsetPixels - ChipW / 2));
            var r = new Rect(cx, EdgeOff, ChipW, ChipH);
            DrawChip(dc, r, col.Index, col.DisplayLabel);
            var snap = col;
            _hits.Add(new HitRegion(new Rect(r.Right - BtnW * 2, r.Y, BtnW, ChipH), () => OpenTypeMenu(snap)));
            _hits.Add(new HitRegion(new Rect(r.Right - BtnW,     r.Y, BtnW, ChipH), () => FireRemoved(snap)));
            if (col.Index < _cols.Count - 1)
                _drags.Add(new DragRegion(new Rect(col.EndOffsetPixels - GripW / 2, 0, GripW, h), true, col.Index));
        }

        // Row chips
        foreach (var row in _rows)
        {
            double ry = Math.Max(0, Math.Min(h - ChipH, row.CenterOffsetPixels - ChipH / 2));
            var r = new Rect(EdgeOff, ry, ChipW, ChipH);
            DrawChip(dc, r, row.Index, row.DisplayLabel);
            var snap = row;
            _hits.Add(new HitRegion(new Rect(r.Right - BtnW * 2, r.Y, BtnW, ChipH), () => OpenTypeMenu(snap)));
            _hits.Add(new HitRegion(new Rect(r.Right - BtnW,     r.Y, BtnW, ChipH), () => FireRemoved(snap)));
            if (row.Index < _rows.Count - 1)
                _drags.Add(new DragRegion(new Rect(0, row.EndOffsetPixels - GripW / 2, w, GripW), false, row.Index));
        }

        // Add buttons
        var addCol = new Rect(w - AddSz - EdgeOff, EdgeOff, AddSz, AddSz);
        DrawAddBtn(dc, addCol);
        _hits.Add(new HitRegion(addCol, () => GuideAdded?.Invoke(this,
            new GridGuideAddedEventArgs { IsColumn = true, InsertAfter = _cols.Count - 1, Definition = "*" })));

        var addRow = new Rect(EdgeOff, h - AddSz - EdgeOff, AddSz, AddSz);
        DrawAddBtn(dc, addRow);
        _hits.Add(new HitRegion(addRow, () => GuideAdded?.Invoke(this,
            new GridGuideAddedEventArgs { IsColumn = false, InsertAfter = _rows.Count - 1, Definition = "*" })));

        // Drag preview
        if (_dragging)
        {
            var dp = new Pen(Brushes.DeepSkyBlue, 1.5) { DashStyle = DashStyles.Dash }; dp.Freeze();
            if (_activeDrag.IsColumn && _activeDrag.Index < _cols.Count)
            {
                double x = _cols[_activeDrag.Index].EndOffsetPixels + _dragDelta;
                dc.DrawLine(dp, new Point(x, 0), new Point(x, h));
                DrawSizeLabel(dc, ComputeNewValue(_cols[_activeDrag.Index], _dragDelta), new Point(x + 4, h / 2 - 10));
            }
            else if (!_activeDrag.IsColumn && _activeDrag.Index < _rows.Count)
            {
                double y = _rows[_activeDrag.Index].EndOffsetPixels + _dragDelta;
                dc.DrawLine(dp, new Point(0, y), new Point(w, y));
                DrawSizeLabel(dc, ComputeNewValue(_rows[_activeDrag.Index], _dragDelta), new Point(w / 2 - 20, y + 4));
            }
        }

        // Hover highlight overlay — drawn last so it appears above everything.
        if (!_dragging && _hoveredBounds != Rect.Empty)
        {
            var hl = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)); hl.Freeze();
            dc.DrawRoundedRectangle(hl, null, _hoveredBounds, 2, 2);
        }
    }

    // -- Draw helpers ----------------------------------------------------------

    private void DrawChip(DrawingContext dc, Rect r, int index, string label)
    {
        var bg  = ChipBg();
        var pen = new Pen(Accent(), 1.0); pen.Freeze();
        var dim = new SolidColorBrush(Color.FromArgb(180, 200, 200, 200)); dim.Freeze();
        var del = new SolidColorBrush(Color.FromRgb(0xC0, 0x70, 0x70));   del.Freeze();

        dc.DrawRoundedRectangle(bg, pen, r, 3, 3);
        var ft = T($"{index}  {label}", 10, Brushes.White);
        dc.DrawText(ft, new Point(r.X + 4, r.Y + (ChipH - ft.Height) / 2));

        double d1 = r.Right - BtnW * 2;
        dc.DrawLine(pen, new Point(d1, r.Y + 3), new Point(d1, r.Bottom - 3));
        var dft = T("▾", 9, dim);
        dc.DrawText(dft, new Point(d1 + (BtnW - dft.Width) / 2, r.Y + (ChipH - dft.Height) / 2));

        double d2 = r.Right - BtnW;
        dc.DrawLine(pen, new Point(d2, r.Y + 3), new Point(d2, r.Bottom - 3));
        var xft = T("×", 11, del);
        dc.DrawText(xft, new Point(d2 + (BtnW - xft.Width) / 2, r.Y + (ChipH - xft.Height) / 2));
    }

    private static void DrawAddBtn(DrawingContext dc, Rect r)
    {
        var bg  = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)); bg.Freeze();
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(0x26, 0x7F, 0xCF)), 1.0); pen.Freeze();
        dc.DrawRoundedRectangle(bg, pen, r, 3, 3);
        var ft = new FormattedText("+", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, Brushes.White, 96);
        dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) / 2, r.Y + (r.Height - ft.Height) / 2));
    }

    private static void DrawSizeLabel(DrawingContext dc, string text, Point pos)
    {
        if (string.IsNullOrEmpty(text)) return;
        var bg = new SolidColorBrush(Color.FromArgb(200, 25, 25, 50)); bg.Freeze();
        var ft = T(text, 10, Brushes.White);
        dc.DrawRoundedRectangle(bg, null, new Rect(pos.X - 2, pos.Y - 1, ft.Width + 6, ft.Height + 2), 2, 2);
        dc.DrawText(ft, pos);
    }

    // -- Brushes / text --------------------------------------------------------

    private static Brush ChipBg()
        => Application.Current?.TryFindResource("XD_GridHandleBackground") as Brush
           ?? new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F));

    private static Brush Accent()
        => Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
           ?? new SolidColorBrush(Color.FromRgb(0x26, 0x7F, 0xCF));

    private static Pen GuidePen()
    {
        var p = new Pen(Accent(), 1.0) { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) };
        p.Freeze(); return p;
    }

    private static FormattedText T(string text, double size, Brush brush)
        => new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
               new Typeface("Segoe UI"), size, brush, 96);

    // -- Mouse -----------------------------------------------------------------

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        foreach (var h in _hits)
        {
            if (!h.Bounds.Contains(pos)) continue;
            h.OnClick(); e.Handled = true; return;
        }
        foreach (var d in _drags)
        {
            if (!d.Bounds.Contains(pos)) continue;
            _dragging = true; _activeDrag = d; _dragOrigin = pos; _dragDelta = 0;
            CaptureMouse(); InvalidateVisual(); e.Handled = true; return;
        }
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        if (_dragging)
        {
            _dragDelta = _activeDrag.IsColumn ? pos.X - _dragOrigin.X : pos.Y - _dragOrigin.Y;
            InvalidateVisual(); e.Handled = true;
        }
        else
        {
            var hitRegion  = _hits.FirstOrDefault(h => h.Bounds.Contains(pos));
            var dragRegion = _drags.FirstOrDefault(d => d.Bounds.Contains(pos));

            // Update cursor: resize cursor on drag grips, hand on clickable chips/buttons.
            Cursor = dragRegion is not null ? (dragRegion.IsColumn ? Cursors.SizeWE : Cursors.SizeNS)
                   : hitRegion  is not null ? Cursors.Hand
                   : Cursors.Arrow;

            // Update hover highlight, invalidating only when the hovered region changes.
            var newBounds = hitRegion?.Bounds ?? dragRegion?.Bounds ?? Rect.Empty;
            if (newBounds != _hoveredBounds)
            {
                _hoveredBounds = newBounds;
                InvalidateVisual();
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        var pos = e.GetPosition(this);
        _dragDelta = _activeDrag.IsColumn ? pos.X - _dragOrigin.X : pos.Y - _dragOrigin.Y;
        CommitDrag(); ReleaseMouseCapture(); _dragging = false; _dragDelta = 0;
        InvalidateVisual(); e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_dragging) return;
        Cursor = Cursors.Arrow;
        if (_hoveredBounds != Rect.Empty)
        {
            _hoveredBounds = Rect.Empty;
            InvalidateVisual();
        }
    }

    // -- Actions ---------------------------------------------------------------

    private void CommitDrag()
    {
        var def = _activeDrag.IsColumn
            ? (_activeDrag.Index < _cols.Count ? _cols[_activeDrag.Index] : null)
            : (_activeDrag.Index < _rows.Count ? _rows[_activeDrag.Index] : null);
        if (def is null) return;
        var v = ComputeNewValue(def, _dragDelta);
        if (!string.IsNullOrEmpty(v))
            GuideResized?.Invoke(this, new GridGuideResizedEventArgs
                { IsColumn = _activeDrag.IsColumn, Index = _activeDrag.Index, NewRawValue = v });
    }

    private void FireRemoved(GridDefinitionModel d)
        => GuideRemoved?.Invoke(this, new GridGuideRemovedEventArgs { IsColumn = d.IsColumn, Index = d.Index });

    private void OpenTypeMenu(GridDefinitionModel def)
    {
        var menu = new ContextMenu();
        menu.SetResourceReference(ContextMenu.BackgroundProperty, "DockMenuBackgroundBrush");
        menu.SetResourceReference(ContextMenu.ForegroundProperty, "DockMenuForegroundBrush");

        void Add(string header, GridSizeType type, string raw)
        {
            var item = new MenuItem { Header = header, IsChecked = def.SizeType == type };
            item.SetResourceReference(MenuItem.ForegroundProperty, "DockMenuForegroundBrush");
            item.Click += (_, _) => GuideTypeChanged?.Invoke(this,
                new GridGuideTypeChangedEventArgs
                    { IsColumn = def.IsColumn, Index = def.Index, NewType = type, NewRawValue = raw });
            menu.Items.Add(item);
        }
        Add("Star (*)",    GridSizeType.Star,  "*");
        Add("Auto",        GridSizeType.Auto,  "Auto");
        Add("Fixed (px)", GridSizeType.Fixed, "100");
        menu.PlacementTarget = this;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
        menu.IsOpen    = true;
    }

    private static string ComputeNewValue(GridDefinitionModel d, double delta)
        => d.SizeType switch
        {
            GridSizeType.Fixed =>
                $"{Math.Max(4, Math.Round(d.FixedPixels + delta)):G}",
            GridSizeType.Star when d.ActualPixels > 0 =>
                $"{Math.Max(0.01, d.StarFactor * (d.ActualPixels + delta) / d.ActualPixels):G4}*",
            _ => string.Empty
        };
}
