// ==========================================================
// Project: WpfHexEditor.Docking.Wpf
// File: AutoHideBarHoverPreview.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Provides a lightweight hover-preview Popup for AutoHideBar panel buttons.
//     After a configurable delay (default 400 ms) a small thumbnail of the panel
//     is shown in a Popup beside the hovered button.
//     The thumbnail is supplied by the host (DockControl) via a bitmap provider
//     callback — it is captured lazily when the flyout last closed.
//     When no cached bitmap is available, a title-only card is shown as fallback.
//
// Architecture Notes:
//     Observer Pattern — subscribes to MouseEnter/MouseLeave on each bar button.
//     Lazy Snapshot   — bitmap captured externally (DockControl) at flyout close.
//     Singleton per AutoHideBar — attach with AutoHideBarHoverPreview.Attach().
//
// Theme: DockMenuBackgroundBrush / DockBorderBrush (100 % DynamicResource)
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Wpf.Controls;

/// <summary>
/// Attaches hover-preview behaviour to an <see cref="AutoHideBar"/>.
/// Call <see cref="Attach"/> once per bar, passing a bitmap-provider delegate
/// that returns the last-captured snapshot for a given <see cref="DockItem"/>.
/// </summary>
public sealed class AutoHideBarHoverPreview
{
    // ── Configuration ────────────────────────────────────────────────────────
    private const double PreviewWidth  = 200;
    private const double PreviewHeight = 150;
    private const int    HoverDelayMs  = 400;

    // ── State ────────────────────────────────────────────────────────────────
    private readonly AutoHideBar                    _owner;
    private readonly Func<DockItem, BitmapSource?>  _bitmapProvider;
    private readonly Popup                          _popup;
    private readonly Border                         _border;
    private readonly Image                          _previewImage;
    private readonly TextBlock                      _titleBlock;
    private readonly DispatcherTimer                _hoverTimer;

    private Button?   _hoveredButton;
    private DockItem? _hoveredItem;

    // ── Constructor ──────────────────────────────────────────────────────────

    private AutoHideBarHoverPreview(AutoHideBar owner, Func<DockItem, BitmapSource?> bitmapProvider)
    {
        _owner          = owner;
        _bitmapProvider = bitmapProvider;

        // ── Preview image ────────────────────────────────────────────────────
        _previewImage = new Image
        {
            Width               = PreviewWidth,
            Height              = PreviewHeight,
            Stretch             = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top
        };

        // ── Title label (shown when no bitmap) ──────────────────────────────
        _titleBlock = new TextBlock
        {
            FontWeight        = FontWeights.SemiBold,
            FontSize          = 11,
            Margin            = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var stack = new StackPanel();
        stack.Children.Add(_previewImage);
        stack.Children.Add(_titleBlock);

        // ── Themed border + shadow ───────────────────────────────────────────
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

        // ── Popup ────────────────────────────────────────────────────────────
        _popup = new Popup
        {
            Child              = _border,
            AllowsTransparency = true,
            Placement          = PlacementMode.Mouse,
            StaysOpen          = false,
            IsHitTestVisible   = false   // pointer events pass through
        };

        // ── Hover timer ──────────────────────────────────────────────────────
        _hoverTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(HoverDelayMs)
        };
        _hoverTimer.Tick += OnHoverTimerTick;

        // Hide when mouse leaves the bar entirely
        owner.MouseLeave += (_, _) => HidePreview();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a hover-preview to every button in <paramref name="bar"/>.
    /// <paramref name="bitmapProvider"/> should return the last-captured snapshot
    /// for the given <see cref="DockItem"/>, or <c>null</c> if none is available yet.
    /// </summary>
    public static AutoHideBarHoverPreview Attach(AutoHideBar bar, Func<DockItem, BitmapSource?> bitmapProvider)
    {
        var preview = new AutoHideBarHoverPreview(bar, bitmapProvider);

        // Re-wire on every UpdateItems call (bar rebuilds its children).
        bar.ItemsUpdated += preview.WireButtons;
        preview.WireButtons();

        return preview;
    }

    // ── Wiring ───────────────────────────────────────────────────────────────

    private void WireButtons()
    {
        foreach (UIElement child in _owner.Children)
        {
            if (child is Button btn && btn.Tag is DockItem item)
                WireButton(btn, item);
        }
    }

    private void WireButton(Button btn, DockItem item)
    {
        // Guard: tag with sentinel to avoid double-subscription
        if (btn.Tag is string s && s.Contains("__phovered__")) return;

        btn.MouseEnter += (_, _) => OnButtonMouseEnter(btn, item);
        btn.MouseLeave += (_, _) => OnButtonMouseLeave();
    }

    // ── Hover handlers ───────────────────────────────────────────────────────

    private void OnButtonMouseEnter(Button btn, DockItem item)
    {
        _hoveredButton = btn;
        _hoveredItem   = item;
        _popup.PlacementTarget = btn;
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void OnButtonMouseLeave()
    {
        _hoverTimer.Stop();
        HidePreview();
    }

    private void OnHoverTimerTick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (_hoveredItem is null || _hoveredButton is null) return;

        var bitmap = _bitmapProvider(_hoveredItem);

        _titleBlock.Text      = _hoveredItem.Title;
        _previewImage.Source  = bitmap;
        _previewImage.Visibility = bitmap is null ? Visibility.Collapsed : Visibility.Visible;

        _popup.IsOpen = true;
    }

    // ── Hide ─────────────────────────────────────────────────────────────────

    private void HidePreview()
    {
        _popup.IsOpen  = false;
        _hoveredButton = null;
        _hoveredItem   = null;
    }
}
