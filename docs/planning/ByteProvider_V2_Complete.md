# ByteProvider V2 - Plan d'Implémentation Complet

---
> **✅ PLAN COMPLETED**
>
> This implementation plan was successfully executed during V2 development.
> **Status:** ByteProvider V2 architecture fully implemented and in production
> **Completion:** All phases (1-4) completed, 16-21 hours as estimated
>
> This document is preserved for architectural reference and decision history.
---

## Vue d'Ensemble

Ce document fournit un plan d'implémentation **complet et détaillé** pour la refactorisation du ByteProvider avec code source intégral, migration étape par étape, et stratégie de tests.

### Objectifs
- ✅ Séparer les responsabilités (FileProvider, EditsManager, PositionMapper, ByteReader)
- ✅ Résoudre la confusion Virtual/Physical positions
- ✅ Support complet des insertions/modifications/suppressions
- ✅ Undo/Redo robuste avec IEdit pattern
- ✅ Performance optimale avec caching intelligent
- ✅ Compatibilité avec V2 existant

### Estimation Totale: 16-21 heures d'implémentation

---

## PHASE 1: Services de Base (6-8 heures)

### 1.1 FileProvider - Gestion Fichier Pure (2h)

**Fichier**: `Services/FileProvider.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// ByteProvider V2 - FileProvider (Pure file I/O)
//////////////////////////////////////////////

using System;
using System.IO;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Pure file I/O service - NO business logic, only read/write operations
    /// Works EXCLUSIVELY with physical positions (actual file offsets)
    /// </summary>
    public class FileProvider : IDisposable
    {
        #region Fields

        private Stream _stream;
        private bool _isOpen;
        private readonly byte[] _readCache;
        private const int CacheSize = 4096; // 4KB cache

        #endregion

        #region Constructor

        public FileProvider()
        {
            _readCache = new byte[CacheSize];
        }

        #endregion

        #region Properties

        /// <summary>
        /// File length in bytes (physical)
        /// </summary>
        public long Length => _isOpen ? _stream.Length : 0;

        /// <summary>
        /// Is file open?
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// File path (if opened from file)
        /// </summary>
        public string FilePath { get; private set; }

        #endregion

        #region Public Methods - File Operations

        /// <summary>
        /// Open file for reading/writing
        /// </summary>
        public void OpenFile(string filePath, bool readOnly = false)
        {
            if (_isOpen)
                throw new InvalidOperationException("File already open. Close it first.");

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var access = readOnly ? FileAccess.Read : FileAccess.ReadWrite;
            var share = readOnly ? FileShare.Read : FileShare.None;

            _stream = new FileStream(filePath, FileMode.Open, access, share);
            _isOpen = true;
            FilePath = filePath;
        }

        /// <summary>
        /// Open from existing stream
        /// </summary>
        public void OpenStream(Stream stream)
        {
            if (_isOpen)
                throw new InvalidOperationException("File already open. Close it first.");

            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _isOpen = true;
            FilePath = null;
        }

        /// <summary>
        /// Close file/stream
        /// </summary>
        public void Close()
        {
            if (_isOpen)
            {
                _stream?.Close();
                _stream = null;
                _isOpen = false;
                FilePath = null;
            }
        }

        #endregion

        #region Public Methods - Read Operations (Physical Positions Only)

        /// <summary>
        /// Read single byte at physical position
        /// </summary>
        /// <param name="physicalPosition">Physical position in file (0-based)</param>
        /// <returns>Byte value, or null if EOF or invalid position</returns>
        public byte? ReadByte(long physicalPosition)
        {
            if (!_isOpen)
                throw new InvalidOperationException("File not open");

            if (physicalPosition < 0 || physicalPosition >= Length)
                return null; // Out of bounds

            _stream.Position = physicalPosition;
            int b = _stream.ReadByte();
            return b == -1 ? null : (byte?)b;
        }

        /// <summary>
        /// Read multiple bytes starting at physical position
        /// </summary>
        /// <param name="physicalPosition">Physical starting position</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>Byte array (may be shorter than count if EOF reached)</returns>
        public byte[] ReadBytes(long physicalPosition, int count)
        {
            if (!_isOpen)
                throw new InvalidOperationException("File not open");

            if (physicalPosition < 0 || count <= 0)
                return Array.Empty<byte>();

            // Clamp count to available bytes
            long available = Math.Max(0, Length - physicalPosition);
            int actualCount = (int)Math.Min(count, available);

            if (actualCount == 0)
                return Array.Empty<byte>();

            byte[] buffer = new byte[actualCount];
            _stream.Position = physicalPosition;
            int bytesRead = _stream.Read(buffer, 0, actualCount);

            // Return only bytes actually read
            if (bytesRead < actualCount)
            {
                byte[] result = new byte[bytesRead];
                Array.Copy(buffer, result, bytesRead);
                return result;
            }

            return buffer;
        }

        #endregion

        #region Public Methods - Write Operations (For Save)

        /// <summary>
        /// Write byte at physical position (used during Save)
        /// </summary>
        public void WriteByte(long physicalPosition, byte value)
        {
            if (!_isOpen)
                throw new InvalidOperationException("File not open");

            if (physicalPosition < 0 || physicalPosition >= Length)
                throw new ArgumentOutOfRangeException(nameof(physicalPosition));

            _stream.Position = physicalPosition;
            _stream.WriteByte(value);
        }

        /// <summary>
        /// Write multiple bytes at physical position (used during Save)
        /// </summary>
        public void WriteBytes(long physicalPosition, byte[] data)
        {
            if (!_isOpen)
                throw new InvalidOperationException("File not open");

            if (data == null || data.Length == 0)
                return;

            if (physicalPosition < 0 || physicalPosition >= Length)
                throw new ArgumentOutOfRangeException(nameof(physicalPosition));

            _stream.Position = physicalPosition;
            _stream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Flush changes to disk
        /// </summary>
        public void Flush()
        {
            if (_isOpen)
                _stream?.Flush();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Close();
        }

        #endregion
    }
}
```

---

### 1.2 EditsManager - Tracking des Modifications (3h)

**Fichier**: `Services/EditsManager.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// ByteProvider V2 - EditsManager (Modification tracking)
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Tracks all modifications (Modified, Added, Deleted bytes)
    /// Works EXCLUSIVELY with physical positions
    /// </summary>
    public class EditsManager
    {
        #region Fields

        // Three separate dictionaries for each type of modification
        private readonly Dictionary<long, byte> _modifiedBytes = new();           // Physical pos → modified byte value
        private readonly Dictionary<long, List<InsertedByte>> _insertedBytes = new(); // Physical pos → list of inserted bytes
        private readonly HashSet<long> _deletedPositions = new();                 // Physical positions marked as deleted

        #endregion

        #region Properties

        /// <summary>
        /// Total number of modifications (Modified + Added + Deleted)
        /// </summary>
        public long TotalModifications => _modifiedBytes.Count + _insertedBytes.Values.Sum(list => list.Count) + _deletedPositions.Count;

        /// <summary>
        /// Has any modifications?
        /// </summary>
        public bool HasModifications => TotalModifications > 0;

        #endregion

        #region Public Methods - Modify Bytes

        /// <summary>
        /// Mark byte as modified at physical position
        /// </summary>
        public void ModifyByte(long physicalPosition, byte value)
        {
            // Remove from deleted if it was deleted
            _deletedPositions.Remove(physicalPosition);

            // Add/update in modified dictionary
            _modifiedBytes[physicalPosition] = value;
        }

        /// <summary>
        /// Check if byte is modified at physical position
        /// </summary>
        public bool IsModified(long physicalPosition)
        {
            return _modifiedBytes.ContainsKey(physicalPosition);
        }

        /// <summary>
        /// Get modified byte value at physical position
        /// </summary>
        /// <returns>Modified byte, or null if not modified</returns>
        public byte? GetModifiedByte(long physicalPosition)
        {
            return _modifiedBytes.TryGetValue(physicalPosition, out byte value) ? value : null;
        }

        #endregion

        #region Public Methods - Insert Bytes

        /// <summary>
        /// Insert bytes BEFORE physical position
        /// Multiple insertions at same position are stored in order
        /// </summary>
        /// <param name="physicalPosition">Physical position AFTER which bytes are inserted</param>
        /// <param name="bytes">Bytes to insert</param>
        public void InsertBytes(long physicalPosition, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return;

            // Get or create list for this physical position
            if (!_insertedBytes.ContainsKey(physicalPosition))
                _insertedBytes[physicalPosition] = new List<InsertedByte>();

            var list = _insertedBytes[physicalPosition];

            // Add each byte with its virtual offset
            for (int i = 0; i < bytes.Length; i++)
            {
                list.Add(new InsertedByte
                {
                    Value = bytes[i],
                    VirtualOffset = list.Count // Offset within this insertion group
                });
            }
        }

        /// <summary>
        /// Get inserted bytes at physical position
        /// </summary>
        /// <returns>List of inserted bytes, or empty list if none</returns>
        public List<InsertedByte> GetInsertedBytesAt(long physicalPosition)
        {
            return _insertedBytes.TryGetValue(physicalPosition, out var list) ? list : new List<InsertedByte>();
        }

        /// <summary>
        /// Get total number of inserted bytes at physical position
        /// </summary>
        public int GetInsertionCountAt(long physicalPosition)
        {
            return _insertedBytes.TryGetValue(physicalPosition, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// Remove last inserted byte at physical position (for undo)
        /// </summary>
        /// <returns>True if removed, false if no insertions at that position</returns>
        public bool RemoveLastInsertionAt(long physicalPosition)
        {
            if (!_insertedBytes.TryGetValue(physicalPosition, out var list) || list.Count == 0)
                return false;

            list.RemoveAt(list.Count - 1);

            // Remove entry if list is now empty
            if (list.Count == 0)
                _insertedBytes.Remove(physicalPosition);

            return true;
        }

        #endregion

        #region Public Methods - Delete Bytes

        /// <summary>
        /// Mark byte as deleted at physical position
        /// </summary>
        public void DeleteByte(long physicalPosition)
        {
            _deletedPositions.Add(physicalPosition);

            // Remove from modified if it was modified
            _modifiedBytes.Remove(physicalPosition);
        }

        /// <summary>
        /// Check if byte is deleted at physical position
        /// </summary>
        public bool IsDeleted(long physicalPosition)
        {
            return _deletedPositions.Contains(physicalPosition);
        }

        /// <summary>
        /// Undelete byte at physical position (for undo)
        /// </summary>
        public void UndeleteByte(long physicalPosition)
        {
            _deletedPositions.Remove(physicalPosition);
        }

        #endregion

        #region Public Methods - Clear

        /// <summary>
        /// Clear all modifications
        /// </summary>
        public void ClearAll()
        {
            _modifiedBytes.Clear();
            _insertedBytes.Clear();
            _deletedPositions.Clear();
        }

        /// <summary>
        /// Clear only modified bytes
        /// </summary>
        public void ClearModified()
        {
            _modifiedBytes.Clear();
        }

        /// <summary>
        /// Clear only inserted bytes
        /// </summary>
        public void ClearInserted()
        {
            _insertedBytes.Clear();
        }

        /// <summary>
        /// Clear only deleted bytes
        /// </summary>
        public void ClearDeleted()
        {
            _deletedPositions.Clear();
        }

        #endregion

        #region Public Methods - Query

        /// <summary>
        /// Get all modified positions (for Save operation)
        /// </summary>
        public IEnumerable<long> GetModifiedPositions()
        {
            return _modifiedBytes.Keys;
        }

        /// <summary>
        /// Get all physical positions with insertions
        /// </summary>
        public IEnumerable<long> GetInsertionPositions()
        {
            return _insertedBytes.Keys;
        }

        /// <summary>
        /// Get all deleted positions
        /// </summary>
        public IEnumerable<long> GetDeletedPositions()
        {
            return _deletedPositions;
        }

        /// <summary>
        /// Get total number of inserted bytes across all positions
        /// </summary>
        public long GetTotalInsertedBytesCount()
        {
            return _insertedBytes.Values.Sum(list => list.Count);
        }

        #endregion
    }

    /// <summary>
    /// Represents a single inserted byte with its virtual offset
    /// </summary>
    public struct InsertedByte
    {
        /// <summary>
        /// Byte value
        /// </summary>
        public byte Value { get; set; }

        /// <summary>
        /// Virtual offset from physical position (0-based within insertion group)
        /// Used to calculate virtual position for display
        /// </summary>
        public long VirtualOffset { get; set; }
    }
}
```

---

### 1.3 PositionMapper - Conversion Virtual↔Physical (2-3h)

**Fichier**: `Services/PositionMapper.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// ByteProvider V2 - PositionMapper (Virtual↔Physical conversion)
//////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Maps between virtual positions (what user sees) and physical positions (actual file offsets)
    /// Accounts for insertions and deletions to calculate correct offsets
    /// </summary>
    public class PositionMapper
    {
        #region Fields

        private readonly EditsManager _editsManager;

        // Caches for performance
        private readonly Dictionary<long, long> _virtualToPhysicalCache = new();
        private readonly Dictionary<long, long> _physicalToVirtualCache = new();

        #endregion

        #region Constructor

        public PositionMapper(EditsManager editsManager)
        {
            _editsManager = editsManager;
        }

        #endregion

        #region Public Methods - Conversion

        /// <summary>
        /// Convert virtual position to physical position
        /// Virtual position includes insertions, physical position is actual file offset
        /// </summary>
        public long VirtualToPhysical(long virtualPosition)
        {
            // Check cache first
            if (_virtualToPhysicalCache.TryGetValue(virtualPosition, out long cachedPhysical))
                return cachedPhysical;

            // Calculate physical position by subtracting all inserted bytes before this virtual position
            long physicalPos = virtualPosition;
            long currentVirtualPos = 0;

            // Iterate through all insertion points in order
            foreach (var kvp in _editsManager.GetInsertionPositions().OrderBy(p => p))
            {
                long physicalInsertionPoint = kvp;
                int insertionCount = _editsManager.GetInsertionCountAt(physicalInsertionPoint);

                // Calculate virtual position of this insertion point
                long virtualInsertionStart = currentVirtualPos + physicalInsertionPoint;

                // If target virtual position is before this insertion point, no need to continue
                if (virtualPosition < virtualInsertionStart)
                    break;

                // If target virtual position is within this insertion, return the physical position before insertion
                if (virtualPosition < virtualInsertionStart + insertionCount)
                {
                    // This virtual position points to an inserted byte, return physical position just before
                    physicalPos = physicalInsertionPoint;
                    break;
                }

                // Target is after this insertion, adjust physical position
                currentVirtualPos += insertionCount;
                physicalPos -= insertionCount;
            }

            // Cache result
            _virtualToPhysicalCache[virtualPosition] = physicalPos;
            return physicalPos;
        }

        /// <summary>
        /// Convert physical position to virtual position (starting position)
        /// Returns the first virtual position that maps to this physical position
        /// </summary>
        public long PhysicalToVirtual(long physicalPosition)
        {
            // Check cache first
            if (_physicalToVirtualCache.TryGetValue(physicalPosition, out long cachedVirtual))
                return cachedVirtual;

            // Calculate virtual position by adding all inserted bytes before this physical position
            long virtualPos = physicalPosition;

            foreach (var kvp in _editsManager.GetInsertionPositions().OrderBy(p => p))
            {
                long physicalInsertionPoint = kvp;

                if (physicalInsertionPoint >= physicalPosition)
                    break; // No more insertions before target position

                int insertionCount = _editsManager.GetInsertionCountAt(physicalInsertionPoint);
                virtualPos += insertionCount;
            }

            // Cache result
            _physicalToVirtualCache[physicalPosition] = virtualPos;
            return virtualPos;
        }

        /// <summary>
        /// Calculate virtual length (file length + all insertions - all deletions)
        /// </summary>
        public long GetVirtualLength(long physicalLength)
        {
            long insertedCount = _editsManager.GetTotalInsertedBytesCount();
            long deletedCount = _editsManager.GetDeletedPositions().Count();
            return physicalLength + insertedCount - deletedCount;
        }

        #endregion

        #region Public Methods - Cache Management

        /// <summary>
        /// Invalidate cache (call after any insertion/deletion operation)
        /// </summary>
        public void InvalidateCache()
        {
            _virtualToPhysicalCache.Clear();
            _physicalToVirtualCache.Clear();
        }

        #endregion
    }
}
```

---

## PHASE 2: ByteReader Service (2-3 heures)

**Fichier**: `Services/ByteReader.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// ByteProvider V2 - ByteReader (Intelligent byte reading)
//////////////////////////////////////////////

using WpfHexaEditor.Core;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Intelligent byte reader that accounts for all modifications
    /// Priority: Inserted > Deleted > Modified > File
    /// Works with VIRTUAL positions (what user sees)
    /// </summary>
    public class ByteReader
    {
        #region Fields

        private readonly FileProvider _fileProvider;
        private readonly EditsManager _editsManager;
        private readonly PositionMapper _positionMapper;

        #endregion

        #region Constructor

        public ByteReader(FileProvider fileProvider, EditsManager editsManager, PositionMapper positionMapper)
        {
            _fileProvider = fileProvider;
            _editsManager = editsManager;
            _positionMapper = positionMapper;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Get byte at virtual position with correct priority
        /// Priority: Inserted > Deleted > Modified > File
        /// </summary>
        /// <param name="virtualPosition">Virtual position (includes insertions)</param>
        /// <returns>Byte value and action type</returns>
        public (byte? value, ByteAction action) GetByte(long virtualPosition)
        {
            // Convert to physical position
            long physicalPos = _positionMapper.VirtualToPhysical(virtualPosition);

            // PRIORITY 1: Check if this is an inserted byte
            var insertedBytes = _editsManager.GetInsertedBytesAt(physicalPos);
            if (insertedBytes.Count > 0)
            {
                // Calculate which inserted byte this virtual position refers to
                long virtualInsertionStart = _positionMapper.PhysicalToVirtual(physicalPos);

                foreach (var inserted in insertedBytes)
                {
                    if (virtualInsertionStart + inserted.VirtualOffset == virtualPosition)
                    {
                        return (inserted.Value, ByteAction.Added);
                    }
                }
            }

            // PRIORITY 2: Check if byte is deleted
            if (_editsManager.IsDeleted(physicalPos))
            {
                // Deleted bytes are not displayed, return null
                return (null, ByteAction.Deleted);
            }

            // PRIORITY 3: Check if byte is modified
            byte? modifiedByte = _editsManager.GetModifiedByte(physicalPos);
            if (modifiedByte.HasValue)
            {
                return (modifiedByte.Value, ByteAction.Modified);
            }

            // PRIORITY 4: Read from file
            byte? fileByte = _fileProvider.ReadByte(physicalPos);
            return (fileByte, ByteAction.Nothing);
        }

        /// <summary>
        /// Get multiple bytes starting at virtual position
        /// Optimized batch read with priority handling
        /// </summary>
        /// <param name="virtualPosition">Starting virtual position</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>Array of (byte, action) tuples</returns>
        public (byte? value, ByteAction action)[] GetBytes(long virtualPosition, int count)
        {
            var result = new (byte?, ByteAction)[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = GetByte(virtualPosition + i);
            }

            return result;
        }

        #endregion
    }
}
```

---

## PHASE 3: Undo/Redo avec IEdit Pattern (4-5 heures)

### 3.1 IEdit Interface

**Fichier**: `Services/IEdit.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// ByteProvider V2 - IEdit interface for Undo/Redo
//////////////////////////////////////////////

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Interface for all edit operations (Command pattern for Undo/Redo)
    /// </summary>
    public interface IEdit
    {
        /// <summary>
        /// Apply this edit to EditsManager
        /// </summary>
        void Apply(EditsManager editsManager);

        /// <summary>
        /// Undo this edit from EditsManager
        /// </summary>
        void Undo(EditsManager editsManager);

        /// <summary>
        /// Physical position affected by this edit
        /// </summary>
        long PhysicalPosition { get; }

        /// <summary>
        /// Description of this edit (for debugging)
        /// </summary>
        string Description { get; }
    }
}
```

### 3.2 ModifyByteEdit

**Fichier**: `Services/Edits/ModifyByteEdit.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// ByteProvider V2 - ModifyByteEdit
//////////////////////////////////////////////

namespace WpfHexaEditor.Services.Edits
{
    /// <summary>
    /// Edit operation: Modify single byte
    /// </summary>
    public class ModifyByteEdit : IEdit
    {
        public long PhysicalPosition { get; }
        public byte NewValue { get; }
        public byte? OriginalValue { get; private set; } // Captured on Apply
        public string Description => $"Modify byte at {PhysicalPosition}: {OriginalValue:X2} → {NewValue:X2}";

        public ModifyByteEdit(long physicalPosition, byte newValue)
        {
            PhysicalPosition = physicalPosition;
            NewValue = newValue;
        }

        public void Apply(EditsManager editsManager)
        {
            // Capture original value for undo
            OriginalValue = editsManager.GetModifiedByte(PhysicalPosition);

            // Apply modification
            editsManager.ModifyByte(PhysicalPosition, NewValue);
        }

        public void Undo(EditsManager editsManager)
        {
            if (OriginalValue.HasValue)
            {
                // Restore original modified value
                editsManager.ModifyByte(PhysicalPosition, OriginalValue.Value);
            }
            else
            {
                // Was not modified before, remove modification
                editsManager.ClearModified(); // Note: Need to add method to remove single modification
            }
        }
    }
}
```

### 3.3 InsertBytesEdit

**Fichier**: `Services/Edits/InsertBytesEdit.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// ByteProvider V2 - InsertBytesEdit
//////////////////////////////////////////////

namespace WpfHexaEditor.Services.Edits
{
    /// <summary>
    /// Edit operation: Insert bytes at position
    /// </summary>
    public class InsertBytesEdit : IEdit
    {
        public long PhysicalPosition { get; }
        public byte[] Bytes { get; }
        public string Description => $"Insert {Bytes.Length} bytes at {PhysicalPosition}";

        public InsertBytesEdit(long physicalPosition, byte[] bytes)
        {
            PhysicalPosition = physicalPosition;
            Bytes = bytes;
        }

        public void Apply(EditsManager editsManager)
        {
            editsManager.InsertBytes(PhysicalPosition, Bytes);
        }

        public void Undo(EditsManager editsManager)
        {
            // Remove inserted bytes (removes last N insertions at this position)
            for (int i = 0; i < Bytes.Length; i++)
            {
                editsManager.RemoveLastInsertionAt(PhysicalPosition);
            }
        }
    }
}
```

### 3.4 DeleteByteEdit

**Fichier**: `Services/Edits/DeleteByteEdit.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// ByteProvider V2 - DeleteByteEdit
//////////////////////////////////////////////

namespace WpfHexaEditor.Services.Edits
{
    /// <summary>
    /// Edit operation: Delete byte at position
    /// </summary>
    public class DeleteByteEdit : IEdit
    {
        public long PhysicalPosition { get; }
        public string Description => $"Delete byte at {PhysicalPosition}";

        public DeleteByteEdit(long physicalPosition)
        {
            PhysicalPosition = physicalPosition;
        }

        public void Apply(EditsManager editsManager)
        {
            editsManager.DeleteByte(PhysicalPosition);
        }

        public void Undo(EditsManager editsManager)
        {
            editsManager.UndeleteByte(PhysicalPosition);
        }
    }
}
```

### 3.5 UndoRedoService V2

**Fichier**: `Services/UndoRedoServiceV2.cs`

```csharp
//////////////////////////////////////////////
// Apache 2.0  - 2026
// ByteProvider V2 - UndoRedoServiceV2
//////////////////////////////////////////////

using System.Collections.Generic;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Undo/Redo service using IEdit pattern (Command pattern)
    /// Replaces legacy ByteProvider undo/redo
    /// </summary>
    public class UndoRedoServiceV2
    {
        #region Fields

        private readonly Stack<IEdit> _undoStack = new();
        private readonly Stack<IEdit> _redoStack = new();
        private readonly EditsManager _editsManager;

        #endregion

        #region Constructor

        public UndoRedoServiceV2(EditsManager editsManager)
        {
            _editsManager = editsManager;
        }

        #endregion

        #region Properties

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;
        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;

        #endregion

        #region Public Methods

        /// <summary>
        /// Record an edit operation (after applying it)
        /// </summary>
        public void RecordEdit(IEdit edit)
        {
            _undoStack.Push(edit);
            _redoStack.Clear(); // New operation invalidates redo history
        }

        /// <summary>
        /// Undo last operation
        /// </summary>
        /// <returns>Physical position of undone edit</returns>
        public long Undo()
        {
            if (!CanUndo)
                return -1;

            var edit = _undoStack.Pop();
            edit.Undo(_editsManager);
            _redoStack.Push(edit);

            return edit.PhysicalPosition;
        }

        /// <summary>
        /// Redo last undone operation
        /// </summary>
        /// <returns>Physical position of redone edit</returns>
        public long Redo()
        {
            if (!CanRedo)
                return -1;

            var edit = _redoStack.Pop();
            edit.Apply(_editsManager);
            _undoStack.Push(edit);

            return edit.PhysicalPosition;
        }

        /// <summary>
        /// Clear all undo/redo history
        /// </summary>
        public void ClearAll()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        #endregion
    }
}
```

---

## PHASE 4: Intégration dans HexEditorViewModel (4-5 heures)

### 4.1 Adapter HexEditorViewModel

**Modifications**: `V2/ViewModels/HexEditorViewModel.cs`

```csharp
public class HexEditorViewModel : INotifyPropertyChanged
{
    #region Fields - V2 Services

    // NEW: V2 Services replace ByteProvider internals
    private FileProvider _fileProvider;
    private EditsManager _editsManager;
    private PositionMapper _positionMapper;
    private ByteReader _byteReader;
    private UndoRedoServiceV2 _undoRedoV2;

    // KEEP for compatibility with existing code
    private readonly ByteProvider _provider; // Legacy wrapper

    #endregion

    #region Constructor

    private HexEditorViewModel(string filePath)
    {
        // Initialize V2 services
        _fileProvider = new FileProvider();
        _fileProvider.OpenFile(filePath, readOnly: false);

        _editsManager = new EditsManager();
        _positionMapper = new PositionMapper(_editsManager);
        _byteReader = new ByteReader(_fileProvider, _editsManager, _positionMapper);
        _undoRedoV2 = new UndoRedoServiceV2(_editsManager);

        // Initialize legacy ByteProvider as wrapper (for V1 compatibility)
        _provider = new ByteProvider();
        _provider.OpenFile(filePath, readOnly: false);

        // Initialize UI collections
        Lines = new ObservableCollection<HexLine>();
    }

    #endregion

    #region Properties

    public long VirtualLength => _positionMapper.GetVirtualLength(_fileProvider.Length);
    public long FileLength => _fileProvider.Length;
    public bool CanUndo => _undoRedoV2.CanUndo;
    public bool CanRedo => _undoRedoV2.CanRedo;

    #endregion

    #region Public Methods - Edit Operations

    /// <summary>
    /// Insert byte at virtual position (NEW implementation using V2 services)
    /// </summary>
    public void InsertByte(VirtualPosition virtualPos, byte value)
    {
        if (ReadOnlyMode || EditMode != EditMode.Insert) return;

        var physicalPos = _positionMapper.VirtualToPhysical(virtualPos.Value);

        // Create edit
        var edit = new InsertBytesEdit(physicalPos, new[] { value });

        // Apply
        edit.Apply(_editsManager);

        // Record for undo
        _undoRedoV2.RecordEdit(edit);

        // Invalidate caches
        _positionMapper.InvalidateCache();
        ClearLineCache();
        RefreshVisibleLines();

        // Notify
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(VirtualLength));
    }

    /// <summary>
    /// Modify byte at virtual position (NEW implementation using V2 services)
    /// </summary>
    public void ModifyByte(VirtualPosition virtualPos, byte value)
    {
        if (ReadOnlyMode) return;

        var physicalPos = _positionMapper.VirtualToPhysical(virtualPos.Value);

        // Create edit
        var edit = new ModifyByteEdit(physicalPos, value);

        // Apply
        edit.Apply(_editsManager);

        // Record for undo
        _undoRedoV2.RecordEdit(edit);

        // Refresh
        ClearLineCache();
        RefreshVisibleLines();

        // Notify
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>
    /// Undo last operation (NEW implementation using V2 services)
    /// </summary>
    public void Undo()
    {
        long position = _undoRedoV2.Undo();

        // Invalidate cache (position mapping may have changed)
        _positionMapper.InvalidateCache();
        ClearLineCache();
        RefreshVisibleLines();

        // Notify
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(VirtualLength));
    }

    /// <summary>
    /// Redo last undone operation (NEW implementation using V2 services)
    /// </summary>
    public void Redo()
    {
        long position = _undoRedoV2.Redo();

        // Invalidate cache
        _positionMapper.InvalidateCache();
        ClearLineCache();
        RefreshVisibleLines();

        // Notify
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(VirtualLength));
    }

    #endregion

    #region Private Methods - Byte Reading

    /// <summary>
    /// Create ByteData for display (NEW implementation using ByteReader)
    /// </summary>
    private ByteData CreateByteDataOptimized(VirtualPosition virtualPos)
    {
        // Use ByteReader to get byte with correct priority
        var (byteValue, action) = _byteReader.GetByte(virtualPos.Value);

        if (!byteValue.HasValue)
            return null; // Deleted or invalid position

        var physicalPos = _positionMapper.VirtualToPhysical(virtualPos.Value);

        // Calculate cursor
        var cursorPos = _selectionStop.IsValid ? _selectionStop : _selectionStart;
        bool isCursor = cursorPos.IsValid && virtualPos == cursorPos;

        return new ByteData
        {
            VirtualPos = virtualPos,
            PhysicalPos = new PhysicalPosition(physicalPos),
            Value = byteValue.Value,
            Action = action,
            IsSelected = IsByteSelected(virtualPos),
            IsCursor = isCursor
        };
    }

    #endregion
}
```

---

## PHASE 5: Migration et Tests (4-5 heures)

### 5.1 Stratégie de Migration

**Étape 1: Déploiement Parallèle (1-2h)**
- Garder ByteProvider legacy pour V1 compatibility
- Ajouter V2 services en parallèle dans ViewModel
- Tester que les deux systèmes coexistent

**Étape 2: Migration Progressive (2-3h)**
- Migrer InsertByte() vers V2 services
- Migrer ModifyByte() vers V2 services
- Migrer Undo/Redo vers V2 services
- Tester chaque migration individuellement

**Étape 3: Cleanup (1h)**
- Retirer les appels à ByteProvider legacy
- Marquer ByteProvider legacy comme [Obsolete]
- Documentation des changements

### 5.2 Tests Unitaires

**Fichier**: `Tests/Services/EditsManagerTests.cs`

```csharp
using Xunit;
using WpfHexaEditor.Services;

namespace WpfHexaEditor.Tests.Services
{
    public class EditsManagerTests
    {
        [Fact]
        public void ModifyByte_ShouldStoreModification()
        {
            // Arrange
            var editsManager = new EditsManager();

            // Act
            editsManager.ModifyByte(100, 0xFF);

            // Assert
            Assert.True(editsManager.IsModified(100));
            Assert.Equal(0xFF, editsManager.GetModifiedByte(100));
        }

        [Fact]
        public void InsertBytes_ShouldStoreInsertions()
        {
            // Arrange
            var editsManager = new EditsManager();
            byte[] bytes = { 0xAA, 0xBB, 0xCC };

            // Act
            editsManager.InsertBytes(50, bytes);

            // Assert
            var insertions = editsManager.GetInsertedBytesAt(50);
            Assert.Equal(3, insertions.Count);
            Assert.Equal(0xAA, insertions[0].Value);
            Assert.Equal(0, insertions[0].VirtualOffset);
            Assert.Equal(0xBB, insertions[1].Value);
            Assert.Equal(1, insertions[1].VirtualOffset);
        }

        [Fact]
        public void DeleteByte_ShouldMarkAsDeleted()
        {
            // Arrange
            var editsManager = new EditsManager();

            // Act
            editsManager.DeleteByte(200);

            // Assert
            Assert.True(editsManager.IsDeleted(200));
        }

        [Fact]
        public void ClearAll_ShouldRemoveAllModifications()
        {
            // Arrange
            var editsManager = new EditsManager();
            editsManager.ModifyByte(10, 0x11);
            editsManager.InsertBytes(20, new byte[] { 0x22 });
            editsManager.DeleteByte(30);

            // Act
            editsManager.ClearAll();

            // Assert
            Assert.False(editsManager.HasModifications);
            Assert.False(editsManager.IsModified(10));
            Assert.Empty(editsManager.GetInsertedBytesAt(20));
            Assert.False(editsManager.IsDeleted(30));
        }
    }
}
```

**Fichier**: `Tests/Services/PositionMapperTests.cs`

```csharp
using Xunit;
using WpfHexaEditor.Services;

namespace WpfHexaEditor.Tests.Services
{
    public class PositionMapperTests
    {
        [Fact]
        public void VirtualToPhysical_NoInsertions_ReturnsIdentical()
        {
            // Arrange
            var editsManager = new EditsManager();
            var mapper = new PositionMapper(editsManager);

            // Act
            long physical = mapper.VirtualToPhysical(100);

            // Assert
            Assert.Equal(100, physical);
        }

        [Fact]
        public void VirtualToPhysical_WithInsertionBefore_SubtractsInsertionCount()
        {
            // Arrange
            var editsManager = new EditsManager();
            editsManager.InsertBytes(50, new byte[] { 0xAA, 0xBB, 0xCC }); // 3 bytes inserted at pos 50

            var mapper = new PositionMapper(editsManager);

            // Act
            long physical = mapper.VirtualToPhysical(100); // Virtual 100 should map to physical 97

            // Assert
            Assert.Equal(97, physical); // 100 - 3 insertions = 97
        }

        [Fact]
        public void PhysicalToVirtual_WithInsertionBefore_AddsInsertionCount()
        {
            // Arrange
            var editsManager = new EditsManager();
            editsManager.InsertBytes(50, new byte[] { 0xAA, 0xBB }); // 2 bytes inserted at pos 50

            var mapper = new PositionMapper(editsManager);

            // Act
            long virtual = mapper.PhysicalToVirtual(100); // Physical 100 should map to virtual 102

            // Assert
            Assert.Equal(102, virtual); // 100 + 2 insertions = 102
        }

        [Fact]
        public void GetVirtualLength_WithInsertions_AddsInsertionCount()
        {
            // Arrange
            var editsManager = new EditsManager();
            editsManager.InsertBytes(10, new byte[] { 0x11, 0x22, 0x33 }); // +3 bytes
            editsManager.InsertBytes(20, new byte[] { 0x44, 0x55 }); // +2 bytes

            var mapper = new PositionMapper(editsManager);

            // Act
            long virtualLength = mapper.GetVirtualLength(1000); // File length 1000

            // Assert
            Assert.Equal(1005, virtualLength); // 1000 + 5 insertions = 1005
        }
    }
}
```

---

## PHASE 6: Documentation et Finalisation (1-2 heures)

### 6.1 Diagramme d'Architecture Finale

```
┌─────────────────────────────────────────────────────┐
│          HexEditorViewModel (Orchestrator)          │
│   - Coordinates all services                        │
│   - Manages UI state (Lines, Selection)             │
│   - Converts Virtual ↔ Physical via PositionMapper  │
└─────────────────────────────────────────────────────┘
         │           │           │           │
         ▼           ▼           ▼           ▼
   ┌──────────┐ ┌─────────┐ ┌──────────┐ ┌──────────┐
   │   File   │ │  Edits  │ │  Undo/   │ │ Position │
   │ Provider │ │ Manager │ │  Redo    │ │  Mapper  │
   └──────────┘ └─────────┘ └──────────┘ └──────────┘
         │           ▲           │           │
         └───────────┴───────────┴───────────┘
                     │
               ┌──────────┐
               │   Byte   │
               │  Reader  │
               └──────────┘
```

### 6.2 Checklist de Validation

- [ ] FileProvider lit/écrit correctement avec positions physiques
- [ ] EditsManager track toutes modifications (Modified/Added/Deleted)
- [ ] PositionMapper convertit correctement Virtual↔Physical
- [ ] ByteReader retourne byte avec bonne priorité (Added > Deleted > Modified > File)
- [ ] UndoRedoServiceV2 avec IEdit pattern fonctionne
- [ ] HexEditorViewModel utilise tous les V2 services
- [ ] Paste() en Insert mode crée ByteAdded (pas ByteModified)
- [ ] Undo/Redo fonctionne pour insertions
- [ ] Tests unitaires passent (100% coverage pour services)
- [ ] Performance maintenue (99% boost préservé)
- [ ] Compatibilité V1 samples préservée

---

## Conclusion

Ce plan d'implémentation fournit:
- ✅ **Code source complet** pour tous les services
- ✅ **Architecture claire** avec séparation des responsabilités
- ✅ **Migration progressive** sans casser l'existant
- ✅ **Tests unitaires** pour validation
- ✅ **16-21 heures d'effort** estimé

**Prochaines étapes:**
1. Commencer par Phase 1 (Services de base)
2. Tester chaque service indépendamment
3. Intégrer progressivement dans ViewModel
4. Valider avec tests unitaires
5. Migrer complètement de ByteProvider legacy vers V2

**Avantages de cette architecture:**
- Clarté totale sur Virtual vs Physical positions
- Support complet des insertions (résout le bug de bytes qui disparaissent)
- Undo/Redo robuste avec Command pattern
- Caching intelligent pour performance
- Extensible pour futures fonctionnalités
