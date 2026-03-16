// ==========================================================
// Project: WpfHexEditor.Events
// File: IDEEvents/EditorSelectionChangedEvent.cs
// Created: 2026-03-15
// Description:
//     Published when the hex editor selection or cursor offset changes.
//     SelectedBytes is limited to first 256 bytes to avoid large IPC payloads.
// ==========================================================

namespace WpfHexEditor.Events.IDEEvents;

/// <summary>Published when the hex editor selection or cursor position changes.</summary>
public sealed record EditorSelectionChangedEvent : IDEEventBase
{
    public long Offset { get; init; }
    public long Length { get; init; }

    /// <summary>
    /// First up to 256 bytes of the current selection.
    /// Intentionally bounded — do not use for full data extraction.
    /// </summary>
    public byte[] SelectedBytes { get; init; } = [];
}
