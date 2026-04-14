// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.UndoAware.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-13
// Description:
//     Partial class implementing IUndoAwareEditor so the HexEditor
//     participates in the shared per-buffer UndoEngine when two or more
//     editors co-edit the same file (Feature #107).
//
// Architecture Notes:
//     Pattern: Opt-in bridge — activated only by DocumentManager.AttachEditor().
//     Standalone guarantee: _sharedUndoEngine stays null forever when
//       DocumentManager is not involved. All undo/redo paths fall through to the
//       existing ByteProvider/UndoRedoManager stack.
//
//     Promotion flow (byte edit → shared engine):
//       ByteModified fires → OnByteEditedForSharedUndo checks guards →
//       creates HexByteUndoEntry with revert/apply closures → Push to shared engine.
//
//     Batch handling: batch ops (paste/fill) use UndoRedoManager.IsInBatchMode.
//       During a batch _pendingBatchPromotion is set true on first ByteModified.
//       When batch exits (EndBatch) ByteModified fires one final time with
//       IsInBatchMode=false — that single event promotes the group entry.
//
//     Replay flow (shared engine → ByteProvider):
//       SharedUndo/SharedRedo pop from shared engine → HexByteUndoEntry.Revert/Apply →
//       closure sets _suppressSharedPromotion=true → calls _viewModel.Provider.Undo/Redo()
//       → ByteModified fires but is suppressed → _suppressSharedPromotion=false.
//
//     Guards:
//       _suppressSharedPromotion — prevents re-promotion during replay.
// ==========================================================

using System;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Undo;
using WpfHexEditor.HexEditor.Undo;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — <see cref="IUndoAwareEditor"/> implementation.
    /// Opt-in bridge between the hex engine and the shared <see cref="UndoEngine"/>.
    /// </summary>
    public partial class HexEditor : IUndoAwareEditor
    {
        // ── Shared engine reference ───────────────────────────────────────────
        private UndoEngine? _sharedUndoEngine;

        // ── Guards ────────────────────────────────────────────────────────────
        /// <summary>
        /// Set to <see langword="true"/> while a shared-engine replay (Undo/Redo)
        /// is driving <c>ByteProvider.Undo()/Redo()</c>. Prevents <see cref="OnByteEditedForSharedUndo"/>
        /// from re-promoting the resulting <c>ByteModified</c> event back to the shared engine.
        /// </summary>
        private bool _suppressSharedPromotion;

        /// <summary>
        /// Set to <see langword="true"/> on the first <c>ByteModified</c> inside a batch.
        /// Reset to <see langword="false"/> after the group entry is promoted at batch exit.
        /// </summary>
        private bool _pendingBatchPromotion;

        // ── IUndoAwareEditor ─────────────────────────────────────────────────

        /// <inheritdoc/>
        void IUndoAwareEditor.AttachSharedUndo(UndoEngine sharedEngine)
        {
            _sharedUndoEngine = sharedEngine;
            ByteModified += OnByteEditedForSharedUndo;
        }

        /// <inheritdoc/>
        void IUndoAwareEditor.DetachSharedUndo()
        {
            ByteModified -= OnByteEditedForSharedUndo;
            _sharedUndoEngine     = null;
            _suppressSharedPromotion = false;
            _pendingBatchPromotion   = false;
        }

        // ── Shared undo/redo dispatch ─────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="Undo()"/> when the shared engine is active.
        /// Pops one entry from the shared engine and invokes its <c>Revert()</c> closure.
        /// </summary>
        internal void SharedUndo()
        {
            var entry = _sharedUndoEngine!.TryUndo();
            if (entry is IExecutableUndoEntry exec)
                exec.Revert();
        }

        /// <summary>
        /// Called by <see cref="Redo()"/> when the shared engine is active.
        /// Pops one entry from the shared engine and invokes its <c>Apply()</c> closure.
        /// </summary>
        internal void SharedRedo()
        {
            var entry = _sharedUndoEngine!.TryRedo();
            if (entry is IExecutableUndoEntry exec)
                exec.Apply();
        }

        // ── Shared engine state ───────────────────────────────────────────────

        /// <summary>
        /// <see langword="true"/> when Undo is available via the shared engine.
        /// Falls back to the local ByteProvider stack when no shared engine is attached.
        /// </summary>
        internal bool SharedCanUndo  => _sharedUndoEngine?.CanUndo  ?? false;

        /// <summary>
        /// <see langword="true"/> when Redo is available via the shared engine.
        /// Falls back to the local ByteProvider stack when no shared engine is attached.
        /// </summary>
        internal bool SharedCanRedo  => _sharedUndoEngine?.CanRedo  ?? false;

        /// <summary>
        /// <see langword="true"/> when the shared engine is at the save point.
        /// Used to derive <c>IsDirty</c> in unified mode.
        /// </summary>
        internal bool SharedIsAtSavePoint => _sharedUndoEngine?.IsAtSavePoint ?? true;

        /// <summary>
        /// Marks the current shared engine position as the save point.
        /// Call this alongside the existing save-point logic when saving the file.
        /// No-op when no shared engine is attached.
        /// </summary>
        internal void MarkSharedSaved() => _sharedUndoEngine?.MarkSaved();

        // ── Promotion: byte edit → shared engine ─────────────────────────────

        private void OnByteEditedForSharedUndo(object? sender, Core.Events.ByteModifiedEventArgs e)
        {
            if (_sharedUndoEngine is null)          return;
            if (_suppressSharedPromotion)            return;

            var provider = _viewModel?.Provider;
            if (provider is null)                   return;

            // Inside a batch: record that at least one edit happened, but defer promotion
            // until the batch exits (IsInBatchMode transitions false on the next event).
            if (provider.UndoRedoManager.IsInBatchMode)
            {
                _pendingBatchPromotion = true;
                return;
            }

            // If we just exited a batch, promote the group entry (one entry for the whole batch).
            // If this is a standalone edit, promote it directly.
            PromoteTopEntryToSharedEngine(provider);
            _pendingBatchPromotion = false;
        }

        private void PromoteTopEntryToSharedEngine(WpfHexEditor.Core.Bytes.ByteProvider provider)
        {
            var description = provider.PeekUndoDescription() ?? "Edit bytes";

            // Capture provider reference in closures — safe because provider lifetime
            // is tied to the HexEditor's ViewModel, which outlives any individual tab.
            var capturedProvider = provider;

            var entry = new HexByteUndoEntry(
                description,
                revert: () =>
                {
                    _suppressSharedPromotion = true;
                    try   { capturedProvider.Undo(); }
                    finally { _suppressSharedPromotion = false; }
                },
                apply: () =>
                {
                    _suppressSharedPromotion = true;
                    try   { capturedProvider.Redo(); }
                    finally { _suppressSharedPromotion = false; }
                });

            _sharedUndoEngine!.Push(entry);
        }
    }
}
