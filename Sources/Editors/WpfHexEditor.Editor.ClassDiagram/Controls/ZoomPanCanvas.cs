// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Controls/ZoomPanCanvas.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Canvas subclass that provides zoom (ScaleTransform) and pan
//     (TranslateTransform) via mouse wheel, middle-mouse drag,
//     and dependency properties. FitToContent computes the optimal
//     zoom and offset to show all children.
//
// Architecture Notes:
//     Pattern: Composite Transform — ScaleTransform + TranslateTransform
//     wrapped in TransformGroup applied to RenderTransform.
//     ZoomFactor and Offset DPs trigger transform recomputation.
//     Middle-mouse pan captures the mouse and tracks delta per frame.
//     FitToContent iterates child UIElements for bounding box.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfHexEditor.Editor.ClassDiagram.Controls;

/// <summary>
/// Zoomable and pannable Canvas for the diagram editor.
/// </summary>
public class ZoomPanCanvas : Canvas
{
    private readonly ScaleTransform     _scale     = new(1.0, 1.0);
    private readonly TranslateTransform _translate = new(0, 0);

    private bool _isPanning;
    private Point _panStartMouse;
    private double _panStartOffsetX;
    private double _panStartOffsetY;

    // Rubber-band lasso — owned here because DiagramCanvas is too small to receive
    // empty-area hit-tests (Canvas without explicit size = 0x0 effective hit area).
    private DiagramCanvas? _rubberBandTarget;

    // ---------------------------------------------------------------------------
    // Dependency Properties
    // ---------------------------------------------------------------------------

    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register(nameof(ZoomFactor), typeof(double), typeof(ZoomPanCanvas),
            new PropertyMetadata(1.0, OnZoomFactorChanged));

    public static readonly DependencyProperty OffsetXProperty =
        DependencyProperty.Register(nameof(OffsetX), typeof(double), typeof(ZoomPanCanvas),
            new PropertyMetadata(0.0, OnOffsetChanged));

    public static readonly DependencyProperty OffsetYProperty =
        DependencyProperty.Register(nameof(OffsetY), typeof(double), typeof(ZoomPanCanvas),
            new PropertyMetadata(0.0, OnOffsetChanged));

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    public ZoomPanCanvas()
    {
        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_translate);
        RenderTransform = group;
        RenderTransformOrigin = new Point(0, 0);

        // ClipToBounds must be false here. ZoomPanCanvas applies zoom+pan via
        // RenderTransform on *itself*, so its LOCAL coordinate space never shifts.
        // ClipToBounds would permanently clip nodes at X > ActualWidth regardless of pan.
        // The parent _diagramHost (Border, ClipToBounds=true) provides the correct clip.
        ClipToBounds = false;
        Background   = Brushes.Transparent;   // hit-testable in empty areas
    }

    // ---------------------------------------------------------------------------
    // HitTestCore override — ZoomPanCanvas applies RenderTransform to itself, which
    // means a panned view maps viewport clicks to negative local coordinates that fall
    // outside the layout bounding box and are not hit by the default Canvas check.
    // We override to always return a hit so the viewport background is fully clickable
    // regardless of zoom or pan offset.
    // ---------------------------------------------------------------------------

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => new PointHitTestResult(this, hitTestParameters.HitPoint);

    protected override GeometryHitTestResult HitTestCore(GeometryHitTestParameters hitTestParameters)
        => new GeometryHitTestResult(this, IntersectionDetail.FullyContains);

    // Right-click delegation — forward to DiagramCanvas when empty area is hit
    // (DiagramCanvas is sized to its content so empty-area right-clicks land here)
    // ---------------------------------------------------------------------------

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.Handled) return;
        if (_isPanning) return;   // Middle-mouse pan takes priority

        // Click landed on ZoomPanCanvas (empty viewport area outside diagram content).
        // Start rubber-band lasso; use e.GetPosition(dc) which WPF transforms through
        // the ancestor RenderTransform chain → correct diagram-space coordinates.
        foreach (UIElement child in InternalChildren)
        {
            if (child is DiagramCanvas dc)
            {
                _rubberBandTarget = dc;
                dc.StartRubberBandAt(e.GetPosition(dc));
                Mouse.OverrideCursor = Cursors.Cross;
                CaptureMouse();
                e.Handled = true;
                return;
            }
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (e.Handled) return;

        // Find the first DiagramCanvas child and let it handle the right-click.
        // e.GetPosition(dc) correctly transforms screen→diagram-local coordinates
        // via the ancestor RenderTransform chain, so HitTestNode/Arrow work correctly.
        foreach (UIElement child in InternalChildren)
        {
            if (child is DiagramCanvas dc)
            {
                dc.HandleEmptyAreaRightClick(e.GetPosition(dc));
                e.Handled = true;
                return;
            }
        }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Raised whenever ZoomFactor, OffsetX, or OffsetY changes so that external
    /// consumers (e.g. ClassDiagramSplitHost scrollbars) can update synchronously.
    /// </summary>
    public event EventHandler? TransformChanged;

    // ---------------------------------------------------------------------------
    // CLR wrappers
    // ---------------------------------------------------------------------------

    public double ZoomFactor
    {
        get => (double)GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, Math.Max(0.1, Math.Min(10.0, value)));
    }

    public double OffsetX
    {
        get => (double)GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    public double OffsetY
    {
        get => (double)GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    // ---------------------------------------------------------------------------
    // DP callbacks
    // ---------------------------------------------------------------------------

    private static void OnZoomFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ZoomPanCanvas c) return;
        c._scale.ScaleX = c._scale.ScaleY = (double)e.NewValue;
        c.TransformChanged?.Invoke(c, EventArgs.Empty);
    }

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ZoomPanCanvas c) return;
        c._translate.X = c.OffsetX;
        c._translate.Y = c.OffsetY;
        c.TransformChanged?.Invoke(c, EventArgs.Empty);
    }

    // ---------------------------------------------------------------------------
    // Content bounds (for scrollbar wiring in ClassDiagramSplitHost)
    // ---------------------------------------------------------------------------

    /// <summary>Returns the diagram extent in logical (unscaled) coordinates.</summary>
    public Rect GetContentBounds()
    {
        if (Children.Count > 0 && Children[0] is DiagramCanvas dc)
            return dc.GetDiagramBounds();
        return new Rect(0, 0, 800, 600);
    }

    // ---------------------------------------------------------------------------
    // FitToContent
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Computes the bounding box of all children and adjusts ZoomFactor and Offset
    /// to fit them within the visible area of the parent scroll container.
    /// </summary>
    public void FitToContent()
    {
        // Use GetContentBounds() which reads actual node positions from DiagramDocument
        // rather than DiagramCanvas.RenderSize (which is the Canvas size, not content extent).
        var b = GetContentBounds();
        if (b.Width < 1 || b.Height < 1) return;

        double availableWidth  = ActualWidth  > 0 ? ActualWidth  : 800;
        double availableHeight = ActualHeight > 0 ? ActualHeight : 600;

        const double padding = 40.0;
        double scaleX = (availableWidth  - padding * 2) / b.Width;
        double scaleY = (availableHeight - padding * 2) / b.Height;
        double zoom   = Math.Max(0.1, Math.Min(4.0, Math.Min(scaleX, scaleY)));

        ZoomFactor = zoom;
        OffsetX    = -b.X * zoom + padding;
        OffsetY    = -b.Y * zoom + padding;
    }

    /// <summary>
    /// Zooms and pans so that the given rectangle fills the viewport with the specified padding.
    /// </summary>
    public void ZoomToRect(Rect r, double padding = 40.0)
    {
        if (r.Width < 1 || r.Height < 1) return;
        double availableWidth  = ActualWidth  > 0 ? ActualWidth  : 800;
        double availableHeight = ActualHeight > 0 ? ActualHeight : 600;

        double scaleX = (availableWidth  - padding * 2) / r.Width;
        double scaleY = (availableHeight - padding * 2) / r.Height;
        double zoom   = Math.Min(scaleX, scaleY);
        zoom = Math.Max(0.1, Math.Min(4.0, zoom));

        ZoomFactor = zoom;
        OffsetX    = -(r.X * zoom) + padding;
        OffsetY    = -(r.Y * zoom) + padding;
    }

    // ---------------------------------------------------------------------------
    // Mouse handling — wheel zoom + middle-mouse pan
    // ---------------------------------------------------------------------------

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            // Ctrl+Wheel → zoom (centred on mouse cursor); step proportional to delta
            double step  = e.Delta / 120.0 * 0.1;
            double oldZ  = ZoomFactor;
            double newZ  = Math.Clamp(oldZ + step, 0.1, 10.0);
            if (Math.Abs(newZ - oldZ) < 1e-10) { e.Handled = true; return; }

            // Adjust offset so the point under the cursor stays fixed
            Point mouse  = e.GetPosition(this);
            OffsetX      = mouse.X - (mouse.X - OffsetX) * (newZ / oldZ);
            OffsetY      = mouse.Y - (mouse.Y - OffsetY) * (newZ / oldZ);
            ZoomFactor   = newZ;
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            // Shift+Wheel → pan horizontally
            OffsetX += e.Delta > 0 ? 40 : -40;
        }
        else
        {
            // Plain Wheel → pan vertically (standard editor behaviour)
            OffsetY += e.Delta > 0 ? 40 : -40;
        }

        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.MiddleButton != MouseButtonState.Pressed) return;

        _isPanning      = true;
        _panStartMouse  = e.GetPosition(Parent as IInputElement);
        _panStartOffsetX = OffsetX;
        _panStartOffsetY = OffsetY;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isPanning)
        {
            Point current = e.GetPosition(Parent as IInputElement);
            OffsetX = _panStartOffsetX + (current.X - _panStartMouse.X);
            OffsetY = _panStartOffsetY + (current.Y - _panStartMouse.Y);
            e.Handled = true;
            return;
        }

        if (_rubberBandTarget is not null)
        {
            _rubberBandTarget.UpdateRubberBandAt(e.GetPosition(_rubberBandTarget));
            e.Handled = true;
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isPanning && e.ChangedButton == MouseButton.Middle)
        {
            _isPanning = false;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        if (_rubberBandTarget is not null && e.ChangedButton == MouseButton.Left)
        {
            _rubberBandTarget.FinishRubberBandAt(e.GetPosition(_rubberBandTarget));
            _rubberBandTarget = null;
            Mouse.OverrideCursor = null;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_rubberBandTarget is not null)
        {
            _rubberBandTarget.CancelRubberBand();
            _rubberBandTarget = null;
            Mouse.OverrideCursor = null;
        }
        _isPanning = false;
    }
}

// ---------------------------------------------------------------------------
// Extension helper
// ---------------------------------------------------------------------------

internal static class DoubleExtensions
{
    public static double IfNaN(this double value, double fallback) =>
        double.IsNaN(value) ? fallback : value;
}
