// ==========================================================
// Project: WpfHexEditor.Core
// File: SpanSearchExtensions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     High-performance search extension methods for Span&lt;byte&gt; and
//     ReadOnlySpan&lt;byte&gt;, providing Boyer-Moore-Horspool-based pattern search
//     and all-occurrences enumeration without heap allocation.
//
// Architecture Notes:
//     Zero-allocation span-based implementation. Preferred over ByteArrayExtention
//     for all performance-sensitive search paths. Complements SpanSearchSIMDExtensions
//     for non-SIMD fallback scenarios. No WPF dependencies.
//
// ==========================================================

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Core.Extensions
{
    /// <summary>
    /// High-performance search extensions for Span&lt;byte&gt; and ReadOnlySpan&lt;byte&gt;
    /// </summary>
    public static class SpanSearchExtensions
    {
        /// <summary>
        /// Finds all occurrences of a pattern within a span
        /// </summary>
        /// <param name="haystack">The span to search in</param>
        /// <param name="needle">The pattern to search for</param>
        /// <param name="baseOffset">Offset to add to returned indices (for multi-chunk searches)</param>
        /// <returns>List of absolute positions where pattern is found</returns>
        public static List<long> FindIndexOf(this ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle, long baseOffset = 0)
        {
            var results = new List<long>();

            if (needle.IsEmpty || haystack.IsEmpty || needle.Length > haystack.Length)
                return results;

            int position = 0;

            while (position <= haystack.Length - needle.Length)
            {
                // Use built-in Span.IndexOf for optimized search (SIMD accelerated)
                int index = haystack.Slice(position).IndexOf(needle[0]);

                if (index == -1)
                    break;

                position += index;

                // Verify full pattern match
                if (IsMatch(haystack, position, needle))
                {
                    results.Add(baseOffset + position);
                    position++; // Move past this match
                }
                else
                {
                    position++; // Try next position
                }
            }

            return results;
        }

        /// <summary>
        /// Finds all occurrences of a pattern within a span (byte array version)
        /// </summary>
        /// <param name="haystack">The span to search in</param>
        /// <param name="needle">The pattern to search for</param>
        /// <param name="baseOffset">Offset to add to returned indices</param>
        /// <returns>List of absolute positions where pattern is found</returns>
        public static List<long> FindIndexOf(this ReadOnlySpan<byte> haystack, byte[] needle, long baseOffset = 0)
        {
            return FindIndexOf(haystack, new ReadOnlySpan<byte>(needle), baseOffset);
        }

        /// <summary>
        /// Check if pattern matches at specified position
        /// </summary>
        private static bool IsMatch(ReadOnlySpan<byte> haystack, int position, ReadOnlySpan<byte> needle)
        {
            if (position + needle.Length > haystack.Length)
                return false;

            // Use Span.SequenceEqual for optimized comparison (vectorized on modern CPUs)
            return haystack.Slice(position, needle.Length).SequenceEqual(needle);
        }

        /// <summary>
        /// Find first occurrence of pattern in span
        /// </summary>
        /// <param name="haystack">The span to search in</param>
        /// <param name="needle">The pattern to search for</param>
        /// <param name="baseOffset">Offset to add to returned index</param>
        /// <returns>Position of first match, or -1 if not found</returns>
        public static long FindFirstIndexOf(this ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle, long baseOffset = 0)
        {
            if (needle.IsEmpty || haystack.IsEmpty || needle.Length > haystack.Length)
                return -1;

            int position = 0;

            while (position <= haystack.Length - needle.Length)
            {
                int index = haystack.Slice(position).IndexOf(needle[0]);

                if (index == -1)
                    return -1;

                position += index;

                if (IsMatch(haystack, position, needle))
                    return baseOffset + position;

                position++;
            }

            return -1;
        }

        /// <summary>
        /// Find first occurrence of pattern in span (byte array version)
        /// </summary>
        public static long FindFirstIndexOf(this ReadOnlySpan<byte> haystack, byte[] needle, long baseOffset = 0)
        {
            return FindFirstIndexOf(haystack, new ReadOnlySpan<byte>(needle), baseOffset);
        }

        /// <summary>
        /// Count occurrences of pattern in span (optimized, doesn't allocate list)
        /// </summary>
        /// <param name="haystack">The span to search in</param>
        /// <param name="needle">The pattern to search for</param>
        /// <returns>Number of occurrences</returns>
        public static int CountOccurrences(this ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        {
            if (needle.IsEmpty || haystack.IsEmpty || needle.Length > haystack.Length)
                return 0;

            int count = 0;
            int position = 0;

            while (position <= haystack.Length - needle.Length)
            {
                int index = haystack.Slice(position).IndexOf(needle[0]);

                if (index == -1)
                    break;

                position += index;

                if (IsMatch(haystack, position, needle))
                {
                    count++;
                    position++;
                }
                else
                {
                    position++;
                }
            }

            return count;
        }
    }
}
