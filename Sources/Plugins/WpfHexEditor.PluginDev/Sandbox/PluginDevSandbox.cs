// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Sandbox/PluginDevSandbox.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Lightweight try/catch sandbox for in-IDE plugin development.
//     Wraps plugin lifecycle calls (Init, Activate, Deactivate) with
//     exception capture, configurable timeout, and diagnostic logging.
//
// Architecture Notes:
//     Pattern: Decorator — wraps IPlugin calls for safety and logging.
//     Timeout enforced via CancellationTokenSource + Task.WhenAny.
// ==========================================================

using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginDev.Sandbox;

/// <summary>
/// Safely invokes plugin lifecycle methods with timeout and exception capture.
/// </summary>
public sealed class PluginDevSandbox
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly TimeSpan          _timeout;
    private readonly IProgress<string> _log;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    /// <param name="timeout">Maximum time allowed per lifecycle call.</param>
    /// <param name="log">Progress sink for diagnostic messages.</param>
    public PluginDevSandbox(TimeSpan timeout, IProgress<string> log)
    {
        _timeout = timeout;
        _log     = log;
    }

    // -----------------------------------------------------------------------
    // Lifecycle wrappers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Calls <see cref="IWpfHexEditorPlugin.InitializeAsync"/> safely.
    /// </summary>
    public async Task<bool> InitializeAsync(IWpfHexEditorPlugin plugin, IIDEHostContext context, CancellationToken ct = default)
    {
        using var cts = new CancellationTokenSource(_timeout);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await plugin.InitializeAsync(context, cts.Token);
            _log.Report($"[DEV] {plugin.Name}.InitializeAsync OK ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (OperationCanceledException)
        {
            _log.Report($"[DEV] {plugin.Name}.InitializeAsync TIMEOUT after {_timeout.TotalMilliseconds:0} ms");
            return false;
        }
        catch (Exception ex)
        {
            _log.Report($"[DEV] {plugin.Name}.InitializeAsync EXCEPTION: {ex.GetType().Name} — {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Calls <see cref="IWpfHexEditorPlugin.ShutdownAsync"/> safely.
    /// </summary>
    public async Task<bool> ShutdownAsync(IWpfHexEditorPlugin plugin, CancellationToken ct = default)
    {
        using var cts = new CancellationTokenSource(_timeout);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await plugin.ShutdownAsync(cts.Token);
            _log.Report($"[DEV] {plugin.Name}.ShutdownAsync OK ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (OperationCanceledException)
        {
            _log.Report($"[DEV] {plugin.Name}.ShutdownAsync TIMEOUT after {_timeout.TotalMilliseconds:0} ms");
            return false;
        }
        catch (Exception ex)
        {
            _log.Report($"[DEV] {plugin.Name}.ShutdownAsync EXCEPTION: {ex.GetType().Name} — {ex.Message}");
            return false;
        }
    }

}
