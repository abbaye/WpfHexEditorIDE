// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/CommandInvokedEvent.cs
// Created: 2026-04-06
// Description:
//     Published by MainWindow.Commands Reg() helper whenever a built-in IDE
//     command is executed. Used by PluginActivationService to lazy-load dormant
//     plugins that declare command-based activation triggers.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>
/// Published on the IDE event bus immediately before a registered command executes.
/// </summary>
public sealed record CommandInvokedEvent : IDEEventBase
{
    /// <summary>The command ID as registered in CommandRegistry (e.g. "View.CompareFiles").</summary>
    public string CommandId { get; init; } = string.Empty;
}
