// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/TerminalCommandExecutedEvent.cs
// Created: 2026-03-15
// Description:
//     Published when a command is executed in the integrated terminal.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>Published when a command is executed in the integrated terminal.</summary>
public sealed record TerminalCommandExecutedEvent : IDEEventBase
{
    public string Command { get; init; } = string.Empty;
    public string ShellType { get; init; } = string.Empty;
}
