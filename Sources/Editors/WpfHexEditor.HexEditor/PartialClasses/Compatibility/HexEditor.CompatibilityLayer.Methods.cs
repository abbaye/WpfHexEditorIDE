// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.CompatibilityLayer.Methods.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class providing backward-compatibility method shims for the HexEditor.
//     Exposes V1-era public API methods that delegate to the new V2 architecture,
//     ensuring existing consumer code continues to compile without modification.
//
// Architecture Notes:
//     Compatibility layer pattern — thin delegation only, no business logic.
//     Kept in a separate partial class to isolate legacy API surface.
//
// ==========================================================

using System;
using System.Collections.Generic;
using System.Windows.Input;
using WpfHexEditor.Core;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Core.Models;
using WpfHexEditor.Core.Properties;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Compatibility Layer Methods
    /// Contains V1 backward compatibility methods: aliases, wrappers, and legacy method signatures
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - V1 Additional Compatibility

        /// <summary>
        /// Set position from hex string
        /// </summary>
        public void SetPosition(string hexLiteralPosition)
        {
            if (string.IsNullOrEmpty(hexLiteralPosition)) return;
            try
            {
                hexLiteralPosition = hexLiteralPosition.Replace("0x", "").Replace("0X", "");
                long position = Convert.ToInt64(hexLiteralPosition, 16);
                SetPosition(position);
            }
            catch { }
        }

        /// <summary>
        /// Set position and create selection
        /// </summary>
        public void SetPosition(long position, long byteLength)
        {
            if (_viewModel == null) return;
            SelectionStart = position;
            SelectionStop = position + byteLength - 1;
            SetPosition(position);
        }

        /// <summary>
        /// Submit changes (alias for Save)
        /// </summary>
        public void SubmitChanges() => Save();

        /// <summary>
        /// Submit changes to new file (alias for SaveAs)
        /// </summary>
        public void SubmitChanges(string newFilename, bool overwrite)
        {
            if (_viewModel == null) return;
            try
            {
                bool success = _viewModel.SaveAs(newFilename, overwrite);
                if (success)
                {
                    FileName = newFilename;
                    StatusText.Text = $"Saved to {System.IO.Path.GetFileName(newFilename)}";
                }
                else
                {
                    StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusText_FileAlreadyExists;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to save: {ex.Message}";
            }
        }

        /// <summary>
        /// Unselect all
        /// </summary>
        public void UnSelectAll(bool cleanFocus = false)
        {
            ClearSelection();
            if (cleanFocus) Keyboard.ClearFocus();
        }

        /// <summary>
        /// Undo with repeat count
        /// </summary>
        public void Undo(int repeat)
        {
            if (_viewModel == null) return;
            for (int i = 0; i < repeat; i++)
            {
                if (_viewModel.CanUndo)
                    Undo();
                else
                    break;
            }
        }

        /// <summary>
        /// Redo with repeat count
        /// </summary>
        public void Redo(int repeat)
        {
            if (_viewModel == null) return;
            for (int i = 0; i < repeat; i++)
            {
                if (_viewModel.CanRedo)
                    Redo();
                else
                    break;
            }
        }

        /// <summary>
        /// Clear all modifications and undo/redo history
        /// </summary>
        public void ClearAllChange()
        {
            if (_viewModel?.Provider == null) return;

            _viewModel.Provider.ClearAllEdits();
            IsModified = false;
            StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusText_AllChangesCleared;
        }

        /// <summary>
        /// Refresh view with options
        /// </summary>
        public void RefreshView(bool controlResize = false, bool refreshData = true)
        {
            if (_viewModel == null) return;
            if (refreshData)
            {
                _viewModel.RefreshDisplay();
                HexViewport?.InvalidateVisual();
            }
            if (controlResize)
            {
                InvalidateMeasure();
                InvalidateArrange();
            }
            InvalidateVisual();
        }

        /// <summary>
        /// Update visual rendering
        /// </summary>
        public void UpdateVisual()
        {
            InvalidateVisual();
            HexViewport?.InvalidateVisual();
        }

        /// <summary>
        /// Get line number from position
        /// </summary>
        public long GetLineNumber(long position) => _viewModel == null ? 0 : position / BytePerLine;

        /// <summary>
        /// Get column number from position
        /// </summary>
        public long GetColumnNumber(long position) => _viewModel == null ? 0 : position % BytePerLine;

        /// <summary>
        /// Check if byte position is visible in viewport
        /// </summary>
        public bool IsBytePositionAreVisible(long position)
        {
            if (_viewModel == null || HexViewport == null) return false;
            long startLine = _viewModel.ScrollPosition;
            long endLine = startLine + _viewModel.VisibleLines;
            long positionLine = position / BytePerLine;
            return positionLine >= startLine && positionLine < endLine;
        }

        /// <summary>
        /// Close provider with option to clear filename
        /// </summary>
        public void CloseProvider(bool clearFileName = true)
        {
            Close();
            if (clearFileName)
                FileName = string.Empty;
        }

        // ResetZoom moved to Zoom Support region above

        /// <summary>
        /// Update focus
        /// </summary>
        public void UpdateFocus()
        {
            HexViewport?.Focus();
        }

        /// <summary>
        /// Set focus at selection start
        /// </summary>
        public void SetFocusAtSelectionStart()
        {
            if (_viewModel != null && SelectionStart >= 0)
            {
                SetPosition(SelectionStart);
                UpdateFocus();
            }
        }

        /// <summary>
        /// Set focus at specific position
        /// </summary>
        public void SetFocusAt(long position)
        {
            SetPosition(position);
            UpdateFocus();
        }

        #endregion
        #region Missing V1 Methods - Bookmarks (Naming Alias)

        /// <summary>
        /// Set bookmark at current position (note capital M)
        /// This is an alias for SetBookmark() with different casing
        /// </summary>
        [Obsolete("Use SetBookmark() instead. This method exists only for V1 case-sensitive compatibility.", false)]
        public void SetBookMark()
        {
            SetBookmark(Position);
        }

        /// <summary>
        /// Set bookmark at position (note capital M)
        /// This is an alias for SetBookmark() with different casing
        /// </summary>
        /// <param name="position">Position to bookmark</param>
        [Obsolete("Use SetBookmark(long position) instead. This method exists only for V1 case-sensitive compatibility.", false)]
        public void SetBookMark(long position)
        {
            SetBookmark(position);
        }

        #endregion
        #region Missing V1 Methods - Scroll Markers

        /// <summary>
        /// Clear all scroll markers 
        /// </summary>
        public void ClearScrollMarker()
        {
            if (_scrollMarkers != null)
            {
                _scrollMarkers.ClearAllMarkers();
            }
        }

        /// <summary>
        /// Clear specific type of scroll marker 
        /// </summary>
        /// <param name="marker">Type of marker to clear</param>
        public void ClearScrollMarker(ScrollMarker marker)
        {
            if (_scrollMarkers == null) return;

            switch (marker)
            {
                case ScrollMarker.Nothing:
                    _scrollMarkers.ClearAllMarkers();
                    break;

                case ScrollMarker.SearchHighLight:
                    _scrollMarkers.SearchResultPositions = new HashSet<long>();
                    break;

                case ScrollMarker.Bookmark:
                case ScrollMarker.TblBookmark:
                    _scrollMarkers.BookmarkPositions = new HashSet<long>();
                    break;

                case ScrollMarker.ByteModified:
                case ScrollMarker.ByteDeleted:
                    _scrollMarkers.ModifiedPositions = new HashSet<long>();
                    break;

                case ScrollMarker.SelectionStart:
                    // Selection is not shown in scroll markers, so nothing to clear
                    break;
            }
        }

        #endregion
        #region Missing V1 Methods - TBL Support (Naming Alias)

        /// <summary>
        /// Load TBL file (note lowercase 'bl')
        /// This is an alias for LoadTBLFile() with different casing
        /// </summary>
        /// <param name="path">Path to TBL file</param>
        [Obsolete("Use LoadTBLFile(string path) instead. This method exists only for V1 case-sensitive compatibility.", false)]
        public void LoadTblFile(string path)
        {
            LoadTBLFile(path);
        }

        /// <summary>
        /// Load a default built-in TBL table with ASCII encoding 
        /// </summary>
        public void LoadDefaultTbl()
        {
            LoadDefaultTbl(DefaultCharacterTableType.Ascii);
        }

        /// <summary>
        /// Load a default built-in TBL table 
        /// </summary>
        /// <param name="type">Type of default table to load</param>
        public void LoadDefaultTbl(DefaultCharacterTableType type)
        {
            try
            {
                _tblStream = TblStream.CreateDefaultTbl(type);
                _characterTableType = CharacterTableType.TblFile;

                // Sync TblStream to HexViewport for color rendering
                if (HexViewport != null)
                    HexViewport.TblStream = _tblStream;

                StatusText.Text = $"Default TBL loaded: {type}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to load default TBL: {ex.Message}";
                _tblStream = null;
                _characterTableType = CharacterTableType.Ascii;
            }
        }

        #endregion
        #region Missing V1 Methods - Reverse Selection

        /// <summary>
        /// Reverse the byte order of the current selection 
        /// </summary>
        public void ReverseSelection()
        {
            if (_viewModel == null || !_viewModel.HasSelection)
            {
                StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusText_NoSelectionToReverse;
                return;
            }

            try
            {
                // Get the selected bytes
                var start = _viewModel.SelectionStart.Value;
                var bytes = _viewModel.GetSelectionBytes();
                if (bytes == null || bytes.Length == 0)
                {
                    StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusText_SelectionIsEmpty;
                    return;
                }

                // Reverse the byte array
                Array.Reverse(bytes);

                // Write the reversed bytes back
                for (int i = 0; i < bytes.Length; i++)
                {
                    _viewModel.ModifyByte(new VirtualPosition(start + i), bytes[i]);
                }

                StatusText.Text = $"Reversed {bytes.Length} bytes";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Reverse failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Invert the bits of each byte in the current selection (XOR with 0xFF)
        /// </summary>
        public void InvertSelection()
        {
            if (_viewModel == null || !_viewModel.HasSelection)
            {
                StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusText_NoSelectionToInvert;
                return;
            }

            try
            {
                // Get the selected bytes
                var start = _viewModel.SelectionStart.Value;
                var bytes = _viewModel.GetSelectionBytes();
                if (bytes == null || bytes.Length == 0)
                {
                    StatusText.Text = WpfHexEditor.Core.Properties.Resources.StatusText_SelectionIsEmpty;
                    return;
                }

                // Invert each byte (XOR with 0xFF)
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = (byte)(bytes[i] ^ 0xFF);
                }

                // Write the inverted bytes back
                for (int i = 0; i < bytes.Length; i++)
                {
                    _viewModel.ModifyByte(new VirtualPosition(start + i), bytes[i]);
                }

                StatusText.Text = $"Inverted {bytes.Length} bytes";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Invert failed: {ex.Message}";
            }
        }

        #endregion
    }
}
