// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/MarketplaceUpdateScheduler.cs
// Description:
//     Background service that periodically checks for marketplace plugin
//     updates and publishes MarketplaceUpdatesAvailableEvent on the IDE
//     event bus when new versions are found.
//
// Architecture Notes:
//     Runs a single Task loop (no Thread.Sleep — uses Task.Delay with CT).
//     Interval sourced from AppSettings.Marketplace.UpdateCheckIntervalHours.
//     First check fires after 60 s to avoid blocking startup.
//     Disposal cancels the loop cleanly.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Core.Options;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Hosted background service — checks the marketplace for updates on a
/// configurable interval and publishes <see cref="MarketplaceUpdatesAvailableEvent"/>.
/// </summary>
public sealed class MarketplaceUpdateScheduler : IDisposable
{
    private readonly IMarketplaceService _marketplace;
    private readonly IIDEEventBus        _eventBus;
    private readonly AppSettings         _settings;
    private readonly Dispatcher          _dispatcher;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public MarketplaceUpdateScheduler(
        IMarketplaceService marketplace,
        IIDEEventBus        eventBus,
        AppSettings         settings,
        Dispatcher          dispatcher)
    {
        _marketplace = marketplace;
        _eventBus    = eventBus;
        _settings    = settings;
        _dispatcher  = dispatcher;
    }

    /// <summary>Start the background check loop.</summary>
    public void Start()
    {
        if (_loop is not null) return;
        _loop = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    /// <summary>Stop the loop and release resources.</summary>
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    // ── Loop ──────────────────────────────────────────────────────────────────

    private async Task RunLoopAsync(CancellationToken ct)
    {
        // Initial delay — allow IDE to fully boot before first check
        await Task.Delay(TimeSpan.FromSeconds(60), ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            if (_settings.Marketplace.AutoCheckUpdates)
            {
                await CheckAndPublishAsync(ct).ConfigureAwait(false);
            }

            int hours = Math.Max(1, _settings.Marketplace.UpdateCheckIntervalHours);
            await Task.Delay(TimeSpan.FromHours(hours), ct).ConfigureAwait(false);
        }
    }

    private async Task CheckAndPublishAsync(CancellationToken ct)
    {
        try
        {
            var updates = await _marketplace.GetUpdatesAsync(ct).ConfigureAwait(false);
            if (updates.Count == 0) return;

            var evt = new MarketplaceUpdatesAvailableEvent
            {
                UpdateCount = updates.Count,
                ListingIds  = updates.Select(u => u.ListingId).ToArray(),
                Source      = nameof(MarketplaceUpdateScheduler),
            };

            await _dispatcher.InvokeAsync(() => _eventBus.Publish(evt));
        }
        catch (OperationCanceledException) { /* disposed */ }
        catch { /* network errors: silent — retry on next interval */ }
    }
}
