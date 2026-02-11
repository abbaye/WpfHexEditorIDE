//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service responsible for undo/redo operations
    /// </summary>
    public class UndoRedoService
    {
        #region Undo Methods

        /// <summary>
        /// Perform undo operation(s)
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="repeat">Number of undo operations to perform</param>
        /// <returns>Position of last undone byte, or -1 if no undo was performed</returns>
        public long Undo(ByteProvider provider, int repeat = 1)
        {
            if (provider == null || !provider.IsOpen)
                return -1;

            for (var i = 0; i < repeat; i++)
                provider.Undo();

            // Return position of first item in undo stack for UI update
            if (provider.UndoStack != null && provider.UndoStack.Count > 0)
            {
                var topItem = provider.UndoStack.Peek();
                return topItem?.BytePositionInStream ?? -1;
            }

            return -1;
        }

        /// <summary>
        /// Perform redo operation(s)
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="repeat">Number of redo operations to perform</param>
        /// <returns>Position of last redone byte, or -1 if no redo was performed</returns>
        public long Redo(ByteProvider provider, int repeat = 1)
        {
            if (provider == null || !provider.IsOpen)
                return -1;

            for (var i = 0; i < repeat; i++)
                provider.Redo();

            // Return position of first item in redo stack for UI update
            if (provider.RedoStack != null && provider.RedoStack.Count > 0)
            {
                var topItem = provider.RedoStack.Peek();
                return topItem?.BytePositionInStream ?? -1;
            }

            return -1;
        }

        #endregion

        #region Clear Methods

        /// <summary>
        /// Clear all undo and redo history
        /// </summary>
        public void ClearAll(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return;

            provider.ClearUndoChange();
            provider.ClearRedoChange();
        }

        /// <summary>
        /// Clear only undo history
        /// </summary>
        public void ClearUndo(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return;

            provider.ClearUndoChange();
        }

        /// <summary>
        /// Clear only redo history
        /// </summary>
        public void ClearRedo(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return;

            provider.ClearRedoChange();
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Check if undo is possible
        /// </summary>
        public bool CanUndo(ByteProvider provider)
        {
            return provider != null && provider.IsOpen && provider.UndoCount > 0;
        }

        /// <summary>
        /// Check if redo is possible
        /// </summary>
        public bool CanRedo(ByteProvider provider)
        {
            return provider != null && provider.IsOpen && provider.RedoCount > 0;
        }

        /// <summary>
        /// Get undo count
        /// </summary>
        public long GetUndoCount(ByteProvider provider)
        {
            return provider != null && provider.IsOpen ? provider.UndoCount : 0;
        }

        /// <summary>
        /// Get redo count
        /// </summary>
        public long GetRedoCount(ByteProvider provider)
        {
            return provider != null && provider.IsOpen ? provider.RedoCount : 0;
        }

        /// <summary>
        /// Get undo stack
        /// </summary>
        public Stack<ByteModified> GetUndoStack(ByteProvider provider)
        {
            return provider != null && provider.IsOpen ? provider.UndoStack : null;
        }

        /// <summary>
        /// Get redo stack
        /// </summary>
        public Stack<ByteModified> GetRedoStack(ByteProvider provider)
        {
            return provider != null && provider.IsOpen ? provider.RedoStack : null;
        }

        #endregion
    }
}
