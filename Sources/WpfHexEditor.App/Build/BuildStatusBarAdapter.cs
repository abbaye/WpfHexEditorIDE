// ==========================================================
// Project: WpfHexEditor.App
// File: Build/BuildStatusBarAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Listens to build lifecycle events and updates the StatusBar
//     "Build status" indicator in MainWindow.
//     Must be created and used on the WPF dispatcher thread.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to IDEEventBus build events.
//     UI updates are dispatched back to the WPF thread via the
//     provided Action<> callbacks (setter injected by MainWindow).
// ==========================================================

using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;

namespace WpfHexEditor.App.Build;

/// <summary>
/// Updates the MainWindow StatusBar build indicator in response to build events.
/// </summary>
internal sealed class BuildStatusBarAdapter : IDisposable
{
    private readonly IDisposable[]                    _subscriptions;
    private readonly Action<string, string, bool, int> _update;  // (text, icon, visible, progressPercent: 0–100 | -1=hide)

    // -----------------------------------------------------------------------

    /// <summary>
    /// Initialises the adapter.
    /// </summary>
    /// <param name="eventBus">IDE event bus.</param>
    /// <param name="updateStatusBar">
    /// Callback invoked (on any thread) with (statusText, mdl2Icon, isVisible).
    /// The implementation must marshal to the WPF thread internally.
    /// </param>
    public BuildStatusBarAdapter(IIDEEventBus eventBus, Action<string, string, bool, int> updateStatusBar)
    {
        if (eventBus is null) throw new ArgumentNullException(nameof(eventBus));
        _update = updateStatusBar ?? throw new ArgumentNullException(nameof(updateStatusBar));

        _subscriptions =
        [
            eventBus.Subscribe<BuildStartedEvent>   (OnBuildStarted),
            eventBus.Subscribe<BuildProgressUpdatedEvent>(OnProgress),
            eventBus.Subscribe<BuildSucceededEvent> (OnBuildSucceeded),
            eventBus.Subscribe<BuildFailedEvent>    (OnBuildFailed),
            eventBus.Subscribe<BuildCancelledEvent> (OnBuildCancelled),
        ];
    }

    // -----------------------------------------------------------------------
    // Handlers
    // -----------------------------------------------------------------------

    private void OnBuildStarted(BuildStartedEvent _)
        => _update("Building...", "\uE8B1", true, 0);

    private void OnProgress(BuildProgressUpdatedEvent e)
        => _update($"Building... {e.StatusText}", "\uE8B1", true, e.ProgressPercent);

    private void OnBuildSucceeded(BuildSucceededEvent e)
        => _update($"Build succeeded  ({e.WarningCount} warning(s))  {e.Duration.TotalSeconds:F1}s", "\uE930", true, -1);

    private void OnBuildFailed(BuildFailedEvent e)
        => _update($"Build failed  ({e.ErrorCount} error(s))  {e.Duration.TotalSeconds:F1}s", "\xEA39", true, -1);

    private void OnBuildCancelled(BuildCancelledEvent _)
        => _update("Build cancelled", "\uE711", true, -1);

    // -----------------------------------------------------------------------

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
    }
}
