//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service for comparing two hex files and finding differences
    /// </summary>
    /// <example>
    /// <code>
    /// var service = new ComparisonService();
    ///
    /// // Compare two Core.Bytes.ByteProviderLegacys
    /// var differences = service.Compare(provider1, provider2);
    ///
    /// // Or compare two editors
    /// var differences = service.CompareEditors(editor1, editor2);
    ///
    /// foreach (var diff in differences)
    /// {
    ///     Console.WriteLine($"Diff at {diff.Position}: {diff.OriginalByte} vs {diff.CompareByte}");
    /// }
    /// </code>
    /// </example>
    public class ComparisonService
    {
        /// <summary>
        /// Compare two Core.Bytes.ByteProviderLegacys and return all differences
        /// </summary>
        /// <param name="original">First Core.Bytes.ByteProviderLegacy (original)</param>
        /// <param name="compare">Second Core.Bytes.ByteProviderLegacy (to compare against)</param>
        /// <param name="maxDifferences">Maximum number of differences to return (0 = unlimited)</param>
        /// <returns>Enumerable of byte differences</returns>
        public IEnumerable<ByteDifference> Compare(Core.Bytes.ByteProviderLegacy original, Core.Bytes.ByteProviderLegacy compare, long maxDifferences = 0)
        {
            if (original == null || compare == null || !original.IsOpen || !compare.IsOpen)
                yield break;

            long minLength = System.Math.Min(original.Length, compare.Length);
            long differencesFound = 0;

            // Compare bytes up to the minimum length
            for (long position = 0; position < minLength; position++)
            {
                var (originalByte, originalSuccess) = original.GetByte(position);
                var (compareByte, compareSuccess) = compare.GetByte(position);

                if (!originalSuccess || !compareByte.HasValue)
                    continue;

                if (originalByte != compareByte)
                {
                    yield return new ByteDifference(
                        originalByte ?? 0,
                        compareByte.Value,
                        position);

                    differencesFound++;
                    if (maxDifferences > 0 && differencesFound >= maxDifferences)
                        yield break;
                }
            }

            // Report length difference as virtual differences (if files have different lengths)
            if (original.Length != compare.Length)
            {
                long maxLength = System.Math.Max(original.Length, compare.Length);
                bool originalIsLonger = original.Length > compare.Length;

                for (long position = minLength; position < maxLength; position++)
                {
                    byte origine = originalIsLonger ? (original.GetByte(position).singleByte ?? 0) : (byte)0;
                    byte destination = originalIsLonger ? (byte)0 : (compare.GetByte(position).singleByte ?? 0);

                    yield return new ByteDifference(origine, destination, position);

                    differencesFound++;
                    if (maxDifferences > 0 && differencesFound >= maxDifferences)
                        yield break;
                }
            }
        }

        /// <summary>
        /// Compare two regions of Core.Bytes.ByteProviderLegacys
        /// </summary>
        /// <param name="original">First Core.Bytes.ByteProviderLegacy</param>
        /// <param name="compare">Second Core.Bytes.ByteProviderLegacy</param>
        /// <param name="startPosition">Start position for comparison</param>
        /// <param name="length">Length of region to compare</param>
        /// <param name="maxDifferences">Maximum differences to return</param>
        /// <returns>Enumerable of byte differences</returns>
        public IEnumerable<ByteDifference> CompareRegion(Core.Bytes.ByteProviderLegacy original, Core.Bytes.ByteProviderLegacy compare,
            long startPosition, long length, long maxDifferences = 0)
        {
            if (original == null || compare == null || !original.IsOpen || !compare.IsOpen)
                yield break;

            long differencesFound = 0;
            long endPosition = startPosition + length;

            for (long position = startPosition; position < endPosition; position++)
            {
                if (position >= original.Length || position >= compare.Length)
                    break;

                var (originalByte, originalSuccess) = original.GetByte(position);
                var (compareByte, compareSuccess) = compare.GetByte(position);

                if (!originalSuccess || !compareByte.HasValue)
                    continue;

                if (originalByte != compareByte)
                {
                    yield return new ByteDifference(
                        originalByte ?? 0,
                        compareByte.Value,
                        position);

                    differencesFound++;
                    if (maxDifferences > 0 && differencesFound >= maxDifferences)
                        yield break;
                }
            }
        }

        /// <summary>
        /// Count total number of differences between two Core.Bytes.ByteProviderLegacys (faster than enumerating all)
        /// </summary>
        public long CountDifferences(Core.Bytes.ByteProviderLegacy original, Core.Bytes.ByteProviderLegacy compare)
        {
            if (original == null || compare == null || !original.IsOpen || !compare.IsOpen)
                return 0;

            long differences = 0;
            long minLength = System.Math.Min(original.Length, compare.Length);

            for (long position = 0; position < minLength; position++)
            {
                var (originalByte, originalSuccess) = original.GetByte(position);
                var (compareByte, compareSuccess) = compare.GetByte(position);

                if (originalSuccess && compareSuccess && originalByte != compareByte)
                    differences++;
            }

            // Add length difference
            if (original.Length != compare.Length)
            {
                differences += System.Math.Abs(original.Length - compare.Length);
            }

            return differences;
        }

        /// <summary>
        /// Calculate similarity percentage between two Core.Bytes.ByteProviderLegacys (0.0 - 100.0)
        /// </summary>
        public double CalculateSimilarity(Core.Bytes.ByteProviderLegacy original, Core.Bytes.ByteProviderLegacy compare)
        {
            if (original == null || compare == null || !original.IsOpen || !compare.IsOpen)
                return 0.0;

            long maxLength = System.Math.Max(original.Length, compare.Length);
            if (maxLength == 0)
                return 100.0;

            long differences = CountDifferences(original, compare);
            long matches = maxLength - differences;

            return (matches / (double)maxLength) * 100.0;
        }
    }
}
