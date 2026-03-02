//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Core
{
    /// <summary>
    /// Cache for formatted field values to avoid redundant formatting operations
    /// Uses a composite key: (offset, length, valueType, formatterType, valueHash)
    /// </summary>
    public class FormattedValueCache
    {
        private readonly Dictionary<CacheKey, string> _cache = new Dictionary<CacheKey, string>();
        private readonly int _maxCacheSize;
        private int _hits;
        private int _misses;

        /// <summary>
        /// Initialize cache with maximum size limit
        /// </summary>
        /// <param name="maxCacheSize">Maximum number of cached values (default: 10000)</param>
        public FormattedValueCache(int maxCacheSize = 10000)
        {
            _maxCacheSize = maxCacheSize;
        }

        /// <summary>
        /// Try to get cached formatted value
        /// </summary>
        public bool TryGet(long offset, int length, string valueType, string formatterType, object rawValue, out string formattedValue)
        {
            var key = new CacheKey(offset, length, valueType, formatterType, rawValue?.GetHashCode() ?? 0);

            if (_cache.TryGetValue(key, out formattedValue))
            {
                _hits++;
                return true;
            }

            _misses++;
            formattedValue = null;
            return false;
        }

        /// <summary>
        /// Store formatted value in cache
        /// </summary>
        public void Set(long offset, int length, string valueType, string formatterType, object rawValue, string formattedValue)
        {
            // Enforce cache size limit
            if (_cache.Count >= _maxCacheSize)
            {
                // Simple eviction: clear 25% of cache when full
                var itemsToRemove = _maxCacheSize / 4;
                var keysToRemove = new List<CacheKey>(itemsToRemove);

                int removed = 0;
                foreach (var key in _cache.Keys)
                {
                    keysToRemove.Add(key);
                    if (++removed >= itemsToRemove)
                        break;
                }

                foreach (var key in keysToRemove)
                {
                    _cache.Remove(key);
                }
            }

            var cacheKey = new CacheKey(offset, length, valueType, formatterType, rawValue?.GetHashCode() ?? 0);
            _cache[cacheKey] = formattedValue;
        }

        /// <summary>
        /// Clear all cached values
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Invalidate cache entries for a specific formatter type
        /// </summary>
        public void InvalidateFormatter(string formatterType)
        {
            var keysToRemove = new List<CacheKey>();

            foreach (var key in _cache.Keys)
            {
                if (key.FormatterType == formatterType)
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public (int CachedItems, int Hits, int Misses, double HitRate) GetStatistics()
        {
            int total = _hits + _misses;
            double hitRate = total > 0 ? (_hits / (double)total) * 100.0 : 0.0;

            return (_cache.Count, _hits, _misses, hitRate);
        }

        /// <summary>
        /// Reset statistics counters
        /// </summary>
        public void ResetStatistics()
        {
            _hits = 0;
            _misses = 0;
        }

        /// <summary>
        /// Composite cache key
        /// </summary>
        private struct CacheKey : IEquatable<CacheKey>
        {
            public long Offset { get; }
            public int Length { get; }
            public string ValueType { get; }
            public string FormatterType { get; }
            public int ValueHash { get; }

            public CacheKey(long offset, int length, string valueType, string formatterType, int valueHash)
            {
                Offset = offset;
                Length = length;
                ValueType = valueType;
                FormatterType = formatterType;
                ValueHash = valueHash;
            }

            public bool Equals(CacheKey other)
            {
                return Offset == other.Offset &&
                       Length == other.Length &&
                       ValueType == other.ValueType &&
                       FormatterType == other.FormatterType &&
                       ValueHash == other.ValueHash;
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + Offset.GetHashCode();
                    hash = hash * 31 + Length.GetHashCode();
                    hash = hash * 31 + (ValueType?.GetHashCode() ?? 0);
                    hash = hash * 31 + (FormatterType?.GetHashCode() ?? 0);
                    hash = hash * 31 + ValueHash;
                    return hash;
                }
            }
        }
    }
}
