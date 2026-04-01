// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/DocumentSavedEvent.cs
// Created: 2026-03-15
// Description:
//     Published when a document is saved to disk.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>Published when a document is saved to disk.</summary>
public sealed record DocumentSavedEvent : IDEEventBase
{
    public string FilePath { get; init; } = string.Empty;
}
