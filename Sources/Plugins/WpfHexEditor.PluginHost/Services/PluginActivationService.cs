// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Services/PluginActivationService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Watches IDE events (FileOpened, command triggers) and activates
//     dormant plugins whose activation config matches the trigger.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to IIDEEventBus and delegates
//     actual loading to WpfPluginHost via the provided callback.
//     Already-activated plugin IDs are tracked to prevent re-load.
//     Activation is fire-and-forget with exception isolation so one
//     failed activation cannot crash others.
// ==========================================================

using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Activates dormant plugins in response to IDE events.
/// </summary>
internal sealed class PluginActivationService : IDisposable
{
    private readonly IIDEEventBus _ideEvents;
    private readonly Func<string, Task> _activateAsync;
    private readonly IReadOnlyDictionary<string, PluginEntry> _entries;
    private readonly HashSet<string> _activatedIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    // Keep subscriptions alive.
    private readonly List<IDisposable> _subscriptions = [];

    public PluginActivationService(
        IIDEEventBus ideEvents,
        IReadOnlyDictionary<string, PluginEntry> entries,
        Func<string, Task> activateAsync)
    {
        _ideEvents    = ideEvents    ?? throw new ArgumentNullException(nameof(ideEvents));
        _entries      = entries      ?? throw new ArgumentNullException(nameof(entries));
        _activateAsync = activateAsync ?? throw new ArgumentNullException(nameof(activateAsync));

        _subscriptions.Add(_ideEvents.Subscribe<FileOpenedEvent>(OnFileOpened));
    }

    private void OnFileOpened(FileOpenedEvent evt)
    {
        var ext = evt.FileExtension?.ToLowerInvariant() ?? string.Empty;
        foreach (var entry in _entries.Values)
        {
            if (entry.State != SDK.Models.PluginState.Dormant)
                continue;

            var activation = entry.Manifest.Activation;
            if (activation is null) continue;

            bool matches = activation.FileExtensions.Any(
                fe => string.Equals(fe, ext, StringComparison.OrdinalIgnoreCase));
            if (!matches) continue;

            ActivateIfNeeded(entry.Manifest.Id);
        }
    }

    /// <summary>
    /// Triggers activation of a dormant plugin by ID (e.g., from "Load Now" button).
    /// </summary>
    public void ActivateById(string pluginId) => ActivateIfNeeded(pluginId);

    private void ActivateIfNeeded(string pluginId)
    {
        lock (_lock)
        {
            if (_activatedIds.Contains(pluginId)) return;
            _activatedIds.Add(pluginId);
        }

        // Fire-and-forget with exception isolation.
        Task.Run(async () =>
        {
            try { await _activateAsync(pluginId).ConfigureAwait(false); }
            catch
            {
                // Re-allow re-activation on failure.
                lock (_lock) { _activatedIds.Remove(pluginId); }
            }
        });
    }

    public void Dispose()
    {
        foreach (var s in _subscriptions)
            s.Dispose();
        _subscriptions.Clear();
    }
}
