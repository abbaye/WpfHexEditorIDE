//////////////////////////////////////////////
// Apache 2.0  - 2026
// Custom JsonEditor - Undo/Redo System (Phase 3)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexaEditor.Models.JsonEditor
{
    /// <summary>
    /// Undo/Redo stack for text editing operations.
    /// Maintains history of edits with configurable max size.
    /// </summary>
    public class UndoRedoStack
    {
        private Stack<TextEdit> _undoStack = new Stack<TextEdit>();
        private Stack<TextEdit> _redoStack = new Stack<TextEdit>();
        private const int MaxStackSize = 100;

        /// <summary>
        /// Push new edit onto undo stack
        /// Clears redo stack (new edit invalidates future)
        /// </summary>
        public void Push(TextEdit edit)
        {
            if (edit == null)
                return;

            _undoStack.Push(edit);
            _redoStack.Clear(); // Clear redo stack on new edit

            // Limit stack size (prevent memory issues)
            if (_undoStack.Count > MaxStackSize)
            {
                var temp = _undoStack.ToList();
                temp.RemoveAt(temp.Count - 1); // Remove oldest
                _undoStack = new Stack<TextEdit>(temp.AsEnumerable().Reverse());
            }
        }

        /// <summary>
        /// Undo last operation
        /// Returns edit to undo, or null if stack is empty
        /// </summary>
        public TextEdit Undo()
        {
            if (_undoStack.Count == 0)
                return null;

            var edit = _undoStack.Pop();
            _redoStack.Push(edit);
            return edit;
        }

        /// <summary>
        /// Redo last undone operation
        /// Returns edit to redo, or null if stack is empty
        /// </summary>
        public TextEdit Redo()
        {
            if (_redoStack.Count == 0)
                return null;

            var edit = _redoStack.Pop();
            _undoStack.Push(edit);
            return edit;
        }

        /// <summary>
        /// Can undo?
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Can redo?
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Clear all history
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        /// <summary>
        /// Get undo stack size
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Get redo stack size
        /// </summary>
        public int RedoCount => _redoStack.Count;
    }

    /// <summary>
    /// Represents a single text edit operation
    /// </summary>
    public class TextEdit
    {
        public TextEditType Type { get; set; }
        public TextPosition Position { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }

        public TextEdit()
        {
            Timestamp = DateTime.Now;
        }

        public TextEdit(TextEditType type, TextPosition position, string text) : this()
        {
            Type = type;
            Position = position;
            Text = text;
        }

        /// <summary>
        /// Create inverse operation (for undo)
        /// </summary>
        public TextEdit CreateInverse()
        {
            switch (Type)
            {
                case TextEditType.Insert:
                    return new TextEdit(TextEditType.Delete, Position, Text);

                case TextEditType.Delete:
                    return new TextEdit(TextEditType.Insert, Position, Text);

                case TextEditType.Replace:
                    // Replace is stored as delete + insert, so inverse is same
                    return new TextEdit(TextEditType.Replace, Position, Text);

                default:
                    return null;
            }
        }

        public override string ToString()
        {
            return $"{Type} at {Position}: \"{Text}\"";
        }
    }

    /// <summary>
    /// Type of text edit operation
    /// </summary>
    public enum TextEditType
    {
        Insert,
        Delete,
        Replace,
        NewLine,
        DeleteLine
    }
}
