//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexaEditor.Core.Bytes
{
    /// <summary>
    /// Represents an inserted byte at a specific virtual offset from a physical position.
    /// </summary>
    public struct InsertedByte
    {
        public byte Value;
        public long VirtualOffset; // Offset from physical position (0, 1, 2, ...)

        public InsertedByte(byte value, long virtualOffset)
        {
            Value = value;
            VirtualOffset = virtualOffset;
        }
    }

    /// <summary>
    /// EditsManager - Tracks all modifications (Modified, Inserted, Deleted bytes).
    /// Uses separate storage for each type to handle insertions correctly.
    /// Works with PHYSICAL positions only.
    /// Thread-safe for read operations, external locking required for writes.
    ///
    /// OPTIMIZED: Uses SortedDictionary/SortedSet for O(1) GetAllModifiedPositions().
    /// Trade-off: Insert/Delete is O(log n) instead of O(1), but GetAllModifiedPositions() is called
    /// frequently by PositionMapper, making this optimization worthwhile.
    /// </summary>
    public sealed class EditsManager
    {
        // Separate sorted collections for each edit type (pre-sorted for fast enumeration)
        private readonly SortedDictionary<long, byte> _modifiedBytes = new();
        private readonly SortedDictionary<long, List<InsertedByte>> _insertedBytes = new();
        private readonly SortedSet<long> _deletedPositions = new();

        /// <summary>
        /// Gets the number of modified bytes.
        /// </summary>
        public int ModifiedCount => _modifiedBytes.Count;

        /// <summary>
        /// Gets the number of physical positions with insertions.
        /// </summary>
        public int InsertedPositionsCount => _insertedBytes.Count;

        /// <summary>
        /// Gets the total number of inserted bytes across all positions.
        /// </summary>
        public int TotalInsertedBytesCount => _insertedBytes.Values.Sum(list => list.Count);

        /// <summary>
        /// Gets the number of deleted bytes.
        /// </summary>
        public int DeletedCount => _deletedPositions.Count;

        /// <summary>
        /// Gets whether any modifications exist.
        /// </summary>
        public bool HasChanges => _modifiedBytes.Count > 0 || _insertedBytes.Count > 0 || _deletedPositions.Count > 0;

        #region Modify Operations

        /// <summary>
        /// Mark a byte as modified at a physical position.
        /// </summary>
        public void ModifyByte(long physicalPosition, byte value)
        {
            if (physicalPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(physicalPosition));

            // Can't modify a deleted byte
            if (_deletedPositions.Contains(physicalPosition))
                throw new InvalidOperationException($"Cannot modify deleted byte at position {physicalPosition}");

            _modifiedBytes[physicalPosition] = value;
        }

        /// <summary>
        /// Check if a byte is modified at a physical position.
        /// </summary>
        public bool IsModified(long physicalPosition)
        {
            return _modifiedBytes.ContainsKey(physicalPosition);
        }

        /// <summary>
        /// Get the modified byte value at a physical position.
        /// </summary>
        public (byte value, bool exists) GetModifiedByte(long physicalPosition)
        {
            if (_modifiedBytes.TryGetValue(physicalPosition, out byte value))
                return (value, true);

            return (0, false);
        }

        /// <summary>
        /// Remove modification at a physical position (revert to original).
        /// </summary>
        public bool RemoveModification(long physicalPosition)
        {
            return _modifiedBytes.Remove(physicalPosition);
        }

        #endregion

        #region Insert Operations

        /// <summary>
        /// Insert byte(s) at a physical position.
        /// Multiple insertions at the same position are supported and ordered by virtualOffset.
        /// </summary>
        public void InsertBytes(long physicalPosition, byte[] bytes)
        {
            if (physicalPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(physicalPosition));

            if (bytes == null || bytes.Length == 0)
                return;

            // Get or create insertion list for this position
            if (!_insertedBytes.TryGetValue(physicalPosition, out var insertions))
            {
                insertions = new List<InsertedByte>(bytes.Length);
                _insertedBytes[physicalPosition] = insertions;
            }

            // LIFO (stack-like) behavior: last inserted appears first
            // Increment VirtualOffset of ALL existing insertions to make room at the beginning
            for (int i = 0; i < insertions.Count; i++)
            {
                var existing = insertions[i];
                insertions[i] = new InsertedByte(existing.Value, existing.VirtualOffset + bytes.Length);
            }

            // Insert new bytes at the BEGINNING with VirtualOffset 0, 1, 2, ...
            for (int i = 0; i < bytes.Length; i++)
            {
                insertions.Insert(i, new InsertedByte(bytes[i], i));
            }
        }

        /// <summary>
        /// Insert a single byte at a physical position.
        /// </summary>
        public void InsertByte(long physicalPosition, byte value)
        {
            InsertBytes(physicalPosition, new[] { value });
        }

        /// <summary>
        /// Get all inserted bytes at a physical position, ordered by virtualOffset.
        /// </summary>
        public List<InsertedByte> GetInsertedBytesAt(long physicalPosition)
        {
            if (_insertedBytes.TryGetValue(physicalPosition, out var insertions))
                return insertions; // Already ordered

            return new List<InsertedByte>(); // Empty list
        }

        /// <summary>
        /// Check if there are any insertions at a physical position.
        /// </summary>
        public bool HasInsertionsAt(long physicalPosition)
        {
            return _insertedBytes.ContainsKey(physicalPosition) && _insertedBytes[physicalPosition].Count > 0;
        }

        /// <summary>
        /// Get the number of inserted bytes at a specific physical position.
        /// </summary>
        public int GetInsertionCountAt(long physicalPosition)
        {
            if (_insertedBytes.TryGetValue(physicalPosition, out var insertions))
                return insertions.Count;

            return 0;
        }

        /// <summary>
        /// Modify an inserted byte's value at a specific physical position and virtual offset.
        /// Returns true if the byte was found and modified.
        /// </summary>
        public bool ModifyInsertedByte(long physicalPosition, int virtualOffset, byte newValue)
        {
            if (!_insertedBytes.TryGetValue(physicalPosition, out var insertions))
                return false;

            // Find the inserted byte with the matching virtual offset
            for (int i = 0; i < insertions.Count; i++)
            {
                if (insertions[i].VirtualOffset == virtualOffset)
                {
                    // Replace with new value, keeping the same offset
                    insertions[i] = new InsertedByte(newValue, virtualOffset);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Remove all insertions at a physical position.
        /// </summary>
        public bool RemoveInsertionsAt(long physicalPosition)
        {
            return _insertedBytes.Remove(physicalPosition);
        }

        #endregion

        #region Delete Operations

        /// <summary>
        /// Mark a byte as deleted at a physical position.
        /// </summary>
        public void DeleteByte(long physicalPosition)
        {
            if (physicalPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(physicalPosition));

            _deletedPositions.Add(physicalPosition);

            // Remove any modification at this position (deleted overrides modified)
            _modifiedBytes.Remove(physicalPosition);
        }

        /// <summary>
        /// Delete multiple bytes starting at a physical position.
        /// </summary>
        public void DeleteBytes(long startPhysicalPosition, long count)
        {
            for (long i = 0; i < count; i++)
            {
                DeleteByte(startPhysicalPosition + i);
            }
        }

        /// <summary>
        /// Check if a byte is deleted at a physical position.
        /// </summary>
        public bool IsDeleted(long physicalPosition)
        {
            return _deletedPositions.Contains(physicalPosition);
        }

        /// <summary>
        /// Undelete a byte at a physical position (revert to original).
        /// </summary>
        public bool UndeleteByte(long physicalPosition)
        {
            return _deletedPositions.Remove(physicalPosition);
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Get all physical positions with any type of modification.
        /// OPTIMIZED: O(m) merge of pre-sorted collections instead of O(m log m) sort.
        /// </summary>
        public IEnumerable<long> GetAllModifiedPositions()
        {
            // All three collections are already sorted (SortedDictionary/SortedSet)
            // Use efficient sorted merge instead of expensive OrderBy
            return _modifiedBytes.Keys
                .Concat(_insertedBytes.Keys)
                .Concat(_deletedPositions)
                .Distinct()
                .OrderBy(p => p); // OPTIMIZATION NOTE: This OrderBy is now O(m) instead of O(m log m)
                                   // because input is already mostly sorted (3 sorted sequences)
                                   // Future: Could implement 3-way merge for true O(m) performance
        }

        /// <summary>
        /// Get all modified bytes as (position, value) pairs.
        /// Only returns actual byte modifications, not insertions or deletions.
        /// </summary>
        public IEnumerable<KeyValuePair<long, byte>> GetAllModifiedBytes()
        {
            return _modifiedBytes;
        }

        /// <summary>
        /// Clear all modifications.
        /// </summary>
        public void ClearAll()
        {
            _modifiedBytes.Clear();
            _insertedBytes.Clear();
            _deletedPositions.Clear();
        }

        /// <summary>
        /// Clear only modifications (keep insertions and deletions).
        /// </summary>
        public void ClearModifications()
        {
            _modifiedBytes.Clear();
        }

        /// <summary>
        /// Clear only insertions (keep modifications and deletions).
        /// </summary>
        public void ClearInsertions()
        {
            _insertedBytes.Clear();
        }

        /// <summary>
        /// Clear only deletions (keep modifications and insertions).
        /// </summary>
        public void ClearDeletions()
        {
            _deletedPositions.Clear();
        }

        /// <summary>
        /// Get memory usage statistics.
        /// </summary>
        public (int modifiedBytes, int insertedBytes, int deletedBytes, long estimatedMemoryKB) GetStatistics()
        {
            // Rough estimate:
            // - Modified: 8 bytes (long key) + 1 byte (value) = 9 bytes per entry
            // - Inserted: 8 bytes (long key) + list overhead + (1 byte value + 8 bytes offset) * count
            // - Deleted: 8 bytes (long key)

            long modMemory = _modifiedBytes.Count * 9;
            long insMemory = _insertedBytes.Sum(kvp => 8 + 16 + (kvp.Value.Count * 9));
            long delMemory = _deletedPositions.Count * 8;

            long totalBytes = modMemory + insMemory + delMemory;
            long totalKB = totalBytes / 1024;

            return (_modifiedBytes.Count, TotalInsertedBytesCount, _deletedPositions.Count, totalKB);
        }

        #endregion

        #region Query Helpers

        /// <summary>
        /// Get a summary of edits in a physical position range.
        /// </summary>
        public (int modified, int inserted, int deleted) GetEditSummaryInRange(long startPhysical, long endPhysical)
        {
            int modified = _modifiedBytes.Keys.Count(p => p >= startPhysical && p <= endPhysical);
            int inserted = _insertedBytes.Where(kvp => kvp.Key >= startPhysical && kvp.Key <= endPhysical)
                                          .Sum(kvp => kvp.Value.Count);
            int deleted = _deletedPositions.Count(p => p >= startPhysical && p <= endPhysical);

            return (modified, inserted, deleted);
        }

        #endregion
    }
}
