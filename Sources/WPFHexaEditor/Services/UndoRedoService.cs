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
    /// Service responsible for undo/redo operations.
    /// Supports both ByteProviderLegacy (V1) and ByteProvider (V2).
    /// </summary>
    /// <example>
    /// Basic usage with ByteProvider V2:
    /// <code>
    /// var service = new UndoRedoService();
    /// var provider = new ByteProvider();
    ///
    /// // Perform undo
    /// if (service.CanUndo(provider))
    /// {
    ///     service.Undo(provider);
    ///     Console.WriteLine("Undone");
    /// }
    ///
    /// // Perform redo
    /// if (service.CanRedo(provider))
    /// {
    ///     service.Redo(provider);
    ///     Console.WriteLine("Redone");
    /// }
    ///
    /// // Clear history
    /// service.ClearAll(provider);
    ///
    /// // Check counts
    /// int undoCount = service.GetUndoCount(provider);
    /// int redoCount = service.GetRedoCount(provider);
    /// </code>
    ///
    /// Basic usage with ByteProviderLegacy (V1):
    /// <code>
    /// var service = new UndoRedoService();
    /// var legacyProvider = new ByteProviderLegacy();
    ///
    /// // Perform undo
    /// if (service.CanUndo(legacyProvider))
    /// {
    ///     long position = service.Undo(legacyProvider);
    ///     Console.WriteLine($"Undone to position {position}");
    /// }
    ///
    /// // Undo multiple times
    /// service.Undo(legacyProvider, repeat: 5); // Undo last 5 operations
    /// </code>
    /// </example>
    public class UndoRedoService
    {
        #region Undo Methods - ByteProvider V2

        /// <summary>
        /// Perform undo operation (ByteProvider V2)
        /// </summary>
        /// <param name="provider">ByteProvider V2 instance</param>
        /// <returns>True if undo was performed</returns>
        public bool Undo(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen || !provider.CanUndo)
                return false;

            provider.Undo();
            return true;
        }

        /// <summary>
        /// Perform redo operation (ByteProvider V2)
        /// </summary>
        /// <param name="provider">ByteProvider V2 instance</param>
        /// <returns>True if redo was performed</returns>
        public bool Redo(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen || !provider.CanRedo)
                return false;

            provider.Redo();
            return true;
        }

        #endregion

        #region Clear Methods - ByteProvider V2

        /// <summary>
        /// Clear all undo and redo history (ByteProvider V2)
        /// </summary>
        public void ClearAll(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return;

            provider.ClearUndoRedoHistory();
        }

        #endregion

        #region Query Methods - ByteProvider V2

        /// <summary>
        /// Check if undo is possible (ByteProvider V2)
        /// </summary>
        public bool CanUndo(ByteProvider provider)
        {
            return provider != null && provider.IsOpen && provider.CanUndo;
        }

        /// <summary>
        /// Check if redo is possible (ByteProvider V2)
        /// </summary>
        public bool CanRedo(ByteProvider provider)
        {
            return provider != null && provider.IsOpen && provider.CanRedo;
        }

        #endregion

        #region Undo Methods - ByteProviderLegacy (V1)

        /// <summary>
        /// Perform undo operation(s) (ByteProviderLegacy V1)
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="repeat">Number of undo operations to perform</param>
        /// <returns>Position of last undone byte, or -1 if no undo was performed</returns>
        public long Undo(ByteProviderLegacy provider, int repeat = 1)
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
        /// Perform redo operation(s) (ByteProviderLegacy V1)
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="repeat">Number of redo operations to perform</param>
        /// <returns>Position of last redone byte, or -1 if no redo was performed</returns>
        public long Redo(ByteProviderLegacy provider, int repeat = 1)
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

        #region Clear Methods - ByteProviderLegacy (V1)

        /// <summary>
        /// Clear all undo and redo history (ByteProviderLegacy V1)
        /// </summary>
        public void ClearAll(ByteProviderLegacy provider)
        {
            if (provider == null || !provider.IsOpen)
                return;

            provider.ClearUndoChange();
            provider.ClearRedoChange();
        }

        /// <summary>
        /// Clear only undo history (ByteProviderLegacy V1)
        /// </summary>
        public void ClearUndo(ByteProviderLegacy provider)
        {
            if (provider == null || !provider.IsOpen)
                return;

            provider.ClearUndoChange();
        }

        /// <summary>
        /// Clear only redo history (ByteProviderLegacy V1)
        /// </summary>
        public void ClearRedo(ByteProviderLegacy provider)
        {
            if (provider == null || !provider.IsOpen)
                return;

            provider.ClearRedoChange();
        }

        #endregion

        #region Query Methods - ByteProviderLegacy (V1)

        /// <summary>
        /// Check if undo is possible (ByteProviderLegacy V1)
        /// </summary>
        public bool CanUndo(ByteProviderLegacy provider)
        {
            return provider != null && provider.IsOpen && provider.UndoCount > 0;
        }

        /// <summary>
        /// Check if redo is possible (ByteProviderLegacy V1)
        /// </summary>
        public bool CanRedo(ByteProviderLegacy provider)
        {
            return provider != null && provider.IsOpen && provider.RedoCount > 0;
        }

        /// <summary>
        /// Get undo count (ByteProviderLegacy V1)
        /// </summary>
        public long GetUndoCount(ByteProviderLegacy provider)
        {
            return provider != null && provider.IsOpen ? provider.UndoCount : 0;
        }

        /// <summary>
        /// Get redo count (ByteProviderLegacy V1)
        /// </summary>
        public long GetRedoCount(ByteProviderLegacy provider)
        {
            return provider != null && provider.IsOpen ? provider.RedoCount : 0;
        }

        /// <summary>
        /// Get undo stack (ByteProviderLegacy V1)
        /// </summary>
        public Stack<ByteModified> GetUndoStack(ByteProviderLegacy provider)
        {
            return provider != null && provider.IsOpen ? provider.UndoStack : null;
        }

        /// <summary>
        /// Get redo stack (ByteProviderLegacy V1)
        /// </summary>
        public Stack<ByteModified> GetRedoStack(ByteProviderLegacy provider)
        {
            return provider != null && provider.IsOpen ? provider.RedoStack : null;
        }

        #endregion
    }
}
