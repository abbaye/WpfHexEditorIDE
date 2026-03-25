// ==========================================================
// Project: WpfHexEditor.Shell
// File: TabHoverPreview.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Provides a lightweight hover-preview Popup for document/panel tabs.
//     After a configurable delay (default 400 ms) a small thumbnail of the
//     tab content is shown in a Popup beneath the tab header, with a filename
//     footer at the bottom (Windows taskbar thumbnail style).
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
//     Settings injection   — TabPreviewSettings passed from DockControl; live updates
//                            via ApplySettings() without recreating the popup.
//
// Theme: DockMenuBackgroundBrush / DockBorderBrush (100% DynamicResource)
// ==========================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Shell.Controls;

// ── Settings POCO (lives in Docking.Wpf — no reference to Core.Options) ──────

/// <summary>
/// Runtime settings for <see cref="TabHoverPreview"/>.
/// Populated by <c>DockControl</c> from <c>AppSettings.TabPreview</c> via
/// <c>DockControl.RefreshTabPreviewSettings()</c>.
/// </summary>
public sealed class TabPreviewSettings
{
    /// <summary>When false, no popup is shown on tab hover.</summary>
    public bool   Enabled       { get; set; } = true;

    /// <summary>Show the filename footer below the screenshot thumbnail.</summary>
    public bool   ShowFileName  { get; set; } = true;

    /// <summary>Thumbnail width in pixels. Default: 200.</summary>
    public double PreviewWidth  { get; set; } = 200;

    /// <summary>Thumbnail height in pixels. Default: 150.</summary>
    public double PreviewHeight { get; set; } = 150;

    /// <summary>Milliseconds the mouse must hover before the popup appears. Default: 400.</summary>
    public int    OpenDelayMs   { get; set; } = 400;

    /// <summary>Milliseconds before the popup closes after mouse leave. Default: 150.</summary>
    public int    CloseDelayMs  { get; set; } = 150;
}

// ── TabHoverPreview ───────────────────────────────────────────────────────────

/// <summary>
/// Attaches hover-preview behaviour to a <see cref="DockTabControl"/>.
/// Call <see cref="Attach"/> once per tab control at construction time.
/// </summary>
public sealed class TabHoverPreview
{
    // -- State ----------------------------------------------------------------

    private readonly DockTabControl                     _owner;
    private readonly TabPreviewSettings                 _settings;
    private readonly Popup                              _popup;
    private readonly Border                             _border;
    private readonly Grid                               _previewArea;
    private readonly Image                              _previewImage;
    private readonly Border                             _noPreviewCard;
    private readonly TextBlock                          _noPreviewTitle;
    private readonly Border                             _footerBorder;
    private readonly TextBlock                          _fileNameBlock;
    private readonly DispatcherTimer                    _openTimer;
    private readonly DispatcherTimer                    _closeTimer;
    private readonly Dictionary<TabItem, BitmapSource> _snapshotCache = new();
    private readonly HashSet<TabItem>                   _wired         = new();

    private TabItem? _hoveredTab;

    // -- Constructor ----------------------------------------------------------

    private TabHoverPreview(DockTabControl owner, TabPreviewSettings settings)
    {
        _owner    = owner;
        _settings = settings;

        // ── Preview area (fixed size, layered) ────────────────────────────────
        _previewImage = new Image
        {
            Stretch             = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
            Visibility          = Visibility.Collapsed
        };

        // "No preview" card — shown when tab was never activated (no snapshot).
        _noPreviewTitle = new TextBlock
        {
            FontSize          = 11,
            FontWeight        = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            TextWrapping      = TextWrapping.Wrap,
            TextAlignment     = TextAlignment.Center,
            Margin            = new Thickness(8),
        };
        _noPreviewTitle.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var noPreviewIcon = new TextBlock
        {
            Text              = "\uE8A5",   // Page glyph (Segoe MDL2)
            FontFamily        = new FontFamily("Segoe MDL2 Assets"),
            FontSize          = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin            = new Thickness(0, 0, 0, 6),
            Opacity           = 0.45
        };
        noPreviewIcon.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var noPreviewStack = new StackPanel
        {
            Orientation         = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };
        noPreviewStack.Children.Add(noPreviewIcon);
        noPreviewStack.Children.Add(_noPreviewTitle);

        _noPreviewCard = new Border
        {
            Child      = noPreviewStack,
            Visibility = Visibility.Visible,
            Opacity    = 0.80
        };
        _noPreviewCard.SetResourceReference(Border.BackgroundProperty, "DockMenuBackgroundBrush");

        _previewArea = new Grid
        {
            Width  = settings.PreviewWidth,
            Height = settings.PreviewHeight
        };
        _previewArea.Children.Add(_noPreviewCard);   // z=0 (back)
        _previewArea.Children.Add(_previewImage);    // z=1 (front)

        // ── Filename footer ───────────────────────────────────────────────────
        var footerGlyph = new TextBlock
        {
            Text       = "\uE8A5",   // Document glyph
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize   = 10,
            Margin     = new Thickness(0, 0, 5, 0),
            Opacity    = 0.70,
            VerticalAlignment = VerticalAlignment.Center
        };
        footerGlyph.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        _fileNameBlock = new TextBlock
        {
            FontSize          = 11,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming      = TextTrimming.CharacterEllipsis,
            MaxWidth          = settings.PreviewWidth - 30
        };
        _fileNameBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var footerContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(6, 3, 6, 3)
        };
        footerContent.Children.Add(footerGlyph);
        footerContent.Children.Add(_fileNameBlock);

        _footerBorder = new Border
        {
            Child           = footerContent,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Visibility      = settings.ShowFileName ? Visibility.Visible : Visibility.Collapsed
        };
        _footerBorder.SetResourceReference(Border.BackgroundProperty,  "DockMenuBackgroundBrush");
        _footerBorder.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        // ── Outer container ───────────────────────────────────────────────────
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(_previewArea);
        stack.Children.Add(_footerBorder);

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
            Interval = TimeSpan.FromMilliseconds(settings.OpenDelayMs)
        };
        _openTimer.Tick += OnOpenTimerTick;

        // Close delay: absorbs spurious MouseLeave events triggered by popup creation.
        _closeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(settings.CloseDelayMs)
        };
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); HidePreview(); };

        // Immediate close when mouse exits the whole tab control.
        owner.MouseLeave += (_, _) => { _openTimer.Stop(); _closeTimer.Stop(); HidePreview(); };
    }

    // -- Public API -----------------------------------------------------------

    /// <summary>
    /// Attaches a <see cref="TabHoverPreview"/> to the given <paramref name="tabControl"/>.
    /// Safe to call once per control — creates a single instance.
    /// </summary>
    /// <param name="tabControl">The tab control to observe.</param>
    /// <param name="settings">
    /// Shared settings object. When the caller mutates the object and calls
    /// <see cref="ApplySettings"/>, the popup updates without recreation.
    /// </param>
    public static TabHoverPreview Attach(DockTabControl tabControl, TabPreviewSettings? settings = null)
    {
        var preview = new TabHoverPreview(tabControl, settings ?? new TabPreviewSettings());

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

    /// <summary>
    /// Applies the current values of the shared <see cref="TabPreviewSettings"/> to the popup.
    /// Call after mutating the settings object (e.g. from an options page).
    /// </summary>
    internal void ApplySettings()
    {
        _openTimer.Interval  = TimeSpan.FromMilliseconds(_settings.OpenDelayMs);
        _closeTimer.Interval = TimeSpan.FromMilliseconds(_settings.CloseDelayMs);
        _previewArea.Width   = _settings.PreviewWidth;
        _previewArea.Height  = _settings.PreviewHeight;
        _fileNameBlock.MaxWidth = _settings.PreviewWidth - 30;
        _footerBorder.Visibility = _settings.ShowFileName
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Close any open popup immediately when disabled via settings.
        if (!_settings.Enabled)
            HidePreview();
    }

    // -- Eager Snapshot on Deselection ----------------------------------------

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

    // -- Wire tab items -------------------------------------------------------

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

    // -- Hover handlers -------------------------------------------------------

    private void OnTabMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not TabItem tab) return;

        // Cancel any pending close from a previous hover.
        _closeTimer.Stop();

        // Do not preview the currently selected tab (it is already fully visible).
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
        if (!_settings.Enabled || _hoveredTab is null) return;

        // Populate filename footer
        var displayName = GetDisplayName(_hoveredTab);
        _fileNameBlock.Text = displayName;
        _footerBorder.Visibility = _settings.ShowFileName && !string.IsNullOrEmpty(displayName)
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Use the cached snapshot — never attempt to render invisible content on-the-fly.
        var bitmap = _snapshotCache.GetValueOrDefault(_hoveredTab);

        if (bitmap is not null)
        {
            _previewImage.Source     = bitmap;
            _previewImage.Visibility = Visibility.Visible;
            _noPreviewCard.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Tab was never activated — show a "no preview" placeholder card.
            if (string.IsNullOrEmpty(displayName)) return;

            _noPreviewTitle.Text      = displayName;
            _previewImage.Visibility  = Visibility.Collapsed;
            _noPreviewCard.Visibility = Visibility.Visible;
        }

        _popup.IsOpen = true;
    }

    // -- Helpers --------------------------------------------------------------

    private static string GetDisplayName(TabItem tab)
    {
        if (tab.Tag is not DockItem item) return string.Empty;

        if (item.Metadata.TryGetValue("FilePath", out var fp) && !string.IsNullOrEmpty(fp))
            return Path.GetFileName(fp);

        return item.Title;
    }

    // -- Hide -----------------------------------------------------------------

    private void HidePreview()
    {
        _popup.IsOpen = false;
        _hoveredTab   = null;
    }
}
