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

using System.Globalization;
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
    private const double SkewHandleSize  = 8.0;
    private const double SkewHandleGap   = 16.0; // px beyond edge

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly VisualCollection         _visuals;
    private readonly Thumb[]                  _handles = new Thumb[8];
    private readonly Thumb                    _rotationThumb;
    private readonly Thumb[]                  _skewHandles = new Thumb[4]; // N, E, S, W
    private readonly DesignInteractionService _interaction;
    private readonly int                      _elementUid;

    private Point  _dragStart;
    private bool   _isMoving;
    private bool   _isResizing;

    // Rotation drag state
    private Point  _rotCenter;
    private double _rotStartAngle;
    private double _rotCurrentAngle;
    private bool   _isRotating;

    // Skew drag state
    private double _skewStartX;
    private double _skewStartY;
    private double _skewAngleX;
    private double _skewAngleY;

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

        BuildSkewHandles();
        WireMouseEvents();
    }

    // ── Adorner overrides ─────────────────────────────────────────────────────

    protected override int    VisualChildrenCount        => _visuals.Count;
    protected override Visual GetVisualChild(int index)  => _visuals[index];

    /// <summary>
    /// Extends the hit-testable area beyond AdornedElement.RenderSize to cover:
    /// • corner/edge grip handles that are arranged up to HandleSize/2 px outside the element,
    /// • the rotation thumb that sits RotHandleOffset+RotHandleSize px above the element.
    /// Without this override the default Adorner hit-test clips to (0,0,w,h) and the
    /// handles outside that rect are invisible to WPF's pointer event routing.
    /// </summary>
    protected override HitTestResult HitTestCore(PointHitTestParameters p)
    {
        const double extra    = HandleSize / 2.0;
        double       rotExtra = RotHandleOffset + RotHandleSize;
        double       skExtra  = SkewHandleGap + SkewHandleSize;
        var expanded = new Rect(
            -(extra + skExtra),
            -rotExtra,
            AdornedElement.RenderSize.Width  + (extra + skExtra) * 2,
            AdornedElement.RenderSize.Height + (extra + skExtra) * 2 + rotExtra);
        return expanded.Contains(p.HitPoint)
            ? new PointHitTestResult(this, p.HitPoint)
            : base.HitTestCore(p);
    }

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

        // Skew handles: N (top-center outside), E (right-center outside),
        //               S (bottom-center outside), W (left-center outside).
        double sh = SkewHandleSize;
        // N skew — above top edge, offset from rotation thumb
        _skewHandles[0].Arrange(new Rect(w / 2.0 + RotHandleSize + 4, -SkewHandleGap - sh / 2.0, sh, sh));
        // E skew — right of right edge
        _skewHandles[1].Arrange(new Rect(w + SkewHandleGap - sh / 2.0, h / 2.0 - sh / 2.0, sh, sh));
        // S skew — below bottom edge
        _skewHandles[2].Arrange(new Rect(w / 2.0 - sh / 2.0, h + SkewHandleGap - sh / 2.0, sh, sh));
        // W skew — left of left edge
        _skewHandles[3].Arrange(new Rect(-SkewHandleGap - sh / 2.0, h / 2.0 - sh / 2.0, sh, sh));

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

        // ── Angle badge (shown during rotation drag) ──────────────────────────
        if (_isRotating)
        {
            var angleBg = Application.Current?.TryFindResource("XD_AngleBadgeBackground") as Brush
                          ?? new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));
            var angleText = new FormattedText(
                $"{Math.Round(_rotCurrentAngle, 1)}°",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9.0,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            const double apadH = 5, apadV = 2;
            double abw = angleText.Width  + apadH * 2;
            double abh = angleText.Height + apadV * 2;
            // Position badge to the right of the rotation thumb center
            double abx = bounds.Width / 2.0 + RotHandleSize / 2.0 + 4;
            double aby = -RotHandleOffset - RotHandleSize / 2.0 - abh / 2.0;

            dc.DrawRoundedRectangle(angleBg, null, new Rect(abx, aby, abw, abh), 3, 3);
            dc.DrawText(angleText, new Point(abx + apadH, aby + apadV));
        }

        // ── Dimension badge (shown during drag-move and resize) ────────────────
        if ((_isMoving || _isResizing) && AdornedElement is FrameworkElement aFe)
        {
            double w = aFe.ActualWidth;
            double h = aFe.ActualHeight;
            var dimText = new FormattedText(
                $"W: {Math.Round(w)}  H: {Math.Round(h)}",
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                9.0,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            const double padH = 5, padV = 2;
            double bw = dimText.Width  + padH * 2;
            double bh = dimText.Height + padV * 2;
            double bx = bounds.Width / 2.0 - bw / 2.0;
            double by = bounds.Height + 4;

            var dimBg = Application.Current?.TryFindResource("XD_DimensionBadgeBackground") as Brush
                        ?? new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));

            dc.DrawRoundedRectangle(dimBg, null, new Rect(bx, by, bw, bh), 3, 3);
            dc.DrawText(dimText, new Point(bx + padH, by + padV));
        }
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
                _isResizing = true;
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
                _isResizing = false;
                InvalidateVisual();
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

    /// <summary>Builds the 4 skew drag thumbs (N/E/S/W) and wires their drag events.</summary>
    private void BuildSkewHandles()
    {
        // 0=N (X skew), 1=E (Y skew), 2=S (X skew), 3=W (Y skew)
        for (int i = 0; i < 4; i++)
        {
            int capturedIndex = i;
            bool isHorizontalSkew = (i == 0 || i == 2);

            var thumb = CreateSkewThumb();
            thumb.DragStarted += (_, e) =>
            {
                _skewStartX = e.HorizontalOffset;
                _skewStartY = e.VerticalOffset;
                if (AdornedElement is FrameworkElement fe)
                {
                    _skewAngleX = GetCurrentSkewX(fe);
                    _skewAngleY = GetCurrentSkewY(fe);
                }
            };
            thumb.DragDelta += (_, e) =>
            {
                if (AdornedElement is not FrameworkElement fe) return;
                if (isHorizontalSkew)
                    _interaction.OnSkewDelta(fe, _skewAngleX + e.HorizontalChange * 0.5, _skewAngleY);
                else
                    _interaction.OnSkewDelta(fe, _skewAngleX, _skewAngleY + e.VerticalChange * 0.5);
                InvalidateArrange();
            };
            thumb.DragCompleted += (_, _) => InvalidateVisual();

            _skewHandles[i] = thumb;
            _visuals.Add(thumb);
        }
    }

    private static Thumb CreateSkewThumb()
    {
        var fillBrush   = Application.Current?.TryFindResource("XD_SkewHandleBrush")          as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0x9B, 0x59, 0xB6));
        var strokeBrush = Application.Current?.TryFindResource("XD_ResizeHandleStrokeBrush")  as Brush
                          ?? new SolidColorBrush(Color.FromRgb(0, 122, 204));

        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty,      fillBrush);
        factory.SetValue(Border.BorderBrushProperty,     strokeBrush);
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        // Diamond shape via LayoutTransform rotate 45°
        factory.SetValue(Border.LayoutTransformProperty, new RotateTransform(45));

        return new Thumb
        {
            Width    = SkewHandleSize,
            Height   = SkewHandleSize,
            Cursor   = Cursors.SizeAll,
            Template = new ControlTemplate(typeof(Thumb)) { VisualTree = factory }
        };
    }

    private static double GetCurrentSkewX(FrameworkElement fe)
    {
        if (fe.RenderTransform is SkewTransform sk) return sk.AngleX;
        if (fe.RenderTransform is TransformGroup tg)
            return tg.Children.OfType<SkewTransform>().FirstOrDefault()?.AngleX ?? 0.0;
        return 0.0;
    }

    private static double GetCurrentSkewY(FrameworkElement fe)
    {
        if (fe.RenderTransform is SkewTransform sk) return sk.AngleY;
        if (fe.RenderTransform is TransformGroup tg)
            return tg.Children.OfType<SkewTransform>().FirstOrDefault()?.AngleY ?? 0.0;
        return 0.0;
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

        _isRotating = true;
        _interaction.OnRotateStarted(fe, _elementUid);
    }

    private void RotThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (AdornedElement is not FrameworkElement fe) return;

        var mp    = Mouse.GetPosition(null);
        double a  = Math.Atan2(mp.Y - _rotCenter.Y, mp.X - _rotCenter.X) * 180.0 / Math.PI;
        _rotCurrentAngle = _rotCurrentAngle + (a - _rotStartAngle);
        _rotStartAngle   = a;
        _interaction.OnRotateDelta(fe, _rotCurrentAngle);
        InvalidateArrange();
        InvalidateVisual();
    }

    private void RotThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isRotating = false;
        if (AdornedElement is FrameworkElement fe)
            _interaction.OnRotateCompleted(fe);
        InvalidateVisual();
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

        // Nudge the adorner to follow and update the dimension badge.
        InvalidateArrange();
        InvalidateVisual();
    }

    private void OnMoveEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_isMoving) return;
        _isMoving = false;
        ReleaseMouseCapture();

        if (AdornedElement is FrameworkElement fe)
            _interaction.OnMoveCompleted(fe);

        InvalidateVisual(); // clear dimension badge
    }
}
