// ==========================================================
// Project: WpfHexEditor.App
// File: Services/NotificationServiceImpl.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Thread-safe implementation of INotificationService.
//     Always raises NotificationsChanged on the WPF Dispatcher thread.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.Editor.Core.Notifications;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Concrete <see cref="INotificationService"/> used by the IDE shell.
/// </summary>
internal sealed class NotificationServiceImpl : INotificationService
{
    private readonly Dispatcher               _dispatcher;
    private readonly List<NotificationItem>   _items = [];
    private readonly object                   _lock  = new();

    public NotificationServiceImpl(Dispatcher dispatcher)
        => _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    // ── INotificationService ─────────────────────────────────────────────────

    public IReadOnlyList<NotificationItem> ActiveNotifications
    {
        get { lock (_lock) return _items.ToList(); }
    }

    public int UnreadCount
    {
        get { lock (_lock) return _items.Count; }
    }

    public void Post(NotificationItem notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        lock (_lock)
        {
            var idx = _items.FindIndex(i => i.Id == notification.Id);
            if (idx >= 0)
                _items[idx] = notification;   // in-place update (progress)
            else
                _items.Insert(0, notification); // newest first
        }
        RaiseChanged();
    }

    public void Dismiss(string notificationId)
    {
        lock (_lock)
            _items.RemoveAll(i => i.Id == notificationId);
        RaiseChanged();
    }

    public void DismissAll()
    {
        lock (_lock) _items.Clear();
        RaiseChanged();
    }

    public event EventHandler? NotificationsChanged;

    // ── Private ──────────────────────────────────────────────────────────────

    private void RaiseChanged()
    {
        if (_dispatcher.CheckAccess())
            NotificationsChanged?.Invoke(this, EventArgs.Empty);
        else
            _dispatcher.InvokeAsync(() => NotificationsChanged?.Invoke(this, EventArgs.Empty));
    }
}
