// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Terminal/ITerminalOutput.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Output sink contract for terminal command results.
//     Moved from WpfHexEditor.Core.Terminal to the SDK so that
//     plugin authors can implement ITerminalCommandProvider without
//     taking a direct dependency on Core.Terminal.
//
// Architecture Notes:
//     WpfHexEditor.Core.Terminal re-exports this type via a global using alias
//     so all existing built-in commands compile without any changes.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Terminal;

/// <summary>
/// Output sink for terminal command results.
/// Implementations write colored lines to the active Terminal session.
/// </summary>
public interface ITerminalOutput
{
    /// <summary>Writes text without a trailing newline.</summary>
    void Write(string text);

    /// <summary>Writes a standard (white) line. Empty string writes a blank line.</summary>
    void WriteLine(string text = "");

    /// <summary>Writes an error (red) line.</summary>
    void WriteError(string text);

    /// <summary>Writes a warning (amber) line.</summary>
    void WriteWarning(string text);

    /// <summary>Writes an informational (blue) line.</summary>
    void WriteInfo(string text);

    /// <summary>Clears all output from the terminal session.</summary>
    void Clear();
}
