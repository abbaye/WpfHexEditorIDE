// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Undo/IUndoEntry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Core interface for a single unit of undoable work.
//     Implemented by editor-specific concrete entry types.
//
// Architecture Notes:
//     Pattern: Command + Template Method
//     UndoEngine manages the stack; concrete entries carry the data.
//     TryMerge supports character coalescing without coupling the engine to any editor.
// ==========================================================

using System;
using System.Diagnostics.CodeAnalysis;

namespace WpfHexEditor.Editor.Core.Undo;

/// <summary>
/// Represents a single undoable operation recorded in an <see cref="UndoEngine"/>.
/// </summary>
public interface IUndoEntry
{
    /// <summary>Human-readable label shown in undo/redo menu headers and history panels.</summary>
    string Description { get; }

    /// <summary>
    /// Monotonically increasing revision number assigned by <see cref="UndoEngine"/>
    /// at push time. Used to identify the save-point.
    /// </summary>
    long Revision { get; set; }

    /// <summary>UTC timestamp when this entry was originally recorded.</summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Attempts to merge <paramref name="next"/> into this entry (character coalescing).
    /// Returns <see langword="true"/> and sets <paramref name="merged"/> when the two
    /// entries can be combined into a single undo step.
    /// </summary>
    bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged);
}
