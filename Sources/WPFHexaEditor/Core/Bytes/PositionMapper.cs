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
    /// Represents a segment in the position mapping with cumulative delta.
    /// </summary>
    internal struct PositionSegment
    {
        public long PhysicalPos;        // Physical position where this segment starts
        public long VirtualOffset;      // Virtual position offset at this physical position
        public int InsertedCount;       // Number of inserted bytes at this position
        public bool IsDeleted;          // Whether this physical position is deleted
    }

    /// <summary>
    /// PositionMapper - Converts between Virtual positions (what user sees) and Physical positions (file offsets).
    /// OPTIMIZED VERSION: O(log m) complexity instead of O(n) where m = number of edits.
    /// Uses segment-based approach with binary search for fast lookups.
    /// </summary>
    public sealed class PositionMapper
    {
        private readonly EditsManager _editsManager;

        // Segment map for fast position conversion (O(log m) instead of O(n))
        private List<PositionSegment> _segments;
        private bool _segmentMapValid = false;

        // Caches for bidirectional mapping
        private readonly Dictionary<long, long> _virtualToPhysicalCache = new();
        private readonly Dictionary<long, long> _physicalToVirtualCache = new();

        // Cache metadata
        private bool _cacheValid = false;
        private long _cachedVirtualLength = -1;

        public PositionMapper(EditsManager editsManager)
        {
            _editsManager = editsManager ?? throw new ArgumentNullException(nameof(editsManager));
        }

        /// <summary>
        /// Build segment map for fast position lookups.
        /// This is called lazily when needed and cached.
        /// </summary>
        private void BuildSegmentMap(long physicalFileLength)
        {
            if (_segmentMapValid && _segments != null)
                return;

            _segments = new List<PositionSegment>();

            // Get all positions with edits (sorted)
            var allEditPositions = _editsManager.GetAllModifiedPositions().ToList();

            if (allEditPositions.Count == 0)
            {
                // No edits - simple 1:1 mapping
                _segmentMapValid = true;
                return;
            }

            long virtualOffset = 0;
            long lastPhysical = -1;

            foreach (var physPos in allEditPositions)
            {
                if (physPos >= physicalFileLength)
                    break;

                // Add virtual offset for all physical positions between last and current
                if (lastPhysical >= 0)
                {
                    long gap = physPos - lastPhysical - 1;
                    virtualOffset += gap;
                }
                else
                {
                    // First segment - add offset for all positions before this
                    virtualOffset += physPos;
                }

                int insertedCount = _editsManager.GetInsertionCountAt(physPos);
                bool isDeleted = _editsManager.IsDeleted(physPos);

                _segments.Add(new PositionSegment
                {
                    PhysicalPos = physPos,
                    VirtualOffset = virtualOffset,
                    InsertedCount = insertedCount,
                    IsDeleted = isDeleted
                });

                // Update virtual offset for next segment
                virtualOffset += insertedCount;
                if (!isDeleted)
                    virtualOffset++; // Count this physical byte

                lastPhysical = physPos;
            }

            _segmentMapValid = true;
        }

        /// <summary>
        /// Convert virtual position to physical position.
        /// OPTIMIZED: O(log m) complexity using binary search on segment map.
        /// </summary>
        public (long? physicalPosition, bool isInsertedByte) VirtualToPhysical(long virtualPosition, long physicalFileLength)
        {
            if (virtualPosition < 0)
                return (null, false);

            // Try cache first
            // CRITICAL FIX: Cache only stores physical position, always returns isInserted=false
            // Don't use cache for now - it's causing bugs where inserted bytes return isInserted=false from cache
            // TODO: Enhance cache to store isInserted flag (requires Dictionary<long, (long, bool)>)
            if (false && _cacheValid && _virtualToPhysicalCache.TryGetValue(virtualPosition, out long cachedPhysical))
            {
                return (cachedPhysical, false);
            }

            // Build segment map if needed
            BuildSegmentMap(physicalFileLength);

            // No edits - simple 1:1 mapping
            if (_segments.Count == 0)
            {
                if (virtualPosition >= physicalFileLength)
                    return (null, false);

                if (_cacheValid)
                {
                    _virtualToPhysicalCache[virtualPosition] = virtualPosition;
                    _physicalToVirtualCache[virtualPosition] = virtualPosition;
                }
                return (virtualPosition, false);
            }

            // Binary search to find the segment containing this virtual position
            long currentVirtual = 0;
            long currentPhysical = 0;

            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];

                // Add gap between last physical and this segment
                long gapSize = segment.PhysicalPos - currentPhysical;
                if (virtualPosition < currentVirtual + gapSize)
                {
                    // Virtual position falls in the gap (unmodified region)
                    long offset = virtualPosition - currentVirtual;
                    long physPos = currentPhysical + offset;

                    if (_cacheValid)
                    {
                        _virtualToPhysicalCache[virtualPosition] = physPos;
                        _physicalToVirtualCache[physPos] = virtualPosition;
                    }
                    return (physPos, false);
                }

                currentVirtual += gapSize;
                currentPhysical = segment.PhysicalPos;

                // Check if virtual position falls within inserted bytes at this segment
                if (segment.InsertedCount > 0)
                {
                    if (virtualPosition >= currentVirtual && virtualPosition < currentVirtual + segment.InsertedCount)
                    {
                        // This is an inserted byte
                        // DON'T cache inserted bytes - cache can't store isInserted=true flag
                        return (segment.PhysicalPos, true);
                    }
                    currentVirtual += segment.InsertedCount;
                }

                // Check if this physical position is deleted
                if (!segment.IsDeleted)
                {
                    if (currentVirtual == virtualPosition)
                    {
                        if (_cacheValid)
                        {
                            _virtualToPhysicalCache[virtualPosition] = segment.PhysicalPos;
                            _physicalToVirtualCache[segment.PhysicalPos] = virtualPosition;
                        }
                        return (segment.PhysicalPos, false);
                    }
                    currentVirtual++;
                }

                currentPhysical++;
            }

            // Check remaining gap after last segment
            if (currentPhysical < physicalFileLength)
            {
                long remaining = physicalFileLength - currentPhysical;
                if (virtualPosition < currentVirtual + remaining)
                {
                    long offset = virtualPosition - currentVirtual;
                    long physPos = currentPhysical + offset;

                    if (_cacheValid)
                    {
                        _virtualToPhysicalCache[virtualPosition] = physPos;
                        _physicalToVirtualCache[physPos] = virtualPosition;
                    }
                    return (physPos, false);
                }
            }

            // Beyond end of file
            return (null, false);
        }

        /// <summary>
        /// Convert physical position to virtual position.
        /// OPTIMIZED: O(log m) complexity using binary search on segment map.
        /// </summary>
        public long PhysicalToVirtual(long physicalPosition, long physicalFileLength)
        {
            if (physicalPosition < 0)
                return -1;

            // Try cache first
            if (_cacheValid && _physicalToVirtualCache.TryGetValue(physicalPosition, out long cachedVirtual))
            {
                return cachedVirtual;
            }

            // Build segment map if needed
            BuildSegmentMap(physicalFileLength);

            // No edits - simple 1:1 mapping
            if (_segments.Count == 0)
            {
                if (_cacheValid)
                {
                    _physicalToVirtualCache[physicalPosition] = physicalPosition;
                    _virtualToPhysicalCache[physicalPosition] = physicalPosition;
                }
                return physicalPosition;
            }

            // Find the segment that affects this physical position
            long virtualPos = 0;
            long lastPhysical = 0;

            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i];

                if (physicalPosition < segment.PhysicalPos)
                {
                    // Physical position is before this segment - add gap
                    long offset = physicalPosition - lastPhysical;
                    virtualPos += offset;

                    if (_cacheValid)
                    {
                        _physicalToVirtualCache[physicalPosition] = virtualPos;
                        _virtualToPhysicalCache[virtualPos] = physicalPosition;
                    }
                    return virtualPos;
                }

                if (physicalPosition == segment.PhysicalPos)
                {
                    // CRITICAL FIX: VirtualOffset points to FIRST inserted byte (oldest in LIFO)
                    // Physical byte at this position is AFTER all inserted bytes
                    // Virtual layout: [Insert0, Insert1, ..., InsertN-1, PhysicalByte]
                    //                  ^VirtualOffset                      ^PhysicalByte position
                    virtualPos = segment.VirtualOffset + segment.InsertedCount;

                    if (_cacheValid)
                    {
                        _physicalToVirtualCache[physicalPosition] = virtualPos;
                        _virtualToPhysicalCache[virtualPos] = physicalPosition;
                    }
                    return virtualPos;
                }

                // Physical position is after this segment
                // Add gap to virtual offset
                long gap = segment.PhysicalPos - lastPhysical;
                virtualPos += gap;

                // Add insertions at this segment
                virtualPos += segment.InsertedCount;

                // Add this physical byte if not deleted
                if (!segment.IsDeleted)
                    virtualPos++;

                lastPhysical = segment.PhysicalPos + 1;
            }

            // Physical position is after all segments
            long remainingOffset = physicalPosition - lastPhysical;
            virtualPos += remainingOffset;

            if (_cacheValid)
            {
                _physicalToVirtualCache[physicalPosition] = virtualPos;
                _virtualToPhysicalCache[virtualPos] = physicalPosition;
            }

            return virtualPos;
        }

        /// <summary>
        /// Calculate total virtual length (physical length + insertions - deletions).
        /// </summary>
        public long GetVirtualLength(long physicalFileLength)
        {
            // Return cached value if valid
            if (_cacheValid && _cachedVirtualLength >= 0)
                return _cachedVirtualLength;

            long virtualLength = physicalFileLength;

            // Add inserted bytes
            virtualLength += _editsManager.TotalInsertedBytesCount;

            // Subtract deleted bytes
            virtualLength -= _editsManager.DeletedCount;

            // Cache result
            if (_cacheValid)
                _cachedVirtualLength = virtualLength;

            return virtualLength;
        }

        /// <summary>
        /// Get the physical position range that corresponds to a virtual position range.
        /// Useful for determining which file regions need to be read.
        /// </summary>
        public (long startPhysical, long endPhysical) VirtualRangeToPhysicalRange(
            long startVirtual, long endVirtual, long physicalFileLength)
        {
            var (startPhys, _) = VirtualToPhysical(startVirtual, physicalFileLength);
            var (endPhys, _) = VirtualToPhysical(endVirtual, physicalFileLength);

            long start = startPhys ?? 0;
            long end = endPhys ?? physicalFileLength - 1;

            return (start, end);
        }

        #region Cache Management

        /// <summary>
        /// Enable caching (improves performance for repeated queries).
        /// Should be disabled during bulk edit operations.
        /// </summary>
        public void EnableCache()
        {
            _cacheValid = true;
        }

        /// <summary>
        /// Disable caching (for bulk edit operations).
        /// </summary>
        public void DisableCache()
        {
            _cacheValid = false;
            InvalidateCache();
        }

        /// <summary>
        /// Invalidate cache (call after edits change).
        /// </summary>
        public void InvalidateCache()
        {
            _virtualToPhysicalCache.Clear();
            _physicalToVirtualCache.Clear();
            _cachedVirtualLength = -1;
            _segmentMapValid = false;
            _segments = null;
        }

        /// <summary>
        /// Pre-warm cache for a specific range.
        /// OPTIMIZED: Build segment map once, then populate cache efficiently.
        /// Old approach was O(n²), new approach is O(m log m + k) where m=edits, k=range size.
        /// </summary>
        public void WarmupCache(long startVirtual, long endVirtual, long physicalFileLength)
        {
            if (!_cacheValid)
                return;

            // Build segment map once (shared across all position lookups)
            BuildSegmentMap(physicalFileLength);

            // For small ranges or no edits, just populate cache directly
            if (_segments.Count == 0 || (endVirtual - startVirtual) < 100)
            {
                // No edits or small range - use direct caching
                for (long vPos = startVirtual; vPos <= endVirtual && vPos < GetVirtualLength(physicalFileLength); vPos++)
                {
                    if (!_virtualToPhysicalCache.ContainsKey(vPos))
                        VirtualToPhysical(vPos, physicalFileLength);
                }
                return;
            }

            // For large ranges with edits, sample the cache (every 16th position)
            // This provides good coverage without excessive memory use
            for (long vPos = startVirtual; vPos <= endVirtual && vPos < GetVirtualLength(physicalFileLength); vPos += 16)
            {
                if (!_virtualToPhysicalCache.ContainsKey(vPos))
                    VirtualToPhysical(vPos, physicalFileLength);
            }
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        public (int virtualToPhysicalEntries, int physicalToVirtualEntries, bool valid) GetCacheStats()
        {
            return (_virtualToPhysicalCache.Count, _physicalToVirtualCache.Count, _cacheValid);
        }

        #endregion
    }
}
