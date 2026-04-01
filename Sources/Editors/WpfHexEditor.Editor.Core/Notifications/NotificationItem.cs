// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Notifications/NotificationItem.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Model classes for the IDE Notification Center.
//     NotificationItem is immutable (record); actions are Func<Task> delegates
//     so callers can post async work (downloads, etc.) without blocking the UI.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Notifications;

/// <summary>Visual severity of a notification entry.</summary>
public enum NotificationSeverity { Info, Warning, Error, Success }

/// <summary>
/// An actionable button shown inside a <see cref="NotificationItem"/>.
/// </summary>
/// <param name="Label">Button label displayed in the popup.</param>
/// <param name="ExecuteAsync">Async callback invoked on click.</param>
/// <param name="IsDefault">When <c>true</c> the button is rendered with accent styling.</param>
public sealed record NotificationAction(
    string       Label,
    Func<Task>   ExecuteAsync,
    bool         IsDefault = false);

/// <summary>
/// Immutable notification entry posted to <see cref="INotificationService"/>.
/// Posting a new item with the same <see cref="Id"/> replaces the existing one
/// (use this pattern to update progress messages in-place).
/// </summary>
public sealed record NotificationItem
{
    /// <summary>
    /// Stable identifier used for in-place updates and programmatic dismissal.
    /// Must be unique per logical notification (e.g. "lsp-first-run").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Short one-line title shown in bold.</summary>
    public required string Title { get; init; }

    /// <summary>Optional descriptive body text shown below the title.</summary>
    public string? Message { get; init; }

    /// <summary>Determines the icon and accent colour in the popup.</summary>
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;

    /// <summary>Action buttons rendered below the message.</summary>
    public IReadOnlyList<NotificationAction> Actions { get; init; } = [];

    /// <summary>UTC timestamp recorded when the item was first posted.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When <c>false</c> the × dismiss button is hidden.
    /// Use for in-progress operations that must not be accidentally closed.
    /// </summary>
    public bool IsDismissible { get; init; } = true;

    /// <summary>
    /// Operation progress indicator.
    /// <c>null</c> = no progress bar; <c>-1</c> = indeterminate;
    /// <c>0.0–1.0</c> = determinate percentage.
    /// </summary>
    public double? Progress { get; init; }

    /// <summary>
    /// When <c>true</c> this notification contributes to the aggregate
    /// download progress shown on the titlebar bell icon.
    /// </summary>
    public bool IsActiveDownload { get; init; }
}
