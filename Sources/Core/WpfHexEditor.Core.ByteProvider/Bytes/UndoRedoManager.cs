// ==========================================================
// Project: WpfHexEditor.Core
// File: UndoRedoManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Manages the undo and redo stacks for byte modification operations in the
//     hex editor. Supports Modify, Insert, and Delete operation types with
//     grouped transactions for atomic multi-byte undo/redo.
//
// Architecture Notes:
//     Pure domain model — no WPF dependencies. Consumed by ByteProvider and
//     UndoRedoService. Stack entries are either a single UndoOperation or an
//     UndoGroup (composite of multiple operations with a description). The
//     UndoGroup concept enables paste/cut/fill to undo as a single step.
//     Coalescence merges consecutive adjacent single-byte Modify ops within
//     500 ms to keep the history clean during hex digit typing.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace WpfHexEditor.Core.Bytes
{
    /// <summary>
    /// Type of undo/redo operation.
    /// </summary>
    public enum UndoOperationType
    {
        /// <summary>Modify existing byte(s).</summary>
        Modify,
        /// <summary>Insert new byte(s).</summary>
        Insert,
        /// <summary>Delete byte(s).</summary>
        Delete
    }

    /// <summary>
    /// Represents a single undoable/redoable operation.
    /// Stores enough information to reverse the operation.
    /// </summary>
    public struct UndoOperation
    {
        /// <summary>Type of operation (Modify, Insert, Delete).</summary>
        public UndoOperationType Type;
        /// <summary>Virtual position where the operation occurred.</summary>
        public long VirtualPosition;
        /// <summary>For Modify: original values. For Delete: deleted values. For Insert: null.</summary>
        public byte[]? OldValues;
        /// <summary>For Modify: new values. For Insert: inserted values. For Delete: null.</summary>
        public byte[]? NewValues;
        /// <summary>Number of bytes affected.</summary>
        public int Count;
        /// <summary>UTC timestamp ticks — used for coalescence window.</summary>
        internal long TimestampTicks;

        /// <summary>
        /// Physical file positions captured at delete time (Delete ops only).
        /// Each entry: &gt;= 0 = physical file offset (restore via UndeleteByte — no green border),
        ///              -1  = byte was an inserted byte (must re-insert on undo).
        /// Null for Modify and Insert operations.
        /// </summary>
        internal long[]? PhysicalPositions;

        public UndoOperation(UndoOperationType type, long virtualPosition,
            byte[]? oldValues, byte[]? newValues, int count)
        {
            Type = type;
            VirtualPosition = virtualPosition;
            OldValues = oldValues;
            NewValues = newValues;
            Count = count;
            TimestampTicks = DateTime.UtcNow.Ticks;
        }

        public override string ToString() => $"{Type} at pos {VirtualPosition}, count {Count}";
    }

    /// <summary>
    /// A composite undo entry grouping multiple <see cref="UndoOperation"/> into a single step.
    /// Created by <see cref="UndoRedoManager.EndBatch"/> — enables paste/cut/fill to undo as one step.
    /// </summary>
    internal sealed class UndoGroup
    {
        public List<UndoOperation> Operations { get; }
        public string Description { get; }

        public UndoGroup(List<UndoOperation> operations, string description)
        {
            Operations = operations;
            Description = description;
        }

        public override string ToString() => $"{Description} ({Operations.Count} ops)";
    }

    /// <summary>
    /// UndoRedoManager — Manages undo/redo stacks for ByteProvider V2.
    /// Stack entries are <c>UndoOperation</c> (single op) or <c>UndoGroup</c> (composite).
    /// Thread-safe for reads; external locking required for writes.
    /// </summary>
    public sealed class UndoRedoManager
    {
        // Stack entries are boxed UndoOperation structs or UndoGroup instances.
        private readonly Stack<object> _undoStack = new();
        private readonly Stack<object> _redoStack = new();

        private int _maxUndoStackSize = 1000;

        // Batch / transaction support
        private bool _batchMode;
        private string _batchDescription = string.Empty;
        private readonly List<UndoOperation> _batchOperations = new();

        // Coalescence: merge consecutive adjacent single-byte Modify ops within this window.
        private static readonly long CoalesceWindowTicks = TimeSpan.FromMilliseconds(500).Ticks;

        #region Properties

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public int UndoStackCount => _undoStack.Count;
        public int RedoStackCount => _redoStack.Count;
        public bool IsInBatchMode => _batchMode;

        public int MaxUndoStackSize
        {
            get => _maxUndoStackSize;
            set => _maxUndoStackSize = Math.Max(1, value);
        }

        #endregion

        #region Record Operations

        public void RecordModify(long virtualPosition, byte[] oldValues, byte[] newValues)
        {
            if (oldValues == null || newValues == null)
                throw new ArgumentNullException(nameof(oldValues), "oldValues and newValues cannot be null");
            if (oldValues.Length != newValues.Length)
                throw new ArgumentException("oldValues and newValues must have same length");

            AddOperation(new UndoOperation(UndoOperationType.Modify, virtualPosition,
                oldValues, newValues, oldValues.Length));
        }

        public void RecordInsert(long virtualPosition, byte[] insertedValues)
        {
            if (insertedValues == null || insertedValues.Length == 0)
                throw new ArgumentException("insertedValues cannot be null or empty", nameof(insertedValues));

            AddOperation(new UndoOperation(UndoOperationType.Insert, virtualPosition,
                null, insertedValues, insertedValues.Length));
        }

        public void RecordDelete(long virtualPosition, byte[] deletedValues, long[]? physicalPositions = null)
        {
            if (deletedValues == null || deletedValues.Length == 0)
                throw new ArgumentException("deletedValues cannot be null or empty", nameof(deletedValues));

            AddOperation(new UndoOperation(UndoOperationType.Delete, virtualPosition,
                deletedValues, null, deletedValues.Length)
            {
                PhysicalPositions = physicalPositions
            });
        }

        private void AddOperation(UndoOperation op)
        {
            if (_batchMode)
            {
                _batchOperations.Add(op);
                return;
            }

            // New user action invalidates redo history.
            _redoStack.Clear();

            // Attempt coalescence only for single-byte Modify ops (hex digit typing).
            if (op.Type == UndoOperationType.Modify && op.Count == 1 && TryCoalesce(op))
            {
                EnforceMaxStackSize();
                return;
            }

            _undoStack.Push(op);
            EnforceMaxStackSize();
        }

        /// <summary>
        /// Tries to merge <paramref name="op"/> into the top of the undo stack.
        /// Conditions: top is a single Modify op, adjacent position, within 500 ms window.
        /// Returns true when merged (caller must NOT push <paramref name="op"/> again).
        /// </summary>
        private bool TryCoalesce(UndoOperation op)
        {
            if (_undoStack.Count == 0) return false;
            if (_undoStack.Peek() is not UndoOperation topOp) return false;
            if (topOp.Type != UndoOperationType.Modify || topOp.Count < 1) return false;

            // Time window
            if (DateTime.UtcNow.Ticks - topOp.TimestampTicks > CoalesceWindowTicks) return false;

            // Must be strictly adjacent (next position)
            if (op.VirtualPosition != topOp.VirtualPosition + topOp.Count) return false;

            _undoStack.Pop();

            var merged = new UndoOperation(UndoOperationType.Modify, topOp.VirtualPosition,
                Concat(topOp.OldValues!, op.OldValues!),
                Concat(topOp.NewValues!, op.NewValues!),
                topOp.Count + op.Count)
            {
                TimestampTicks = topOp.TimestampTicks // preserve original window start
            };

            _undoStack.Push(merged);
            return true;
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            var result = new byte[a.Length + b.Length];
            a.CopyTo(result, 0);
            b.CopyTo(result, a.Length);
            return result;
        }

        private void EnforceMaxStackSize()
        {
            if (_undoStack.Count <= _maxUndoStackSize) return;

            var list = _undoStack.ToList(); // newest first (Stack enumerates top→bottom)
            list.Reverse();                 // oldest first
            list.RemoveRange(0, list.Count - _maxUndoStackSize);
            list.Reverse();                 // newest first

            _undoStack.Clear();
            foreach (var entry in list)
                _undoStack.Push(entry);
        }

        #endregion

        #region Descriptions / Peek

        /// <summary>Human-readable description of the next undo step, or null if nothing to undo.</summary>
        public string? PeekUndoDescription()
            => _undoStack.Count > 0 ? DescribeEntry(_undoStack.Peek()) : null;

        /// <summary>
        /// Returns the raw top-of-undo-stack object (<see cref="UndoOperation"/> or <see cref="UndoGroup"/>)
        /// without removing it, or <see langword="null"/> when the stack is empty.
        /// Used by the HexEditor shared-undo bridge to identify the entry just pushed.
        /// Callers must not mutate the returned object.
        /// </summary>
        internal object? PeekUndoEntryRaw()
            => _undoStack.Count > 0 ? _undoStack.Peek() : null;

        /// <summary>Human-readable description of the next redo step, or null if nothing to redo.</summary>
        public string? PeekRedoDescription()
            => _redoStack.Count > 0 ? DescribeEntry(_redoStack.Peek()) : null;

        /// <summary>Descriptions of all undo steps (newest first), up to <paramref name="maxCount"/>.</summary>
        public IReadOnlyList<string> GetUndoDescriptions(int maxCount = 20)
        {
            var result = new List<string>(Math.Min(maxCount, _undoStack.Count));
            foreach (var entry in _undoStack)
            {
                if (result.Count >= maxCount) break;
                result.Add(DescribeEntry(entry));
            }
            return result;
        }

        /// <summary>Descriptions of all redo steps (most recent first), up to <paramref name="maxCount"/>.</summary>
        public IReadOnlyList<string> GetRedoDescriptions(int maxCount = 20)
        {
            var result = new List<string>(Math.Min(maxCount, _redoStack.Count));
            foreach (var entry in _redoStack)
            {
                if (result.Count >= maxCount) break;
                result.Add(DescribeEntry(entry));
            }
            return result;
        }

        private static string DescribeEntry(object entry)
        {
            if (entry is UndoGroup group) return group.Description;

            if (entry is UndoOperation op)
            {
                return op.Type switch
                {
                    UndoOperationType.Modify => op.Count == 1 ? "Modify byte" : $"Modify {op.Count} bytes",
                    UndoOperationType.Insert => op.Count == 1 ? "Insert byte" : $"Insert {op.Count} bytes",
                    UndoOperationType.Delete => op.Count == 1 ? "Delete byte" : $"Delete {op.Count} bytes",
                    _ => "Edit"
                };
            }

            return "Edit";
        }

        // Backwards-compatible single-op peek (used by UndoRedoService)
        public UndoOperation? PeekUndo()
        {
            if (_undoStack.Count == 0) return null;
            var top = _undoStack.Peek();
            return top switch
            {
                UndoOperation op => op,
                UndoGroup g when g.Operations.Count > 0 => g.Operations[^1],
                _ => null
            };
        }

        public UndoOperation? PeekRedo()
        {
            if (_redoStack.Count == 0) return null;
            var top = _redoStack.Peek();
            return top switch
            {
                UndoOperation op => op,
                UndoGroup g when g.Operations.Count > 0 => g.Operations[^1],
                _ => null
            };
        }

        #endregion

        #region Pop Undo / Redo

        /// <summary>
        /// Pop the next undo entry (single <see cref="UndoOperation"/> or <see cref="UndoGroup"/>),
        /// move it to the redo stack, and return it.
        /// </summary>
        public object PopUndo()
        {
            if (_undoStack.Count == 0)
                throw new InvalidOperationException("No operations to undo");

            var entry = _undoStack.Pop();
            _redoStack.Push(entry);
            return entry;
        }

        /// <summary>
        /// Pop the next redo entry, move it to the undo stack, and return it.
        /// </summary>
        public object PopRedo()
        {
            if (_redoStack.Count == 0)
                throw new InvalidOperationException("No operations to redo");

            var entry = _redoStack.Pop();
            _undoStack.Push(entry);
            return entry;
        }

        #endregion

        #region Batch / Transaction

        /// <summary>
        /// Begin collecting operations into a named transaction.
        /// Call <see cref="EndBatch"/> to commit as a single undo step, or
        /// <see cref="RollbackBatch"/> to discard.
        /// </summary>
        public void BeginBatch(string description = "")
        {
            _batchMode = true;
            _batchDescription = description;
            _batchOperations.Clear();
        }

        /// <summary>
        /// Commit the current batch as a single <see cref="UndoGroup"/> on the undo stack.
        /// If zero operations were collected, nothing is pushed.
        /// </summary>
        public void EndBatch()
        {
            _batchMode = false;

            if (_batchOperations.Count == 0)
                return;

            string desc = string.IsNullOrEmpty(_batchDescription)
                ? $"Edit {_batchOperations.Sum(o => o.Count)} bytes"
                : _batchDescription;

            var group = new UndoGroup(new List<UndoOperation>(_batchOperations), desc);
            _batchOperations.Clear();
            _batchDescription = string.Empty;

            _redoStack.Clear();
            _undoStack.Push(group);
            EnforceMaxStackSize();
        }

        /// <summary>
        /// Discard the collected batch operations without pushing to the undo stack.
        /// </summary>
        public void RollbackBatch()
        {
            _batchMode = false;
            _batchOperations.Clear();
            _batchDescription = string.Empty;
        }

        #endregion

        #region Clear

        public void ClearAll()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _batchOperations.Clear();
            _batchMode = false;
        }

        public void ClearRedo() => _redoStack.Clear();

        #endregion

        #region Statistics

        public (int undoOps, int redoOps, long estimatedMemoryKB) GetStatistics()
        {
            static long CalcStackMemory(Stack<object> stack)
            {
                long mem = 0;
                foreach (var entry in stack)
                {
                    if (entry is UndoOperation op)
                        mem += 12 + (op.OldValues?.Length ?? 0) + (op.NewValues?.Length ?? 0);
                    else if (entry is UndoGroup g)
                        foreach (var gop in g.Operations)
                            mem += 12 + (gop.OldValues?.Length ?? 0) + (gop.NewValues?.Length ?? 0);
                }
                return mem;
            }

            long totalBytes = CalcStackMemory(_undoStack) + CalcStackMemory(_redoStack);
            return (_undoStack.Count, _redoStack.Count, totalBytes / 1024);
        }

        #endregion
    }
}
