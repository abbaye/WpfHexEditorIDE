// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ZoomPanCanvas.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ContentControl wrapper that applies ScaleTransform + TranslateTransform
//     to enable zoom-in/out (Ctrl+Wheel) and pan (middle-mouse drag / Shift+Wheel)
//     of the inner design canvas.
//
// Architecture Notes:
//     ContentControl — wraps DesignCanvas.
//     Uses RenderTransform (TransformGroup: ScaleTransform + TranslateTransform) on the
//     content element — NOT LayoutTransform. LayoutTransform causes WPF NeedsClipBounds
//     to fire on the content due to floating-point precision in the inverse-transform
//     round-trip (1280 * zoom / zoom = 1279.9999...), silently clipping the design canvas
//     rendering to the viewport size at any zoom level above fit-to-view.
//     RenderTransform is purely visual: no layout participation, no clip guard triggered.
//     ZoomLevel clamped to [0.1, 4.0]. Transforms composed as:
//       TransformGroup { ScaleTransform(zoom, zoom), TranslateTransform(offsetX, offsetY) }
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfHexEditor.Editor.XamlDesigner.Controls;

/// <summary>
/// Wraps a DesignCanvas with zoom (Ctrl+Wheel) and pan (middle-mouse / Shift+Wheel) capability.
/// </summary>
public sealed class ZoomPanCanvas : ContentControl
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const double MinZoom  = 0.10;
    private const double MaxZoom  = 4.00;
    private const double ZoomStep = 0.10;

    // Extra blank space around the canvas that scrollbars expose (VS-Like).
    // Shared with XamlDesignerSplitHost so both sides use the same constant.
    internal const double ScrollExtraMargin = 300.0;

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly ScaleTransform     _scale          = new(1, 1);
    private readonly TranslateTransform _translate      = new(0, 0);
    // Combined Scale + Translate applied as RenderTransform on the content element.
    // Scale is applied first (around origin 0,0), then Translate shifts the result.
    // Both transforms are mutated in-place by OnZoomLevelChanged / OnOffsetChanged.
    private readonly TransformGroup     _contentTransform;

    private Point _panStart;
    private bool  _isPanning;

    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(
            nameof(ZoomLevel),
            typeof(double),
            typeof(ZoomPanCanvas),
            new FrameworkPropertyMetadata(1.0, OnZoomLevelChanged));

    public static readonly DependencyProperty OffsetXProperty =
        DependencyProperty.Register(nameof(OffsetX), typeof(double), typeof(ZoomPanCanvas),
            new FrameworkPropertyMetadata(0.0, OnOffsetChanged));

    public static readonly DependencyProperty OffsetYProperty =
        DependencyProperty.Register(nameof(OffsetY), typeof(double), typeof(ZoomPanCanvas),
            new FrameworkPropertyMetadata(0.0, OnOffsetChanged));

    // ── Constructor ───────────────────────────────────────────────────────────

    public ZoomPanCanvas()
    {
        // Build the combined RenderTransform before any content is set.
        _contentTransform = new TransformGroup();
        _contentTransform.Children.Add(_scale);
        _contentTransform.Children.Add(_translate);

        // HorizontalContentAlignment = Left prevents ContentPresenter from giving the
        // content an arrangement rect equal to the viewport size (the default Stretch).
        // With Stretch, the content's explicit Width (1280) exceeds the arrangement rect
        // → NeedsClipBounds = true → layout clip applied → content visually clipped to
        // the viewport footprint regardless of zoom or pan offset.
        // With Left/Top, ContentPresenter gives content its DesiredSize (= explicit
        // Width/Height = 1280×720) as the arrangement rect → no clip guard triggers.
        HorizontalContentAlignment = HorizontalAlignment.Left;
        VerticalContentAlignment   = VerticalAlignment.Top;

        // ZoomPanCanvas itself has NO transform.
        // Transforms are applied to the content element in OnContentChanged as a
        // single TransformGroup (Scale then Translate) on RenderTransform.
        // HorizontalAlignment=Left + VerticalAlignment=Top on the content element
        // ensures content.ActualWidth reflects its natural (explicit) size.
        ClipToBounds = true;

        // Use PreviewMouseWheel (tunneling) so Ctrl+Wheel zoom works even when
        // inner content (e.g. ScrollViewer in the designed XAML) would otherwise
        // consume the bubbling MouseWheel event first.
        PreviewMouseWheel += OnMouseWheel;
        MouseDown         += OnMouseDown;
        MouseMove         += OnMouseMove;
        MouseUp           += OnMouseUp;

        // Clamp offsets whenever the host viewport is resized.
        SizeChanged += (_, _) => ClampOffsets();

        // Auto-fit content when the control first becomes visible.
        Loaded += (_, _) =>
        {
            if (ActualWidth > 0 && Content is FrameworkElement)
                Dispatcher.InvokeAsync(FitToContent, DispatcherPriority.Loaded);
        };
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, Math.Clamp(value, MinZoom, MaxZoom));
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

    // ── Public commands ───────────────────────────────────────────────────────

    public void ZoomIn()  => ZoomLevel = Math.Min(MaxZoom, Math.Round(ZoomLevel + ZoomStep, 2));
    public void ZoomOut() => ZoomLevel = Math.Max(MinZoom, Math.Round(ZoomLevel - ZoomStep, 2));
    public void ZoomReset() { ZoomLevel = 1.0; OffsetX = 0; OffsetY = 0; }

    public void FitToContent()
    {
        if (Content is not FrameworkElement fe) return;
        if (fe.ActualWidth <= 0 || fe.ActualHeight <= 0) return;

        // Fit to viewport WIDTH (fill horizontal space at 90%).
        // When scaleX ≤ scaleY (normal case): identical to min(scaleX,scaleY)*0.9 — no change.
        // When scaleX > scaleY (wide-short viewport, e.g. VerticalDesignTop thin strip):
        //   min-scale used scaleY → tiny canvas with huge side margins ("rectangles on each side").
        //   width-fit uses scaleX → canvas fills width, overflows vertically via scrollbar.
        double scaleX = ActualWidth / fe.ActualWidth;
        ZoomLevel = Math.Clamp(scaleX * 0.9, MinZoom, MaxZoom);
        CenterContent();
    }

    // ── Content wiring ────────────────────────────────────────────────────────

    /// <summary>
    /// Applies zoom/pan transforms directly to the content element so that only
    /// the design content scales — not the ZoomPanCanvas viewport frame.
    /// Uses a combined RenderTransform (ScaleTransform + TranslateTransform) —
    /// NOT LayoutTransform — to avoid WPF's layout-clip guard.
    /// Also anchors content to top-left so ActualWidth/Height reflect natural size.
    /// </summary>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        // Remove transforms from previous content.
        if (oldContent is FrameworkElement oldFe)
        {
            oldFe.LayoutTransform = Transform.Identity;
            oldFe.RenderTransform = Transform.Identity;
            oldFe.SizeChanged -= OnContentSizeChanged;
        }

        // Apply combined RenderTransform to new content.
        // No LayoutTransform: using LayoutTransform here causes WPF's NeedsClipBounds
        // guard to fire due to floating-point rounding in the inverse-transform path
        // (1280*zoom / zoom = 1279.9999...), silently clipping the rendered content.
        if (newContent is FrameworkElement newEl)
        {
            newEl.LayoutTransform = Transform.Identity;
            newEl.RenderTransform = _contentTransform;
            // Left+Top alignment: content.ActualWidth = natural design width (not viewport).
            newEl.HorizontalAlignment = HorizontalAlignment.Left;
            newEl.VerticalAlignment   = VerticalAlignment.Top;
            newEl.SizeChanged += OnContentSizeChanged;
        }
    }

    /// <summary>Auto-fits content once it has a measured natural size.</summary>
    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ActualWidth > 0 && e.NewSize.Width > 10)
            Dispatcher.InvokeAsync(FitToContent, DispatcherPriority.Render);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// When content fits inside the viewport, forces it to the center (no panning needed).
    /// When content overflows, clamps pan offsets so the canvas can be scrolled up to
    /// ScrollExtraMargin px beyond each edge — matching the VS Designer blank-canvas breathing room.
    /// Force-centering on every SizeChanged (including layout switches) ensures the canvas is never
    /// left at a stale offset after the viewport is resized.
    /// </summary>
    private void ClampOffsets()
    {
        if (Content is not FrameworkElement content || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        double cw = content.ActualWidth  * ZoomLevel;
        double ch = content.ActualHeight * ZoomLevel;

        // Content fits horizontally → force-center; no user panning is meaningful here.
        if (cw <= ActualWidth)
            OffsetX = (ActualWidth - cw) / 2.0;
        else
        {
            // Content overflows → allow panning within [content-left-edge - margin, margin].
            double xMin = ActualWidth - cw - ScrollExtraMargin;
            OffsetX = Math.Clamp(OffsetX, Math.Min(xMin, -ScrollExtraMargin), ScrollExtraMargin);
        }

        // Same logic vertically.
        if (ch <= ActualHeight)
            OffsetY = (ActualHeight - ch) / 2.0;
        else
        {
            double yMin = ActualHeight - ch - ScrollExtraMargin;
            OffsetY = Math.Clamp(OffsetY, Math.Min(yMin, -ScrollExtraMargin), ScrollExtraMargin);
        }
    }

    /// <summary>Centers content in the viewport.</summary>
    private void CenterContent()
    {
        if (Content is not FrameworkElement content || ActualWidth <= 0) return;
        // Setting OffsetX/OffsetY triggers OnOffsetChanged which syncs transforms.
        OffsetX = (ActualWidth  - content.ActualWidth  * ZoomLevel) / 2.0;
        OffsetY = (ActualHeight - content.ActualHeight * ZoomLevel) / 2.0;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler? ZoomChanged;
    public event EventHandler? PanChanged;

    // ── Mouse wheel ───────────────────────────────────────────────────────────

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            // Zoom centered on mouse position.
            var mousePos = e.GetPosition(this);
            double factor = e.Delta > 0 ? 1.0 + ZoomStep : 1.0 - ZoomStep;
            double newZoom = Math.Clamp(ZoomLevel * factor, MinZoom, MaxZoom);

            // Adjust offset so zoom anchors to mouse position.
            OffsetX = mousePos.X - (mousePos.X - OffsetX) * (newZoom / ZoomLevel);
            OffsetY = mousePos.Y - (mousePos.Y - OffsetY) * (newZoom / ZoomLevel);

            ZoomLevel = newZoom;
            ClampOffsets();
            e.Handled = true;
        }
        else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            OffsetX += e.Delta * 0.3;
            ClampOffsets();
            e.Handled = true;
        }
        else
        {
            OffsetY += e.Delta * 0.3;
            ClampOffsets();
            e.Handled = true;
        }
    }

    // ── Middle-mouse pan ──────────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle-button → pan.
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            _panStart  = e.GetPosition(this);
            _isPanning = true;
            CaptureMouse();
            Cursor    = Cursors.Hand;
            e.Handled = true;
            return;
        }

        // Ctrl+Left-Click → zoom in toward the click point.
        // Ctrl+Alt+Left-Click → zoom out from the click point.
        if (e.LeftButton == MouseButtonState.Pressed &&
            (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            bool zoomOut = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            var mousePos = e.GetPosition(this);
            double factor   = zoomOut ? 1.0 - ZoomStep : 1.0 + ZoomStep;
            double newZoom  = Math.Clamp(ZoomLevel * factor, MinZoom, MaxZoom);

            // Anchor zoom to the mouse position.
            OffsetX   = mousePos.X - (mousePos.X - OffsetX) * (newZoom / ZoomLevel);
            OffsetY   = mousePos.Y - (mousePos.Y - OffsetY) * (newZoom / ZoomLevel);
            ZoomLevel = newZoom;
            ClampOffsets();
            e.Handled = true;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;

        var current = e.GetPosition(this);
        OffsetX += current.X - _panStart.X;
        OffsetY += current.Y - _panStart.Y;
        _panStart = current;
        ClampOffsets();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning) return;
        _isPanning = false;
        ReleaseMouseCapture();
        Cursor = null;
    }

    // ── Hit-testing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Makes the entire viewport bounds hit-testable so that mouse-wheel zoom and
    /// middle-mouse pan work everywhere in the designer area — not just over the
    /// rendered design element.  Without this override, WPF only considers the
    /// ZoomPanCanvas "hit" where the content element's visual occupies space;
    /// the blank canvas background has no hit-testable surface (Background = null)
    /// and therefore receives no routed mouse events.
    /// </summary>
    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => new PointHitTestResult(this, hitTestParameters.HitPoint);

    // ── Transform sync ────────────────────────────────────────────────────────

    private static void OnZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (ZoomPanCanvas)d;
        double z = (double)e.NewValue;
        ctrl._scale.ScaleX = z;
        ctrl._scale.ScaleY = z;
        ctrl.ZoomChanged?.Invoke(ctrl, EventArgs.Empty);
    }

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (ZoomPanCanvas)d;
        ctrl._translate.X = ctrl.OffsetX;
        ctrl._translate.Y = ctrl.OffsetY;
        ctrl.PanChanged?.Invoke(ctrl, EventArgs.Empty);
    }
}
