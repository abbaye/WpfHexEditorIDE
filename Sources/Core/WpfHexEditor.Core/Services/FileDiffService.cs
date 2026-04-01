//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// File Diff Service - Enhanced comparison with diff algorithms
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com), Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.Models.Comparison;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service for comparing two files and identifying differences
    /// Provides detailed diff information with multiple algorithms
    /// </summary>
    public class FileDiffService
    {
        #region Constants

        private const int DefaultChunkSize = 4096; // 4KB chunks for efficient comparison

        #endregion

        #region Public Methods

        /// <summary>
        /// Compare two byte providers and return list of differences
        /// </summary>
        public List<FileDifference> CompareFiles(ByteProvider provider1, ByteProvider provider2,
            IProgress<int> progress = null)
        {
            if (provider1 == null || provider2 == null)
                return new List<FileDifference>();

            var differences = new List<FileDifference>();
            var length1 = provider1.Length;
            var length2 = provider2.Length;
            var commonLength = Math.Min(length1, length2);

            // Compare common portion
            long offset = 0;
            while (offset < commonLength)
            {
                var chunkSize = (int)Math.Min(DefaultChunkSize, commonLength - offset);
                var bytes1 = provider1.GetBytes(offset, chunkSize);
                var bytes2 = provider2.GetBytes(offset, chunkSize);

                // Find differences in this chunk
                for (int i = 0; i < chunkSize; i++)
                {
                    if (bytes1[i] != bytes2[i])
                    {
                        // Found a difference, find its extent
                        var diffStart = offset + i;
                        var diffLength = 1;

                        // Extend diff while bytes are different
                        while (i + diffLength < chunkSize &&
                               bytes1[i + diffLength] != bytes2[i + diffLength])
                        {
                            diffLength++;
                        }

                        var diffBytes1 = new byte[diffLength];
                        var diffBytes2 = new byte[diffLength];
                        Array.Copy(bytes1, i, diffBytes1, 0, diffLength);
                        Array.Copy(bytes2, i, diffBytes2, 0, diffLength);

                        differences.Add(new FileDifference
                        {
                            Offset = diffStart,
                            Length = diffLength,
                            Type = DifferenceType.Modified,
                            BytesFile1 = diffBytes1,
                            BytesFile2 = diffBytes2,
                            Description = $"Modified at offset 0x{diffStart:X}"
                        });

                        i += diffLength - 1; // Skip already processed bytes
                    }
                }

                offset += chunkSize;

                // Report progress
                progress?.Report((int)((offset * 100) / commonLength));
            }

            // Handle length differences
            if (length1 > length2)
            {
                // File 1 has extra bytes
                differences.Add(new FileDifference
                {
                    Offset = length2,
                    Length = (int)(length1 - length2),
                    Type = DifferenceType.DeletedInSecond,
                    BytesFile1 = provider1.GetBytes(length2, (int)(length1 - length2)),
                    BytesFile2 = new byte[0],
                    Description = $"Extra bytes in File 1 from offset 0x{length2:X}"
                });
            }
            else if (length2 > length1)
            {
                // File 2 has extra bytes
                differences.Add(new FileDifference
                {
                    Offset = length1,
                    Length = (int)(length2 - length1),
                    Type = DifferenceType.AddedInSecond,
                    BytesFile1 = new byte[0],
                    BytesFile2 = provider2.GetBytes(length1, (int)(length2 - length1)),
                    Description = $"Extra bytes in File 2 from offset 0x{length1:X}"
                });
            }

            return differences;
        }

        /// <summary>
        /// Get diff statistics
        /// </summary>
        public DiffStatistics GetStatistics(List<FileDifference> differences)
        {
            return new DiffStatistics
            {
                TotalDifferences = differences.Count,
                ModifiedCount = differences.Count(d => d.Type == DifferenceType.Modified),
                DeletedCount = differences.Count(d => d.Type == DifferenceType.DeletedInSecond),
                AddedCount = differences.Count(d => d.Type == DifferenceType.AddedInSecond),
                TotalModifiedBytes = differences.Where(d => d.Type == DifferenceType.Modified)
                                               .Sum(d => d.Length),
                TotalDeletedBytes = differences.Where(d => d.Type == DifferenceType.DeletedInSecond)
                                              .Sum(d => d.Length),
                TotalAddedBytes = differences.Where(d => d.Type == DifferenceType.AddedInSecond)
                                            .Sum(d => d.Length)
            };
        }

        /// <summary>
        /// Export diff report to text
        /// </summary>
        public string ExportDiffReport(List<FileDifference> differences, DiffStatistics stats)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== File Comparison Report ===");
            report.AppendLine();
            report.AppendLine($"Total Differences: {stats.TotalDifferences}");
            report.AppendLine($"Modified: {stats.ModifiedCount} ({stats.TotalModifiedBytes} bytes)");
            report.AppendLine($"Deleted: {stats.DeletedCount} ({stats.TotalDeletedBytes} bytes)");
            report.AppendLine($"Added: {stats.AddedCount} ({stats.TotalAddedBytes} bytes)");
            report.AppendLine();
            report.AppendLine("=== Differences Detail ===");
            report.AppendLine();

            foreach (var diff in differences.Take(100)) // Limit to first 100 for report
            {
                report.AppendLine($"Offset: 0x{diff.Offset:X8} | Type: {diff.Type} | Length: {diff.Length}");
                report.AppendLine($"  File 1: {BitConverter.ToString(diff.BytesFile1).Replace("-", " ")}");
                report.AppendLine($"  File 2: {BitConverter.ToString(diff.BytesFile2).Replace("-", " ")}");
                report.AppendLine();
            }

            if (differences.Count > 100)
            {
                report.AppendLine($"... and {differences.Count - 100} more differences");
            }

            return report.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Statistics about file differences
    /// </summary>
    public class DiffStatistics
    {
        public int TotalDifferences { get; set; }
        public int ModifiedCount { get; set; }
        public int DeletedCount { get; set; }
        public int AddedCount { get; set; }
        public long TotalModifiedBytes { get; set; }
        public long TotalDeletedBytes { get; set; }
        public long TotalAddedBytes { get; set; }
    }
}
