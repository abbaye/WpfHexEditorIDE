// ==========================================================
// Project: WpfHexEditor.App
// File: StatusBar/NotificationBellAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Keeps the notification bell badge in the status bar in sync with
//     INotificationService.UnreadCount.
//     Badge is hidden when count == 0.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.App.Controls;
using WpfHexEditor.Editor.Core.Notifications;

namespace WpfHexEditor.App.StatusBar;

/// <summary>
/// Wires <see cref="INotificationService.NotificationsChanged"/> to the
/// bell badge controls in the MainWindow title bar.
/// </summary>
internal sealed class NotificationBellAdapter : IDisposable
{
    private readonly INotificationService     _service;
    private readonly Border                   _badge;
    private readonly TextBlock                _badgeText;
    private readonly NotificationCenterPopup  _popup;
    private readonly UIElement                _bellAnchor;

    internal NotificationBellAdapter(
        INotificationService service,
        Border               badgeBorder,
        TextBlock            badgeText,
        UIElement            bellAnchor)
    {
        _service    = service    ?? throw new ArgumentNullException(nameof(service));
        _badge      = badgeBorder;
        _badgeText  = badgeText;
        _bellAnchor = bellAnchor;
        _popup      = new NotificationCenterPopup(service)
        {
            PlacementTarget  = bellAnchor,
            Placement        = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            HorizontalOffset = -320,
            VerticalOffset   = 4,
        };

        // StaysOpen=true — close manually when clicking outside both popup and bell
        _popup.Opened += OnPopupOpened;
        _popup.Closed += OnPopupClosed;

        _service.NotificationsChanged += OnChanged;
        Refresh();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Toggles the notification flyout open/closed.</summary>
    public void TogglePopup()
    {
        if (_popup.IsOpen)
            _popup.IsOpen = false;
        else
        {
            _popup.Rebuild();
            _popup.IsOpen = true;
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (Window.GetWindow(_bellAnchor) is Window w)
            w.PreviewMouseDown += OnWindowPreviewMouseDown;
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (Window.GetWindow(_bellAnchor) is Window w)
            w.PreviewMouseDown -= OnWindowPreviewMouseDown;
    }

    private void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // If the click is on the bell button or inside the popup, do not close.
        if (e.Source is DependencyObject src &&
            (_bellAnchor.IsAncestorOf(src) || _popup.IsAncestorOf(src)))
            return;
        _popup.IsOpen = false;
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        int count = _service.UnreadCount;
        _badge.Visibility  = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _badgeText.Text    = count > 9 ? "9+" : count.ToString();
    }

    public void Dispose()
    {
        _service.NotificationsChanged -= OnChanged;
        _popup.Opened -= OnPopupOpened;
        _popup.Closed -= OnPopupClosed;
    }
}
