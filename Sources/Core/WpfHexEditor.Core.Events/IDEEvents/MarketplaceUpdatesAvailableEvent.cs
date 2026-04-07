// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/MarketplaceUpdatesAvailableEvent.cs
// Description:
//     IDE event raised by MarketplaceUpdateScheduler when a background
//     update check finds new plugin versions.
//
// Architecture Notes:
//     Published on the UI thread (Dispatcher) so consumers can update
//     status-bar badges and show startup toasts without marshalling.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>
/// Published when the background marketplace update check finds plugins
/// with new versions available. Triggers the status bar badge and startup toast.
/// </summary>
public sealed record MarketplaceUpdatesAvailableEvent : IDEEventBase
{
    /// <summary>Number of plugins with updates.</summary>
    public int UpdateCount { get; init; }

    /// <summary>IDs of the listings with available updates.</summary>
    public IReadOnlyList<string> ListingIds { get; init; } = [];
}
