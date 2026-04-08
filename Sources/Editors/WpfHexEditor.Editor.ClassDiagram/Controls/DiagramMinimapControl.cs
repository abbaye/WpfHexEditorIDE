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
using System.Windows.Media.Animation;
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

    // Viewport drag (left-button click outside grip strip)
    private bool   _dragging;
    private Point  _dragStart;
    private Vector _dragViewportOrigin;

    // Reposition drag (left-button on grip strip — top 10px)
    private bool   _repositionDragging;
    private Point  _repoParentStart;   // parent-canvas-space start for reposition drag

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Raised when the user drags the viewport rect inside the minimap.</summary>
    public event EventHandler<Point>?          ViewportNavigateRequested;
    /// <summary>Raised every frame during a reposition drag with the pixel delta to apply.</summary>
    public event EventHandler<Vector>?         PositionDeltaRequested;
    /// <summary>Raised when the user finishes a reposition drag; provides the new corner.</summary>
    public event EventHandler<MinimapCorner>?  CornerChangeRequested;
    /// <summary>Raised when the user chooses "Hide Minimap" from the context menu.</summary>
    public event EventHandler?                 HideRequested;

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

    private const double GripHeight = 10.0;

    protected override void OnRender(DrawingContext dc)
    {
        var mapRect = new Rect(0, 0, MapWidth, MapHeight);

        // Background + border
        dc.DrawRectangle(_bgBrush, _mapBorderPen, mapRect);

        // Grip strip at top (drag to reposition)
        var gripBrush = new SolidColorBrush(Color.FromArgb(120, 150, 150, 200));
        dc.DrawRectangle(gripBrush, null, new Rect(0, 0, MapWidth, GripHeight));
        // Grip dots
        var dotBrush = new SolidColorBrush(Color.FromArgb(160, 220, 220, 240));
        for (double x = 6; x < MapWidth - 4; x += 5)
            dc.DrawEllipse(dotBrush, null, new Point(x, GripHeight / 2), 1.0, 1.0);

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

    // ── Mouse interaction ─────────────────────────────────────────────────────

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pt = e.GetPosition(this);

        if (pt.Y <= GripHeight)
        {
            // Reposition drag — track parent-canvas-space position (same coordinate system as
            // Canvas.SetLeft/Top) so the delta can be applied directly without zoom correction.
            _repositionDragging = true;
            _repoParentStart    = e.GetPosition((IInputElement)Parent);
            Cursor              = Cursors.SizeAll;
        }
        else
        {
            // Viewport drag
            _dragging           = true;
            _dragStart          = pt;
            _dragViewportOrigin = _viewport.IsEmpty
                ? new Vector(0, 0)
                : new Vector(_viewport.X, _viewport.Y);
        }
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_repositionDragging)
        {
            // Compute delta in parent-canvas coordinate space.
            // GetPosition(Parent) travels through the ZoomPanCanvas RenderTransform chain
            // so the returned value is already in DiagramCanvas local units — the same
            // space used by Canvas.SetLeft/Top, so no zoom correction is needed.
            Point  cur   = e.GetPosition((IInputElement)Parent);
            Vector delta = cur - _repoParentStart;
            _repoParentStart = cur;
            PositionDeltaRequested?.Invoke(this, delta);
            e.Handled = true;
            return;
        }

        if (_dragging)
        {
            Point cur    = e.GetPosition(this);
            Vector delta = (Vector)(cur - _dragStart);
            double diagDX = delta.X / _scale;
            double diagDY = delta.Y / _scale;
            ViewportNavigateRequested?.Invoke(this,
                new Point(_dragViewportOrigin.X + diagDX, _dragViewportOrigin.Y + diagDY));
        }
        e.Handled = _dragging;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_repositionDragging)
        {
            // Determine new corner based on final mouse position relative to parent canvas
            var parent = Parent as Canvas;
            if (parent is not null)
            {
                Point posInParent = e.GetPosition(parent);
                double cx = parent.ActualWidth  / 2;
                double cy = parent.ActualHeight / 2;
                var corner = (posInParent.X < cx, posInParent.Y < cy) switch
                {
                    (true,  true)  => MinimapCorner.TopLeft,
                    (false, true)  => MinimapCorner.TopRight,
                    (true,  false) => MinimapCorner.BottomLeft,
                    (false, false) => MinimapCorner.BottomRight
                };
                CornerChangeRequested?.Invoke(this, corner);
            }
            Cursor = Cursors.Hand;
        }
        _dragging           = false;
        _repositionDragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        var menu = DiagramMenuHelpers.StyledMenu();

        menu.Items.Add(DiagramMenuHelpers.MakeItem("\uE7BA", "Hide Minimap",
            () => HideRequested?.Invoke(this, EventArgs.Empty)));

        menu.Items.Add(new Separator());

        var moveSub = DiagramMenuHelpers.MakeItem("\uE893", "Move to Corner", null);
        moveSub.Items.Add(DiagramMenuHelpers.MakeItem("\uE893", "Top-Left",
            () => CornerChangeRequested?.Invoke(this, MinimapCorner.TopLeft)));
        moveSub.Items.Add(DiagramMenuHelpers.MakeItem("\uE893", "Top-Right",
            () => CornerChangeRequested?.Invoke(this, MinimapCorner.TopRight)));
        moveSub.Items.Add(DiagramMenuHelpers.MakeItem("\uE893", "Bottom-Left",
            () => CornerChangeRequested?.Invoke(this, MinimapCorner.BottomLeft)));
        moveSub.Items.Add(DiagramMenuHelpers.MakeItem("\uE893", "Bottom-Right",
            () => CornerChangeRequested?.Invoke(this, MinimapCorner.BottomRight)));
        menu.Items.Add(moveSub);

        menu.IsOpen = true;
        e.Handled   = true;
    }
}
