//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Event arguments for the plugin faulted notification.
/// </summary>
public sealed class PluginFaultedEventArgs : EventArgs
{
    /// <summary>Plugin identifier that faulted.</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>Plugin display name.</summary>
    public string PluginName { get; init; } = string.Empty;

    /// <summary>Exception that caused the fault.</summary>
    public Exception Exception { get; init; } = null!;

    /// <summary>Phase during which the fault occurred (e.g. "InitializeAsync", "ShutdownAsync").</summary>
    public string Phase { get; init; } = string.Empty;
}

/// <summary>
/// Handles plugin crash events â€” marks Faulted state and raises notifications.
/// </summary>
internal sealed class PluginCrashHandler
{
    /// <summary>
    /// Raised when a plugin is marked Faulted.
    /// Raised on the calling thread â€” MainWindow subscribes to show InfoBar.
    /// </summary>
    public event EventHandler<PluginFaultedEventArgs>? PluginFaulted;

    /// <summary>
    /// Handles a plugin fault: updates entry state, raises PluginFaulted event.
    /// </summary>
    /// <param name="entry">Plugin entry to mark as Faulted.</param>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="phase">Phase name where the fault occurred.</param>
    public void HandleCrash(PluginEntry entry, Exception exception, string phase)
    {
        entry.SetState(PluginState.Faulted);
        entry.SetFaultException(exception);

        PluginFaulted?.Invoke(this, new PluginFaultedEventArgs
        {
            PluginId = entry.Manifest.Id,
            PluginName = entry.Manifest.Name,
            Exception = exception,
            Phase = phase
        });
    }

    /// <summary>
    /// Async wrapper over <see cref="HandleCrash"/> — marshals the event to the UI
    /// dispatcher when called from a background thread.
    /// </summary>
    public Task HandleCrashAsync(PluginEntry entry, Exception exception, string phase)
    {
        HandleCrash(entry, exception, phase);
        return Task.CompletedTask;
    }
}
