// ==========================================================
// Project: WpfHexEditor.SDK
// File: IOutputService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Plugin-facing service for writing log messages to the IDE OutputPanel.
//     Provides categorized output (info, warning, error, debug).
//
// Architecture Notes:
//     Implemented by OutputServiceImpl in App/Services, wrapping the OutputPanel.
//     All writes are marshaled to the UI thread if called from a background thread.
//     Requires PluginPermission.WriteOutput to be granted.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Provides access to the IDE OutputPanel for plugin log messages.
/// Requires <c>WriteOutput</c> permission.
/// </summary>
public interface IOutputService
{
    /// <summary>Writes an informational message to the OutputPanel.</summary>
    /// <param name="message">Message text.</param>
    void Info(string message);

    /// <summary>Writes a warning message to the OutputPanel.</summary>
    void Warning(string message);

    /// <summary>Writes an error message to the OutputPanel (displayed in red).</summary>
    void Error(string message);

    /// <summary>Writes a debug-level message (only visible when debug output is enabled).</summary>
    void Debug(string message);

    /// <summary>
    /// Writes a message with explicit category label (e.g. plugin name prefix).
    /// </summary>
    /// <param name="category">Category prefix shown in the output line (e.g. "MyPlugin").</param>
    /// <param name="message">Message text.</param>
    void Write(string category, string message);

    /// <summary>Clears all messages from the OutputPanel.</summary>
    void Clear();
}
