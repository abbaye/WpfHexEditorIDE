//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Arguments for the <see cref="PluginWatchdog.PluginNonResponsive"/> event.
/// </summary>
public sealed class PluginNonResponsiveEventArgs : EventArgs
{
    /// <summary>Plugin identifier that timed out.</summary>
    public string PluginId { get; init; } = string.Empty;

    /// <summary>Operation that timed out.</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>Configured timeout that was exceeded.</summary>
    public TimeSpan Timeout { get; init; }
}

/// <summary>
/// Provides timeout-bounded execution for plugin calls.
/// Raises <see cref="PluginNonResponsive"/> when a call exceeds its timeout.
/// </summary>
internal sealed class PluginWatchdog
{
    private static readonly TimeSpan DefaultInitTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>Raised when a plugin call exceeds its timeout threshold.</summary>
    public event EventHandler<PluginNonResponsiveEventArgs>? PluginNonResponsive;

    /// <summary>
    /// Executes a plugin async operation with a timeout guard.
    /// Returns the result or throws <see cref="TimeoutException"/> if exceeded.
    /// </summary>
    /// <param name="pluginId">Plugin identifier (for event reporting).</param>
    /// <param name="operation">Operation name (for diagnostics).</param>
    /// <param name="task">The async operation to wrap.</param>
    /// <param name="timeout">Timeout (null = use default per-call timeout).</param>
    /// <summary>
    /// Executes a plugin async operation with a timeout guard.
    /// Returns the measured wall-clock execution time, or throws <see cref="TimeoutException"/> if exceeded.
    /// </summary>
    public async Task<TimeSpan> WrapAsync(string pluginId, string operation, Task task, TimeSpan? timeout = null)
    {
        TimeSpan limit = timeout ?? DefaultCallTimeout;
        using var cts  = new CancellationTokenSource(limit);

        var sw          = System.Diagnostics.Stopwatch.StartNew();
        var timeoutTask = Task.Delay(limit, cts.Token);
        var completed   = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);
        sw.Stop();

        if (completed == timeoutTask)
        {
            PluginNonResponsive?.Invoke(this, new PluginNonResponsiveEventArgs
            {
                PluginId  = pluginId,
                Operation = operation,
                Timeout   = limit
            });
            throw new TimeoutException($"Plugin '{pluginId}' did not complete '{operation}' within {limit.TotalSeconds:F1}s.");
        }

        // Cancel the timeout task to avoid resource leak.
        await cts.CancelAsync().ConfigureAwait(false);

        // Re-throw any exception from the plugin's task.
        await task.ConfigureAwait(false);

        return sw.Elapsed;
    }

    /// <summary>
    /// Timeout to use for InitializeAsync calls (longer, default 5s).
    /// </summary>
    public TimeSpan InitTimeout { get; set; } = DefaultInitTimeout;

    /// <summary>
    /// Timeout to use for per-call plugin operations (short, default 200ms).
    /// </summary>
    public TimeSpan CallTimeout { get; set; } = DefaultCallTimeout;
}
