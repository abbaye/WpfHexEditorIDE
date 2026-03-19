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
//     Uses LayoutTransform (not RenderTransform) so hit-testing coordinates
//     remain correct after scaling.
//     ZoomLevel clamped to [0.1, 4.0]. Transforms composed as:
//       ScaleTransform(zoom, zoom) + TranslateTransform(offsetX, offsetY)
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

    private readonly ScaleTransform     _scale     = new(1, 1);
    private readonly TranslateTransform _translate = new(0, 0);

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
        // ZoomPanCanvas itself has NO transform.
        // Transforms are applied to the content element in OnContentChanged:
        //   LayoutTransform = _scale   → content is measured/arranged at scaled size
        //   RenderTransform = _translate → content pans without re-measuring
        // HorizontalAlignment=Left + VerticalAlignment=Top on the content element
        // ensures content.ActualWidth reflects its natural size, not the viewport.
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

        double scaleX = ActualWidth  / fe.ActualWidth;
        double scaleY = ActualHeight / fe.ActualHeight;
        ZoomLevel = Math.Clamp(Math.Min(scaleX, scaleY) * 0.9, MinZoom, MaxZoom);
        CenterContent();
    }

    // ── Content wiring ────────────────────────────────────────────────────────

    /// <summary>
    /// Applies zoom/pan transforms directly to the content element so that only
    /// the design content scales — not the ZoomPanCanvas viewport frame.
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

        // Apply transforms to new content.
        if (newContent is FrameworkElement newEl)
        {
            newEl.LayoutTransform = _scale;
            newEl.RenderTransform = _translate;
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
    /// Clamps pan offsets so the canvas can be scrolled up to ScrollExtraMargin px
    /// beyond each edge — matching the VS Designer blank-canvas breathing room.
    /// </summary>
    private void ClampOffsets()
    {
        if (Content is not FrameworkElement content || ActualWidth <= 0 || ActualHeight <= 0)
            return;
        double cw = content.ActualWidth  * ZoomLevel;
        double ch = content.ActualHeight * ZoomLevel;
        OffsetX = Math.Clamp(OffsetX, ActualWidth  - cw - ScrollExtraMargin, ScrollExtraMargin);
        OffsetY = Math.Clamp(OffsetY, ActualHeight - ch - ScrollExtraMargin, ScrollExtraMargin);
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
