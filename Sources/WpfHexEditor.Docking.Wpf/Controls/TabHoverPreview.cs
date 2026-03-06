// ==========================================================
// Project: WpfHexEditor.Docking.Wpf
// File: TabHoverPreview.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Provides a lightweight hover-preview Popup for document/panel tabs.
//     After a configurable delay (default 400 ms) a small thumbnail of the
//     tab content is shown in a Popup beneath the tab header.
//
// Architecture Notes:
//     Observer Pattern     — subscribes to MouseEnter/MouseLeave on each TabItem.
//     Eager Snapshot       — bitmap captured on SelectionChanged (while content is
//                            still rendered). WPF only renders the selected tab's
//                            content; non-selected content is always IsVisible=false,
//                            making on-the-fly capture impossible.
//     Deselection Cache    — Dictionary<TabItem, BitmapSource> keyed per tab.
//     Double Timer Pattern — open delay (400ms) + close delay (150ms) prevents the
//                            popup from flashing due to spurious MouseLeave events
//                            caused by WPF's Popup HwndSource creation.
//     StaysOpen = true     — popup closed manually to avoid the OS-level mouse hook
//                            that StaysOpen=false installs (which itself triggers
//                            a spurious MouseLeave on the placement target).
//     Singleton per owner  — attach with TabHoverPreview.Attach().
//
// Theme: DockMenuBackgroundBrush / DockBorderBrush (100% DynamicResource)
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfHexEditor.Docking.Core.Nodes;

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
    private const int    OpenDelayMs   = 400;
    private const int    CloseDelayMs  = 150;

    // ── State ────────────────────────────────────────────────────────────────
    private readonly DockTabControl                     _owner;
    private readonly Popup                              _popup;
    private readonly Border                             _border;
    private readonly Image                              _previewImage;
    private readonly TextBlock                          _titleBlock;
    private readonly DispatcherTimer                    _openTimer;
    private readonly DispatcherTimer                    _closeTimer;
    private readonly Dictionary<TabItem, BitmapSource> _snapshotCache = new();
    private readonly HashSet<TabItem>                   _wired         = new();

    private TabItem? _hoveredTab;

    // ── Constructor ──────────────────────────────────────────────────────────

    private TabHoverPreview(DockTabControl owner)
    {
        _owner = owner;

        _previewImage = new Image
        {
            Width               = PreviewWidth,
            Height              = PreviewHeight,
            Stretch             = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top
        };

        // Title fallback: shown when no snapshot is cached (tab never activated).
        _titleBlock = new TextBlock
        {
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 11,
            Margin            = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility        = Visibility.Collapsed
        };
        _titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var stack = new StackPanel();
        stack.Children.Add(_previewImage);
        stack.Children.Add(_titleBlock);

        _border = new Border
        {
            Child           = stack,
            BorderThickness = new Thickness(1),
            Padding         = new Thickness(1)
        };
        _border.SetResourceReference(Border.BackgroundProperty,  "DockMenuBackgroundBrush");
        _border.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        _border.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius  = 6,
            ShadowDepth = 2,
            Opacity     = 0.35,
            Color       = Colors.Black
        };

        // StaysOpen = true: managed close avoids the OS-level mouse hook that
        // StaysOpen=false installs, which causes spurious MouseLeave on the tab.
        _popup = new Popup
        {
            Child              = _border,
            AllowsTransparency = true,
            Placement          = PlacementMode.Bottom,
            StaysOpen          = true,
            IsHitTestVisible   = false
        };

        // Open delay: show popup only after sustained hover.
        _openTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(OpenDelayMs)
        };
        _openTimer.Tick += OnOpenTimerTick;

        // Close delay: absorbs spurious MouseLeave events triggered by popup creation.
        _closeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(CloseDelayMs)
        };
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); HidePreview(); };

        // Immediate close when mouse exits the whole tab control.
        owner.MouseLeave += (_, _) => { _openTimer.Stop(); _closeTimer.Stop(); HidePreview(); };
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a <see cref="TabHoverPreview"/> to the given <paramref name="tabControl"/>.
    /// Safe to call once per control — creates a single instance.
    /// </summary>
    public static TabHoverPreview Attach(DockTabControl tabControl)
    {
        var preview = new TabHoverPreview(tabControl);

        // Capture snapshot when a tab loses selection (content still rendered at this point).
        tabControl.SelectionChanged += preview.OnOwnerSelectionChanged;

        // Wire existing and future tab item containers.
        tabControl.ItemContainerGenerator.StatusChanged += (_, _) =>
        {
            if (tabControl.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                preview.WireAllTabs(tabControl);
        };
        tabControl.Loaded += (_, _) => preview.WireAllTabs(tabControl);

        return preview;
    }

    // ── Eager Snapshot on Deselection ────────────────────────────────────────

    private void OnOwnerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // RemovedItems contains the tab that just lost selection.
        // At this point in the handler the content is still rendered (IsVisible=true).
        foreach (var item in e.RemovedItems)
        {
            if (item is TabItem tab)
                CaptureAndCache(tab);
        }
    }

    /// <summary>
    /// Renders the tab's content into a <see cref="RenderTargetBitmap"/> and caches it.
    /// Must be called while the content is still in the visual tree and visible.
    /// </summary>
    private void CaptureAndCache(TabItem tab)
    {
        if (tab.Content is not UIElement content) return;

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

            _snapshotCache[tab] = rtb;
        }
        catch
        {
            // Swallow render errors (hardware-accelerated content not yet realised, etc.)
        }
    }

    // ── Wire tab items ───────────────────────────────────────────────────────

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
        if (_wired.Contains(tab)) return;
        _wired.Add(tab);

        tab.MouseEnter += OnTabMouseEnter;
        tab.MouseLeave += OnTabMouseLeave;
    }

    // ── Hover handlers ───────────────────────────────────────────────────────

    private void OnTabMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not TabItem tab) return;

        // Cancel any pending close from a previous hover.
        _closeTimer.Stop();

        // Do not preview the currently selected tab (it is already fully visible).
        // However, explicitly hide any existing preview so it does not persist when
        // the mouse moves from an unselected tab (with an open preview) to the
        // selected tab — stopping the close timer would otherwise leave it open forever.
        if (tab.IsSelected)
        {
            HidePreview();
            return;
        }

        _hoveredTab = tab;
        _popup.PlacementTarget = tab;
        _openTimer.Stop();
        _openTimer.Start();
    }

    private void OnTabMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _openTimer.Stop();
        // Close after a short grace period to absorb spurious MouseLeave events
        // that WPF fires when the Popup HwndSource is created.
        _closeTimer.Stop();
        _closeTimer.Start();
    }

    private void OnOpenTimerTick(object? sender, EventArgs e)
    {
        _openTimer.Stop();
        if (_hoveredTab is null) return;

        // Use the cached snapshot — never attempt to render invisible content on-the-fly.
        var bitmap = _snapshotCache.GetValueOrDefault(_hoveredTab);

        if (bitmap is not null)
        {
            _previewImage.Source     = bitmap;
            _previewImage.Visibility = Visibility.Visible;
            _titleBlock.Visibility   = Visibility.Collapsed;
        }
        else
        {
            // Tab was never activated — no snapshot available.
            // Show a title-only fallback card so the user still gets visual feedback.
            // Header is a DockTabHeader (StackPanel), not a string — use Tag (DockItem) for the title.
            var title = (_hoveredTab.Tag as DockItem)?.Title ?? string.Empty;
            if (string.IsNullOrEmpty(title)) return;

            _previewImage.Visibility = Visibility.Collapsed;
            _titleBlock.Text         = title;
            _titleBlock.Visibility   = Visibility.Visible;
        }

        _popup.IsOpen = true;
    }

    // ── Hide ─────────────────────────────────────────────────────────────────

    private void HidePreview()
    {
        _popup.IsOpen = false;
        _hoveredTab   = null;
    }
}
