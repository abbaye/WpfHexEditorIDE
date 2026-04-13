// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Controls/CodeEditor.UndoAware.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-13
// Description:
//     Partial class implementing IUndoAwareEditor so CodeEditor participates
//     in the shared per-buffer UndoEngine when co-editing a file (Feature #107).
//
// Architecture Notes:
//     Pattern: Opt-in dual-push — CodeEditor keeps its own _undoEngine intact.
//     Standalone guarantee: _sharedUndoEngine stays null when DocumentManager
//       is not involved. All existing code paths are unchanged in that case.
//
//     Dual-push strategy:
//       At the single push site in CodeEditor.Document.cs, entries are pushed
//       to both _undoEngine AND _sharedUndoEngine (when non-null).
//       Each entry pushed to the shared engine is wrapped as IExecutableUndoEntry
//       with ApplyForwardEntry/ApplyInverseEntry closures.
//
//     Replay guard:
//       _suppressLocalUndoRedo prevents CodeEditor.Document.cs from re-pushing
//       to the shared engine during a shared-engine replay. The flag is set before
//       calling ApplyForwardEntry/ApplyInverseEntry from the shared engine.
//
//     Undo/Redo dispatch:
//       When _sharedUndoEngine is non-null, Undo()/Redo() pop from the shared
//       engine and invoke the entry's Revert()/Apply() closure.
//       Local _undoEngine is NOT popped during shared replay — its stack stays
//       intact as a fallback if DetachSharedUndo() is called mid-session.
// ==========================================================

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Undo;

namespace WpfHexEditor.Editor.CodeEditor.Controls;

public partial class CodeEditor : IUndoAwareEditor
{
    // ── Shared engine reference ───────────────────────────────────────────
    private UndoEngine? _sharedUndoEngine;

    // ── Replay guard ─────────────────────────────────────────────────────
    /// <summary>
    /// Set to <see langword="true"/> while a shared-engine replay drives
    /// <c>ApplyForwardEntry</c>/<c>ApplyInverseEntry</c>. Prevents the resulting
    /// document mutation from re-pushing an entry to the shared engine.
    /// </summary>
    internal bool _suppressLocalUndoRedo;

    // ── IUndoAwareEditor ─────────────────────────────────────────────────

    /// <inheritdoc/>
    void IUndoAwareEditor.AttachSharedUndo(UndoEngine sharedEngine)
    {
        _sharedUndoEngine = sharedEngine;
        // Subscribe so the shared engine's StateChanged updates CanUndo/CanRedo/IsDirty.
        _sharedUndoEngine.StateChanged += OnSharedEngineStateChanged;
    }

    /// <inheritdoc/>
    void IUndoAwareEditor.DetachSharedUndo()
    {
        if (_sharedUndoEngine is not null)
        {
            _sharedUndoEngine.StateChanged -= OnSharedEngineStateChanged;
            _sharedUndoEngine = null;
        }
        _suppressLocalUndoRedo = false;
    }

    // ── Shared engine state → editor events ──────────────────────────────

    private void OnSharedEngineStateChanged(object? sender, EventArgs e)
    {
        if (_sharedUndoEngine is null) return;

        bool dirty = !_sharedUndoEngine.IsAtSavePoint;
        if (dirty != _isDirty)
        {
            _isDirty = dirty;
            ModifiedChanged?.Invoke(this, EventArgs.Empty);
            TitleChanged?.Invoke(this, BuildTitle());
        }
        CanUndoChanged?.Invoke(this, EventArgs.Empty);
        CanRedoChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Shared Undo/Redo dispatch ─────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="Undo()"/> when the shared engine is active.
    /// Pops one entry and invokes its Revert() closure.
    /// </summary>
    private void SharedUndo()
    {
        var entry = _sharedUndoEngine!.TryUndo();
        if (entry is IExecutableUndoEntry exec)
            exec.Revert();
    }

    /// <summary>
    /// Called by <see cref="Redo()"/> when the shared engine is active.
    /// Pops one entry and invokes its Apply() closure.
    /// </summary>
    private void SharedRedo()
    {
        var entry = _sharedUndoEngine!.TryRedo();
        if (entry is IExecutableUndoEntry exec)
            exec.Apply();
    }

    // ── Promotion helper: wrap a CodeEditorUndoEntry for the shared engine ──

    /// <summary>
    /// Creates an <see cref="IExecutableUndoEntry"/> wrapping <paramref name="entry"/>
    /// and pushes it to the shared engine. Called from the single push site in
    /// <c>CodeEditor.Document.cs</c> when <see cref="_sharedUndoEngine"/> is non-null.
    /// </summary>
    internal void PushToSharedEngine(Models.CodeEditorUndoEntry entry)
    {
        if (_sharedUndoEngine is null || _suppressLocalUndoRedo) return;

        var capturedEntry = entry;
        var wrapped       = new CodeEditorExecutableEntry(
            capturedEntry,
            revert: () =>
            {
                _suppressLocalUndoRedo = true;
                try
                {
                    _isInternalEdit = true;
                    ApplyInverseEntry(capturedEntry);
                    InvalidateVisual();
                    _changeTracker.RebuildFromLines(_document.Lines.Select(l => l.Text).ToList());
                }
                finally
                {
                    _isInternalEdit        = false;
                    _suppressLocalUndoRedo = false;
                }
            },
            apply: () =>
            {
                _suppressLocalUndoRedo = true;
                try
                {
                    _isInternalEdit = true;
                    ApplyForwardEntry(capturedEntry);
                    InvalidateVisual();
                    _changeTracker.RebuildFromLines(_document.Lines.Select(l => l.Text).ToList());
                }
                finally
                {
                    _isInternalEdit        = false;
                    _suppressLocalUndoRedo = false;
                }
            });

        _sharedUndoEngine.Push(wrapped);
    }

    // ── MarkSaved delegation ──────────────────────────────────────────────

    /// <summary>
    /// Marks the current shared engine position as the save point.
    /// Call alongside the existing save-point logic in <c>Save()</c>.
    /// No-op when no shared engine is attached.
    /// </summary>
    internal void MarkSharedSaved() => _sharedUndoEngine?.MarkSaved();

    // ── Private: IExecutableUndoEntry wrapper ─────────────────────────────

    private sealed class CodeEditorExecutableEntry : IExecutableUndoEntry
    {
        private readonly Action _revert;
        private readonly Action _apply;

        public string   Description { get; }
        public long     Revision    { get; set; }
        public DateTime Timestamp   { get; }

        public CodeEditorExecutableEntry(
            Models.CodeEditorUndoEntry source,
            Action revert,
            Action apply)
        {
            Description = source.Description;
            Timestamp   = source.Timestamp;
            _revert     = revert;
            _apply      = apply;
        }

        public void Revert() => _revert();
        public void Apply()  => _apply();

        public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
        {
            // Shared-engine wrappers are never coalesced — coalescing already
            // occurred at the local _undoEngine level before promotion.
            merged = null;
            return false;
        }
    }
}
