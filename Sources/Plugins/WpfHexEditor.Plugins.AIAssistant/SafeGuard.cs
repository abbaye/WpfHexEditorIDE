// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: SafeGuard.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Defensive execution wrapper. All UI event handlers in the plugin must
//     use SafeGuard.Run() to prevent unhandled exceptions from crashing the IDE.
// ==========================================================
using System.Diagnostics;

namespace WpfHexEditor.Plugins.AIAssistant;

/// <summary>
/// Wraps all plugin actions in try/catch to prevent IDE crashes.
/// Logs errors to Output panel when available, falls back to Debug.
/// </summary>
internal static class SafeGuard
{
    private static Action<string>? _errorLogger;

    /// <summary>Set the error logger (typically context.Output?.Error).</summary>
    internal static void SetLogger(Action<string>? logger) => _errorLogger = logger;

    /// <summary>Wraps a synchronous action — swallows exceptions safely.</summary>
    internal static void Run(Action action, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogError(caller, ex);
        }
    }

    /// <summary>Wraps an async action — swallows exceptions safely.</summary>
    internal static async void RunAsync(Func<Task> action, [System.Runtime.CompilerServices.CallerMemberName] string? caller = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            LogError(caller, ex);
        }
    }

    private static void LogError(string? caller, Exception ex)
    {
        var msg = $"[AIAssistant] Error in {caller}: {ex.GetType().Name}: {ex.Message}";
        _errorLogger?.Invoke(msg);
        Debug.WriteLine(msg);
        Debug.WriteLine(ex.StackTrace);
    }
}
