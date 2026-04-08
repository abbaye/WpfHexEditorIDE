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

        ClipToBounds = true;
        Background   = Brushes.Transparent;   // hit-testable in empty areas
    }

    // ---------------------------------------------------------------------------
    // Right-click delegation — forward to DiagramCanvas when empty area is hit
    // (DiagramCanvas is sized to its content so empty-area right-clicks land here)
    // ---------------------------------------------------------------------------

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
        if (Children.Count == 0) return;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (UIElement child in Children)
        {
            double left   = GetLeft(child).IfNaN(0);
            double top    = GetTop(child).IfNaN(0);
            double width  = child.RenderSize.Width;
            double height = child.RenderSize.Height;

            minX = Math.Min(minX, left);
            minY = Math.Min(minY, top);
            maxX = Math.Max(maxX, left + width);
            maxY = Math.Max(maxY, top + height);
        }

        double contentWidth  = maxX - minX;
        double contentHeight = maxY - minY;
        if (contentWidth < 1 || contentHeight < 1) return;

        double availableWidth  = ActualWidth  > 0 ? ActualWidth  : 800;
        double availableHeight = ActualHeight > 0 ? ActualHeight : 600;

        const double padding = 40.0;
        double scaleX = (availableWidth  - padding * 2) / contentWidth;
        double scaleY = (availableHeight - padding * 2) / contentHeight;
        double zoom   = Math.Max(0.1, Math.Min(1.0, Math.Min(scaleX, scaleY)));

        ZoomFactor = zoom;
        OffsetX    = -minX * zoom + padding;
        OffsetY    = -minY * zoom + padding;
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

        double delta = e.Delta > 0 ? 0.1 : -0.1;
        ZoomFactor   = Math.Max(0.1, Math.Min(10.0, ZoomFactor + delta));
        e.Handled    = true;
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
        if (!_isPanning) return;

        Point current = e.GetPosition(Parent as IInputElement);
        OffsetX = _panStartOffsetX + (current.X - _panStartMouse.X);
        OffsetY = _panStartOffsetY + (current.Y - _panStartMouse.Y);
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_isPanning) return;
        _isPanning = false;
        ReleaseMouseCapture();
        e.Handled = true;
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
