// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResizeAdorner.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Interactive adorner combining selection highlight with 8 resize handles
//     and drag-to-move capability. Replaces SelectionAdorner when interaction
//     is enabled on the design canvas.
//
// Architecture Notes:
//     Adorner with VisualCollection of 8 Thumb controls for resize.
//     Cursor=SizeAll on the adorner body for drag-move.
//     Delegates all interaction logic to DesignInteractionService.
//     Theme-aware via XD_SelectionBorderBrush and XD_ResizeHandleFillBrush tokens.
// ==========================================================

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Adorner that draws a dashed selection border and 8 interactive resize handles.
/// Supports drag-to-move and drag-to-resize via <see cref="DesignInteractionService"/>.
/// </summary>
public sealed class ResizeAdorner : Adorner
{
    // ── Handle layout (NW, N, NE, E, SE, S, SW, W) ───────────────────────────

    private static readonly (double NormX, double NormY, Cursor Cursor)[] HandleConfigs =
    [
        (0.0, 0.0, Cursors.SizeNWSE), // 0 NW
        (0.5, 0.0, Cursors.SizeNS),   // 1 N
        (1.0, 0.0, Cursors.SizeNESW), // 2 NE
        (1.0, 0.5, Cursors.SizeWE),   // 3 E
        (1.0, 1.0, Cursors.SizeNWSE), // 4 SE
        (0.5, 1.0, Cursors.SizeNS),   // 5 S
        (0.0, 1.0, Cursors.SizeNESW), // 6 SW
        (0.0, 0.5, Cursors.SizeWE),   // 7 W
    ];

    private const double HandleSize      = 8.0;
    private const double BorderInset     = 0.5;
    private const double RotHandleOffset = 28.0; // px above top-center handle
    private const double RotHandleSize   = 14.0;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly VisualCollection         _visuals;
    private readonly Thumb[]                  _handles = new Thumb[8];
    private readonly Thumb                    _rotationThumb;
    private readonly DesignInteractionService _interaction;
    private readonly int                      _elementUid;

    private Point  _dragStart;
    private bool   _isMoving;

    // Rotation drag state
    private Point  _rotCenter;
    private double _rotStartAngle;
    private double _rotCurrentAngle;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ResizeAdorner(
        UIElement                 adornedElement,
        DesignInteractionService  interaction,
        int                       elementUid)
        : base(adornedElement)
    {
        _interaction = interaction;
        _elementUid  = elementUid;
        _visuals     = new VisualCollection(this);

        IsHitTestVisible = true;
        Cursor           = Cursors.SizeAll;

        BuildHandles();

        // 9th handle: rotation circle above the top-center handle.
        _rotationThumb = CreateRotationThumb();
        _visuals.Add(_rotationThumb);
        _rotationThumb.DragStarted   += RotThumb_DragStarted;
        _rotationThumb.DragDelta     += RotThumb_DragDelta;
        _rotationThumb.DragCompleted += RotThumb_DragCompleted;

        WireMouseEvents();
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override int    VisualChildrenCount        => _visuals.Count;
    protected override Visual GetVisualChild(int index)  => _visuals[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        var w = AdornedElement.RenderSize.Width;
        var h = AdornedElement.RenderSize.Height;

        for (int i = 0; i < 8; i++)
        {
            var cfg = HandleConfigs[i];
            var x   = cfg.NormX * w - HandleSize / 2.0;
            var y   = cfg.NormY * h - HandleSize / 2.0;
            _handles[i].Arrange(new Rect(x, y, HandleSize, HandleSize));
        }

        // Rotation thumb: centered above the top-center (N) handle.
        double rx = w / 2.0 - RotHandleSize / 2.0;
        double ry = -RotHandleOffset - RotHandleSize / 2.0;
        _rotationThumb.Arrange(new Rect(rx, ry, RotHandleSize, RotHandleSize));

        return finalSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Transparent hit-test background so move drag works anywhere on the border.
        dc.DrawRectangle(
            Brushes.Transparent,
            null,
            new Rect(AdornedElement.RenderSize));

        // Dashed selection border.
        var borderBrush = Application.Current?.TryFindResource("XD_SelectionBorderBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));

        var pen = new Pen(borderBrush, 1.5)
        {
            DashStyle = new DashStyle(new double[] { 4, 2 }, 0)
        };
        pen.Freeze();

        var bounds = new Rect(AdornedElement.RenderSize);
        dc.DrawRectangle(
            null, pen,
            new Rect(
                bounds.X + BorderInset,
                bounds.Y + BorderInset,
                Math.Max(0, bounds.Width  - BorderInset * 2),
                Math.Max(0, bounds.Height - BorderInset * 2)));

        // Thin connector line from the top-center of the selection border to the rotation thumb.
        var linePen = new Pen(borderBrush, 1.0);
        linePen.Freeze();
        dc.DrawLine(linePen,
            new Point(bounds.Width / 2.0, 0),
            new Point(bounds.Width / 2.0, -RotHandleOffset));
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void BuildHandles()
    {
        for (int i = 0; i < 8; i++)
        {
            int capturedIndex = i;

            var thumb = new Thumb
            {
                Width  = HandleSize,
                Height = HandleSize,
                Cursor = HandleConfigs[i].Cursor
            };

            ApplyHandleStyle(thumb);

            thumb.DragStarted  += (_, _) =>
            {
                if (AdornedElement is FrameworkElement fe)
                    _interaction.OnResizeStarted(fe, _elementUid);
            };

            thumb.DragDelta += (_, e) =>
            {
                if (AdornedElement is FrameworkElement fe)
                    _interaction.OnResizeDelta(fe, capturedIndex, e.HorizontalChange, e.VerticalChange, _elementUid);
                InvalidateArrange();
            };

            thumb.DragCompleted += (_, _) =>
            {
                if (AdornedElement is FrameworkElement fe)
                    _interaction.OnResizeCompleted(fe);
            };

            _handles[i] = thumb;
            _visuals.Add(thumb);
        }
    }

    private static void ApplyHandleStyle(Thumb thumb)
    {
        // Try to use the theme resource; fall back to a hardcoded VS-blue square.
        var fillBrush   = Application.Current?.TryFindResource("XD_ResizeHandleFillBrush")   as Brush
                          ?? Brushes.White;
        var strokeBrush = Application.Current?.TryFindResource("XD_ResizeHandleStrokeBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));

        // Build a simple rectangle template for the thumb.
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty,          fillBrush);
        factory.SetValue(Border.BorderBrushProperty,         strokeBrush);
        factory.SetValue(Border.BorderThicknessProperty,     new Thickness(1));

        thumb.Template = new ControlTemplate(typeof(Thumb))
        {
            VisualTree = factory
        };
    }

    private static Thumb CreateRotationThumb()
    {
        var fillBrush   = Application.Current?.TryFindResource("XD_RotationHandleFillBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0x4F, 0xC1, 0xFF));
        var strokeBrush = Application.Current?.TryFindResource("XD_ResizeHandleStrokeBrush") as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));

        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty,      fillBrush);
        factory.SetValue(Border.BorderBrushProperty,     strokeBrush);
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        factory.SetValue(Border.CornerRadiusProperty,    new CornerRadius(RotHandleSize / 2.0));

        return new Thumb
        {
            Width    = RotHandleSize,
            Height   = RotHandleSize,
            Cursor   = Cursors.Cross,
            Template = new ControlTemplate(typeof(Thumb)) { VisualTree = factory }
        };
    }

    // ── Rotation drag handlers ─────────────────────────────────────────────────

    private void RotThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (AdornedElement is not FrameworkElement fe) return;

        // Compute the element center in screen coordinates.
        var tl = fe.TranslatePoint(new Point(0, 0), null);
        _rotCenter = new Point(tl.X + fe.ActualWidth / 2.0, tl.Y + fe.ActualHeight / 2.0);

        // Capture the angle from the current mouse position to the element center.
        var mp = Mouse.GetPosition(null);
        _rotStartAngle   = Math.Atan2(mp.Y - _rotCenter.Y, mp.X - _rotCenter.X) * 180.0 / Math.PI;
        _rotCurrentAngle = GetCurrentRotation(fe);

        _interaction.OnRotateStarted(fe, _elementUid);
    }

    private void RotThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is not FrameworkElement fe) return;

        var mp    = Mouse.GetPosition(null);
        double a  = Math.Atan2(mp.Y - _rotCenter.Y, mp.X - _rotCenter.X) * 180.0 / Math.PI;
        _interaction.OnRotateDelta(fe, _rotCurrentAngle + (a - _rotStartAngle));
        InvalidateArrange();
    }

    private void RotThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (AdornedElement is FrameworkElement fe)
            _interaction.OnRotateCompleted(fe);
    }

    private static double GetCurrentRotation(FrameworkElement fe)
    {
        if (fe.RenderTransform is RotateTransform rt) return rt.Angle;
        if (fe.RenderTransform is TransformGroup tg)
            return tg.Children.OfType<RotateTransform>().FirstOrDefault()?.Angle ?? 0.0;
        return 0.0;
    }

    // ── Move mouse events ─────────────────────────────────────────────────────

    private void WireMouseEvents()
    {
        MouseLeftButtonDown += OnMoveStart;
        MouseMove           += OnMoveMove;
        MouseLeftButtonUp   += OnMoveEnd;
    }

    private void OnMoveStart(object sender, MouseButtonEventArgs e)
    {
        // If the click landed on a resize handle, let the Thumb handle it.
        if (e.OriginalSource is Thumb) return;

        if (AdornedElement is not FrameworkElement fe) return;

        _dragStart = e.GetPosition(fe.Parent as UIElement ?? fe);
        _isMoving  = true;

        _interaction.OnMoveStart(fe, _dragStart, _elementUid);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMoveMove(object sender, MouseEventArgs e)
    {
        if (!_isMoving || AdornedElement is not FrameworkElement fe) return;

        var current = e.GetPosition(fe.Parent as UIElement ?? fe);
        _interaction.OnMoveDelta(fe, current);

        // Nudge the adorner to follow.
        InvalidateArrange();
    }

    private void OnMoveEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_isMoving) return;
        _isMoving = false;
        ReleaseMouseCapture();

        if (AdornedElement is FrameworkElement fe)
            _interaction.OnMoveCompleted(fe);
    }
}
