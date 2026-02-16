//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Services
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
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="byte">New byte value (null to delete)</param>
        /// <param name="bytePositionInStream">Position to modify</param>
        /// <param name="undoLength">Length for undo operation (default 1)</param>
        /// <param name="readOnlyMode">If true, modification is not allowed</param>
        /// <returns>True if modification was successful</returns>
        public bool ModifyByte(ByteProviderLegacy provider, byte? @byte, long bytePositionInStream,
            long undoLength = 1, bool readOnlyMode = false)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            if (provider.ReadOnlyMode || readOnlyMode)
                return false;

            if (bytePositionInStream < 0 || bytePositionInStream >= provider.Length)
                return false;

            provider.AddByteModified(@byte, bytePositionInStream, undoLength);
            return true;
        }

        #endregion

        #region Insert Operations

        /// <summary>
        /// Insert a single byte at specified position
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="byte">Byte value to insert</param>
        /// <param name="bytePositionInStream">Position to insert at</param>
        /// <param name="canInsertAnywhere">If false, insertion is not allowed</param>
        /// <returns>True if insertion was successful</returns>
        public bool InsertByte(ByteProviderLegacy provider, byte @byte, long bytePositionInStream,
            bool canInsertAnywhere = true)
        {
            return InsertByte(provider, @byte, bytePositionInStream, 1, canInsertAnywhere);
        }

        /// <summary>
        /// Insert a byte multiple times at specified position
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="byte">Byte value to insert</param>
        /// <param name="bytePositionInStream">Position to insert at</param>
        /// <param name="length">Number of times to insert the byte</param>
        /// <param name="canInsertAnywhere">If false, insertion is not allowed</param>
        /// <returns>True if insertion was successful</returns>
        public bool InsertByte(ByteProviderLegacy provider, byte @byte, long bytePositionInStream,
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
                provider.AddByteAdded(@byte, bytePositionInStream + i);

            return true;
        }

        /// <summary>
        /// Insert an array of bytes at specified position
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="bytes">Bytes to insert</param>
        /// <param name="bytePositionInStream">Position to insert at</param>
        /// <param name="canInsertAnywhere">If false, insertion is not allowed</param>
        /// <returns>Number of bytes inserted</returns>
        public int InsertBytes(ByteProviderLegacy provider, byte[] bytes, long bytePositionInStream,
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
                provider.AddByteAdded(@byte, position++);
                count++;
            }

            return count;
        }

        #endregion

        #region Delete Operations

        /// <summary>
        /// Delete bytes at specified position
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="bytePositionInStream">Position to start deletion</param>
        /// <param name="length">Number of bytes to delete</param>
        /// <param name="readOnlyMode">If true, deletion is not allowed</param>
        /// <param name="allowDelete">If false, deletion is not allowed</param>
        /// <returns>Last position after deletion, or -1 if failed</returns>
        public long DeleteBytes(ByteProviderLegacy provider, long bytePositionInStream, long length = 1,
            bool readOnlyMode = false, bool allowDelete = true)
        {
            if (provider == null || !provider.IsOpen)
                return -1;

            if (provider.ReadOnlyMode || readOnlyMode || !allowDelete)
                return -1;

            if (bytePositionInStream < 0 || bytePositionInStream >= provider.Length)
                return -1;

            if (length <= 0)
                return -1;

            var lastPosition = provider.AddByteDeleted(bytePositionInStream, length);
            return lastPosition;
        }

        /// <summary>
        /// Delete a range of bytes
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="startPosition">Start position (inclusive)</param>
        /// <param name="stopPosition">Stop position (inclusive)</param>
        /// <param name="readOnlyMode">If true, deletion is not allowed</param>
        /// <param name="allowDelete">If false, deletion is not allowed</param>
        /// <returns>Last position after deletion, or -1 if failed</returns>
        public long DeleteRange(ByteProviderLegacy provider, long startPosition, long stopPosition,
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
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="readOnlyMode">External read-only mode flag</param>
        /// <returns>True if modification is allowed</returns>
        public bool CanModify(ByteProviderLegacy provider, bool readOnlyMode = false)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return !provider.ReadOnlyMode && !readOnlyMode;
        }

        /// <summary>
        /// Check if insertion is allowed
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="canInsertAnywhere">External insert permission flag</param>
        /// <returns>True if insertion is allowed</returns>
        public bool CanInsert(ByteProviderLegacy provider, bool canInsertAnywhere = true)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return canInsertAnywhere;
        }

        /// <summary>
        /// Check if deletion is allowed
        /// </summary>
        /// <param name="provider">ByteProviderLegacy instance</param>
        /// <param name="readOnlyMode">External read-only mode flag</param>
        /// <param name="allowDelete">External delete permission flag</param>
        /// <returns>True if deletion is allowed</returns>
        public bool CanDelete(ByteProviderLegacy provider, bool readOnlyMode = false, bool allowDelete = true)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return !provider.ReadOnlyMode && !readOnlyMode && allowDelete;
        }

        #endregion
    }
}
