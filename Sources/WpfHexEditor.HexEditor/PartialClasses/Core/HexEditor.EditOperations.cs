// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.EditOperations.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class containing edit operation methods for the HexEditor.
//     Covers undo, redo, copy, paste, delete, and insert operations on byte ranges,
//     all integrated with the changeset system for full undo/redo support.
//
// Architecture Notes:
//     All mutations use the changeset pipeline. Clipboard operations use WPF Clipboard
//     with hex string and raw byte formats. Depends on WpfHexEditor.Core.Models.
//
// ==========================================================

using System;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.HexEditor
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

            // Notify plugins — ViewModel.SelectAll bypasses DP callbacks.
            OnSelectionStartChanged(EventArgs.Empty);
            OnSelectionStopChanged(EventArgs.Empty);
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
        /// Get selected bytes as byte array.
        /// When no multi-byte selection exists (caret only), returns up to 8 bytes
        /// starting at the caret position so the DataInspector always has data to show.
        /// </summary>
        public byte[] GetSelectionByteArray()
        {
            if (_viewModel == null) return null;

            // Multi-byte selection: return exactly the selected bytes.
            var selection = _viewModel.GetSelectionBytes();
            if (selection != null && selection.Length > 0) return selection;

            // Caret-only: read up to 8 bytes at SelectionStart for DataInspector.
            var caretPos = _viewModel.SelectionStart;
            if (!caretPos.IsValid) return null;

            long count = Math.Min(8, _viewModel.VirtualLength - caretPos.Value);
            if (count <= 0) return null;

            var bytes = new byte[count];
            for (int i = 0; i < count; i++)
                bytes[i] = _viewModel.GetByteAt(new VirtualPosition(caretPos.Value + i));

            return bytes;
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

        /// <summary>
        /// Clear only byte modifications (keep insertions and deletions)
        /// </summary>
        /// <remarks>
        /// This method provides granular control over clearing edits.
        /// Most use cases should use ClearAllChange() instead.
        ///
        /// Example use case:
        /// <code>
        /// // User wants to keep structural changes (insertions/deletions)
        /// // but reset all byte value modifications
        /// hexEditor.ClearModifications();
        /// </code>
        /// </remarks>
        public void ClearModifications()
        {
            if (_viewModel?.Provider == null) return;
            _viewModel.Provider.ClearModifications();
            UpdateVisibleLines();
        }

        /// <summary>
        /// Clear only byte insertions (keep modifications and deletions)
        /// </summary>
        /// <remarks>
        /// This method provides granular control over clearing edits.
        /// Most use cases should use ClearAllChange() instead.
        ///
        /// Example use case:
        /// <code>
        /// // User wants to keep byte modifications and deletions
        /// // but remove all insertions
        /// hexEditor.ClearInsertions();
        /// </code>
        /// </remarks>
        public void ClearInsertions()
        {
            if (_viewModel?.Provider == null) return;
            _viewModel.Provider.ClearInsertions();
            UpdateVisibleLines();
        }

        /// <summary>
        /// Clear only byte deletions (keep modifications and insertions)
        /// </summary>
        /// <remarks>
        /// This method provides granular control over clearing edits.
        /// Most use cases should use ClearAllChange() instead.
        ///
        /// Example use case:
        /// <code>
        /// // User wants to keep byte modifications and insertions
        /// // but restore all deleted bytes
        /// hexEditor.ClearDeletions();
        /// </code>
        /// </remarks>
        public void ClearDeletions()
        {
            if (_viewModel?.Provider == null) return;
            _viewModel.Provider.ClearDeletions();
            UpdateVisibleLines();
        }

        #endregion
    }
}
