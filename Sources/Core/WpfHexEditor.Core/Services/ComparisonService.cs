//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service for comparing two hex files and finding differences
    /// </summary>
    /// <example>
    /// <code>
    /// var service = new ComparisonService();
    ///
    /// // Compare two ByteProviders
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
        #region ByteProvider V2 Methods

        /// <summary>
        /// Compare two ByteProvider V2 instances and return all differences
        /// </summary>
        /// <param name="original">First ByteProvider (original)</param>
        /// <param name="compare">Second ByteProvider (to compare against)</param>
        /// <param name="maxDifferences">Maximum number of differences to return (0 = unlimited)</param>
        /// <returns>Enumerable of byte differences</returns>
        public IEnumerable<ByteDifference> Compare(ByteProvider original, ByteProvider compare, long maxDifferences = 0)
        {
            if (original == null || compare == null || !original.IsOpen || !compare.IsOpen)
                yield break;

            long minLength = System.Math.Min(original.VirtualLength, compare.VirtualLength);
            long differencesFound = 0;

            // Compare bytes up to the minimum length
            for (long position = 0; position < minLength; position++)
            {
                var (originalByte, originalSuccess) = original.GetByte(position);
                var (compareByte, compareSuccess) = compare.GetByte(position);

                if (!originalSuccess || !compareSuccess)
                    continue;

                if (originalByte != compareByte)
                {
                    yield return new ByteDifference(
                        originalByte,
                        compareByte,
                        position);

                    differencesFound++;
                    if (maxDifferences > 0 && differencesFound >= maxDifferences)
                        yield break;
                }
            }

            // Report length difference as virtual differences (if files have different lengths)
            if (original.VirtualLength != compare.VirtualLength)
            {
                long maxLength = System.Math.Max(original.VirtualLength, compare.VirtualLength);
                bool originalIsLonger = original.VirtualLength > compare.VirtualLength;

                for (long position = minLength; position < maxLength; position++)
                {
                    byte origine = originalIsLonger ? original.GetByte(position).value : (byte)0;
                    byte destination = originalIsLonger ? (byte)0 : compare.GetByte(position).value;

                    yield return new ByteDifference(origine, destination, position);

                    differencesFound++;
                    if (maxDifferences > 0 && differencesFound >= maxDifferences)
                        yield break;
                }
            }
        }

        /// <summary>
        /// Count total number of differences between two ByteProvider V2 instances (faster than enumerating all)
        /// </summary>
        public long CountDifferences(ByteProvider original, ByteProvider compare)
        {
            if (original == null || compare == null || !original.IsOpen || !compare.IsOpen)
                return 0;

            long differences = 0;
            long minLength = System.Math.Min(original.VirtualLength, compare.VirtualLength);

            for (long position = 0; position < minLength; position++)
            {
                var (originalByte, originalSuccess) = original.GetByte(position);
                var (compareByte, compareSuccess) = compare.GetByte(position);

                if (originalSuccess && compareSuccess && originalByte != compareByte)
                    differences++;
            }

            // Add length difference
            if (original.VirtualLength != compare.VirtualLength)
            {
                differences += System.Math.Abs(original.VirtualLength - compare.VirtualLength);
            }

            return differences;
        }

        /// <summary>
        /// Calculate similarity percentage between two ByteProvider V2 instances (0.0 - 100.0)
        /// </summary>
        public double CalculateSimilarity(ByteProvider original, ByteProvider compare)
        {
            if (original == null || compare == null || !original.IsOpen || !compare.IsOpen)
                return 0.0;

            long maxLength = System.Math.Max(original.VirtualLength, compare.VirtualLength);
            if (maxLength == 0)
                return 100.0;

            long differences = CountDifferences(original, compare);
            long matches = maxLength - differences;

            return (matches / (double)maxLength) * 100.0;
        }

        /// <summary>
        /// Count differences using SIMD optimization (16-32x faster for large files).
        /// Falls back to regular CountDifferences on .NET Framework without SIMD support.
        /// </summary>
        public long CountDifferencesSIMD(ByteProvider original, ByteProvider compare)
        {
            #if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            // Use SIMD on modern .NET
            return ComparisonServiceSIMD.CountDifferencesSIMD(original, compare);
            #else
            // Fallback to scalar on .NET Framework (SIMD requires System.Numerics.Vectors NuGet)
            return CountDifferences(original, compare);
            #endif
        }

        /// <summary>
        /// Calculate similarity using SIMD optimization (16-32x faster for large files).
        /// </summary>
        public double CalculateSimilaritySIMD(ByteProvider original, ByteProvider compare)
        {
            #if NET5_0_OR_GREATER || NETCOREAPP3_0_OR_GREATER
            return ComparisonServiceSIMD.CalculateSimilaritySIMD(original, compare);
            #else
            return CalculateSimilarity(original, compare);
            #endif
        }

        /// <summary>
        /// Count differences using parallel processing (2-4x faster for files > 100MB).
        /// Automatically chooses scalar or parallel based on file size.
        /// </summary>
        public long CountDifferencesParallel(ByteProvider original, ByteProvider compare)
        {
            return ComparisonServiceParallel.CountDifferencesParallel(original, compare);
        }

        /// <summary>
        /// Calculate similarity using parallel processing (2-4x faster for files > 100MB).
        /// </summary>
        public double CalculateSimilarityParallel(ByteProvider original, ByteProvider compare)
        {
            return ComparisonServiceParallel.CalculateSimilarityParallel(original, compare);
        }

        #endregion
    }
}
