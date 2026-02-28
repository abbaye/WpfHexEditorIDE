//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Linq;

namespace WpfHexEditor.Core.Cache
{
    /// <summary>
    /// Cache key for search operations.
    /// Combines pattern bytes, start position, and file length for unique identification.
    /// </summary>
    public struct SearchCacheKey : IEquatable<SearchCacheKey>
    {
        /// <summary>
        /// Hash of the search pattern (for fast comparison)
        /// </summary>
        public int PatternHash { get; }

        /// <summary>
        /// Start position of the search
        /// </summary>
        public long StartPosition { get; }

        /// <summary>
        /// Length of the file (to detect file modifications)
        /// </summary>
        public long FileLength { get; }

        /// <summary>
        /// Creates a new search cache key
        /// </summary>
        /// <param name="pattern">Search pattern bytes</param>
        /// <param name="startPosition">Start position</param>
        /// <param name="fileLength">File length</param>
        public SearchCacheKey(byte[] pattern, long startPosition, long fileLength)
        {
            if (pattern == null || pattern.Length == 0)
            {
                PatternHash = 0;
            }
            else
            {
                // Compute hash using polynomial rolling hash for fast comparison
                // This is more efficient than storing/comparing the entire pattern
                unchecked
                {
                    int hash = 17;
                    for (int i = 0; i < Math.Min(pattern.Length, 32); i++) // Limit to first 32 bytes
                    {
                        hash = hash * 31 + pattern[i];
                    }
                    PatternHash = hash;
                }
            }

            StartPosition = startPosition;
            FileLength = fileLength;
        }

        /// <summary>
        /// Checks equality with another SearchCacheKey
        /// </summary>
        public bool Equals(SearchCacheKey other)
        {
            return PatternHash == other.PatternHash &&
                   StartPosition == other.StartPosition &&
                   FileLength == other.FileLength;
        }

        /// <summary>
        /// Checks equality with an object
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is SearchCacheKey other && Equals(other);
        }

        /// <summary>
        /// Gets hash code for dictionary lookups
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + PatternHash;
                hash = hash * 31 + StartPosition.GetHashCode();
                hash = hash * 31 + FileLength.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        public static bool operator ==(SearchCacheKey left, SearchCacheKey right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        public static bool operator !=(SearchCacheKey left, SearchCacheKey right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// String representation for debugging
        /// </summary>
        public override string ToString()
        {
            return $"SearchKey(Hash={PatternHash:X8}, Start={StartPosition}, FileLen={FileLength})";
        }
    }
}
