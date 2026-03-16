// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEvents/FileClosedEvent.cs
// Created: 2026-03-15
// Description:
//     Published when a file is closed in the hex editor.
// ==========================================================

namespace WpfHexEditor.Events.IDEEvents;

/// <summary>Published when the active file is closed in the hex editor.</summary>
public sealed record FileClosedEvent : IDEEventBase
{
    public string FilePath { get; init; } = string.Empty;
}
