// ==========================================================
// Project: WpfHexEditor.SDK
// File: ITerminalService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     SDK contract for plugin-accessible Terminal output.
//     Plugins use this service to write messages to the IDE Terminal panel
//     without taking a direct dependency on WpfHexEditor.Core.Terminal.
//
// Architecture Notes:
//     - Write-only surface: plugins push lines, they do not pull history.
//     - Command registration stays outside the SDK to avoid a circular
//       dependency (Core.Terminal already references SDK via ITerminalContext).
//     - Implemented by TerminalServiceImpl in WpfHexEditor.App; null-object
//       fallback (NullTerminalService) used when Terminal panel is unavailable.
//     - Feature #92: OpenSession / CloseActiveSession enable plugins to manage
//       terminal tabs without a direct WPF dependency.
// ==========================================================

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
}
