// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Undo/IExecutableUndoEntry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-13
// Description:
//     Optional extension of IUndoEntry for entries that carry their own
//     apply/revert logic via delegates (Feature #107 — undo/redo unification).
//
// Architecture Notes:
//     Pattern: Command with self-contained execute/undo logic
//     Used by: HexByteUndoEntry (HexEditor bridge), CodeEditorExecutableEntry
//     Dispatch: the shared UndoEngine returns IUndoEntry; the handler checks
//       for IExecutableUndoEntry and calls Revert()/Apply() directly.
//       This avoids any cross-editor reference at dispatch time — each entry
//       carries closures that capture the originating editor's state.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Undo;

/// <summary>
/// Optional extension of <see cref="IUndoEntry"/> for entries that carry their
/// own apply and revert logic via captured closures.
/// Used in cross-editor unified undo so the dispatch handler needs no reference
/// to the originating editor — <see cref="Revert"/> and <see cref="Apply"/>
/// route back through closures.
/// </summary>
public interface IExecutableUndoEntry : IUndoEntry
{
    /// <summary>
    /// Reverts (undoes) this entry — called by the unified undo handler
    /// when <see cref="UndoEngine.TryUndo"/> returns this entry.
    /// </summary>
    void Revert();

    /// <summary>
    /// Reapplies (redoes) this entry — called by the unified redo handler
    /// when <see cref="UndoEngine.TryRedo"/> returns this entry.
    /// </summary>
    void Apply();
}
