// ==========================================================
// Project: WpfHexEditor.Core.Terminal
// File: TerminalShellType.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Description:
//     Core-layer enum for shell session types.
//     Mirrors WpfHexEditor.Terminal.TerminalMode but lives in the
//     dependency-free Core layer so ShellSession can reference it.
//
// Architecture Notes:
//     Feature #92: Multi-tab shell sessions.
//     WpfHexEditor.Terminal.TerminalMode maps 1-to-1 to these values.
//
// ==========================================================

namespace WpfHexEditor.Core.Terminal.ShellSession;

/// <summary>
/// The type of shell running in a <see cref="ShellSession"/>.
/// </summary>
public enum TerminalShellType
{
    /// <summary>Built-in HxScriptEngine terminal (no external process).</summary>
    HxTerminal,

    /// <summary>External PowerShell process (pwsh.exe or powershell.exe).</summary>
    PowerShell,

    /// <summary>External Bash shell (bash.exe via WSL or Git Bash).</summary>
    Bash,

    /// <summary>External Windows Command Prompt (cmd.exe).</summary>
    Cmd
}
