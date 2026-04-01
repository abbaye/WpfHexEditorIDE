// ==========================================================
// Project: WpfHexEditor.App
// File: Controls/NotificationCenterPopup.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Flyout popup that displays active IDE notifications.
//     Code-behind only (no XAML). Anchors to the notification bell button.
//
// Architecture Notes:
//     Each NotificationItem is rendered as a card:
//       [severity icon]  Title (bold)
//                        Message (optional, gray)
//                        [Action btn…]  [×]
//     Tokens: DockBackgroundBrush, DockMenuForegroundBrush, DockBorderBrush.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using WpfHexEditor.Editor.Core.Notifications;

namespace WpfHexEditor.App.Controls;

/// <summary>
/// Flyout notification center — shows all active <see cref="NotificationItem"/>s
/// with action buttons and dismiss controls.
/// </summary>
internal sealed class NotificationCenterPopup : Popup
{
    private readonly INotificationService _service;
    private readonly ScrollViewer         _scroll;
    private readonly StackPanel           _list;

    public NotificationCenterPopup(INotificationService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        StaysOpen        = true;   // closed manually via Mouse.AddPreviewMouseDownOutsidePopupHandler
        AllowsTransparency = true;
        PopupAnimation   = PopupAnimation.Fade;
        Width            = 360;

        // ── Header ─────────────────────────────────────────────────────────
        var clearAllBtn = new Button
        {
            Content    = "Clear all",
            Padding    = new Thickness(8, 2, 8, 2),
            FontSize   = 11,
            Cursor     = System.Windows.Input.Cursors.Hand,
        };
        clearAllBtn.Click += (_, _) => { _service.DismissAll(); Rebuild(); };
        clearAllBtn.SetResourceReference(Button.ForegroundProperty, "DockMenuForegroundBrush");
        clearAllBtn.SetResourceReference(Button.BackgroundProperty, "DockBackgroundBrush");
        clearAllBtn.SetResourceReference(Button.BorderBrushProperty, "DockBorderBrush");

        var header = new DockPanel { Margin = new Thickness(8, 6, 8, 4) };
        DockPanel.SetDock(clearAllBtn, Dock.Right);
        header.Children.Add(clearAllBtn);
        header.Children.Add(new TextBlock
        {
            Text       = "Notifications",
            FontWeight = FontWeights.SemiBold,
            FontSize   = 12,
            VerticalAlignment = VerticalAlignment.Center,
        });
        header.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        // ── List ───────────────────────────────────────────────────────────
        _list   = new StackPanel();
        _scroll = new ScrollViewer
        {
            Content            = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight          = 480,
        };

        // ── Root border ────────────────────────────────────────────────────
        var separator = new Border { Height = 1, Margin = new Thickness(0, 4, 0, 4) };
        separator.SetResourceReference(Border.BackgroundProperty, "DockBorderBrush");

        var root = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Padding         = new Thickness(0, 0, 0, 4),
            Effect          = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8, Opacity = 0.3, ShadowDepth = 2, Direction = 270,
            },
        };
        root.SetResourceReference(Border.BackgroundProperty,   "DockBackgroundBrush");
        root.SetResourceReference(Border.BorderBrushProperty,  "DockBorderBrush");

        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(separator);
        stack.Children.Add(_scroll);
        root.Child = stack;

        Child = root;
        Rebuild();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Rebuild()
    {
        _list.Children.Clear();
        var items = _service.ActiveNotifications;
        if (items.Count == 0)
        {
            var empty = new TextBlock
            {
                Text     = "No notifications",
                FontSize = 11,
                Margin   = new Thickness(12, 8, 12, 8),
                Opacity  = 0.6,
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            _list.Children.Add(empty);
            return;
        }

        foreach (var item in items)
            _list.Children.Add(BuildCard(item));
    }

    // ── Card builder ──────────────────────────────────────────────────────────

    private Border BuildCard(NotificationItem item)
    {
        // Severity icon + color
        var (glyph, colorHex) = item.Severity switch
        {
            NotificationSeverity.Warning => ("\uE7BA", "#F0A30A"),  // Warning
            NotificationSeverity.Error   => ("\uE783", "#E81123"),  // Error badge
            NotificationSeverity.Success => ("\uE73E", "#107C10"),  // CheckMark
            _                            => ("\uE946", "#0078D4"),  // Info
        };

        var icon = new TextBlock
        {
            Text            = glyph,
            FontFamily      = new FontFamily("Segoe MDL2 Assets"),
            FontSize        = 14,
            Foreground      = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)),
            VerticalAlignment = VerticalAlignment.Top,
            Margin          = new Thickness(0, 1, 8, 0),
        };

        var titleBlock = new TextBlock
        {
            Text       = item.Title,
            FontWeight = FontWeights.SemiBold,
            FontSize   = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var body = new StackPanel();
        body.Children.Add(titleBlock);

        if (!string.IsNullOrEmpty(item.Message))
        {
            var msg = new TextBlock
            {
                Text        = item.Message,
                FontSize    = 11,
                TextWrapping = TextWrapping.Wrap,
                Opacity     = 0.75,
                Margin      = new Thickness(0, 2, 0, 0),
            };
            msg.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            body.Children.Add(msg);
        }

        // Progress bar
        if (item.Progress is { } progress)
        {
            var bar = new WpfHexEditor.ProgressBar.Controls.LinearProgressBar
            {
                Height  = 4,
                Margin  = new Thickness(0, 6, 0, 0),
            };

            if (progress < 0)
                bar.IsIndeterminate = true;
            else
                bar.Progress = progress;

            body.Children.Add(bar);
        }

        // Action buttons
        if (item.Actions.Count > 0)
        {
            var actionsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin      = new Thickness(0, 6, 0, 0),
            };
            foreach (var action in item.Actions)
            {
                var btn = new Button
                {
                    Content = action.Label,
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin  = new Thickness(0, 0, 6, 0),
                    FontSize = 11,
                    Cursor  = System.Windows.Input.Cursors.Hand,
                };
                if (action.IsDefault)
                {
                    btn.SetResourceReference(Button.BackgroundProperty, "DockAccentBrush");
                    btn.Foreground = Brushes.White;
                }
                else
                {
                    btn.SetResourceReference(Button.ForegroundProperty, "DockMenuForegroundBrush");
                    btn.SetResourceReference(Button.BackgroundProperty, "DockBackgroundBrush");
                    btn.SetResourceReference(Button.BorderBrushProperty, "DockBorderBrush");
                }
                var captured = action;
                btn.Click += async (_, _) =>
                {
                    btn.IsEnabled = false;
                    await captured.ExecuteAsync();
                    Rebuild();
                };
                actionsRow.Children.Add(btn);
            }
            body.Children.Add(actionsRow);
        }

        // Row with icon + body
        var row = new DockPanel { Margin = new Thickness(0) };
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(body);

        // Dismiss button (×)
        if (item.IsDismissible)
        {
            var dismissBtn = new Button
            {
                Content  = "\uE711",   // Cancel / × glyph
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Padding  = new Thickness(4),
                Width    = 22, Height = 22,
                VerticalAlignment   = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor   = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
                Opacity  = 0.6,
            };
            dismissBtn.SetResourceReference(Button.ForegroundProperty, "DockMenuForegroundBrush");
            dismissBtn.SetResourceReference(Button.BackgroundProperty, "DockBackgroundBrush");
            var capturedId = item.Id;
            dismissBtn.Click += (_, _) => { _service.Dismiss(capturedId); Rebuild(); };

            var outerRow = new DockPanel();
            DockPanel.SetDock(dismissBtn, Dock.Right);
            outerRow.Children.Add(dismissBtn);
            outerRow.Children.Add(row);

            row = outerRow;
        }

        var card = new Border
        {
            Child           = row,
            Padding         = new Thickness(10, 8, 10, 8),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        card.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");
        return card;
    }
}

