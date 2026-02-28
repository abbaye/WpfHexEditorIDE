//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Find/Replace Operations
    /// Contains string and byte[] find/replace methods with V1 compatibility overloads
    /// </summary>
    public partial class HexEditor
    {
        #region String Search/Replace Methods

        /// <summary>
        /// Find first occurrence of string
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <param name="startPosition">Position to start search from</param>
        /// <returns>Position of first occurrence, or -1 if not found</returns>
        public long FindFirst(string text, long startPosition = 0)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            byte[] bytes = GetBytesFromString(text);
            return FindFirst(bytes, startPosition);
        }

        /// <summary>
        /// Find next occurrence of string
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <returns>Position of next occurrence, or -1 if not found</returns>
        public long FindNext(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            byte[] bytes = GetBytesFromString(text);
            // V1 behavior: FindNext searches from current position + 1
            long currentPos = Position;
            return FindNext(bytes, currentPos);
        }

        /// <summary>
        /// Find last occurrence of string
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <returns>Position of last occurrence, or -1 if not found</returns>
        public long FindLast(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            byte[] bytes = GetBytesFromString(text);
            return FindLast(bytes, 0);
        }

        /// <summary>
        /// Replace first occurrence of string
        /// </summary>
        public long ReplaceFirst(string findText, string replaceText, long startPosition = 0, bool truncateLength = false)
        {
            if (string.IsNullOrEmpty(findText)) return -1;
            byte[] findBytes = GetBytesFromString(findText);
            byte[] replaceBytes = GetBytesFromString(replaceText ?? string.Empty);
            return ReplaceFirst(findBytes, replaceBytes, startPosition, truncateLength);
        }

        /// <summary>
        /// Replace next occurrence of string
        /// </summary>
        public long ReplaceNext(string findText, string replaceText, long currentPosition, bool truncateLength = false)
        {
            if (string.IsNullOrEmpty(findText)) return -1;
            byte[] findBytes = GetBytesFromString(findText);
            byte[] replaceBytes = GetBytesFromString(replaceText ?? string.Empty);
            return ReplaceNext(findBytes, replaceBytes, currentPosition, truncateLength);
        }

        /// <summary>
        /// Replace all occurrences of string
        /// </summary>
        public int ReplaceAll(string findText, string replaceText, bool truncateLength = false)
        {
            if (string.IsNullOrEmpty(findText)) return 0;
            byte[] findBytes = GetBytesFromString(findText);
            byte[] replaceBytes = GetBytesFromString(replaceText ?? string.Empty);
            return ReplaceAll(findBytes, replaceBytes, truncateLength);
        }

        /// <summary>
        /// Helper: Convert string to bytes using current character table encoding
        /// </summary>
        private byte[] GetBytesFromString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<byte>();

            // Use encoding based on character table type
            var encoding = _characterTableType == CharacterTableType.Ascii
                ? System.Text.Encoding.ASCII
                : System.Text.Encoding.UTF8;

            return encoding.GetBytes(text);
        }

        #endregion

        #region Missing V1 Methods - Find All Selection

        /// <summary>
        /// Find all occurrences of the current selection
        /// Highlights all matching bytes in the file
        /// </summary>
        /// <param name="highlight">Whether to highlight results (V1 parameter, always highlights in V2)</param>
        public void FindAllSelection(bool highlight = true)
        {
            FindAllSelection();
        }

        /// <summary>
        /// Find all occurrences of the current selection
        /// Highlights all matching bytes in the file
        /// </summary>
        private void FindAllSelection()
        {
            if (_viewModel == null || !_viewModel.HasSelection)
            {
                StatusText.Text = "No selection to find";
                return;
            }

            try
            {
                // Get the selected bytes
                var pattern = _viewModel.GetSelectionBytes();
                if (pattern == null || pattern.Length == 0)
                {
                    StatusText.Text = "Selection is empty";
                    return;
                }

                // Find all occurrences
                var positions = new List<long>();
                long pos = 0;
                while (pos >= 0 && pos < _viewModel.VirtualLength)
                {
                    pos = FindFirst(pattern, pos);
                    if (pos >= 0)
                    {
                        positions.Add(pos);
                        pos++; // Move to next position
                    }
                }

                // Highlight all found positions using custom background blocks
                ClearCustomBackgroundBlock();
                foreach (var position in positions)
                {
                    var block = new CustomBackgroundBlock(
                        position,
                        pattern.Length,
                        new SolidColorBrush(Colors.Yellow),
                        "Found"
                    );
                    AddCustomBackgroundBlock(block);
                }

                StatusText.Text = $"Found {positions.Count} occurrences";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Find all failed: {ex.Message}";
            }
        }

        #endregion

        #region Find/Replace Dialog Overloads

        /// <summary>
        /// Find first occurrence with highlight support (V1 dialog compatible)
        /// </summary>
        public long FindFirst(byte[] data, long startPosition, bool highLight)
        {
            var result = FindFirst(data, startPosition);
            // V2 doesn't support inline highlight parameter, but we can ignore it
            return result;
        }

        /// <summary>
        /// Find next occurrence with highlight support (V1 dialog compatible)
        /// </summary>
        public long FindNext(byte[] data, bool highLight)
        {
            return FindNext(data, Position);
        }

        /// <summary>
        /// Find last occurrence with highlight support (V1 dialog compatible)
        /// </summary>
        public long FindLast(byte[] data, bool highLight)
        {
            return FindLast(data);
        }

        /// <summary>
        /// Find all occurrences with highlight support (V1 dialog compatible)
        /// Returns IEnumerable for V1 compatibility
        /// </summary>
        public IEnumerable<long> FindAll(byte[] data, bool highLight)
        {
            return FindAll(data, 0);
        }

        /// <summary>
        /// Replace first with V1 signature (truckLength, then highlight)
        /// </summary>
        public long ReplaceFirst(byte[] findData, byte[] replaceData, bool truckLength, bool hightlight)
        {
            return ReplaceFirst(findData, replaceData, 0, truckLength);
        }

        /// <summary>
        /// Replace next with V1 signature (truckLength, then highlight)
        /// </summary>
        public long ReplaceNext(byte[] findData, byte[] replaceData, bool truckLength, bool hightlight)
        {
            return ReplaceNext(findData, replaceData, Position + 1, truckLength);
        }

        /// <summary>
        /// Replace all with V1 signature (truckLength, then highlight)
        /// Returns IEnumerable for V1 dialog compatibility
        /// </summary>
        public IEnumerable<long> ReplaceAll(byte[] findData, byte[] replaceData, bool truckLength, bool hightlight)
        {
            // V2 ReplaceAll returns int (count), but V1 dialogs expect IEnumerable<long> (positions)
            // For compatibility, we'll find all positions and replace them, returning the positions
            var positions = new List<long>();
            long pos = 0;
            while (pos >= 0 && pos < VirtualLength)
            {
                pos = FindFirst(findData, pos);
                if (pos >= 0)
                {
                    positions.Add(pos);
                    // Replace at this position
                    ReplaceFirst(findData, replaceData, pos, truckLength);
                    pos += replaceData.Length; // Move past the replaced data
                }
            }
            return positions;
        }

        #endregion
    }
}
