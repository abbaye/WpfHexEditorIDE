// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Notifications/INotificationService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     SDK-level contract for the IDE Notification Center.
//     Plugins obtain this via IIDEHostContext.Notifications.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Notifications;

/// <summary>
/// IDE-wide service for posting and managing user-visible notifications.
/// Accessible to plugins via <c>IIDEHostContext.Notifications</c>.
/// </summary>
public interface INotificationService
{
    /// <summary>All currently active (non-dismissed) notifications, newest first.</summary>
    IReadOnlyList<NotificationItem> ActiveNotifications { get; }

    /// <summary>Number of active notifications (drives the bell badge count).</summary>
    int UnreadCount { get; }

    /// <summary>
    /// Posts a notification. If an entry with the same <see cref="NotificationItem.Id"/>
    /// already exists it is replaced in-place (use for progress updates).
    /// </summary>
    void Post(NotificationItem notification);

    /// <summary>Removes the notification with the specified <paramref name="notificationId"/>.</summary>
    void Dismiss(string notificationId);

    /// <summary>Removes all active notifications.</summary>
    void DismissAll();

    /// <summary>
    /// Aggregate download progress across all notifications with <see cref="NotificationItem.IsActiveDownload"/> = true.
    /// <c>null</c> = no active downloads; <c>-1</c> = at least one indeterminate;
    /// <c>0..1</c> = simple average of all determinate downloads.
    /// </summary>
    double? AggregateDownloadProgress { get; }

    /// <summary>
    /// Raised on the WPF Dispatcher thread whenever the active notification list changes
    /// (post, dismiss, or dismiss-all).
    /// </summary>
    event EventHandler? NotificationsChanged;
}
