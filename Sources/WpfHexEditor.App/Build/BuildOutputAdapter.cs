// ==========================================================
// Project: WpfHexEditor.App
// File: Build/BuildOutputAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Subscribes to build lifecycle events on IDEEventBus and routes
//     all build output lines to the OutputPanel via IOutputService.
//     Also prefixes structured messages ("Build started", "Build succeeded")
//     to make the Build canal readable.
//
// Architecture Notes:
//     Pattern: Observer — listens for IDEEventBus build events.
//     Thread-safety: IOutputService implementations must be thread-safe;
//     IDEEventBus may invoke handlers on a background thread.
// ==========================================================

using WpfHexEditor.Events;
using WpfHexEditor.Events.IDEEvents;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Build;

/// <summary>
/// Routes MSBuild output lines and lifecycle events to the IDE OutputPanel.
/// </summary>
internal sealed class BuildOutputAdapter : IDisposable
{
    private readonly IOutputService  _output;
    private readonly IDisposable[]   _subscriptions;

    // -----------------------------------------------------------------------

    public BuildOutputAdapter(IIDEEventBus eventBus, IOutputService output)
    {
        if (eventBus is null) throw new ArgumentNullException(nameof(eventBus));
        _output = output ?? throw new ArgumentNullException(nameof(output));

        _subscriptions =
        [
            eventBus.Subscribe<BuildStartedEvent>   (OnBuildStarted),
            eventBus.Subscribe<BuildOutputLineEvent>(OnOutputLine),
            eventBus.Subscribe<BuildSucceededEvent> (OnBuildSucceeded),
            eventBus.Subscribe<BuildFailedEvent>    (OnBuildFailed),
            eventBus.Subscribe<BuildCancelledEvent> (OnBuildCancelled),
        ];
    }

    // -----------------------------------------------------------------------
    // Handlers
    // -----------------------------------------------------------------------

    private void OnBuildStarted(BuildStartedEvent e)
        => _output.Write("Build", "========== Build started ==========");

    private void OnOutputLine(BuildOutputLineEvent e)
        => _output.Write("Build", e.Line);

    private void OnBuildSucceeded(BuildSucceededEvent e)
        => _output.Write("Build",
            $"========== Build succeeded — {e.WarningCount} warning(s)  {e.Duration.TotalSeconds:F1}s ==========");

    private void OnBuildFailed(BuildFailedEvent e)
        => _output.Write("Build",
            $"========== Build FAILED — {e.ErrorCount} error(s), {e.Warnings} warning(s)  {e.Duration.TotalSeconds:F1}s ==========");

    private void OnBuildCancelled(BuildCancelledEvent e)
        => _output.Write("Build", "========== Build cancelled ==========");

    // -----------------------------------------------------------------------

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
    }
}
