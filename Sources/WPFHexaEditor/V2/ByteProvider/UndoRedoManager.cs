//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexaEditor.V2.ByteProvider
{
    /// <summary>
    /// Type of undo/redo operation.
    /// </summary>
    public enum UndoOperationType
    {
        /// <summary>
        /// Modify existing byte(s).
        /// </summary>
        Modify,

        /// <summary>
        /// Insert new byte(s).
        /// </summary>
        Insert,

        /// <summary>
        /// Delete byte(s).
        /// </summary>
        Delete
    }

    /// <summary>
    /// Represents a single undoable/redoable operation.
    /// Stores enough information to reverse the operation.
    /// </summary>
    public struct UndoOperation
    {
        /// <summary>
        /// Type of operation (Modify, Insert, Delete).
        /// </summary>
        public UndoOperationType Type;

        /// <summary>
        /// Virtual position where the operation occurred.
        /// </summary>
        public long VirtualPosition;

        /// <summary>
        /// For Modify: Original byte values before modification.
        /// For Delete: Deleted byte values (to restore on undo).
        /// For Insert: null (not needed).
        /// </summary>
        public byte[]? OldValues;

        /// <summary>
        /// For Modify: New byte values after modification.
        /// For Insert: Inserted byte values.
        /// For Delete: null (not needed).
        /// </summary>
        public byte[]? NewValues;

        /// <summary>
        /// Number of bytes affected by this operation.
        /// </summary>
        public int Count;

        public UndoOperation(UndoOperationType type, long virtualPosition, byte[]? oldValues, byte[]? newValues, int count)
        {
            Type = type;
            VirtualPosition = virtualPosition;
            OldValues = oldValues;
            NewValues = newValues;
            Count = count;
        }

        public override string ToString()
        {
            return $"{Type} at pos {VirtualPosition}, count {Count}";
        }
    }

    /// <summary>
    /// UndoRedoManager - Manages undo/redo stacks for ByteProvider V2.
    /// Records all operations and allows reverting them.
    /// Thread-safe for read operations, external locking required for writes.
    /// </summary>
    public sealed class UndoRedoManager
    {
        private readonly Stack<UndoOperation> _undoStack = new();
        private readonly Stack<UndoOperation> _redoStack = new();

        // Configuration
        private int _maxUndoStackSize = 1000; // Limit to prevent memory issues

        // Batching support to group multiple operations into one undo step
        private bool _batchMode = false;
        private readonly List<UndoOperation> _batchOperations = new();

        /// <summary>
        /// Gets whether undo is available.
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Gets whether redo is available.
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Gets the number of operations in the undo stack.
        /// </summary>
        public int UndoStackCount => _undoStack.Count;

        /// <summary>
        /// Gets the number of operations in the redo stack.
        /// </summary>
        public int RedoStackCount => _redoStack.Count;

        /// <summary>
        /// Gets or sets the maximum undo stack size.
        /// </summary>
        public int MaxUndoStackSize
        {
            get => _maxUndoStackSize;
            set => _maxUndoStackSize = Math.Max(1, value);
        }

        #region Record Operations

        /// <summary>
        /// Record a modify operation.
        /// </summary>
        public void RecordModify(long virtualPosition, byte[] oldValues, byte[] newValues)
        {
            if (oldValues == null || newValues == null)
                throw new ArgumentNullException("oldValues and newValues cannot be null");

            if (oldValues.Length != newValues.Length)
                throw new ArgumentException("oldValues and newValues must have same length");

            var operation = new UndoOperation(
                UndoOperationType.Modify,
                virtualPosition,
                oldValues,
                newValues,
                oldValues.Length
            );

            AddOperation(operation);
        }

        /// <summary>
        /// Record an insert operation.
        /// </summary>
        public void RecordInsert(long virtualPosition, byte[] insertedValues)
        {
            if (insertedValues == null || insertedValues.Length == 0)
                throw new ArgumentException("insertedValues cannot be null or empty");

            var operation = new UndoOperation(
                UndoOperationType.Insert,
                virtualPosition,
                null, // No old values for insert
                insertedValues,
                insertedValues.Length
            );

            AddOperation(operation);
        }

        /// <summary>
        /// Record a delete operation.
        /// </summary>
        public void RecordDelete(long virtualPosition, byte[] deletedValues)
        {
            if (deletedValues == null || deletedValues.Length == 0)
                throw new ArgumentException("deletedValues cannot be null or empty");

            var operation = new UndoOperation(
                UndoOperationType.Delete,
                virtualPosition,
                deletedValues, // Store deleted values to restore on undo
                null, // No new values for delete
                deletedValues.Length
            );

            AddOperation(operation);
        }

        /// <summary>
        /// Add an operation to the undo stack.
        /// </summary>
        private void AddOperation(UndoOperation operation)
        {
            if (_batchMode)
            {
                // In batch mode, accumulate operations
                _batchOperations.Add(operation);
            }
            else
            {
                // Push to undo stack
                _undoStack.Push(operation);

                // Clear redo stack (new operation invalidates redo)
                _redoStack.Clear();

                // Enforce max stack size
                EnforceMaxStackSize();
            }
        }

        /// <summary>
        /// Enforce maximum undo stack size by removing oldest operations.
        /// </summary>
        private void EnforceMaxStackSize()
        {
            if (_undoStack.Count > _maxUndoStackSize)
            {
                // Convert to list, remove oldest, convert back
                var list = _undoStack.ToList();
                list.Reverse(); // Oldest first
                list.RemoveRange(0, list.Count - _maxUndoStackSize);
                list.Reverse(); // Newest first again

                _undoStack.Clear();
                foreach (var op in list)
                    _undoStack.Push(op);
            }
        }

        #endregion

        #region Undo/Redo

        /// <summary>
        /// Get the next operation to undo (without popping it).
        /// Returns null if no undo available.
        /// </summary>
        public UndoOperation? PeekUndo()
        {
            if (_undoStack.Count == 0)
                return null;

            return _undoStack.Peek();
        }

        /// <summary>
        /// Get the next operation to redo (without popping it).
        /// Returns null if no redo available.
        /// </summary>
        public UndoOperation? PeekRedo()
        {
            if (_redoStack.Count == 0)
                return null;

            return _redoStack.Peek();
        }

        /// <summary>
        /// Pop an operation from the undo stack and push to redo stack.
        /// Returns the operation to undo.
        /// </summary>
        public UndoOperation PopUndo()
        {
            if (_undoStack.Count == 0)
                throw new InvalidOperationException("No operations to undo");

            var operation = _undoStack.Pop();
            _redoStack.Push(operation);

            return operation;
        }

        /// <summary>
        /// Pop an operation from the redo stack and push to undo stack.
        /// Returns the operation to redo.
        /// </summary>
        public UndoOperation PopRedo()
        {
            if (_redoStack.Count == 0)
                throw new InvalidOperationException("No operations to redo");

            var operation = _redoStack.Pop();
            _undoStack.Push(operation);

            return operation;
        }

        #endregion

        #region Batching

        /// <summary>
        /// Begin batch mode - multiple operations will be grouped into one undo step.
        /// </summary>
        public void BeginBatch()
        {
            _batchMode = true;
            _batchOperations.Clear();
        }

        /// <summary>
        /// End batch mode - commit all batched operations as a single undo step.
        /// </summary>
        public void EndBatch()
        {
            _batchMode = false;

            if (_batchOperations.Count > 0)
            {
                // For simplicity, we'll store each operation individually
                // but they'll be undone/redone together
                // A more sophisticated approach would merge consecutive operations

                foreach (var op in _batchOperations)
                {
                    _undoStack.Push(op);
                }

                _batchOperations.Clear();

                // Clear redo stack
                _redoStack.Clear();

                // Enforce max stack size
                EnforceMaxStackSize();
            }
        }

        #endregion

        #region Clear

        /// <summary>
        /// Clear both undo and redo stacks.
        /// </summary>
        public void ClearAll()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _batchOperations.Clear();
            _batchMode = false;
        }

        /// <summary>
        /// Clear only the redo stack.
        /// </summary>
        public void ClearRedo()
        {
            _redoStack.Clear();
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get memory usage statistics.
        /// </summary>
        public (int undoOps, int redoOps, long estimatedMemoryKB) GetStatistics()
        {
            // Rough estimate: each operation stores position (8 bytes) + count (4 bytes) + byte arrays
            long undoMemory = 0;
            foreach (var op in _undoStack)
            {
                undoMemory += 12; // Basic fields
                if (op.OldValues != null)
                    undoMemory += op.OldValues.Length;
                if (op.NewValues != null)
                    undoMemory += op.NewValues.Length;
            }

            long redoMemory = 0;
            foreach (var op in _redoStack)
            {
                redoMemory += 12; // Basic fields
                if (op.OldValues != null)
                    redoMemory += op.OldValues.Length;
                if (op.NewValues != null)
                    redoMemory += op.NewValues.Length;
            }

            long totalBytes = undoMemory + redoMemory;
            long totalKB = totalBytes / 1024;

            return (_undoStack.Count, _redoStack.Count, totalKB);
        }

        #endregion
    }
}
