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
    /// ByteReader - Intelligent byte reading service that combines FileProvider, EditsManager, and PositionMapper.
    /// Works with VIRTUAL positions (user-visible) and handles Modified/Inserted/Deleted bytes transparently.
    /// Provides ultra-fast reading with multiple caching layers.
    /// </summary>
    public sealed class ByteReader
    {
        private readonly FileProvider _fileProvider;
        private readonly EditsManager _editsManager;
        private readonly PositionMapper _positionMapper;

        // Line cache for rendering performance (most common use case)
        private readonly Dictionary<long, byte[]> _lineCache = new();
        private const int MAX_LINE_CACHE_ENTRIES = 1000; // ~16KB cache for 16-byte lines
        private const int LINE_SIZE = 16; // Fixed line size for cache (must match typical BytesPerLine)

        public ByteReader(FileProvider fileProvider, EditsManager editsManager, PositionMapper positionMapper)
        {
            _fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
            _editsManager = editsManager ?? throw new ArgumentNullException(nameof(editsManager));
            _positionMapper = positionMapper ?? throw new ArgumentNullException(nameof(positionMapper));
        }

        /// <summary>
        /// Read a single byte at a virtual position.
        /// Checks cache first, but doesn't populate cache (use GetLine() for that).
        /// </summary>
        /// <param name="virtualPosition">Virtual position (user-visible)</param>
        /// <returns>Byte value and success flag</returns>
        public (byte value, bool success) GetByte(long virtualPosition)
        {
            // Quick cache check (without populating cache)
            long lineStart = (virtualPosition / LINE_SIZE) * LINE_SIZE;
            if (_lineCache.TryGetValue(lineStart, out byte[] cachedLine))
            {
                int offset = (int)(virtualPosition - lineStart);
                if (offset < cachedLine.Length)
                    return (cachedLine[offset], true);
            }

            // Cache miss - read directly without populating cache
            // (cache is populated by GetLine() calls during rendering)
            return ReadByteInternal(virtualPosition);
        }

        /// <summary>
        /// Internal method to read a single byte without cache.
        /// Used by GetBytes() to populate the cache.
        /// </summary>
        private (byte value, bool success) ReadByteInternal(long virtualPosition)
        {
            if (!_fileProvider.IsOpen || virtualPosition < 0)
                return (0, false);

            long physicalFileLength = _fileProvider.Length;
            long virtualLength = _positionMapper.GetVirtualLength(physicalFileLength);

            if (virtualPosition >= virtualLength)
                return (0, false);

            // Convert virtual to physical
            var (physicalPos, isInserted) = _positionMapper.VirtualToPhysical(virtualPosition, physicalFileLength);

            // Handle inserted bytes
            if (isInserted)
            {
                // Find the inserted byte at this virtual position
                var insertions = _editsManager.GetInsertedBytesAt(physicalPos.Value);

                // CRITICAL FIX: Insertions are stored in LIFO order
                // Array: [newest (offset=0), ..., oldest (offset=N-1)]
                // Virtual positions: [oldest=virtualStart, ..., newest=virtualStart+N-1]
                // Must INVERT the offset calculation!
                long virtualStart = _positionMapper.PhysicalToVirtual(physicalPos.Value, physicalFileLength);
                long relativePosition = virtualPosition - virtualStart;
                int totalInsertions = insertions.Count;

                // Invert: newest (high virtual pos) has low offset, oldest (low virtual pos) has high offset
                long targetOffset = totalInsertions - 1 - relativePosition;

                // Search for inserted byte with matching VirtualOffset
                for (int i = 0; i < insertions.Count; i++)
                {
                    if (insertions[i].VirtualOffset == targetOffset)
                    {
                        return (insertions[i].Value, true);
                    }
                }

                // CRITICAL ERROR: Insertion not found - this indicates a bug in LIFO offset calculation
                // Build detailed diagnostic message showing all available offsets
                var availableOffsets = string.Join(", ",
                    insertions.Select((ib, idx) => $"[{idx}]=offset:{ib.VirtualOffset}/value:0x{ib.Value:X2}"));

                throw new InvalidOperationException(
                    $"CRITICAL: Insertion lookup failed at virtual position 0x{virtualPosition:X}.\n" +
                    $"Details:\n" +
                    $"  Physical Position: 0x{physicalPos.Value:X}\n" +
                    $"  Virtual Start (PhysicalToVirtual): 0x{virtualStart:X}\n" +
                    $"  Relative Position: {relativePosition}\n" +
                    $"  Total Insertions: {totalInsertions}\n" +
                    $"  Target Offset (calculated): {targetOffset}\n" +
                    $"  Available insertions: {availableOffsets}\n" +
                    $"This indicates either:\n" +
                    $"  1. Bug in LIFO offset inversion formula\n" +
                    $"  2. PhysicalToVirtual returns wrong semantics (NEWEST instead of OLDEST)\n" +
                    $"  3. EditsManager insertion list is corrupted");
            }

            // No physical position = beyond file
            if (!physicalPos.HasValue)
                return (0, false);

            long physical = physicalPos.Value;

            // Check if deleted
            if (_editsManager.IsDeleted(physical))
            {
                // Deleted bytes don't appear in virtual view
                // This shouldn't happen if VirtualToPhysical is correct
                return (0, false);
            }

            // Check if modified
            var (modValue, modExists) = _editsManager.GetModifiedByte(physical);
            if (modExists)
            {
                return (modValue, true);
            }

            // Read from file
            return _fileProvider.ReadByte(physical);
        }

        /// <summary>
        /// Read multiple bytes starting at a virtual position.
        /// CRITICAL FIX: Always returns exactly 'count' bytes (or throws exception).
        /// Phase 2 of save bug fix - ensures GetBytes never returns partial data.
        /// </summary>
        /// <param name="virtualPosition">Starting virtual position</param>
        /// <param name="count">Number of bytes to read</param>
        /// <returns>Byte array of exactly 'count' bytes</returns>
        public byte[] GetBytes(long virtualPosition, int count)
        {
            if (!_fileProvider.IsOpen || virtualPosition < 0 || count <= 0)
                return Array.Empty<byte>();

            long physicalFileLength = _fileProvider.Length;
            long virtualLength = _positionMapper.GetVirtualLength(physicalFileLength);

            // Clamp to available bytes
            long available = virtualLength - virtualPosition;
            if (available <= 0)
                return Array.Empty<byte>();

            count = (int)Math.Min(count, available);

            // CRITICAL FIX: Always allocate and fill the full requested buffer
            // Don't return early with partial cache data - that causes massive data loss in SaveAs
            byte[] result = new byte[count];
            int bytesRead = 0;

            // Read bytes one by one until buffer is full
            while (bytesRead < count)
            {
                long currentPos = virtualPosition + bytesRead;
                var (b, success) = ReadByteInternal(currentPos);

                if (!success)
                {
                    // ReadByteInternal should throw detailed exception if it fails
                    // If we reach here, it means virtualLength calculation was wrong
                    throw new InvalidOperationException(
                        $"CRITICAL: Failed to read byte at virtual position 0x{currentPos:X} " +
                        $"(bytesRead={bytesRead}/{count}). VirtualLength={virtualLength} but byte not available. " +
                        $"This indicates a bug in PositionMapper.GetVirtualLength().");
                }

                result[bytesRead] = b;
                bytesRead++;
            }

            // Guarantee: result.Length == count
            return result;
        }

        /// <summary>
        /// Read multiple bytes for a line (optimized for rendering).
        /// Uses aggressive caching.
        /// </summary>
        public byte[] GetLine(long virtualLineStart, int bytesPerLine)
        {
            // Check cache first
            if (_lineCache.TryGetValue(virtualLineStart, out byte[] cachedLine))
            {
                return cachedLine;
            }

            // Read the line
            byte[] line = GetBytes(virtualLineStart, bytesPerLine);

            // Cache it
            CacheLine(virtualLineStart, line);

            return line;
        }

        /// <summary>
        /// Batch read multiple lines (ultra-optimized for viewport rendering).
        /// Pre-warms caches for maximum performance.
        /// </summary>
        public List<byte[]> GetLines(long startVirtualPosition, int lineCount, int bytesPerLine)
        {
            if (!_fileProvider.IsOpen || lineCount <= 0)
                return new List<byte[]>();

            var lines = new List<byte[]>(lineCount);
            long virtualLength = _positionMapper.GetVirtualLength(_fileProvider.Length);

            // Pre-warm position mapper cache for this range
            long endVirtual = Math.Min(startVirtualPosition + (lineCount * bytesPerLine), virtualLength - 1);
            _positionMapper.WarmupCache(startVirtualPosition, endVirtual, _fileProvider.Length);

            // Read each line
            long currentVirtual = startVirtualPosition;
            for (int i = 0; i < lineCount && currentVirtual < virtualLength; i++)
            {
                byte[] line = GetLine(currentVirtual, bytesPerLine);
                if (line.Length == 0)
                    break;

                lines.Add(line);
                currentVirtual += bytesPerLine;
            }

            return lines;
        }

        #region Cache Management

        private void CacheLine(long virtualLineStart, byte[] lineData)
        {
            // Limit cache size
            if (_lineCache.Count >= MAX_LINE_CACHE_ENTRIES)
            {
                // Simple LRU: clear oldest entries (first added)
                var keysToRemove = new List<long>();
                int toRemove = MAX_LINE_CACHE_ENTRIES / 4; // Remove 25%
                int removed = 0;

                foreach (var key in _lineCache.Keys)
                {
                    keysToRemove.Add(key);
                    if (++removed >= toRemove)
                        break;
                }

                foreach (var key in keysToRemove)
                    _lineCache.Remove(key);
            }

            _lineCache[virtualLineStart] = lineData;
        }

        /// <summary>
        /// Clear line cache (call after edits).
        /// </summary>
        public void ClearLineCache()
        {
            _lineCache.Clear();
        }

        /// <summary>
        /// Invalidate line cache in a specific virtual range.
        /// </summary>
        public void InvalidateCacheRange(long startVirtual, long endVirtual, int bytesPerLine)
        {
            long startLine = (startVirtual / bytesPerLine) * bytesPerLine;
            long endLine = (endVirtual / bytesPerLine) * bytesPerLine;

            var keysToRemove = new List<long>();

            foreach (var key in _lineCache.Keys)
            {
                if (key >= startLine && key <= endLine)
                    keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
                _lineCache.Remove(key);
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        public (int lineCacheEntries, long estimatedMemoryKB) GetCacheStats()
        {
            long memory = 0;

            foreach (var line in _lineCache.Values)
            {
                memory += line.Length;
            }

            return (_lineCache.Count, memory / 1024);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Check if a virtual position is valid.
        /// </summary>
        public bool IsValidPosition(long virtualPosition)
        {
            if (!_fileProvider.IsOpen || virtualPosition < 0)
                return false;

            long virtualLength = _positionMapper.GetVirtualLength(_fileProvider.Length);
            return virtualPosition < virtualLength;
        }

        /// <summary>
        /// Get the total virtual length.
        /// </summary>
        public long GetVirtualLength()
        {
            if (!_fileProvider.IsOpen)
                return 0;

            return _positionMapper.GetVirtualLength(_fileProvider.Length);
        }

        #endregion
    }
}
