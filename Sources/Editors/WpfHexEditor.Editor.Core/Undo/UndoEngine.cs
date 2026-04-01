// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Undo/UndoEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Shared undo/redo engine for all document editors in WpfHexEditor.
//     Manages a bounded, index-split entry list with coalescing, atomic
//     transactions, and revision-based save-point tracking.
//
// Architecture Notes:
//     Pattern: Command Manager + Deque (List<T> + split pointer)
//     Storage: List<IUndoEntry> _entries + int _pointer (split index):
//       [0.._pointer) = undo stack | [_pointer.._entries.Count) = redo stack.
//     Advantage over two Stack<T>: O(1) bottom-trim for MaxHistorySize enforcement,
//       no O(n) ToList/Reverse workaround needed.
//     Coalescing: delegates to IUndoEntry.TryMerge — engine is coalescing-strategy-agnostic.
//     Save-point: revision of _entries[_pointer-1] compared to _savedRevision.
//       When no entries exist and _savedRevision == 0, the document is clean.
//     Transactions: Push calls during a transaction buffer entries; CommitTransaction
//       wraps them in a CompositeUndoEntry and pushes to the main list.
// ==========================================================

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Editor.Core.Undo;

/// <summary>
/// Shared undo/redo engine. Thread-affine — must be accessed on a single (UI) thread.
/// </summary>
public sealed class UndoEngine
{
    private readonly List<IUndoEntry> _entries          = new();
    private          int              _pointer           = 0;   // Split index
    private          long             _revision          = 0;   // Push counter
    private          long             _savedRevision     = 0;   // 0 = "at initial clean state"

    private          bool             _inTransaction;
    private          string           _transactionDescription = string.Empty;
    private readonly List<IUndoEntry> _transactionBuffer      = new();

    // ------------------------------------------------------------------

    /// <summary>
    /// Maximum number of undo steps retained.
    /// When exceeded, the oldest entry is silently trimmed.
    /// Default: 500. Clamped to [10, 5000] by <see cref="CodeEditorOptions"/>.
    /// </summary>
    public int MaxHistorySize { get; set; } = 500;

    public bool CanUndo => _pointer > 0;
    public bool CanRedo => _pointer < _entries.Count;

    /// <summary>Number of available undo steps.</summary>
    public int UndoCount => _pointer;

    /// <summary>Number of available redo steps.</summary>
    public int RedoCount => _entries.Count - _pointer;

    /// <summary>
    /// True when the current document state exactly matches the state
    /// at the last <see cref="MarkSaved"/> call.
    /// Use this to drive the editor's <c>IsDirty</c> property.
    /// </summary>
    public bool IsAtSavePoint
    {
        get
        {
            // No edits at all — clean iff MarkSaved was called with an empty stack.
            if (_pointer == 0)
                return _savedRevision == 0;

            return _entries[_pointer - 1].Revision == _savedRevision;
        }
    }

    /// <summary>Description of the next undo step (for dynamic menu headers).</summary>
    public string? PeekUndoDescription => CanUndo ? _entries[_pointer - 1].Description : null;

    /// <summary>Description of the next redo step (for dynamic menu headers).</summary>
    public string? PeekRedoDescription => CanRedo ? _entries[_pointer].Description : null;

    /// <summary>
    /// Raised whenever <see cref="CanUndo"/>, <see cref="CanRedo"/>,
    /// or <see cref="IsAtSavePoint"/> may have changed.
    /// </summary>
    public event EventHandler? StateChanged;

    // ------------------------------------------------------------------
    // Core operations
    // ------------------------------------------------------------------

    /// <summary>
    /// Records a new undo entry. Clears the redo stack, attempts coalescing with the
    /// current top entry, and trims the oldest entry if <see cref="MaxHistorySize"/>
    /// is exceeded. When inside a <see cref="BeginTransaction"/> scope, buffers
    /// the entry instead of pushing to the main list.
    /// </summary>
    public void Push(IUndoEntry entry)
    {
        if (_inTransaction)
        {
            _transactionBuffer.Add(entry);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Clear redo entries above the pointer (new branch overrides forward history).
        if (_pointer < _entries.Count)
            _entries.RemoveRange(_pointer, _entries.Count - _pointer);

        // Attempt coalescing with the top-of-stack entry (e.g. character merging).
        if (_pointer > 0 && _entries[_pointer - 1].TryMerge(entry, out var merged))
        {
            merged.Revision           = ++_revision;
            _entries[_pointer - 1]    = merged;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Trim oldest entry when at capacity (O(n) shift — rare, bounded by MaxHistorySize).
        if (_entries.Count >= MaxHistorySize && _pointer > 0)
        {
            _entries.RemoveAt(0);
            _pointer = Math.Max(0, _pointer - 1);

            // If the trimmed entry held the save-point revision, the save-point is unreachable.
            if (_savedRevision > 0 && !HasRevision(_savedRevision))
                _savedRevision = long.MinValue;  // Sentinel: unreachable
        }

        entry.Revision = ++_revision;
        _entries.Add(entry);
        _pointer++;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves one step back in history. Returns the entry whose inverse
    /// the caller should apply, or <see langword="null"/> if <see cref="CanUndo"/> is false.
    /// </summary>
    public IUndoEntry? TryUndo()
    {
        if (!CanUndo) return null;
        _pointer--;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return _entries[_pointer];
    }

    /// <summary>
    /// Moves one step forward in history. Returns the entry the caller
    /// should re-apply, or <see langword="null"/> if <see cref="CanRedo"/> is false.
    /// </summary>
    public IUndoEntry? TryRedo()
    {
        if (!CanRedo) return null;
        var entry = _entries[_pointer];
        _pointer++;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    // ------------------------------------------------------------------
    // Transaction API
    // ------------------------------------------------------------------

    /// <summary>
    /// Begins a transaction. All subsequent <see cref="Push"/> calls are buffered
    /// until <see cref="CommitTransaction"/> wraps them into a single
    /// <see cref="CompositeUndoEntry"/>.
    /// </summary>
    /// <param name="description">Label for the composite entry (e.g. "Paste").</param>
    /// <returns>
    /// An <see cref="IDisposable"/> scope whose <c>Dispose()</c> commits the transaction.
    /// </returns>
    public TransactionScope BeginTransaction(string description)
    {
        _inTransaction          = true;
        _transactionDescription = description;
        _transactionBuffer.Clear();
        return new TransactionScope(this);
    }

    /// <summary>
    /// Commits the active transaction. Wraps all buffered entries into a
    /// <see cref="CompositeUndoEntry"/> (or a single entry if only one was pushed)
    /// and pushes it to the main undo list. No-op if the buffer is empty.
    /// Called automatically by <see cref="TransactionScope.Dispose"/>.
    /// </summary>
    internal void CommitTransaction()
    {
        _inTransaction = false;

        if (_transactionBuffer.Count == 0)
        {
            _transactionBuffer.Clear();
            return;
        }

        IUndoEntry composite = _transactionBuffer.Count == 1
            ? _transactionBuffer[0]
            : new CompositeUndoEntry(_transactionDescription, _transactionBuffer.ToArray());

        _transactionBuffer.Clear();
        Push(composite);  // _inTransaction is false — goes through normal push path
    }

    /// <summary>
    /// Discards all buffered transaction entries without pushing anything.
    /// Called by <see cref="TransactionScope.Rollback"/>.
    /// </summary>
    internal void RollbackTransaction()
    {
        _inTransaction = false;
        _transactionBuffer.Clear();
    }

    // ------------------------------------------------------------------
    // Save-point
    // ------------------------------------------------------------------

    /// <summary>
    /// Stamps the current document state as the saved state.
    /// <see cref="IsAtSavePoint"/> returns <see langword="true"/> whenever undo/redo
    /// brings the document back to this exact state.
    /// Call this immediately after successfully writing the file to disk.
    /// </summary>
    public void MarkSaved()
    {
        _savedRevision = _pointer == 0 ? 0 : _entries[_pointer - 1].Revision;
    }

    // ------------------------------------------------------------------
    // Reset
    // ------------------------------------------------------------------

    /// <summary>
    /// Clears the entire undo/redo history and resets the save-point to the
    /// "initial clean" state (revision 0). Call on file-open or document revert.
    /// </summary>
    public void Reset()
    {
        _entries.Clear();
        _pointer            = 0;
        _revision           = 0;
        _savedRevision      = 0;
        _inTransaction      = false;
        _transactionBuffer.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private bool HasRevision(long revision)
    {
        foreach (var e in _entries)
            if (e.Revision == revision) return true;
        return false;
    }
}
