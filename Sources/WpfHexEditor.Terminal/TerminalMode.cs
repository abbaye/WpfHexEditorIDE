// ==========================================================
// Project: WpfHexEditor.Terminal
// File: TerminalMode.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Defines the available shell modes for the Terminal panel.
//
// Architecture Notes:
//     Separate enum file to keep TerminalPanelViewModel focused
//     on behaviour rather than type declarations.
//     Feature #92: Added Bash and Cmd external shell modes.
//
// ==========================================================

namespace WpfHexEditor.Terminal;

/// <summary>
/// The shell mode active in a Terminal session tab.
/// </summary>
public enum TerminalMode
{
    /// <summary>Built-in HxScriptEngine terminal.</summary>
    HxTerminal,

    /// <summary>External PowerShell process (pwsh.exe or powershell.exe).</summary>
    PowerShell,

    /// <summary>External Bash process (bash.exe via WSL or Git Bash).</summary>
    Bash,

    /// <summary>External Windows Command Prompt (cmd.exe).</summary>
    Cmd
}
