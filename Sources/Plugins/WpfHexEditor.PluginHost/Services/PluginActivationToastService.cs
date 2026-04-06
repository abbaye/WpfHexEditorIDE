// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/PluginActivationToastService.cs
// Created: 2026-04-06
// Description:
//     Subscribes to PluginActivatingEvent and PluginLoadedEvent on the IDE event bus
//     and posts/updates notification toasts so the user sees a brief "Loading X…"
//     indicator when a lazy/dormant plugin activates.
//
// Architecture Notes:
//     Observer — subscribes to IIDEEventBus, posts to INotificationService.
//     Notification ID pattern: "plugin-activating-{pluginId}" (stable for in-place update).
//     Auto-dismisses "ready" toasts after 3 seconds via a background Task.Delay.
//     Lifetime: created by WpfPluginHost, disposed on host shutdown.
// ==========================================================

using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Editor.Core.Notifications;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Posts IDE notifications when dormant plugins start activating (lazy-load triggers).
/// Shows a brief "Loading X…" indeterminate toast, then updates to "X ready" and auto-dismisses.
/// </summary>
internal sealed class PluginActivationToastService : IDisposable
{
    private readonly IIDEEventBus       _ideEvents;
    private readonly INotificationService _notifications;
    private readonly List<IDisposable>  _subscriptions = [];

    public PluginActivationToastService(
        IIDEEventBus ideEvents,
        INotificationService notifications)
    {
        _ideEvents     = ideEvents     ?? throw new ArgumentNullException(nameof(ideEvents));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

        _subscriptions.Add(_ideEvents.Subscribe<PluginActivatingEvent>(OnPluginActivating));
        _subscriptions.Add(_ideEvents.Subscribe<PluginLoadedEvent>(OnPluginLoaded));
    }

    private void OnPluginActivating(PluginActivatingEvent evt)
    {
        _notifications.Post(new NotificationItem
        {
            Id           = NotificationId(evt.PluginId),
            Title        = $"Loading {evt.PluginName}…",
            Message      = "Plugin activating in background",
            Severity     = NotificationSeverity.Info,
            Progress     = -1,       // indeterminate progress bar
            IsDismissible = false    // prevent accidental close during load
        });
    }

    private void OnPluginLoaded(PluginLoadedEvent evt)
    {
        var id = NotificationId(evt.PluginId);

        // Update in-place: replace loading notification with "ready" confirmation.
        _notifications.Post(new NotificationItem
        {
            Id        = id,
            Title     = $"{evt.PluginName} ready",
            Severity  = NotificationSeverity.Success,
            Progress  = null
        });

        // Auto-dismiss after 3 seconds.
        _ = Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(_ =>
            _notifications.Dismiss(id));
    }

    private static string NotificationId(string pluginId)
        => $"plugin-activating-{pluginId}";

    public void Dispose()
    {
        foreach (var s in _subscriptions)
            s.Dispose();
        _subscriptions.Clear();
    }
}
