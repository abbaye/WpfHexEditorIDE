// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Undo/CompositeUndoEntry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Groups multiple IUndoEntry children into a single atomic undo step.
//     Used for paste operations, rectangular selection deletes, and format-document.
//
// Architecture Notes:
//     Pattern: Composite
//     The host editor applies children in forward (redo) or reverse (undo) order.
//     TryMerge always returns false — composite entries are never coalesced.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace WpfHexEditor.Editor.Core.Undo;

/// <summary>
/// An atomic undo entry that groups N child entries into a single undo/redo step.
/// The host editor is responsible for applying children in the correct order:
/// forward for redo, reverse for undo.
/// </summary>
public sealed class CompositeUndoEntry : IUndoEntry
{
    /// <summary>Child entries in the order they were recorded (forward order).</summary>
    public IReadOnlyList<IUndoEntry> Children { get; }

    public string   Description { get; }
    public long     Revision    { get; set; }
    public DateTime Timestamp   { get; }

    /// <param name="description">Label for undo/redo menu (e.g. "Paste", "Delete Rectangular Selection").</param>
    /// <param name="children">Ordered list of child entries (forward chronological order).</param>
    public CompositeUndoEntry(string description, IReadOnlyList<IUndoEntry> children)
    {
        Description = description;
        Children    = children;
        Timestamp   = children.Count > 0 ? children[0].Timestamp : DateTime.UtcNow;
    }

    /// <summary>Composite entries are never coalesced with adjacent entries.</summary>
    public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
    {
        merged = null;
        return false;
    }
}
