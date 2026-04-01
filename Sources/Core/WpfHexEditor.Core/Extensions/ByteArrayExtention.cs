// ==========================================================
// Project: WpfHexEditor.Core
// File: ByteArrayExtention.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Extension methods for byte arrays providing pattern search functionality,
//     including finding the index of a byte subsequence within a byte array
//     for search and comparison operations.
//
// Architecture Notes:
//     Pure static extension class — no state, no WPF dependencies. Predates
//     SpanSearchExtensions; the Span-based implementations should be preferred
//     for new performance-sensitive code paths.
//
// ==========================================================

using System.Collections.Generic;
using System.Linq;

namespace WpfHexEditor.Core.Extensions
{
    /// <summary>
    /// Extention methodes for find match in byte[]
    /// </summary>
    public static class ByteArrayExtention
    {
        /// <summary>
        /// Finds all index of byte find
        /// </summary>
        public static IEnumerable<long> FindIndexOf(this byte[] self, byte[] candidate)
        {
            if (!IsEmptyLocate(self, candidate))
                for (var i = 0; i < self.Length; i++)
                {
                    if (!IsMatch(self, i, candidate))
                        continue;

                    yield return i;
                }
        }

        /// <summary>
        /// Check if match is finded
        /// </summary>
        private static bool IsMatch(byte[] array,
                                    long position,
                                    byte[] candidate) =>
            candidate.Length <= array.Length - position && !candidate.Where((t, i) => array[position + i] != t).Any();

        /// <summary>
        /// Check if can find
        /// </summary>
        private static bool IsEmptyLocate(byte[] array, byte[] candidate) => array == null
                                                                             || candidate == null
                                                                             || array.Length == 0
                                                                             || candidate.Length == 0
                                                                             || candidate.Length > array.Length;
    }
}
