// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/DiagramMinimapControl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Minimap overlay — a 1:N thumbnail of the entire DiagramDocument
//     drawn in the bottom-left corner of DiagramCanvas.
//     The viewport rectangle shows what portion of the diagram is
//     currently visible and can be dragged to pan the canvas.
//
// Architecture Notes:
//     FrameworkElement with custom OnRender — zero WPF children,
//     same pattern as DiagramVisualLayer.
//     Positioned as a Canvas child with fixed Width/Height.
//     Connects to DiagramCanvas via ScrollViewer scroll events.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.ClassDiagram.Core.Model;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// 1:N thumbnail minimap rendered over the bottom-left corner of the canvas.
/// Shows all class boxes as filled rectangles and the visible viewport as a
/// semi-transparent rectangle.
/// </summary>
public sealed class DiagramMinimapControl : FrameworkElement
{
    // ── Constants ─────────────────────────────────────────────────────────────
    public const double MapWidth  = 200.0;
    public const double MapHeight = 140.0;
    private const double Padding  = 4.0;

    // ── State ─────────────────────────────────────────────────────────────────
    private DiagramDocument? _doc;
    private Rect             _viewport    = Rect.Empty;  // visible area in diagram coords
    private double           _scale       = 1.0;
    private Vector           _offset;                     // diagram-space top-left corner
    private bool             _dragging;
    private Point            _dragStart;
    private Vector           _dragViewportOrigin;

    // ── Event raised when the user drags the viewport rect ────────────────────
    public event EventHandler<Point>? ViewportNavigateRequested;

    // ── Brushes (resolved from DynamicResource or fallback) ──────────────────
    private static readonly Brush _bgBrush       = new SolidColorBrush(Color.FromArgb(200, 30, 30, 40));
    private static readonly Brush _nodeBrush     = new SolidColorBrush(Color.FromRgb(80, 100, 160));
    private static readonly Brush _viewportBrush = new SolidColorBrush(Color.FromArgb(60, 100, 160, 255));
    private static readonly Pen   _borderPen     = new(new SolidColorBrush(Color.FromArgb(180, 100, 160, 255)), 1.0);
    private static readonly Pen   _mapBorderPen  = new(new SolidColorBrush(Color.FromArgb(100, 180, 180, 200)), 1.0);

    static DiagramMinimapControl()
    {
        ((SolidColorBrush)_bgBrush).Freeze();
        ((SolidColorBrush)_nodeBrush).Freeze();
        ((SolidColorBrush)_viewportBrush).Freeze();
        _borderPen.Freeze();
        _mapBorderPen.Freeze();
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public DiagramMinimapControl()
    {
        Width           = MapWidth;
        Height          = MapHeight;
        IsHitTestVisible = true;
        Cursor          = Cursors.Hand;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates the diagram document and repaints.</summary>
    public void SetDocument(DiagramDocument? doc)
    {
        _doc = doc;
        ComputeScale();
        InvalidateVisual();
    }

    /// <summary>
    /// Updates the visible viewport rectangle (in diagram coordinates) and repaints.
    /// </summary>
    public void SetViewport(Rect viewport)
    {
        _viewport = viewport;
        InvalidateVisual();
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var mapRect = new Rect(0, 0, MapWidth, MapHeight);

        // Background + border
        dc.DrawRectangle(_bgBrush, _mapBorderPen, mapRect);

        if (_doc is null || _doc.Classes.Count == 0)
        {
            DrawNoDocLabel(dc);
            return;
        }

        // Draw class nodes
        foreach (var node in _doc.Classes)
        {
            var nr = ToMapRect(new Rect(node.X, node.Y, node.Width, node.Height));
            if (nr.Width < 1) nr = new Rect(nr.X, nr.Y, 1, 1);
            dc.DrawRectangle(_nodeBrush, null, nr);
        }

        // Draw viewport rectangle
        if (!_viewport.IsEmpty)
        {
            var vr = ToMapRect(_viewport);
            dc.DrawRectangle(_viewportBrush, _borderPen, Clip(vr, mapRect));
        }
    }

    private void DrawNoDocLabel(DrawingContext dc)
    {
        var ft = new FormattedText("No diagram", CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9.0,
            Brushes.Gray, 96.0);
        dc.DrawText(ft, new Point((MapWidth - ft.Width) / 2, (MapHeight - ft.Height) / 2));
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    private void ComputeScale()
    {
        if (_doc is null || _doc.Classes.Count == 0) { _scale = 1; _offset = default; return; }

        double minX = _doc.Classes.Min(n => n.X);
        double minY = _doc.Classes.Min(n => n.Y);
        double maxX = _doc.Classes.Max(n => n.X + n.Width);
        double maxY = _doc.Classes.Max(n => n.Y + n.Height);

        double diagW = Math.Max(1, maxX - minX);
        double diagH = Math.Max(1, maxY - minY);

        _scale  = Math.Min((MapWidth  - Padding * 2) / diagW,
                           (MapHeight - Padding * 2) / diagH);
        _offset = new Vector(minX, minY);
    }

    private Rect ToMapRect(Rect diagRect) =>
        new((diagRect.X - _offset.X) * _scale + Padding,
            (diagRect.Y - _offset.Y) * _scale + Padding,
            diagRect.Width  * _scale,
            diagRect.Height * _scale);

    private Point ToMapPoint(Point diagPt) =>
        new((diagPt.X - _offset.X) * _scale + Padding,
            (diagPt.Y - _offset.Y) * _scale + Padding);

    private Point ToDiagramPoint(Point mapPt) =>
        new((mapPt.X - Padding) / _scale + _offset.X,
            (mapPt.Y - Padding) / _scale + _offset.Y);

    private static Rect Clip(Rect r, Rect bounds)
    {
        double x = Math.Max(bounds.Left, Math.Min(r.X, bounds.Right));
        double y = Math.Max(bounds.Top,  Math.Min(r.Y, bounds.Bottom));
        double w = Math.Max(0, Math.Min(r.Right,  bounds.Right)  - x);
        double h = Math.Max(0, Math.Min(r.Bottom, bounds.Bottom) - y);
        return new Rect(x, y, w, h);
    }

    // ── Mouse interaction — drag viewport ─────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragging           = true;
        _dragStart          = e.GetPosition(this);
        _dragViewportOrigin = _viewport.IsEmpty
            ? new Vector(0, 0)
            : new Vector(_viewport.X, _viewport.Y);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging) return;
        Point cur   = e.GetPosition(this);
        Vector delta = (Vector)(cur - _dragStart);
        double diagDX = delta.X / _scale;
        double diagDY = delta.Y / _scale;

        ViewportNavigateRequested?.Invoke(this,
            new Point(_dragViewportOrigin.X + diagDX, _dragViewportOrigin.Y + diagDY));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _dragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }
}
