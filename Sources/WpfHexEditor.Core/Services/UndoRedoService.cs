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
    /// </summary>
    /// <example>
    /// Basic usage with ByteProvider:
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
    /// </code>
    /// </example>
    public class UndoRedoService
    {
        #region Undo Methods - ByteProvider

        /// <summary>
        /// Perform undo operation (ByteProvider)
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <returns>True if undo was performed</returns>
        public bool Undo(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen || !provider.CanUndo)
                return false;

            provider.Undo();
            return true;
        }

        /// <summary>
        /// Perform redo operation (ByteProvider)
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <returns>True if redo was performed</returns>
        public bool Redo(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen || !provider.CanRedo)
                return false;

            provider.Redo();
            return true;
        }

        #endregion

        #region Clear Methods - ByteProvider

        /// <summary>
        /// Clear all undo and redo history (ByteProvider)
        /// </summary>
        public void ClearAll(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return;

            provider.ClearUndoRedoHistory();
        }

        #endregion

        #region Query Methods - ByteProvider

        /// <summary>
        /// Check if undo is possible (ByteProvider)
        /// </summary>
        public bool CanUndo(ByteProvider provider)
        {
            return provider != null && provider.IsOpen && provider.CanUndo;
        }

        /// <summary>
        /// Check if redo is possible (ByteProvider)
        /// </summary>
        public bool CanRedo(ByteProvider provider)
        {
            return provider != null && provider.IsOpen && provider.CanRedo;
        }

        #endregion
    }
}
