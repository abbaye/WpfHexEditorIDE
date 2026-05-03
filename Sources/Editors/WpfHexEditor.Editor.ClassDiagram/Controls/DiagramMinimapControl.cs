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

    // Unified drag state — drag anywhere on minimap repositions it; click navigates viewport
    private bool   _mouseDown;
    private Point  _mouseDownParentPt;   // parent-canvas coords at mouse-down
    private Point  _lastParentPt;        // last parent-canvas coords during drag
    private bool   _repositionConfirmed; // true once drag exceeds DragThreshold
    private const double DragThreshold = 5.0;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Raised when the user drags the viewport rect inside the minimap.</summary>
    public event EventHandler<Point>?          ViewportNavigateRequested;
    /// <summary>Raised every frame during a reposition drag with the pixel delta to apply.</summary>
    public event EventHandler<Vector>?         PositionDeltaRequested;
    /// <summary>Raised when the user finishes a reposition drag; provides the new corner.</summary>
    public event EventHandler<MinimapCorner>?  CornerChangeRequested;
    /// <summary>Raised when the user chooses "Hide Minimap" from the context menu.</summary>
    public event EventHandler?                 HideRequested;

    // ── Brushes resolved at render time from theme tokens (never frozen) ──────
    // Resolved via TryFindResource each OnRender so theme switches take effect immediately.
    private static readonly Brush _bgBrushFallback       = new SolidColorBrush(Color.FromArgb(200, 30, 30, 40));
    private static readonly Brush _nodeBrushFallback     = new SolidColorBrush(Color.FromRgb(80, 100, 160));
    private static readonly Brush _viewportBrushFallback = new SolidColorBrush(Color.FromArgb(60, 100, 160, 255));
    private static readonly Color _borderColorFallback   = Color.FromArgb(180, 100, 160, 255);
    private static readonly Color _mapBorderColorFallback= Color.FromArgb(100, 180, 180, 200);

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
        // Resolve brushes from theme tokens each frame so theme switches take effect.
        var bgBrush       = TryFindResource("CD_CanvasBackground")      as Brush ?? _bgBrushFallback;
        var nodeBrush     = TryFindResource("CD_ClassBoxBackground")     as Brush ?? _nodeBrushFallback;
        var viewportBrush = TryFindResource("CD_MinimapViewportBrush")   as Brush ?? _viewportBrushFallback;
        var borderColor   = TryFindResource("CD_SelectionBorderBrush") is SolidColorBrush sb
            ? Color.FromArgb(180, sb.Color.R, sb.Color.G, sb.Color.B) : _borderColorFallback;
        var mapBorderBrush= TryFindResource("CD_ClassBoxBorderBrush")    as Brush
            ?? new SolidColorBrush(_mapBorderColorFallback);

        var borderPen    = new Pen(new SolidColorBrush(borderColor), 1.0);
        var mapBorderPen = new Pen(mapBorderBrush, 1.0);

        var mapRect = new Rect(0, 0, MapWidth, MapHeight);

        // Background + border
        dc.DrawRectangle(bgBrush, mapBorderPen, mapRect);

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
            dc.DrawRectangle(nodeBrush, null, nr);
        }

        // Draw viewport rectangle
        if (!_viewport.IsEmpty)
        {
            var vr = ToMapRect(_viewport);
            dc.DrawRectangle(viewportBrush, borderPen, Clip(vr, mapRect));
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
        _mouseDown           = true;
        _repositionConfirmed = false;
        _mouseDownParentPt   = e.GetPosition(Parent as IInputElement ?? this);
        _lastParentPt        = _mouseDownParentPt;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_mouseDown) { e.Handled = false; return; }

        Point  cur   = e.GetPosition(Parent as IInputElement ?? this);
        Vector delta = cur - _lastParentPt;

        if (!_repositionConfirmed)
        {
            Vector total = cur - _mouseDownParentPt;
            if (Math.Abs(total.X) > DragThreshold || Math.Abs(total.Y) > DragThreshold)
            {
                _repositionConfirmed = true;
                Cursor = Cursors.SizeAll;
            }
        }

        if (_repositionConfirmed)
        {
            _lastParentPt = cur;
            PositionDeltaRequested?.Invoke(this, delta);
        }

        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        bool wasRepo = _repositionConfirmed;
        _mouseDown           = false;
        _repositionConfirmed = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Hand;

        if (!wasRepo)
        {
            // Treat as click — navigate viewport to clicked diagram point
            Point mapPt  = e.GetPosition(this);
            Point diagPt = ToDiagramPoint(mapPt);
            ViewportNavigateRequested?.Invoke(this, diagPt);
        }
        else
        {
            // Corner snap on release
            if (Parent is Canvas parent)
            {
                Point pos  = e.GetPosition(parent);
                bool  left = pos.X < parent.ActualWidth  / 2;
                bool  top  = pos.Y < parent.ActualHeight / 2;
                var corner = (left, top) switch
                {
                    (true,  true)  => MinimapCorner.TopLeft,
                    (false, true)  => MinimapCorner.TopRight,
                    (true,  false) => MinimapCorner.BottomLeft,
                    _              => MinimapCorner.BottomRight
                };
                CornerChangeRequested?.Invoke(this, corner);
            }
        }

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
