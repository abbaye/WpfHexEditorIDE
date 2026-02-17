//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using WpfHexaEditor.Models;

namespace WpfHexaEditor
{
    /// <summary>
    /// HexEditor partial class - Edit Operations
    /// Contains methods for editing operations (undo, redo, copy, paste, delete, etc.)
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - Edit Operations

        /// <summary>
        /// Undo last operation
        /// </summary>
        public void Undo()
        {
            _viewModel?.Undo();
            OnUndoCompleted(EventArgs.Empty);
            OnUndone(EventArgs.Empty);
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        public void Redo()
        {
            _viewModel?.Redo();
            OnRedoCompleted(EventArgs.Empty);
            OnRedone(EventArgs.Empty);
        }

        /// <summary>
        /// Select all bytes
        /// </summary>
        public void SelectAll()
        {
            _viewModel?.SelectAll();
        }

        /// <summary>
        /// Clear selection
        /// </summary>
        public void ClearSelection()
        {
            _viewModel?.ClearSelection();
        }

        /// <summary>
        /// Delete selected bytes
        /// </summary>
        public void DeleteSelection()
        {
            _viewModel?.DeleteSelection();
        }

        /// <summary>
        /// Get selected bytes as byte array
        /// </summary>
        public byte[] GetSelectionByteArray()
        {
            return _viewModel?.GetSelectionBytes();
        }

        /// <summary>
        /// Set cursor position and scroll to make it visible
        /// </summary>
        public void SetPosition(long position)
        {
            if (_viewModel == null) return;

            var virtualPos = new VirtualPosition(position);
            _viewModel.SetSelection(virtualPos);

            // Scroll to make position visible
            EnsurePositionVisible(virtualPos);
        }

        /// <summary>
        /// Copy selected bytes to clipboard (as Hex or ASCII depending on current edit mode)
        /// </summary>
        public bool Copy()
        {
            bool result = _viewModel?.CopyToClipboard(_isAsciiEditMode) ?? false;
            if (result)
                OnDataCopied(EventArgs.Empty);
            return result;
        }

        /// <summary>
        /// Cut selected bytes to clipboard (copy + delete, clears selection after)
        /// </summary>
        public bool Cut()
        {
            return _viewModel?.Cut(_isAsciiEditMode) ?? false;
        }

        /// <summary>
        /// Paste bytes from clipboard at current position
        /// </summary>
        public bool Paste()
        {
            return _viewModel?.Paste() ?? false;
        }

        #endregion
    }
}
