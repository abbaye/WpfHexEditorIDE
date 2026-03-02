//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service responsible for byte modification operations (modify, insert, delete)
    /// </summary>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var service = new ByteModificationService();
    ///
    /// // Check permissions
    /// if (service.CanModify(provider, readOnlyMode: false))
    /// {
    ///     // Modify a single byte
    ///     service.ModifyByte(provider, 0xFF, position: 0, undoLength: 1, readOnlyMode: false);
    /// }
    ///
    /// // Insert operations
    /// if (service.CanInsert(provider, canInsertAnywhere: true))
    /// {
    ///     // Insert single byte
    ///     service.InsertByte(provider, 0xAA, position: 10, canInsertAnywhere: true);
    ///
    ///     // Insert byte multiple times
    ///     service.InsertByte(provider, 0xBB, position: 20, length: 5, canInsertAnywhere: true);
    ///
    ///     // Insert byte array
    ///     byte[] data = new byte[] { 0x11, 0x22, 0x33 };
    ///     int count = service.InsertBytes(provider, data, position: 30, canInsertAnywhere: true);
    /// }
    ///
    /// // Delete operations
    /// if (service.CanDelete(provider, readOnlyMode: false, allowDelete: true))
    /// {
    ///     // Delete bytes at position
    ///     long lastPos = service.DeleteBytes(provider, position: 40, length: 5,
    ///                                         readOnlyMode: false, allowDelete: true);
    ///
    ///     // Delete range (auto-corrects if start > stop)
    ///     service.DeleteRange(provider, startPosition: 50, stopPosition: 60,
    ///                          readOnlyMode: false, allowDelete: true);
    /// }
    /// </code>
    /// </example>
    public class ByteModificationService
    {
        #region Modify Operations

        /// <summary>
        /// Modify byte at specified position
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="byte">New byte value (null to delete)</param>
        /// <param name="bytePositionInStream">Position to modify</param>
        /// <param name="undoLength">Length for undo operation (default 1)</param>
        /// <param name="readOnlyMode">If true, modification is not allowed</param>
        /// <returns>True if modification was successful</returns>
        public bool ModifyByte(ByteProvider provider, byte? @byte, long bytePositionInStream,
            long undoLength = 1, bool readOnlyMode = false)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            if (provider.IsReadOnly || readOnlyMode)
                return false;

            if (bytePositionInStream < 0 || bytePositionInStream >= provider.VirtualLength)
                return false;

            // V2: AddByteModified requires non-nullable byte
            if (@byte.HasValue)
                provider.AddByteModified(@byte.Value, bytePositionInStream, undoLength);
            return true;
        }

        #endregion

        #region Insert Operations

        /// <summary>
        /// Insert a single byte at specified position
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="byte">Byte value to insert</param>
        /// <param name="bytePositionInStream">Position to insert at</param>
        /// <param name="canInsertAnywhere">If false, insertion is not allowed</param>
        /// <returns>True if insertion was successful</returns>
        public bool InsertByte(ByteProvider provider, byte @byte, long bytePositionInStream,
            bool canInsertAnywhere = true)
        {
            return InsertByte(provider, @byte, bytePositionInStream, 1, canInsertAnywhere);
        }

        /// <summary>
        /// Insert a byte multiple times at specified position
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="byte">Byte value to insert</param>
        /// <param name="bytePositionInStream">Position to insert at</param>
        /// <param name="length">Number of times to insert the byte</param>
        /// <param name="canInsertAnywhere">If false, insertion is not allowed</param>
        /// <returns>True if insertion was successful</returns>
        public bool InsertByte(ByteProvider provider, byte @byte, long bytePositionInStream,
            long length, bool canInsertAnywhere = true)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            if (!canInsertAnywhere)
                return false;

            if (bytePositionInStream < 0)
                return false;

            if (length <= 0)
                return false;

            for (var i = 0; i < length; i++)
                provider.InsertByte(bytePositionInStream + i, @byte);

            return true;
        }

        /// <summary>
        /// Insert an array of bytes at specified position
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="bytes">Bytes to insert</param>
        /// <param name="bytePositionInStream">Position to insert at</param>
        /// <param name="canInsertAnywhere">If false, insertion is not allowed</param>
        /// <returns>Number of bytes inserted</returns>
        public int InsertBytes(ByteProvider provider, byte[] bytes, long bytePositionInStream,
            bool canInsertAnywhere = true)
        {
            if (provider == null || !provider.IsOpen)
                return 0;

            if (!canInsertAnywhere)
                return 0;

            if (bytes == null || bytes.Length == 0)
                return 0;

            if (bytePositionInStream < 0)
                return 0;

            var count = 0;
            var position = bytePositionInStream;

            foreach (var @byte in bytes)
            {
                provider.InsertByte(position++, @byte);
                count++;
            }

            return count;
        }

        #endregion

        #region Delete Operations

        /// <summary>
        /// Delete bytes at specified position
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="bytePositionInStream">Position to start deletion</param>
        /// <param name="length">Number of bytes to delete</param>
        /// <param name="readOnlyMode">If true, deletion is not allowed</param>
        /// <param name="allowDelete">If false, deletion is not allowed</param>
        /// <returns>Last position after deletion, or -1 if failed</returns>
        public long DeleteBytes(ByteProvider provider, long bytePositionInStream, long length = 1,
            bool readOnlyMode = false, bool allowDelete = true)
        {
            if (provider == null || !provider.IsOpen)
                return -1;

            if (provider.IsReadOnly || readOnlyMode || !allowDelete)
                return -1;

            if (bytePositionInStream < 0 || bytePositionInStream >= provider.VirtualLength)
                return -1;

            if (length <= 0)
                return -1;

            var lastPosition = provider.AddByteDeleted(bytePositionInStream, length);
            return lastPosition;
        }

        /// <summary>
        /// Delete a range of bytes
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="startPosition">Start position (inclusive)</param>
        /// <param name="stopPosition">Stop position (inclusive)</param>
        /// <param name="readOnlyMode">If true, deletion is not allowed</param>
        /// <param name="allowDelete">If false, deletion is not allowed</param>
        /// <returns>Last position after deletion, or -1 if failed</returns>
        public long DeleteRange(ByteProvider provider, long startPosition, long stopPosition,
            bool readOnlyMode = false, bool allowDelete = true)
        {
            if (provider == null || !provider.IsOpen)
                return -1;

            if (startPosition < 0 || stopPosition < 0)
                return -1;

            // Fix range if inverted
            var start = startPosition > stopPosition ? stopPosition : startPosition;
            var stop = startPosition > stopPosition ? startPosition : stopPosition;

            var length = stop - start + 1;

            return DeleteBytes(provider, start, length, readOnlyMode, allowDelete);
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Check if modification is allowed
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="readOnlyMode">External read-only mode flag</param>
        /// <returns>True if modification is allowed</returns>
        public bool CanModify(ByteProvider provider, bool readOnlyMode = false)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return !provider.IsReadOnly && !readOnlyMode;
        }

        /// <summary>
        /// Check if insertion is allowed
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="canInsertAnywhere">External insert permission flag</param>
        /// <returns>True if insertion is allowed</returns>
        public bool CanInsert(ByteProvider provider, bool canInsertAnywhere = true)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return canInsertAnywhere;
        }

        /// <summary>
        /// Check if deletion is allowed
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="readOnlyMode">External read-only mode flag</param>
        /// <param name="allowDelete">External delete permission flag</param>
        /// <returns>True if deletion is allowed</returns>
        public bool CanDelete(ByteProvider provider, bool readOnlyMode = false, bool allowDelete = true)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return !provider.IsReadOnly && !readOnlyMode && allowDelete;
        }

        #endregion

        #region Restore Operations (Issue #127)

        /// <summary>
        /// Restore a modified byte to its original value.
        /// Removes the modification from the dictionary and clears the highlight.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="bytePositionInStream">Position to restore</param>
        /// <returns>True if modification was removed, false if no modification existed</returns>
        /// <example>
        /// var modService = new ByteModificationService();
        /// if (modService.RestoreOriginalByte(provider, 0x100))
        ///     Console.WriteLine("Byte restored successfully");
        /// </example>
        public bool RestoreOriginalByte(ByteProvider provider, long bytePositionInStream)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return provider.RestoreOriginalByte(bytePositionInStream);
        }

        /// <summary>
        /// V2-compatible alias for RestoreOriginalByte
        /// </summary>
        public bool RemoveModification(ByteProvider provider, long position)
            => RestoreOriginalByte(provider, position);

        /// <summary>
        /// Concise alias for RestoreOriginalByte
        /// </summary>
        public bool ResetByte(ByteProvider provider, long position)
            => RestoreOriginalByte(provider, position);

        /// <summary>
        /// Restore multiple modified bytes (array overload).
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="positions">Array of positions to restore</param>
        /// <returns>Number of modifications successfully removed</returns>
        /// <example>
        /// var modService = new ByteModificationService();
        /// long[] positions = { 0x100, 0x200, 0x300 };
        /// int count = modService.RestoreOriginalBytes(provider, positions);
        /// Console.WriteLine($"Restored {count} bytes");
        /// </example>
        public int RestoreOriginalBytes(ByteProvider provider, long[] positions)
        {
            if (provider == null || !provider.IsOpen || positions == null)
                return 0;

            return provider.RestoreOriginalBytes(positions);
        }

        /// <summary>
        /// V2-compatible alias for RestoreOriginalBytes(long[])
        /// </summary>
        public int RemoveModifications(ByteProvider provider, long[] positions)
            => RestoreOriginalBytes(provider, positions);

        /// <summary>
        /// Concise alias for RestoreOriginalBytes(long[])
        /// </summary>
        public int ResetBytes(ByteProvider provider, long[] positions)
            => RestoreOriginalBytes(provider, positions);

        /// <summary>
        /// Restore multiple modified bytes (IEnumerable overload).
        /// Supports LINQ queries, List, HashSet, and other collections.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="positions">Enumerable collection of positions to restore</param>
        /// <returns>Number of modifications successfully removed</returns>
        /// <example>
        /// var modService = new ByteModificationService();
        ///
        /// // With LINQ
        /// var positions = provider.GetByteModifieds(ByteAction.Modified)
        ///     .Keys
        ///     .Where(p => p >= 0x1000 && p <= 0x2000);
        /// int count = modService.RestoreOriginalBytes(provider, positions);
        ///
        /// // With List
        /// List&lt;long&gt; posList = new List&lt;long&gt; { 10, 20, 30 };
        /// count = modService.RestoreOriginalBytes(provider, posList);
        /// </example>
        public int RestoreOriginalBytes(ByteProvider provider, IEnumerable<long> positions)
        {
            if (provider == null || !provider.IsOpen || positions == null)
                return 0;

            return provider.RestoreOriginalBytes(positions);
        }

        /// <summary>
        /// V2-compatible alias for RestoreOriginalBytes(IEnumerable)
        /// </summary>
        public int RemoveModifications(ByteProvider provider, IEnumerable<long> positions)
            => RestoreOriginalBytes(provider, positions);

        /// <summary>
        /// Concise alias for RestoreOriginalBytes(IEnumerable)
        /// </summary>
        public int ResetBytes(ByteProvider provider, IEnumerable<long> positions)
            => RestoreOriginalBytes(provider, positions);

        /// <summary>
        /// Restore all modifications in a continuous range.
        /// Automatically handles inverted ranges (start > stop).
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <param name="startPosition">Start position (inclusive)</param>
        /// <param name="stopPosition">Stop position (inclusive)</param>
        /// <returns>Number of modifications successfully removed</returns>
        /// <example>
        /// var modService = new ByteModificationService();
        /// int count = modService.RestoreOriginalBytesInRange(provider, 0x100, 0x200);
        /// Console.WriteLine($"Restored {count} bytes in range");
        /// </example>
        public int RestoreOriginalBytesInRange(ByteProvider provider, long startPosition, long stopPosition)
        {
            if (provider == null || !provider.IsOpen)
                return 0;

            return provider.RestoreOriginalBytesInRange(startPosition, stopPosition);
        }

        /// <summary>
        /// V2-compatible alias for RestoreOriginalBytesInRange
        /// </summary>
        public int RemoveModificationsInRange(ByteProvider provider, long start, long stop)
            => RestoreOriginalBytesInRange(provider, start, stop);

        /// <summary>
        /// Concise alias for RestoreOriginalBytesInRange
        /// </summary>
        public int ResetBytesInRange(ByteProvider provider, long start, long stop)
            => RestoreOriginalBytesInRange(provider, start, stop);

        /// <summary>
        /// Restore ALL modifications in the file.
        /// WARNING: This clears all byte modifications.
        /// Insertions and deletions are NOT affected.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <returns>Number of modifications removed</returns>
        /// <example>
        /// var modService = new ByteModificationService();
        /// int count = modService.RestoreAllModifications(provider);
        /// Console.WriteLine($"Cleared {count} modifications");
        /// </example>
        public int RestoreAllModifications(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return 0;

            return provider.RestoreAllModifications();
        }

        /// <summary>
        /// V2-compatible alias for RestoreAllModifications
        /// </summary>
        public int RemoveAllModifications(ByteProvider provider)
            => RestoreAllModifications(provider);

        /// <summary>
        /// Concise alias for RestoreAllModifications
        /// </summary>
        public int ResetAllBytes(ByteProvider provider)
            => RestoreAllModifications(provider);

        /// <summary>
        /// Check if a restore operation is allowed.
        /// Restore is allowed if the provider is open and not in read-only mode.
        /// </summary>
        /// <param name="provider">ByteProvider instance</param>
        /// <returns>True if restore is allowed</returns>
        public bool CanRestore(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return !provider.IsReadOnly;
        }

        #endregion
    }
}
