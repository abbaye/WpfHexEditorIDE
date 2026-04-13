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
        PreviewMouseWheel            += OnMouseWheel;
        PreviewMouseLeftButtonDown   += OnPreviewLeftDown;
        MouseDown                    += OnMouseDown;
        PreviewMouseMove             += OnPreviewMouseMove;
        PreviewMouseLeftButtonUp     += OnPreviewLeftUp;
        MouseMove                    += OnMouseMove;
        MouseUp                      += OnMouseUp;

        // Clamp offsets on every viewport resize — keeps the canvas within the
        // virtual scrollable area without forcing any re-centering.
        // The LayoutClip truncation is fixed by MeasureOverride/ArrangeOverride, so
        // forced re-centering is no longer needed and would cause the canvas to jump.
        SizeChanged += (_, _) => ClampOffsets();
        // Note: no auto-fit in Loaded — FitToContent is called explicitly by
        // XamlDesignerSplitHost after each new-file render so that layout is
        // fully settled (DispatcherPriority.Background) before computing the scale.
        // This prevents the zoom from resetting when the user switches tabs.
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

    public void ZoomIn()    => AnchorZoom(Math.Min(MaxZoom, Math.Round(ZoomLevel + ZoomStep, 2)));
    public void ZoomOut()   => AnchorZoom(Math.Max(MinZoom, Math.Round(ZoomLevel - ZoomStep, 2)));
    public void ZoomReset() => AnchorZoom(1.0);

    /// <summary>
    /// Changes zoom while keeping the viewport centre fixed over the same canvas point.
    /// This prevents the canvas from drifting right/down when zooming via toolbar buttons,
    /// which was the root cause of the right/bottom truncation with a small viewport.
    /// </summary>
    private void AnchorZoom(double newZoom)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || ZoomLevel == newZoom)
        {
            ZoomLevel = newZoom;
            return;
        }

        // Anchor point = centre of the visible viewport.
        var anchor = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
        double ratio = newZoom / ZoomLevel;
        OffsetX = anchor.X - (anchor.X - OffsetX) * ratio;
        OffsetY = anchor.Y - (anchor.Y - OffsetY) * ratio;

        ZoomLevel = newZoom;   // triggers OnZoomLevelChanged → ClampOffsets
    }

    public void FitToContent()
    {
        if (Content is not FrameworkElement fe) return;
        if (fe.ActualWidth <= 0 || fe.ActualHeight <= 0) return;
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        // Reserve a fixed 10 px free border on every side so the canvas edge
        // never touches the viewport border.
        const double Padding = 10.0;

        double availW = Math.Max(1, ActualWidth  - Padding * 2);
        double availH = Math.Max(1, ActualHeight - Padding * 2);

        double scaleX = availW / fe.ActualWidth;
        double scaleY = availH / fe.ActualHeight;

        // Use the tighter axis so the whole canvas fits. Exception: an extremely
        // wide-short viewport (e.g. VerticalDesignTop thin strip, scaleY < scaleX/2)
        // falls back to width-only fit to avoid a tiny canvas with huge margins.
        double scale = scaleY < scaleX * 0.5 ? scaleX : Math.Min(scaleX, scaleY);

        ZoomLevel = Math.Clamp(scale, MinZoom, MaxZoom);
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
        }
    }

    // OnContentSizeChanged intentionally does NOT auto-call FitToContent.
    // Auto-fit is driven by XamlDesignerSplitHost._fitOnFirstRender so that:
    //  - the fit only happens once per new document (not on every live preview update)
    //  - DispatcherPriority.Background guarantees ActualWidth is fully settled

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Clamps OffsetX/Y so the canvas always stays within the VS-like virtual scrollable area:
    ///   virtual size = max(content, viewport) + 2 × (10 % of viewport) on each side.
    /// The canvas is centred inside this virtual space:
    ///   OffsetX max (scroll at 0) = (virtualW − cw) / 2
    ///   OffsetX at centre         = (vw − cw) / 2  (canvas centred in viewport)
    ///   OffsetX min (scroll max)  = xMax − (virtualW − vw)
    /// This model is consistent with UpdateScrollBars and the scrollbar→canvas handlers.
    /// </summary>
    private void ClampOffsets()
    {
        if (Content is not FrameworkElement content || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        double cw = content.ActualWidth  * ZoomLevel;
        double ch = content.ActualHeight * ZoomLevel;
        double vw = ActualWidth;
        double vh = ActualHeight;

        double mH = Math.Max(SystemParameters.PrimaryScreenWidth  * 0.10, 20);
        double mV = Math.Max(SystemParameters.PrimaryScreenHeight * 0.10, 20);

        double virtualW = Math.Max(cw, vw) + 2 * mH;
        double virtualH = Math.Max(ch, vh) + 2 * mV;

        double xMax = (virtualW - cw) / 2.0;
        double xMin = xMax - (virtualW - vw);
        OffsetX = Math.Clamp(OffsetX, xMin, xMax);

        double yMax = (virtualH - ch) / 2.0;
        double yMin = yMax - (virtualH - vh);
        OffsetY = Math.Clamp(OffsetY, yMin, yMax);
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

    // ── External rubber-band (left-click-drag in empty zone around design canvas) ──

    private bool _isExternalRubberBanding;

    /// <summary>
    /// PreviewMouseLeftButtonDown tunnels from the root down to the OriginalSource.
    /// In the empty zone around the design canvas, OriginalSource = ZoomPanCanvas (via HitTestCore).
    /// We intercept here — before DesignCanvas sees any event — and start an external rubber-band
    /// when the click is outside the rendered bounds of the DesignCanvas.
    /// </summary>
    private void OnPreviewLeftDown(object sender, MouseButtonEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) return;  // Ctrl+click = zoom, handled below
        if (Content is not DesignCanvas dc) return;
        if (!IsClickOutsideDesignCanvas(e, dc)) return;

        _isExternalRubberBanding = true;
        var ptInCanvas = ViewportToCanvas(e.GetPosition(this));
        dc.BeginExternalRubberBand(ptInCanvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isExternalRubberBanding) return;
        if (Content is not DesignCanvas dc) return;
        dc.UpdateExternalRubberBand(ViewportToCanvas(e.GetPosition(this)));
        e.Handled = true;
    }

    private void OnPreviewLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isExternalRubberBanding) return;
        _isExternalRubberBanding = false;
        ReleaseMouseCapture();
        if (Content is DesignCanvas dc)
            dc.EndExternalRubberBand(ViewportToCanvas(e.GetPosition(this)));
        e.Handled = true;
    }

    // ── Middle-mouse pan / Ctrl+zoom ─────────────────────────────────────────

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

    /// <summary>
    /// Converts a point from ZoomPanCanvas viewport space to DesignCanvas local space,
    /// inverting the RenderTransform (Scale then Translate) applied to the content element.
    /// </summary>
    private Point ViewportToCanvas(Point ptInViewport) =>
        new((ptInViewport.X - OffsetX) / ZoomLevel,
            (ptInViewport.Y - OffsetY) / ZoomLevel);

    /// <summary>
    /// Returns true when a left-click in this viewport landed outside the visible rendered
    /// bounds of <paramref name="dc"/> (i.e. in the empty dark zone around the design canvas).
    /// Computes the rendered bounds of dc in ZoomPanCanvas coordinate space via its RenderTransform
    /// so the check is correct even when dc is scaled/translated.
    /// </summary>
    private bool IsClickOutsideDesignCanvas(MouseButtonEventArgs e, DesignCanvas dc)
    {
        var ptInZpc = e.GetPosition(this);

        // Compute the rendered rect of dc in ZoomPanCanvas space.
        // dc has an explicit Width/Height and a RenderTransform (Scale then Translate).
        double renderedW = dc.ActualWidth  * ZoomLevel;
        double renderedH = dc.ActualHeight * ZoomLevel;
        var renderedBounds = new Rect(OffsetX, OffsetY, renderedW, renderedH);

        return !renderedBounds.Contains(ptInZpc);
    }

    // ── Layout overrides ─────────────────────────────────────────────────────
    //
    // Root cause of right/bottom truncation:
    // ContentControl passes the viewport size as constraint to its ContentPresenter,
    // which then receives finalSize = viewport during Arrange.  WPF sets NeedsClipBounds
    // on ContentPresenter (desiredSize 1018 > finalRect 640) and issues a LayoutClip
    // BEFORE RenderTransform — the canvas is clipped to the viewport regardless of pan.
    //
    // Fix: override Measure/Arrange to always give the ContentPresenter (visual child)
    // infinite space during measure and its full DesiredSize during arrange.
    // ZoomPanCanvas itself still returns the real viewport size so docking is unaffected.
    // NOTE: in ContentControl the direct visual child is the ContentPresenter from the
    //       default template — NOT the Content object (which is a logical child hosted
    //       inside that ContentPresenter).  We must operate on GetVisualChild(0).

    protected override Size MeasureOverride(Size availableSize)
    {
        if (VisualChildrenCount > 0 && GetVisualChild(0) is UIElement cp)
            cp.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return availableSize;   // ZoomPanCanvas fills its allocated cell
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (VisualChildrenCount > 0 && GetVisualChild(0) is UIElement cp)
        {
            var natural = cp.DesiredSize;
            // Arrange ContentPresenter at its natural size so WPF does NOT issue a
            // LayoutClip on it.  The visual clip is handled by ClipToBounds = true on
            // ZoomPanCanvas itself, which fires AFTER RenderTransform.
            cp.Arrange(new Rect(0, 0,
                Math.Max(natural.Width,  0.1),
                Math.Max(natural.Height, 0.1)));
        }
        return finalSize;
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
        ctrl.ClampOffsets();          // keep canvas in virtual bounds after any zoom change
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
