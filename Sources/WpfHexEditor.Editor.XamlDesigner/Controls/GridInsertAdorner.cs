// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Controls/GridInsertAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-19
// Updated: 2026-03-19 — v4:
//   • Toggle button is now an INSERT action button (not a mode-switch).
//     Clicking it — or anywhere on the dashed guide line — inserts the
//     RowDefinition / ColumnDefinition at the current guide position.
//   • Mode (Row / Column) is determined automatically by DesignCanvas
//     from the edge-band proximity of the mouse:
//       left/right 18px  →  Row    (horizontal guide follows mouse Y)
//       top/bottom 18px  →  Column (vertical   guide follows mouse X)
//       corner/interior  →  keep last mode
//   • Guide always follows the mouse (no interior freeze).
//
// Visual layout
//   Row mode   : [≡+] button at left inner edge  | – – – – – – guide – – – – |
//   Column mode: [|||+] button at top inner edge  |      guide vertical      |
//
// Architecture Notes:
//   IsHitTestVisible = false.  DesignCanvas owns all interaction.
//   ToggleBounds / LineBounds expose Grid-local click rectangles.
// ==========================================================

using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Dashed "insert definition here" guide adorner rendered over a
/// <see cref="System.Windows.Controls.Grid"/> on the design canvas.
/// </summary>
public sealed class GridInsertAdorner : Adorner
{
    // ── Mode ──────────────────────────────────────────────────────────────────

    public enum InsertMode { Row, Column }

    // ── Layout constants ──────────────────────────────────────────────────────

    private const double LineHitTol = 8.0;   // half-thickness of line click zone
    private const double BtnW       = 22.0;
    private const double BtnH       = 18.0;

    // ── State ─────────────────────────────────────────────────────────────────

    private InsertMode _mode        = InsertMode.Row;
    private double     _linePos     = -1.0;
    private int        _insertAfter = -1;

    // ── Hit regions (Grid-local coordinates) ──────────────────────────────────

    public Rect ToggleBounds { get; private set; } = Rect.Empty;
    public Rect LineBounds   { get; private set; } = Rect.Empty;

    public InsertMode Mode        => _mode;
    public int        InsertAfter => _insertAfter;
    public bool       IsVisible   => _linePos >= 0;

    // ── Constructor ───────────────────────────────────────────────────────────

    public GridInsertAdorner(UIElement adornedElement) : base(adornedElement)
        => IsHitTestVisible = false;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates the guide position, mode and insert index, then redraws.</summary>
    public void Update(double linePos, InsertMode mode, int insertAfter)
    {
        _linePos     = linePos;
        _mode        = mode;
        _insertAfter = insertAfter;
        InvalidateVisual();
    }

    public void Hide()
    {
        if (_linePos < 0) return;
        _linePos     = -1;
        ToggleBounds = Rect.Empty;
        LineBounds   = Rect.Empty;
        InvalidateVisual();
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override HitTestResult HitTestCore(PointHitTestParameters p) => null!;

    protected override void OnRender(DrawingContext dc)
    {
        ToggleBounds = Rect.Empty;
        LineBounds   = Rect.Empty;

        if (_linePos < 0) return;

        double w = AdornedElement.RenderSize.Width;
        double h = AdornedElement.RenderSize.Height;

        // ── Colours ───────────────────────────────────────────────────────────
        var accent    = Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
                        ?? new SolidColorBrush(Color.FromRgb(0x00, 0xBF, 0xFF));
        var btnFill   = new SolidColorBrush(Color.FromRgb(0x10, 0x60, 0xBF)); btnFill.Freeze();
        var borderPen = new Pen(accent, 1.0); borderPen.Freeze();
        var linePen   = new Pen(accent, 1.5)
            { DashStyle = new DashStyle(new double[] { 5, 3 }, 0) };
        linePen.Freeze();

        if (_mode == InsertMode.Row)
        {
            double y    = Math.Max(0, Math.Min(h, _linePos));
            double btnY = Math.Max(0, Math.Min(h - BtnH, y - BtnH / 2));
            var    btn  = new Rect(0, btnY, BtnW, BtnH);

            // Dashed horizontal guide line (starts after the toggle button).
            dc.DrawLine(linePen, new Point(BtnW, y), new Point(w, y));

            // Insert action button on the LEFT inner edge.
            dc.DrawRoundedRectangle(btnFill, borderPen, btn, 3, 3);
            DrawRowAddIcon(dc, btn);
            ToggleBounds = btn;

            // Full-width clickable strip (excludes toggle — handled separately).
            LineBounds = new Rect(BtnW, y - LineHitTol, w - BtnW, LineHitTol * 2);
        }
        else
        {
            double x    = Math.Max(0, Math.Min(w, _linePos));
            double btnX = Math.Max(0, Math.Min(w - BtnW, x - BtnW / 2));
            var    btn  = new Rect(btnX, 0, BtnW, BtnH);

            // Dashed vertical guide line (starts after the toggle button).
            dc.DrawLine(linePen, new Point(x, BtnH), new Point(x, h));

            // Insert action button on the TOP inner edge.
            dc.DrawRoundedRectangle(btnFill, borderPen, btn, 3, 3);
            DrawColumnAddIcon(dc, btn);
            ToggleBounds = btn;

            LineBounds = new Rect(x - LineHitTol, BtnH, LineHitTol * 2, h - BtnH);
        }
    }

    // ── Insert-action icons ───────────────────────────────────────────────────

    /// <summary>
    /// Row-add icon: two horizontal stripes (row symbol) on the left,
    /// bold "+" on the right — conveys "add row here".
    /// </summary>
    private static void DrawRowAddIcon(DrawingContext dc, Rect btn)
    {
        var pen = new Pen(Brushes.White, 1.2); pen.Freeze();
        double cy = btn.Y + btn.Height / 2;
        double mx = btn.X + btn.Width  / 2 - 1;

        // Two horizontal stripes (row symbol)
        dc.DrawLine(pen, new Point(btn.X + 3, cy - 2.5), new Point(mx, cy - 2.5));
        dc.DrawLine(pen, new Point(btn.X + 3, cy + 2.5), new Point(mx, cy + 2.5));

        // Bold "+" on the right half
        var plusPen = new Pen(Brushes.White, 1.8); plusPen.Freeze();
        double px = btn.X + btn.Width * 0.72;
        dc.DrawLine(plusPen, new Point(px - 3.5, cy), new Point(px + 3.5, cy));
        dc.DrawLine(plusPen, new Point(px, cy - 3.5), new Point(px, cy + 3.5));
    }

    /// <summary>
    /// Column-add icon: two vertical stripes (column symbol) on the top,
    /// bold "+" on the bottom — conveys "add column here".
    /// </summary>
    private static void DrawColumnAddIcon(DrawingContext dc, Rect btn)
    {
        var pen = new Pen(Brushes.White, 1.2); pen.Freeze();
        double cx = btn.X + btn.Width  / 2;
        double my = btn.Y + btn.Height / 2 - 1;

        // Two vertical stripes (column symbol)
        dc.DrawLine(pen, new Point(cx - 2.5, btn.Y + 3), new Point(cx - 2.5, my));
        dc.DrawLine(pen, new Point(cx + 2.5, btn.Y + 3), new Point(cx + 2.5, my));

        // Bold "+" on the lower half
        var plusPen = new Pen(Brushes.White, 1.8); plusPen.Freeze();
        double py = btn.Y + btn.Height * 0.72;
        dc.DrawLine(plusPen, new Point(cx - 3.5, py), new Point(cx + 3.5, py));
        dc.DrawLine(plusPen, new Point(cx, py - 3.5), new Point(cx, py + 3.5));
    }
}
