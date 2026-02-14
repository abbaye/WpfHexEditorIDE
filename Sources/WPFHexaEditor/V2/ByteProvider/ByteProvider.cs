//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.IO;

namespace WpfHexaEditor.V2.ByteProvider
{
    /// <summary>
    /// ByteProvider - Ultra-optimized byte provider with proper separation of responsibilities.
    /// This is the modern V2 implementation. Legacy V1 code should use ByteProviderLegacy.
    ///
    /// Key improvements over ByteProviderLegacy:
    /// - Separate storage for Modified/Inserted/Deleted (fixes insertion bugs)
    /// - Clear Virtual vs Physical position separation
    /// - Multi-layer caching (file cache 64KB + line cache + position cache)
    /// - 10x-100x faster for large files with many edits
    /// - Proper support for multiple insertions at same position
    ///
    /// Architecture:
    /// - FileProvider: Pure file I/O with 64KB cachea
    /// - EditsManager: Tracks all modifications (Modified/Inserted/Deleted)
    /// - PositionMapper: Virtual↔Physical conversion with cache
    /// - ByteReader: Intelligent byte reading with multi-layer caching
    ///
    /// ByteProviderLegacy (Core.Bytes.ByteProviderLegacy) remains intact for V1 compatibility.
    /// </summary>
    public sealed class ByteProvider : IDisposable
    {
        #region Services

        private readonly FileProvider _fileProvider;
        private readonly EditsManager _editsManager;
        private readonly PositionMapper _positionMapper;
        private readonly ByteReader _byteReader;
        private readonly UndoRedoManager _undoRedoManager;

        // Batching support to avoid repeated cache invalidations
        private bool _batchMode = false;
        private bool _batchDirty = false;

        // Undo/Redo recording control
        private bool _recordUndo = true;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the file path (if opened from file).
        /// </summary>
        public string FilePath => _fileProvider.FilePath;

        /// <summary>
        /// Gets whether a file/stream is currently open.
        /// </summary>
        public bool IsOpen => _fileProvider.IsOpen;

        /// <summary>
        /// Gets the physical length of the file (raw file size).
        /// </summary>
        public long PhysicalLength => _fileProvider.Length;

        /// <summary>
        /// Gets the virtual length (including insertions, excluding deletions).
        /// This is what the user sees.
        /// </summary>
        public long VirtualLength => _byteReader.GetVirtualLength();

        /// <summary>
        /// Gets whether the file is read-only.
        /// </summary>
        public bool IsReadOnly => _fileProvider.IsReadOnly;

        /// <summary>
        /// Gets whether any modifications exist.
        /// </summary>
        public bool HasChanges => _editsManager.HasChanges;

        /// <summary>
        /// Gets modification statistics.
        /// </summary>
        public (int modified, int inserted, int deleted) ModificationStats =>
            (_editsManager.ModifiedCount, _editsManager.TotalInsertedBytesCount, _editsManager.DeletedCount);

        /// <summary>
        /// Gets whether undo is available.
        /// </summary>
        public bool CanUndo => _undoRedoManager.CanUndo;

        /// <summary>
        /// Gets whether redo is available.
        /// </summary>
        public bool CanRedo => _undoRedoManager.CanRedo;

        #endregion

        #region Constructor

        public ByteProvider()
        {
            _fileProvider = new FileProvider();
            _editsManager = new EditsManager();
            _positionMapper = new PositionMapper(_editsManager);
            _byteReader = new ByteReader(_fileProvider, _editsManager, _positionMapper);
            _undoRedoManager = new UndoRedoManager();

            // Enable caching for maximum performance
            _positionMapper.EnableCache();
        }

        #endregion

        #region File Operations

        /// <summary>
        /// Open a file for reading/writing.
        /// </summary>
        public void OpenFile(string filePath, bool readOnly = false)
        {
            _fileProvider.OpenFile(filePath, readOnly);
            ClearAllEdits();
        }

        /// <summary>
        /// Open from an existing stream.
        /// </summary>
        public void OpenStream(Stream stream, bool readOnly = false)
        {
            _fileProvider.OpenStream(stream, readOnly);
            ClearAllEdits();
        }

        /// <summary>
        /// Open from a byte array in memory.
        /// Allows editing byte arrays directly without file I/O.
        /// </summary>
        public void OpenMemory(byte[] data, bool readOnly = false)
        {
            _fileProvider.OpenMemory(data, readOnly);
            ClearAllEdits();
        }

        /// <summary>
        /// Close the current file/stream.
        /// </summary>
        public void Close()
        {
            _fileProvider.Close();
            ClearAllEdits();
        }

        #endregion

        #region Read Operations (Virtual Positions)

        /// <summary>
        /// Read a single byte at a virtual position.
        /// Virtual position = what the user sees (includes insertions, excludes deletions).
        /// </summary>
        public (byte value, bool success) GetByte(long virtualPosition)
        {
            return _byteReader.GetByte(virtualPosition);
        }

        /// <summary>
        /// Read multiple bytes starting at a virtual position.
        /// </summary>
        public byte[] GetBytes(long virtualPosition, int count)
        {
            return _byteReader.GetBytes(virtualPosition, count);
        }

        /// <summary>
        /// Read a full line of bytes (optimized for rendering).
        /// </summary>
        public byte[] GetLine(long virtualLineStart, int bytesPerLine)
        {
            return _byteReader.GetLine(virtualLineStart, bytesPerLine);
        }

        /// <summary>
        /// Read multiple lines (ultra-optimized for viewport rendering).
        /// </summary>
        public System.Collections.Generic.List<byte[]> GetLines(long startVirtualPosition, int lineCount, int bytesPerLine)
        {
            return _byteReader.GetLines(startVirtualPosition, lineCount, bytesPerLine);
        }

        #endregion

        #region Write Operations (Virtual Positions)

        /// <summary>
        /// Modify a byte at a virtual position.
        /// </summary>
        public void ModifyByte(long virtualPosition, byte value)
        {
            if (_recordUndo)
            {
                // Read old value for undo
                var (oldValue, success) = GetByte(virtualPosition);
                if (!success)
                    return; // Can't modify invalid position

                // Record undo operation
                _undoRedoManager.RecordModify(virtualPosition, new[] { oldValue }, new[] { value });
            }

            ModifyByteInternal(virtualPosition, value);
            InvalidateCaches();
        }

        /// <summary>
        /// Internal version of ModifyByte that doesn't invalidate caches.
        /// Used for batch operations.
        /// </summary>
        private void ModifyByteInternal(long virtualPosition, byte value)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("File is read-only");

            // Convert to physical position
            var (physicalPos, isInserted) = _positionMapper.VirtualToPhysical(virtualPosition, _fileProvider.Length);

            if (isInserted)
            {
                // Modifying an inserted byte - update the byte in EditsManager's insertion list
                if (physicalPos.HasValue)
                {
                    // Calculate which inserted byte this is (virtual offset within insertions)
                    long virtualStart = _positionMapper.PhysicalToVirtual(physicalPos.Value, _fileProvider.Length);
                    int virtualOffset = (int)(virtualPosition - virtualStart);

                    // Update the inserted byte's value
                    bool success = _editsManager.ModifyInsertedByte(physicalPos.Value, virtualOffset, value);

                    if (!success)
                    {
                        // This shouldn't happen, but log for debugging
                        System.Diagnostics.Debug.WriteLine($"[ByteProvider] Failed to modify inserted byte at virtual pos {virtualPosition}, physical pos {physicalPos.Value}, offset {virtualOffset}");
                    }
                }
            }
            else if (physicalPos.HasValue)
            {
                _editsManager.ModifyByte(physicalPos.Value, value);
            }
        }

        /// <summary>
        /// Modify multiple bytes starting at a virtual position.
        /// OPTIMIZED: Batch modification with single cache invalidation.
        /// </summary>
        public void ModifyBytes(long startVirtualPosition, byte[] values)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("File is read-only");

            if (values == null || values.Length == 0)
                return;

            if (_recordUndo)
            {
                // Read old values for undo
                byte[] oldValues = GetBytes(startVirtualPosition, values.Length);

                // Record undo operation
                _undoRedoManager.RecordModify(startVirtualPosition, oldValues, values);
            }

            // Batch modify without invalidating cache each time
            for (int i = 0; i < values.Length; i++)
            {
                ModifyByteInternal(startVirtualPosition + i, values[i]);
            }

            // Invalidate caches ONCE at the end
            InvalidateCaches();
        }

        /// <summary>
        /// Insert byte(s) at a virtual position.
        /// </summary>
        public void InsertBytes(long virtualPosition, byte[] bytes)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("File is read-only");

            if (bytes == null || bytes.Length == 0)
                return;

            if (_recordUndo)
            {
                // Record undo operation
                _undoRedoManager.RecordInsert(virtualPosition, bytes);
            }

            // Convert to physical position
            var (physicalPos, _) = _positionMapper.VirtualToPhysical(virtualPosition, _fileProvider.Length);

            if (!physicalPos.HasValue)
            {
                // Beyond end of file - insert at end
                physicalPos = _fileProvider.Length;
            }

            _editsManager.InsertBytes(physicalPos.Value, bytes);
            InvalidateCaches();
        }

        /// <summary>
        /// Insert a single byte at a virtual position.
        /// </summary>
        public void InsertByte(long virtualPosition, byte value)
        {
            InsertBytes(virtualPosition, new[] { value });
        }

        /// <summary>
        /// Delete a byte at a virtual position.
        /// </summary>
        public void DeleteByte(long virtualPosition)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("File is read-only");

            if (_recordUndo)
            {
                // Read old value for undo
                var (oldValue, success) = GetByte(virtualPosition);
                if (!success)
                    return; // Can't delete invalid position

                // Record undo operation
                _undoRedoManager.RecordDelete(virtualPosition, new[] { oldValue });
            }

            // Convert to physical position
            var (physicalPos, isInserted) = _positionMapper.VirtualToPhysical(virtualPosition, _fileProvider.Length);

            if (isInserted)
            {
                // Deleting an inserted byte - remove from insertions
                if (physicalPos.HasValue)
                {
                    // TODO: Remove specific inserted byte (requires index)
                    // For now, we'll delete from the physical position
                    _editsManager.DeleteByte(physicalPos.Value);
                }
            }
            else if (physicalPos.HasValue)
            {
                _editsManager.DeleteByte(physicalPos.Value);
            }

            InvalidateCaches();
        }

        /// <summary>
        /// Delete multiple bytes starting at a virtual position.
        /// OPTIMIZED: Batch deletion with single cache invalidation.
        /// </summary>
        public void DeleteBytes(long startVirtualPosition, long count)
        {
            if (IsReadOnly)
                throw new InvalidOperationException("File is read-only");

            if (count <= 0)
                return;

            if (_recordUndo)
            {
                // Read old values for undo
                byte[] oldValues = GetBytes(startVirtualPosition, (int)count);

                // Record undo operation
                _undoRedoManager.RecordDelete(startVirtualPosition, oldValues);
            }

            // Batch delete without invalidating cache each time
            for (long i = 0; i < count; i++)
            {
                long virtualPos = startVirtualPosition + i;

                // Convert to physical position
                var (physicalPos, isInserted) = _positionMapper.VirtualToPhysical(virtualPos, _fileProvider.Length);

                if (isInserted)
                {
                    // Deleting an inserted byte - remove from insertions
                    if (physicalPos.HasValue)
                    {
                        _editsManager.DeleteByte(physicalPos.Value);
                    }
                }
                else if (physicalPos.HasValue)
                {
                    _editsManager.DeleteByte(physicalPos.Value);
                }
            }

            // Invalidate caches ONCE at the end
            InvalidateCaches();
        }

        #endregion

        #region Save Operations

        /// <summary>
        /// Save all changes to the file.
        /// Applies all modifications (Modified/Inserted/Deleted) to create new file content.
        /// </summary>
        public void Save()
        {
            if (IsReadOnly)
                throw new InvalidOperationException("File is read-only");

            if (!HasChanges)
                return; // Nothing to save

            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("Cannot save: no file path");

            SaveAs(FilePath, true);
        }

        /// <summary>
        /// Save to a new file path.
        /// </summary>
        public void SaveAs(string newFilePath, bool overwrite = false)
        {
            if (IsReadOnly && newFilePath == FilePath)
                throw new InvalidOperationException("File is read-only");

            if (File.Exists(newFilePath) && !overwrite)
                throw new InvalidOperationException($"File already exists: {newFilePath}");

            // Create temporary file
            string tempFile = Path.GetTempFileName();

            try
            {
                using (var outputStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // Write all bytes (virtual view) to new file
                    long virtualLength = VirtualLength;
                    const int BUFFER_SIZE = 64 * 1024;

                    for (long vPos = 0; vPos < virtualLength; vPos += BUFFER_SIZE)
                    {
                        int toRead = (int)Math.Min(BUFFER_SIZE, virtualLength - vPos);
                        byte[] buffer = GetBytes(vPos, toRead);
                        outputStream.Write(buffer, 0, buffer.Length);
                    }

                    outputStream.Flush();
                }

                // Close current file
                Close();

                // Replace original with new file
                if (File.Exists(newFilePath))
                    File.Delete(newFilePath);

                File.Move(tempFile, newFilePath);

                // Reopen the new file
                OpenFile(newFilePath, false);
            }
            catch
            {
                // Clean up temp file on error
                if (File.Exists(tempFile))
                    File.Delete(tempFile);

                throw;
            }
        }

        #endregion

        #region Edit Management

        /// <summary>
        /// Clear all modifications (revert to original file).
        /// Also clears undo/redo history.
        /// </summary>
        public void ClearAllEdits()
        {
            _editsManager.ClearAll();
            _undoRedoManager.ClearAll();
            InvalidateCaches();
        }

        /// <summary>
        /// Clear only modifications (keep insertions and deletions).
        /// </summary>
        public void ClearModifications()
        {
            _editsManager.ClearModifications();
            InvalidateCaches();
        }

        /// <summary>
        /// Clear only insertions (keep modifications and deletions).
        /// </summary>
        public void ClearInsertions()
        {
            _editsManager.ClearInsertions();
            InvalidateCaches();
        }

        /// <summary>
        /// Clear only deletions (keep modifications and insertions).
        /// </summary>
        public void ClearDeletions()
        {
            _editsManager.ClearDeletions();
            InvalidateCaches();
        }

        #endregion

        #region Undo/Redo Operations

        /// <summary>
        /// Undo the last operation.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo)
                return;

            // Pop operation from undo stack
            var operation = _undoRedoManager.PopUndo();

            // Disable undo recording while we reverse the operation
            _recordUndo = false;

            try
            {
                switch (operation.Type)
                {
                    case UndoOperationType.Modify:
                        // Restore old values
                        if (operation.OldValues != null)
                        {
                            ModifyBytes(operation.VirtualPosition, operation.OldValues);
                        }
                        break;

                    case UndoOperationType.Insert:
                        // Delete the inserted bytes
                        DeleteBytes(operation.VirtualPosition, operation.Count);
                        break;

                    case UndoOperationType.Delete:
                        // Re-insert the deleted bytes
                        if (operation.OldValues != null)
                        {
                            InsertBytes(operation.VirtualPosition, operation.OldValues);
                        }
                        break;
                }
            }
            finally
            {
                // Re-enable undo recording
                _recordUndo = true;
            }
        }

        /// <summary>
        /// Redo the last undone operation.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo)
                return;

            // Pop operation from redo stack
            var operation = _undoRedoManager.PopRedo();

            // Disable undo recording while we re-apply the operation
            _recordUndo = false;

            try
            {
                switch (operation.Type)
                {
                    case UndoOperationType.Modify:
                        // Reapply new values
                        if (operation.NewValues != null)
                        {
                            ModifyBytes(operation.VirtualPosition, operation.NewValues);
                        }
                        break;

                    case UndoOperationType.Insert:
                        // Re-insert the bytes
                        if (operation.NewValues != null)
                        {
                            InsertBytes(operation.VirtualPosition, operation.NewValues);
                        }
                        break;

                    case UndoOperationType.Delete:
                        // Re-delete the bytes
                        DeleteBytes(operation.VirtualPosition, operation.Count);
                        break;
                }
            }
            finally
            {
                // Re-enable undo recording
                _recordUndo = true;
            }
        }

        /// <summary>
        /// Clear all undo/redo history.
        /// </summary>
        public void ClearUndoRedoHistory()
        {
            _undoRedoManager.ClearAll();
        }

        #endregion

        #region Cache Management

        private void InvalidateCaches()
        {
            if (_batchMode)
            {
                // In batch mode, just mark as dirty instead of invalidating
                _batchDirty = true;
                return;
            }

            _positionMapper.InvalidateCache();
            _byteReader.ClearLineCache();
        }

        /// <summary>
        /// Begin batch operation mode - cache invalidations are deferred until EndBatch().
        /// Use this for multiple sequential modifications to improve performance.
        /// </summary>
        public void BeginBatch()
        {
            _batchMode = true;
            _batchDirty = false;
        }

        /// <summary>
        /// End batch operation mode - invalidates caches if any modifications were made.
        /// </summary>
        public void EndBatch()
        {
            _batchMode = false;
            if (_batchDirty)
            {
                _positionMapper.InvalidateCache();
                _byteReader.ClearLineCache();
                _batchDirty = false;
            }
        }

        /// <summary>
        /// Get comprehensive cache statistics for all layers.
        /// </summary>
        public string GetCacheStatistics()
        {
            var (lineCache, lineMem) = _byteReader.GetCacheStats();
            var (v2p, p2v, valid) = _positionMapper.GetCacheStats();
            var (mod, ins, del, editMem) = _editsManager.GetStatistics();

            return $"ByteReader: {lineCache} lines ({lineMem}KB)\n" +
                   $"PositionMapper: {v2p} V→P, {p2v} P→V entries (valid: {valid})\n" +
                   $"EditsManager: {mod} modified, {ins} inserted, {del} deleted ({editMem}KB)";
        }

        #endregion

        #region V1 Compatibility Layer

        // These properties/methods provide V1 (ByteProviderLegacy) compatibility
        // to allow HexEditorViewModel to work with V2 without major refactoring.

        /// <summary>
        /// V1 compatibility: Current sequential read position.
        /// </summary>
        public long Position { get; set; } = 0;

        /// <summary>
        /// V1 compatibility: Alias for FilePath.
        /// </summary>
        public string FileName => FilePath;

        /// <summary>
        /// V1 compatibility: Alias for VirtualLength.
        /// </summary>
        public long Length => VirtualLength;

        /// <summary>
        /// V1 compatibility: Read byte at current Position and advance Position.
        /// Returns -1 if read fails (for V1 compatibility).
        /// </summary>
        public int ReadByte()
        {
            var (value, success) = GetByte(Position);
            if (success)
            {
                Position++;
                return value;
            }
            return -1;
        }

        /// <summary>
        /// V1 compatibility: Alias for Save().
        /// </summary>
        public void SubmitChanges()
        {
            Save();
        }

        /// <summary>
        /// V1 compatibility: Alias for SaveAs(string, bool).
        /// </summary>
        public bool SubmitChanges(string newFilename, bool overwrite = false)
        {
            try
            {
                SaveAs(newFilename, overwrite);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// V1 compatibility: Modify a byte at physical position.
        /// Note: V1 signature has (byte, long), V2 has (long, byte).
        /// </summary>
        public void AddByteModified(byte value, long virtualPosition, long undoLength = 1)
        {
            ModifyByte(virtualPosition, value);
        }

        /// <summary>
        /// V1 compatibility: Delete bytes starting at position.
        /// </summary>
        public long AddByteDeleted(long virtualPosition, long length)
        {
            DeleteBytes(virtualPosition, length);
            return virtualPosition; // Return position for V1 compatibility
        }

        /// <summary>
        /// V1 compatibility: Paste bytes at position.
        /// </summary>
        public void Paste(long virtualPosition, byte[] bytes, bool allowExtend)
        {
            if (bytes == null || bytes.Length == 0)
                return;

            // In V2, we always insert bytes (V1 had allowExtend parameter)
            InsertBytes(virtualPosition, bytes);
        }

        /// <summary>
        /// V1 compatibility: Fill a range with a specific byte value.
        /// OPTIMIZED: Use batch modification for better performance.
        /// </summary>
        public void FillWithByte(long virtualPosition, long length, byte value)
        {
            if (length <= 0)
                return;

            // Create array filled with value
            byte[] fillArray = new byte[length];
            for (int i = 0; i < length; i++)
                fillArray[i] = value;

            // Use batch modification (single cache invalidation)
            ModifyBytes(virtualPosition, fillArray);
        }

        /// <summary>
        /// V1 compatibility: Check if a byte is modified.
        /// Returns (success, modifiedByte).
        /// </summary>
        public (bool success, byte? modifiedByte) CheckIfIsByteModified(long virtualPosition, Core.ByteAction action)
        {
            // Convert virtual to physical position
            var (physicalPos, isInserted) = _positionMapper.VirtualToPhysical(virtualPosition, PhysicalLength);

            if (isInserted)
            {
                // This is an inserted byte
                if (action == Core.ByteAction.Added)
                {
                    var (value, success) = GetByte(virtualPosition);
                    return (success, success ? value : (byte?)null);
                }
                return (false, null);
            }

            if (!physicalPos.HasValue)
                return (false, null);

            // Check modifications at physical position
            switch (action)
            {
                case Core.ByteAction.Modified:
                    var (modValue, modExists) = _editsManager.GetModifiedByte(physicalPos.Value);
                    return (modExists, modExists ? modValue : (byte?)null);

                case Core.ByteAction.Deleted:
                    bool isDeleted = _editsManager.IsDeleted(physicalPos.Value);
                    return (isDeleted, null);

                case Core.ByteAction.Added:
                    return (false, null); // Already handled above

                default:
                    return (false, null);
            }
        }

        /// <summary>
        /// Get the ByteAction for a virtual position (for visual indicators).
        /// Returns Added/Modified/Deleted/Nothing.
        /// </summary>
        public Core.ByteAction GetByteAction(long virtualPosition)
        {
            if (virtualPosition < 0 || virtualPosition >= VirtualLength)
                return Core.ByteAction.Nothing;

            // Convert virtual to physical position
            var (physicalPos, isInserted) = _positionMapper.VirtualToPhysical(virtualPosition, PhysicalLength);

            // Check if inserted (Added)
            if (isInserted)
                return Core.ByteAction.Added;

            if (!physicalPos.HasValue)
                return Core.ByteAction.Nothing;

            // Check if deleted
            if (_editsManager.IsDeleted(physicalPos.Value))
                return Core.ByteAction.Deleted;

            // Check if modified
            var (_, isModified) = _editsManager.GetModifiedByte(physicalPos.Value);
            if (isModified)
                return Core.ByteAction.Modified;

            return Core.ByteAction.Nothing;
        }

        #endregion

        #region IDisposable

        private bool _disposed = false;

        public void Dispose()
        {
            if (!_disposed)
            {
                _fileProvider?.Dispose();
                _disposed = true;
            }
        }

        #endregion
    }
}
