// ==========================================================
// Project: WpfHexEditor.App
// File: StatusBar/NotificationBellAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6, Claude Opus 4.6
// Created: 2026-03-29
// Description:
//     Keeps the notification bell badge in the status bar in sync with
//     INotificationService.UnreadCount.
//     Badge is hidden when count == 0.
//     Shows a circular progress ring around the bell during active downloads.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.App.Controls;
using WpfHexEditor.Editor.Core.Notifications;
using WpfHexEditor.ProgressBar.Controls;

namespace WpfHexEditor.App.StatusBar;

/// <summary>
/// Wires <see cref="INotificationService.NotificationsChanged"/> to the
/// bell badge controls in the MainWindow title bar.
/// Shows a circular progress arc when downloads are active.
/// </summary>
internal sealed class NotificationBellAdapter : IDisposable
{
    private readonly INotificationService     _service;
    private readonly Border                   _badge;
    private readonly TextBlock                _badgeText;
    private readonly NotificationCenterPopup  _popup;
    private readonly UIElement                _bellAnchor;

    // Progress ring
    private readonly CircularProgressRing     _progressRing;

    internal NotificationBellAdapter(
        INotificationService service,
        Border               badgeBorder,
        TextBlock            badgeText,
        UIElement            bellAnchor,
        Grid                 bellGrid)
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

        // ── Progress ring ─────────────────────────────────────────────────
        _progressRing = new CircularProgressRing
        {
            Width           = 18,
            Height          = 18,
            StrokeThickness = 2,
            Visibility      = Visibility.Collapsed,
        };

        // Insert behind the glyph (index 0)
        bellGrid.Children.Insert(0, _progressRing);

        // ── Events ────────────────────────────────────────────────────────
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
        if (e.Source is DependencyObject src &&
            (_bellAnchor.IsAncestorOf(src) || _popup.IsAncestorOf(src)))
            return;
        _popup.IsOpen = false;
    }

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        // Badge
        int count = _service.UnreadCount;
        _badge.Visibility  = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _badgeText.Text    = count > 9 ? "9+" : count.ToString();

        // Progress ring
        var dlProgress = _service.AggregateDownloadProgress;
        if (dlProgress is null)
        {
            _progressRing.Visibility      = Visibility.Collapsed;
            _progressRing.IsIndeterminate = false;
        }
        else if (dlProgress < 0)
        {
            _progressRing.IsIndeterminate = true;
            _progressRing.Visibility      = Visibility.Visible;
        }
        else
        {
            _progressRing.IsIndeterminate = false;
            _progressRing.Progress        = dlProgress.Value;
            _progressRing.Visibility      = Visibility.Visible;
        }

        // Also refresh popup if open
        if (_popup.IsOpen)
            _popup.Rebuild();
    }

    public void Dispose()
    {
        _service.NotificationsChanged -= OnChanged;
        _popup.Opened -= OnPopupOpened;
        _popup.Closed -= OnPopupClosed;
    }
}
