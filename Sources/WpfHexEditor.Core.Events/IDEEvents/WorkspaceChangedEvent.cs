// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/WorkspaceChangedEvent.cs
// Created: 2026-03-15
// Description:
//     Published when the IDE workspace/solution changes.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>Published when the active workspace or solution changes.</summary>
public sealed record WorkspaceChangedEvent : IDEEventBase
{
    public string? WorkspacePath { get; init; }
    public string? PreviousWorkspacePath { get; init; }
}
