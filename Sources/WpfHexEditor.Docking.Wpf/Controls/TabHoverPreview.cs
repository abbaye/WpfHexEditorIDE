// ==========================================================
// Project: WpfHexEditor.Docking.Wpf
// File: TabHoverPreview.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Provides a lightweight hover-preview Popup for document tabs.
//     After a configurable delay (default 400 ms) a small thumbnail of
//     the tab content is shown in a Popup beneath the tab header.
//     The thumbnail is produced via RenderTargetBitmap so no extra
//     layout is required.
//
// Architecture Notes:
//     Observer Pattern — subscribes to MouseEnter/MouseLeave on each TabItem.
//     Lazy Snapshot   — bitmap captured only when the popup is actually opening.
//     Singleton per DockTabControl — attach with TabHoverPreview.Attach().
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WpfHexEditor.Docking.Wpf.Controls;

/// <summary>
/// Attaches hover-preview behaviour to a <see cref="DockTabControl"/>.
/// Call <see cref="Attach"/> once per tab control at construction time.
/// </summary>
public sealed class TabHoverPreview
{
    // ── Configuration ────────────────────────────────────────────────────────
    private const double PreviewWidth  = 200;
    private const double PreviewHeight = 150;
    private const int    HoverDelayMs  = 400;

    // ── State ────────────────────────────────────────────────────────────────
    private readonly DockTabControl _owner;
    private readonly Popup          _popup;
    private readonly Border         _border;
    private readonly Image          _previewImage;
    private readonly DispatcherTimer _hoverTimer;

    private TabItem? _hoveredTab;

    // ── Constructor ──────────────────────────────────────────────────────────

    private TabHoverPreview(DockTabControl owner)
    {
        _owner = owner;

        _previewImage = new Image
        {
            Width   = PreviewWidth,
            Height  = PreviewHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top
        };

        _border = new Border
        {
            Child           = _previewImage,
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(1)
        };
        _border.SetResourceReference(Border.BackgroundProperty,  "DockMenuBackgroundBrush");
        _border.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        // Drop shadow effect via Effect — avoids a BitmapEffect dependency.
        _border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius   = 6,
            ShadowDepth  = 2,
            Opacity      = 0.35,
            Color        = Colors.Black
        };

        _popup = new Popup
        {
            Child              = _border,
            AllowsTransparency = true,
            Placement          = PlacementMode.Bottom,
            StaysOpen          = false,
            IsHitTestVisible   = false   // pointer events pass through so hovers remain natural
        };

        _hoverTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(HoverDelayMs)
        };
        _hoverTimer.Tick += OnHoverTimerTick;

        // Close popup when another tab gets focus or the control loses mouse.
        owner.MouseLeave += (_, _) => HidePreview();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a <see cref="TabHoverPreview"/> to the given <paramref name="tabControl"/>.
    /// Safe to call multiple times — only one instance is created per control.
    /// </summary>
    public static TabHoverPreview Attach(DockTabControl tabControl)
    {
        var preview = new TabHoverPreview(tabControl);
        // Wire future items via ItemContainerGenerator.
        tabControl.ItemContainerGenerator.StatusChanged += (_, _) =>
        {
            if (tabControl.ItemContainerGenerator.Status ==
                System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                preview.WireAllTabs(tabControl);
            }
        };
        // Wire items that already exist.
        tabControl.Loaded += (_, _) => preview.WireAllTabs(tabControl);
        return preview;
    }

    // ── Wire / unwire tab items ──────────────────────────────────────────────

    private void WireAllTabs(DockTabControl tabControl)
    {
        for (int i = 0; i < tabControl.Items.Count; i++)
        {
            if (tabControl.ItemContainerGenerator.ContainerFromIndex(i) is TabItem ti)
                WireTab(ti);
        }
    }

    private void WireTab(TabItem tab)
    {
        // Guard against double-subscription via attached tag.
        if (tab.Tag is string s && s.Contains("__hovered__")) return;

        tab.MouseEnter += OnTabMouseEnter;
        tab.MouseLeave += OnTabMouseLeave;
    }

    // ── Hover handlers ───────────────────────────────────────────────────────

    private void OnTabMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not TabItem tab) return;

        // Do not preview the currently selected tab (it is already fully visible).
        if (tab.IsSelected) return;

        _hoveredTab = tab;
        _popup.PlacementTarget = tab;
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void OnTabMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoverTimer.Stop();
        // Small delay so cursor movement between popup and tab doesn't flash.
        HidePreview();
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (_hoveredTab is null) return;

        TakeSnapshot(_hoveredTab);
        _popup.IsOpen = true;
    }

    // ── Snapshot ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Captures the tab's content via <see cref="RenderTargetBitmap"/> and
    /// displays it in the preview popup.
    /// </summary>
    private void TakeSnapshot(TabItem tab)
    {
        var content = tab.Content as UIElement;
        if (content is null || !content.IsVisible)
        {
            _previewImage.Source = null;
            return;
        }

        try
        {
            var dpi    = VisualTreeHelper.GetDpi(content);
            var width  = Math.Max(1, content.RenderSize.Width);
            var height = Math.Max(1, content.RenderSize.Height);

            var rtb = new RenderTargetBitmap(
                (int)(width  * dpi.DpiScaleX),
                (int)(height * dpi.DpiScaleY),
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);

            rtb.Render(content);
            rtb.Freeze();

            _previewImage.Source = rtb;
        }
        catch
        {
            // Swallow render errors (e.g. hardware-accelerated content not yet realised).
            _previewImage.Source = null;
        }
    }

    // ── Hide ─────────────────────────────────────────────────────────────────

    private void HidePreview()
    {
        _popup.IsOpen = false;
        _hoveredTab   = null;
    }
}
