// ==========================================================
// Project: WpfHexEditor.SDK
// File: ITerminalService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     SDK contract for plugin-accessible Terminal output and command registration.
//     Plugins use this service to write messages to the IDE Terminal panel and
//     register custom HxTerminal commands without a Core.Terminal dependency.
//
// Architecture Notes:
//     - Write-only surface for output: plugins push lines, they do not pull history.
//     - RegisterCommand / UnregisterCommand delegate to TerminalCommandRegistry
//       via TerminalServiceImpl.SetRegistry(), wired by MainWindow.PluginSystem.
//     - Implemented by TerminalServiceImpl in WpfHexEditor.App; null-object
//       fallback (NullTerminalService) used when Terminal panel is unavailable.
//     - Feature #92: OpenSession / CloseActiveSession enable plugins to manage
//       terminal tabs without a direct WPF dependency.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Provides write access to the IDE Terminal panel for plugin output.
/// Requires <c>WriteTerminal</c> permission declared in the plugin manifest.
/// </summary>
public interface ITerminalService
{
    /// <summary>Writes a standard (white) line to the Terminal panel.</summary>
    void WriteLine(string text);

    /// <summary>Writes an informational (blue) line to the Terminal panel.</summary>
    void WriteInfo(string text);

    /// <summary>Writes a warning (amber) line to the Terminal panel.</summary>
    void WriteWarning(string text);

    /// <summary>Writes an error (red) line to the Terminal panel.</summary>
    void WriteError(string text);

    /// <summary>Clears all output from the Terminal panel.</summary>
    void Clear();

    /// <summary>
    /// Opens a new terminal session tab of the specified shell type.
    /// </summary>
    /// <param name="shellType">
    /// Shell type string: <c>"hx"</c>, <c>"powershell"</c>, <c>"bash"</c>, or <c>"cmd"</c>.
    /// Unrecognised values default to <c>"hx"</c>.
    /// </param>
    void OpenSession(string shellType);

    /// <summary>Closes the currently active terminal session tab.</summary>
    void CloseActiveSession();

    /// <summary>
    /// Registers a custom command with the HxTerminal command registry.
    /// The command is immediately available for dispatch in all open sessions.
    /// Silently ignored when no Terminal panel is open.
    /// Requires <c>RegisterTerminalCommands</c> permission in the plugin manifest.
    /// </summary>
    void RegisterCommand(ITerminalCommandProvider command);

    /// <summary>
    /// Unregisters a previously registered command by name (case-insensitive).
    /// Should be called from the plugin's <c>ShutdownAsync</c> to avoid stale commands.
    /// Silently ignored when no Terminal panel is open or the command is not found.
    /// </summary>
    void UnregisterCommand(string commandName);
}
