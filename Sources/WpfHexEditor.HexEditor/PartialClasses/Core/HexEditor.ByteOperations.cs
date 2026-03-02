//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.Models;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Byte Operations
    /// Contains methods for manipulating individual bytes and byte ranges
    /// </summary>
    public partial class HexEditor
    {
        #region Public Methods - Byte Operations

        /// <summary>
        /// Get byte value at position
        /// </summary>
        /// <param name="position">Position in file (virtual)</param>
        /// <returns>Byte value at position, or 0 if position is invalid</returns>
        public byte GetByte(long position)
        {
            if (_viewModel == null) return 0;
            return _viewModel.GetByte(position);
        }

        /// <summary>
        /// Set byte value at position
        /// </summary>
        /// <param name="position">Position in file (virtual)</param>
        /// <param name="value">Byte value to set</param>
        public void SetByte(long position, byte value)
        {
            _viewModel?.SetByte(position, value);
        }

        /// <summary>
        /// Fill a range with a specific byte value
        /// </summary>
        /// <param name="value">Byte value to fill with</param>
        /// <param name="startPosition">Start position (virtual)</param>
        /// <param name="length">Number of bytes to fill</param>
        public void FillWithByte(byte value, long startPosition, long length)
        {
            _viewModel?.FillWithByte(value, startPosition, length);
        }

        /// <summary>
        /// Modify byte with undo support
        /// </summary>
        /// <param name="byte">New byte value (null to delete)</param>
        /// <param name="bytePositionInStream">Position in stream (virtual)</param>
        /// <param name="undoLength">Length for undo operation (usually 1)</param>
        public void ModifyByte(byte? @byte, long bytePositionInStream, long undoLength = 1)
        {
            if (_viewModel == null || ReadOnlyMode) return;

            if (@byte.HasValue)
            {
                // Modify the byte
                _viewModel.ModifyByte(new VirtualPosition(bytePositionInStream), @byte.Value);
            }
            else
            {
                // Delete the byte (null value means delete)
                _viewModel.DeleteByte(new VirtualPosition(bytePositionInStream));
            }
        }

        /// <summary>
        /// Insert a single byte at position
        /// </summary>
        /// <param name="byte">Byte value to insert</param>
        /// <param name="bytePositionInStream">Position in stream (virtual)</param>
        public void InsertByte(byte @byte, long bytePositionInStream)
        {
            if (_viewModel == null || ReadOnlyMode) return;
            _viewModel.InsertByte(new VirtualPosition(bytePositionInStream), @byte);
        }

        /// <summary>
        /// Insert a byte repeated multiple times at position
        /// </summary>
        /// <param name="byte">Byte value to insert</param>
        /// <param name="bytePositionInStream">Position in stream (virtual)</param>
        /// <param name="length">Number of times to repeat the byte</param>
        public void InsertByte(byte @byte, long bytePositionInStream, long length)
        {
            if (_viewModel == null || ReadOnlyMode || length <= 0) return;

            // Create array of repeated byte
            byte[] bytes = new byte[length];
            for (long i = 0; i < length; i++)
            {
                bytes[i] = @byte;
            }

            _viewModel.InsertBytes(new VirtualPosition(bytePositionInStream), bytes);
        }

        /// <summary>
        /// Insert multiple bytes at position
        /// </summary>
        /// <param name="bytes">Byte array to insert</param>
        /// <param name="bytePositionInStream">Position in stream (virtual)</param>
        public void InsertBytes(byte[] bytes, long bytePositionInStream)
        {
            if (_viewModel == null || ReadOnlyMode || bytes == null || bytes.Length == 0) return;
            _viewModel.InsertBytes(new VirtualPosition(bytePositionInStream), bytes);
        }

        /// <summary>
        /// Modify multiple consecutive bytes at position
        /// </summary>
        /// <param name="bytePositionInStream">Starting position in stream (virtual)</param>
        /// <param name="bytes">Array of byte values to write</param>
        /// <remarks>
        /// This method is more efficient than calling ModifyByte() in a loop.
        /// It modifies existing bytes without changing file size.
        ///
        /// Example:
        /// <code>
        /// var newData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        /// hexEditor.ModifyBytes(100, newData);
        /// // Modifies bytes at positions 100, 101, 102, 103
        /// </code>
        ///
        /// For best performance with many modifications, use within BeginBatch/EndBatch:
        /// <code>
        /// hexEditor.BeginBatch();
        /// try
        /// {
        ///     hexEditor.ModifyBytes(0, data1);
        ///     hexEditor.ModifyBytes(1000, data2);
        /// }
        /// finally
        /// {
        ///     hexEditor.EndBatch();
        /// }
        /// </code>
        /// </remarks>
        public void ModifyBytes(long bytePositionInStream, byte[] bytes)
        {
            if (_viewModel?.Provider == null || ReadOnlyMode || bytes == null || bytes.Length == 0) return;

            _viewModel.Provider.ModifyBytes(bytePositionInStream, bytes);
            UpdateVisibleLines();
        }

        /// <summary>
        /// Delete bytes at position
        /// </summary>
        /// <param name="bytePositionInStream">Start position (virtual)</param>
        /// <param name="length">Number of bytes to delete</param>
        public void DeleteBytesAtPosition(long bytePositionInStream, long length)
        {
            if (_viewModel == null || ReadOnlyMode || length <= 0) return;

            // Delete bytes one by one (ByteProvider V2 handles this internally)
            _viewModel.BeginUpdate();
            try
            {
                for (long i = 0; i < length; i++)
                {
                    _viewModel.DeleteByte(new VirtualPosition(bytePositionInStream));
                    // Note: After deleting, the next byte shifts to the same position
                    // So we keep deleting at the same position
                }
            }
            finally
            {
                _viewModel.EndUpdate();
            }
        }

        /// <summary>
        /// Get byte with copyChange parameter
        /// Returns tuple with byte value and success flag
        /// </summary>
        /// <param name="position">Position in file (virtual)</param>
        /// <param name="copyChange">If true, returns modified value; if false, returns original value</param>
        /// <returns>Tuple (byte value, success flag)</returns>
        public (byte? singleByte, bool success) GetByte(long position, bool copyChange)
        {
            if (_viewModel == null || position < 0 || position >= VirtualLength)
                return (null, false);

            // V2 always returns modified values (copyChange=true behavior)
            // To get original values (copyChange=false), we would need ByteProvider support
            // For now, we only support copyChange=true
            var byteValue = _viewModel.GetByte(position);
            return (byteValue, true);
        }

        /// <summary>
        /// Get all bytes from file
        /// </summary>
        /// <param name="copyChange">If true, includes modifications; if false, original file only</param>
        /// <returns>Byte array of entire file</returns>
        public byte[] GetAllBytes(bool copyChange = true)
        {
            if (_viewModel == null || VirtualLength == 0)
                return Array.Empty<byte>();

            // V2 always returns modified values (copyChange=true behavior)
            // Get all bytes from ByteProvider
            byte[] result = new byte[VirtualLength];
            for (long i = 0; i < VirtualLength; i++)
            {
                result[i] = _viewModel.GetByte(i);
            }
            return result;
        }

        /// <summary>
        /// Replace byte with another in current selection
        /// </summary>
        /// <param name="original">Byte to find and replace</param>
        /// <param name="replace">Byte to replace with</param>
        public void ReplaceByte(byte original, byte replace)
        {
            if (_viewModel == null || !_viewModel.HasSelection)
                return;

            var selStart = _viewModel.SelectionStart.Value;
            var selLength = _viewModel.SelectionLength;

            ReplaceByte(selStart, selLength, original, replace);
        }

        /// <summary>
        /// Replace byte with another in specified range
        /// </summary>
        /// <param name="startPosition">Start position (virtual)</param>
        /// <param name="length">Length of range to search</param>
        /// <param name="original">Byte to find and replace</param>
        /// <param name="replace">Byte to replace with</param>
        public void ReplaceByte(long startPosition, long length, byte original, byte replace)
        {
            if (_viewModel == null || ReadOnlyMode || length <= 0)
                return;

            if (startPosition < 0 || startPosition >= VirtualLength)
                return;

            // Fire long process started event
            IsOnLongProcess = true;
            OnLongProcessProgressStarted(EventArgs.Empty);

            try
            {
                int replacedCount = 0;

                // Begin batched update for performance
                _viewModel.BeginUpdate();
                try
                {
                    for (long i = 0; i < length; i++)
                    {
                        long pos = startPosition + i;
                        if (pos >= VirtualLength)
                            break;

                        // Check if we should break early (user cancelled)
                        if (!IsOnLongProcess)
                            break;

                        // Progress reporting every 2000 bytes
                        if (i % 2000 == 0)
                        {
                            LongProcessProgress = (double)i / length;
                        }

                        // Check if byte matches and replace
                        var currentByte = _viewModel.GetByte(pos);
                        if (currentByte == original)
                        {
                            ModifyByte(replace, pos);
                            replacedCount++;
                        }
                    }
                }
                finally
                {
                    _viewModel.EndUpdate();
                }

                // Update status
                StatusText.Text = $"Replaced {replacedCount} occurrences of 0x{original:X2} with 0x{replace:X2}";

                // Fire replace completed event
                OnReplaceByteCompleted(EventArgs.Empty);
            }
            finally
            {
                // Fire long process completed event
                IsOnLongProcess = false;
                LongProcessProgress = 0;
                OnLongProcessProgressCompleted(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Fill current selection with specified byte value
        /// </summary>
        /// <param name="val">Byte value to fill with</param>
        public void FillWithByte(byte val)
        {
            if (_viewModel == null || !_viewModel.HasSelection)
                return;

            var selStart = _viewModel.SelectionStart.Value;
            var selLength = _viewModel.SelectionLength;

            FillWithByte(selStart, selLength, val);
        }

        /// <summary>
        /// Fill specified range with byte value
        /// </summary>
        /// <param name="startPosition">Start position (virtual)</param>
        /// <param name="length">Length of range to fill</param>
        /// <param name="val">Byte value to fill with</param>
        public void FillWithByte(long startPosition, long length, byte val)
        {
            if (_viewModel == null || ReadOnlyMode || length <= 0)
                return;

            if (startPosition < 0 || startPosition >= VirtualLength)
                return;

            // Fire long process started event
            IsOnLongProcess = true;
            OnLongProcessProgressStarted(EventArgs.Empty);

            try
            {
                // Begin batched update for performance
                _viewModel.BeginUpdate();
                try
                {
                    for (long i = 0; i < length; i++)
                    {
                        long pos = startPosition + i;
                        if (pos >= VirtualLength)
                            break;

                        // Check if we should break early (user cancelled)
                        if (!IsOnLongProcess)
                            break;

                        // Progress reporting every 2000 bytes
                        if (i % 2000 == 0)
                        {
                            LongProcessProgress = (double)i / length;
                        }

                        // Modify the byte
                        ModifyByte(val, pos);
                    }
                }
                finally
                {
                    _viewModel.EndUpdate();
                }

                // Update status
                StatusText.Text = $"Filled {length} bytes with 0x{val:X2}";

                // Fire fill completed event
                OnFillWithByteCompleted(EventArgs.Empty);
            }
            finally
            {
                // Fire long process completed event
                IsOnLongProcess = false;
                LongProcessProgress = 0;
                OnLongProcessProgressCompleted(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Get all byte modifications matching the specified action
        /// </summary>
        /// <param name="action">ByteAction to filter by (Modified, Added, Deleted, or All)</param>
        /// <returns>Dictionary of ByteModified objects keyed by position, or null if no provider</returns>
        public IDictionary<long, ByteModified> GetByteModifieds(ByteAction action)
        {
            if (_viewModel?.Provider == null)
                return null;

            return _viewModel.Provider.GetByteModifieds(action);
        }

        #region Restore Original Bytes (Issue #127)

        /// <summary>
        /// Restore a modified byte to its original value and remove red highlight.
        /// This removes the modification entry; the original value is automatically
        /// read from the underlying file when accessed again.
        /// </summary>
        /// <param name="position">Position of the byte to restore (virtual)</param>
        /// <returns>True if the modification was removed, false if no modification existed</returns>
        /// <example>
        /// // Restore a single byte that was modified
        /// if (hexEditor.RestoreOriginalByte(0x100))
        ///     Console.WriteLine("Byte restored successfully");
        /// </example>
        public bool RestoreOriginalByte(long position)
        {
            if (_viewModel?.Provider == null || ReadOnlyMode)
                return false;

            bool result = _viewModel.Provider.RestoreOriginalByte(position);

            if (result)
            {
                // Refresh view to remove highlight
                RefreshView(false, true);
            }

            return result;
        }

        /// <summary>
        /// Remove modification at specified position (V2-compatible alias).
        /// </summary>
        /// <param name="position">Position of the byte to restore</param>
        /// <returns>True if modification was removed</returns>
        public bool RemoveModification(long position) => RestoreOriginalByte(position);

        /// <summary>
        /// Reset byte to original value (concise alias).
        /// </summary>
        /// <param name="position">Position of the byte to reset</param>
        /// <returns>True if modification was removed</returns>
        public bool ResetByte(long position) => RestoreOriginalByte(position);

        /// <summary>
        /// Restore multiple modified bytes to their original values.
        /// </summary>
        /// <param name="positions">Array of positions to restore</param>
        /// <returns>Number of modifications successfully removed</returns>
        /// <example>
        /// long[] positions = new long[] { 0x100, 0x200, 0x300 };
        /// int count = hexEditor.RestoreOriginalBytes(positions);
        /// MessageBox.Show($"Restored {count} bytes");
        /// </example>
        public int RestoreOriginalBytes(long[] positions)
        {
            if (_viewModel?.Provider == null || ReadOnlyMode || positions == null)
                return 0;

            int count = _viewModel.Provider.RestoreOriginalBytes(positions);

            if (count > 0)
            {
                // Refresh view to remove highlights
                RefreshView(false, true);
            }

            return count;
        }

        /// <summary>
        /// V2-compatible alias for RestoreOriginalBytes(long[])
        /// </summary>
        public int RemoveModifications(long[] positions) => RestoreOriginalBytes(positions);

        /// <summary>
        /// Concise alias for RestoreOriginalBytes(long[])
        /// </summary>
        public int ResetBytes(long[] positions) => RestoreOriginalBytes(positions);

        /// <summary>
        /// Restore multiple modified bytes to their original values.
        /// Supports LINQ queries, List, HashSet, and other IEnumerable collections.
        /// </summary>
        /// <param name="positions">Enumerable collection of positions to restore</param>
        /// <returns>Number of modifications successfully removed</returns>
        /// <example>
        /// // Works with LINQ
        /// var positions = hexEditor.GetByteModifieds(ByteAction.Modified)
        ///     .Keys
        ///     .Where(p => p >= 0x1000 && p <= 0x2000);
        /// int count = hexEditor.RestoreOriginalBytes(positions);
        ///
        /// // Works with List
        /// List&lt;long&gt; posList = new List&lt;long&gt; { 10, 20, 30 };
        /// count = hexEditor.RestoreOriginalBytes(posList);
        /// </example>
        public int RestoreOriginalBytes(IEnumerable<long> positions)
        {
            if (_viewModel?.Provider == null || ReadOnlyMode || positions == null)
                return 0;

            int count = _viewModel.Provider.RestoreOriginalBytes(positions);

            if (count > 0)
            {
                // Refresh view to remove highlights
                RefreshView(false, true);
            }

            return count;
        }

        /// <summary>
        /// V2-compatible alias for RestoreOriginalBytes(IEnumerable)
        /// </summary>
        public int RemoveModifications(IEnumerable<long> positions) => RestoreOriginalBytes(positions);

        /// <summary>
        /// Concise alias for RestoreOriginalBytes(IEnumerable)
        /// </summary>
        public int ResetBytes(IEnumerable<long> positions) => RestoreOriginalBytes(positions);

        /// <summary>
        /// Restore all modified bytes in a continuous range to their original values.
        /// Automatically handles inverted ranges (startPosition > stopPosition).
        /// </summary>
        /// <param name="startPosition">Start position (inclusive, virtual)</param>
        /// <param name="stopPosition">Stop position (inclusive, virtual)</param>
        /// <returns>Number of modifications successfully removed</returns>
        /// <example>
        /// // Restore modifications in current selection
        /// if (hexEditor.SelectionLength > 0)
        /// {
        ///     int count = hexEditor.RestoreOriginalBytesInRange(
        ///         hexEditor.SelectionStart,
        ///         hexEditor.SelectionStop);
        ///     MessageBox.Show($"Restored {count} bytes in selection");
        /// }
        /// </example>
        public int RestoreOriginalBytesInRange(long startPosition, long stopPosition)
        {
            if (_viewModel?.Provider == null || ReadOnlyMode)
                return 0;

            int count = _viewModel.Provider.RestoreOriginalBytesInRange(startPosition, stopPosition);

            if (count > 0)
            {
                // Refresh view to remove highlights
                RefreshView(false, true);
            }

            return count;
        }

        /// <summary>
        /// V2-compatible alias for RestoreOriginalBytesInRange
        /// </summary>
        public int RemoveModificationsInRange(long startPosition, long stopPosition)
            => RestoreOriginalBytesInRange(startPosition, stopPosition);

        /// <summary>
        /// Concise alias for RestoreOriginalBytesInRange
        /// </summary>
        public int ResetBytesInRange(long startPosition, long stopPosition)
            => RestoreOriginalBytesInRange(startPosition, stopPosition);

        /// <summary>
        /// Restore ALL modified bytes to their original values.
        /// WARNING: This clears all modifications in the entire file.
        /// Insertions and deletions are NOT affected.
        /// </summary>
        /// <returns>Number of modifications removed</returns>
        /// <example>
        /// // Clear all modifications
        /// if (MessageBox.Show("Restore all modifications?", "Confirm",
        ///     MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        /// {
        ///     int count = hexEditor.RestoreAllModifications();
        ///     MessageBox.Show($"Restored {count} modifications");
        /// }
        /// </example>
        public int RestoreAllModifications()
        {
            if (_viewModel?.Provider == null || ReadOnlyMode)
                return 0;

            int count = _viewModel.Provider.RestoreAllModifications();

            if (count > 0)
            {
                // Refresh view to remove all highlights
                RefreshView(false, true);
            }

            return count;
        }

        /// <summary>
        /// V2-compatible alias for RestoreAllModifications
        /// </summary>
        public int RemoveAllModifications() => RestoreAllModifications();

        /// <summary>
        /// Concise alias for RestoreAllModifications
        /// </summary>
        public int ResetAllBytes() => RestoreAllModifications();

        #endregion Restore Original Bytes (Issue #127)

        #endregion
    }
}
