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

        ClipToBounds = false;
    }

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
        if (d is ZoomPanCanvas c) c._scale.ScaleX = c._scale.ScaleY = (double)e.NewValue;
    }

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ZoomPanCanvas c)
        {
            c._translate.X = c.OffsetX;
            c._translate.Y = c.OffsetY;
        }
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
