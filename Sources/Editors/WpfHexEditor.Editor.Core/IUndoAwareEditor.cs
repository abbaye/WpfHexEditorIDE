// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: IUndoAwareEditor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-13
// Description:
//     Opt-in contract for editors that participate in the shared
//     per-buffer UndoEngine (Feature #107 — undo/redo unification).
//
// Architecture Notes:
//     Pattern: Extension Interface (alongside IBufferAwareEditor)
//     Activation: DocumentManager.AttachEditor() wires this after
//       IBufferAwareEditor.AttachBuffer() when the editor implements both.
//     Standalone guarantee: never called without DocumentManager —
//       editors that are not hosted in the IDE never see AttachSharedUndo.
//     Dual-stack strategy: editors keep their local undo stack and ALSO
//       push to the shared engine. DetachSharedUndo reverts to local-only mode.
// ==========================================================

using WpfHexEditor.Editor.Core.Undo;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Opt-in interface for editors that participate in the shared per-buffer
/// <see cref="UndoEngine"/> when two or more editors co-edit the same file.
/// Implement alongside <see cref="IBufferAwareEditor"/>.
/// </summary>
public interface IUndoAwareEditor : IDocumentEditor
{
    /// <summary>
    /// Called by <c>DocumentManager</c> after the shared <see cref="UndoEngine"/>
    /// is resolved for this buffer. The editor must:
    /// <list type="number">
    ///   <item>Store the reference and also push subsequent undo entries to <paramref name="sharedEngine"/>.</item>
    ///   <item>Delegate <c>Undo()</c>/<c>Redo()</c> to the shared engine when it is non-null.</item>
    ///   <item>Derive <c>CanUndo</c>/<c>CanRedo</c>/<c>IsDirty</c> from the shared engine when attached.</item>
    /// </list>
    /// The editor's own local undo stack must remain intact and operational
    /// so that <see cref="DetachSharedUndo"/> can restore standalone behaviour.
    /// </summary>
    void AttachSharedUndo(UndoEngine sharedEngine);

    /// <summary>
    /// Called by <c>DocumentManager</c> when the last co-editor on this file closes
    /// or when the editor itself is unregistered. The editor must revert to its
    /// own local undo stack immediately.
    /// </summary>
    void DetachSharedUndo();
}
