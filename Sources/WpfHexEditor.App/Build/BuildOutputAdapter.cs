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

using System.Windows.Media;
using WpfHexEditor.Core.Events;
using WpfHexEditor.Core.Events.IDEEvents;
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
        => OutputLogger.BuildRaw("========== Build started ==========");

    private void OnOutputLine(BuildOutputLineEvent e)
    {
        if (e.IsError)
            OutputLogger.BuildError(e.Line);
        else
            _output.Write("Build", e.Line);
    }

    private static readonly Brush _successBrush = MakeBrush(78, 201, 176);
    private static readonly Brush _errorBrush   = MakeBrush(240,  80,  60);

    private static Brush MakeBrush(byte r, byte g, byte b)
    {
        var b2 = new SolidColorBrush(Color.FromRgb(r, g, b));
        b2.Freeze();
        return b2;
    }

    private void OnBuildSucceeded(BuildSucceededEvent e)
    {
        var finishedAt = e.StartedAt + e.Duration;
        OutputLogger.BuildRaw(
            $"========== Build: {e.SucceededCount} succeeded, {e.FailedCount} failed, {e.SkippedCount} up-to-date, 0 skipped ==========",
            _successBrush);
        OutputLogger.BuildRaw(
            $"========== Build finished at {finishedAt:HH:mm} and took {e.Duration.TotalSeconds:00.000} seconds ==========");
    }

    private void OnBuildFailed(BuildFailedEvent e)
    {
        var finishedAt = e.StartedAt + e.Duration;
        OutputLogger.BuildRaw(
            $"========== Build: {e.SucceededCount} succeeded, {e.FailedCount} failed, {e.SkippedCount} up-to-date, 0 skipped ==========",
            _errorBrush);
        OutputLogger.BuildRaw(
            $"========== Build finished at {finishedAt:HH:mm} and took {e.Duration.TotalSeconds:00.000} seconds ==========");
    }

    private void OnBuildCancelled(BuildCancelledEvent e)
        => OutputLogger.BuildRaw("========== Build cancelled ==========");

    // -----------------------------------------------------------------------

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
    }
}
