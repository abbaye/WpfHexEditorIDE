// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignUndoManager.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Undo/Redo manager for the XAML Designer surface.
//     Defines the IDesignUndoEntry hierarchy (Single, Batch, Snapshot)
//     and the DesignUndoManager that orchestrates the dual-stack system
//     with max-depth trimming, batch grouping, and jump-to-state support.
//
// Architecture Notes:
//     Command / Memento hybrid pattern.
//     Single  = one attribute-diff DesignOperation (Move/Resize/PropertyChange).
//     Batch   = N DesignOperations grouped as one step (Alignment).
//     Snapshot= full XAML before/after for structural changes (Insert/Delete).
//     Max stack depth: 200 entries (oldest trimmed).
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

// ── Entry interface & implementations ────────────────────────────────────────

/// <summary>
/// Base contract for a single undo/redo history entry.
/// </summary>
public interface IDesignUndoEntry
{
    /// <summary>Human-readable description shown in History Panel and toolbar tooltip.</summary>
    string   Description   { get; }

    /// <summary>Time the entry was created.</summary>
    DateTime Timestamp     { get; }

    /// <summary>Number of atomic operations bundled in this entry.</summary>
    int      OperationCount { get; }
}

/// <summary>
/// Wraps a single <see cref="DesignOperation"/> (Move, Resize, PropertyChange).
/// Undo/Redo via attribute diff — no full XAML snapshot needed.
/// </summary>
public sealed record SingleDesignUndoEntry(DesignOperation Operation) : IDesignUndoEntry
{
    public string   Description    => Operation.Description;
    public DateTime Timestamp      { get; } = DateTime.Now;
    public int      OperationCount => 1;
}

/// <summary>
/// Groups multiple <see cref="DesignOperation"/>s into one logical step (e.g. Align 5 elements).
/// Undo applies ops in REVERSE order; Redo applies in FORWARD order.
/// </summary>
public sealed record BatchDesignUndoEntry(
    IReadOnlyList<DesignOperation> Operations,
    string Description) : IDesignUndoEntry
{
    public DateTime Timestamp      { get; } = DateTime.Now;
    public int      OperationCount => Operations.Count;
}

/// <summary>
/// Stores the complete XAML text before and after a structural operation
/// (Insert from Toolbox, Delete key). Safest approach — avoids UID invalidation
/// after DOM mutations.
/// </summary>
public sealed record SnapshotDesignUndoEntry(
    string BeforeXaml,
    string AfterXaml,
    string Description) : IDesignUndoEntry
{
    public DateTime Timestamp      { get; } = DateTime.Now;
    public int      OperationCount => 1;
}

// ── Manager ──────────────────────────────────────────────────────────────────

/// <summary>
/// Manages the undo/redo history for the XAML Designer surface.
/// Replaces the raw <c>Stack&lt;DesignOperation&gt;</c> used in Phase 1
/// with a richer, max-depth-limited, batch-aware system.
/// </summary>
public sealed class DesignUndoManager
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>Maximum number of entries retained in the undo stack.</summary>
    public const int MaxDepth = 200;

    // ── Internal state ────────────────────────────────────────────────────────

    private Stack<IDesignUndoEntry> _undoStack = new();
    private Stack<IDesignUndoEntry> _redoStack = new();

    // Batch accumulation buffer (null = not currently batching).
    private List<DesignOperation>? _batchBuffer;
    private string?                _batchDescription;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised whenever the undo or redo stack changes.
    /// Consumers (XamlDesignerSplitHost, DesignHistoryPanel) subscribe to refresh UI.
    /// </summary>
    public event EventHandler? HistoryChanged;

    // ── Query properties ──────────────────────────────────────────────────────

    /// <summary>True when at least one entry can be undone.</summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>True when at least one entry can be redone.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Description of the top undo entry, or null.</summary>
    public string? UndoDescription
        => _undoStack.TryPeek(out var e) ? e.Description : null;

    /// <summary>Description of the top redo entry, or null.</summary>
    public string? RedoDescription
        => _redoStack.TryPeek(out var e) ? e.Description : null;

    /// <summary>
    /// Complete ordered history (oldest → newest), spanning both stacks.
    /// Undo entries come first (applied), redo entries last (undone).
    /// </summary>
    public IReadOnlyList<IDesignUndoEntry> History
        => _undoStack.Reverse().Concat(_redoStack).ToList();

    /// <summary>Number of entries currently in the undo stack.</summary>
    public int UndoDepth => _undoStack.Count;

    /// <summary>Number of entries currently in the redo stack.</summary>
    public int RedoDepth => _redoStack.Count;

    // ── Push ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes a new entry onto the undo stack and clears the redo stack.
    /// When a batch is in progress the entry is accumulated instead.
    /// Trims the undo stack to <see cref="MaxDepth"/> if necessary.
    /// </summary>
    public void PushEntry(IDesignUndoEntry entry)
    {
        // Accumulate into batch buffer when active.
        if (_batchBuffer is not null)
        {
            if (entry is SingleDesignUndoEntry s)
                _batchBuffer.Add(s.Operation);
            return;
        }

        _redoStack.Clear();
        _undoStack.Push(entry);
        TrimToMaxDepth();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Pop / Peek ────────────────────────────────────────────────────────────

    /// <summary>
    /// Pops the top undo entry, pushes it to the redo stack, and fires <see cref="HistoryChanged"/>.
    /// Returns null when the undo stack is empty.
    /// </summary>
    public IDesignUndoEntry? PopUndo()
    {
        if (!_undoStack.TryPop(out var entry)) return null;
        _redoStack.Push(entry);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    /// <summary>
    /// Pops the top undo entry WITHOUT pushing it to the redo stack.
    /// Used by <see cref="JumpToHistoryEntry"/> to allow the caller to
    /// rebuild the redo stack at the jump destination.
    /// </summary>
    public IDesignUndoEntry? UndoWithoutRedoPush()
    {
        if (!_undoStack.TryPop(out var entry)) return null;
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    /// <summary>
    /// Pops the top redo entry, pushes it back onto the undo stack, and fires <see cref="HistoryChanged"/>.
    /// Returns null when the redo stack is empty.
    /// </summary>
    public IDesignUndoEntry? PopRedo()
    {
        if (!_redoStack.TryPop(out var entry)) return null;
        _undoStack.Push(entry);
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    // ── Batch ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts accumulating <see cref="SingleDesignUndoEntry"/> pushes into a batch.
    /// All subsequent <see cref="PushEntry"/> calls will be buffered until
    /// <see cref="EndBatch"/> is called.
    /// </summary>
    public void BeginBatch(string description)
    {
        _batchBuffer      = new List<DesignOperation>();
        _batchDescription = description;
    }

    /// <summary>
    /// Ends the current batch, creates a <see cref="BatchDesignUndoEntry"/> from
    /// the accumulated operations, and pushes it onto the undo stack.
    /// Returns the created entry, or null if no operations were accumulated.
    /// </summary>
    public IDesignUndoEntry? EndBatch()
    {
        if (_batchBuffer is null) return null;

        var ops  = _batchBuffer.AsReadOnly();
        var desc = _batchDescription ?? "Batch";
        _batchBuffer      = null;
        _batchDescription = null;

        if (ops.Count == 0) return null;

        var entry = new BatchDesignUndoEntry(ops, desc);
        _redoStack.Clear();
        _undoStack.Push(entry);
        TrimToMaxDepth();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    /// <summary>Clears only the redo stack.</summary>
    public void ClearRedo()
    {
        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Clears both the undo and redo stacks.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Jump-to-state ─────────────────────────────────────────────────────────

    /// <summary>
    /// Jumps to a specific point in history by undoing or redoing N steps.
    /// Intermediate undo steps do NOT populate the redo stack — the final
    /// redo stack state reflects the jump destination.
    /// <para>
    /// Callers must supply callbacks that apply the XAML changes for each entry.
    /// </para>
    /// </summary>
    /// <param name="undoCount">Number of undo steps to perform.</param>
    /// <param name="redoCount">Number of redo steps to perform.</param>
    /// <param name="onEachUndo">Called for each undone entry (apply BeforeXaml / Before attrs).</param>
    /// <param name="onEachRedo">Called for each redone entry (apply AfterXaml / After attrs).</param>
    public void JumpToHistoryEntry(
        int undoCount,
        int redoCount,
        Action<IDesignUndoEntry> onEachUndo,
        Action<IDesignUndoEntry> onEachRedo)
    {
        for (int i = 0; i < undoCount; i++)
        {
            var e = UndoWithoutRedoPush();
            if (e is not null) onEachUndo(e);
        }

        for (int i = 0; i < redoCount; i++)
        {
            var e = PopRedo();
            if (e is not null) onEachRedo(e);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Trims the undo stack to <see cref="MaxDepth"/> by removing the oldest entry.
    /// Stack.ToArray() enumerates top-first (newest); Reverse() gives oldest-first;
    /// Take(MaxDepth) keeps the newest MaxDepth; Reverse() restores top-first order
    /// for the Stack constructor (which pushes in enumeration order — bottom first).
    /// </summary>
    private void TrimToMaxDepth()
    {
        if (_undoStack.Count <= MaxDepth) return;

        var items = _undoStack.ToArray();                          // newest first
        _undoStack = new Stack<IDesignUndoEntry>(
            items.Take(MaxDepth).Reverse());                       // oldest-first → pushed correctly
    }
}
