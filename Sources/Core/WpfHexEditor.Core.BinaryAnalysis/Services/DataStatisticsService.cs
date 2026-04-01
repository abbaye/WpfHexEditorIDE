//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Linq;
using WpfHexEditor.Core.BinaryAnalysis.Models.Visualization;

namespace WpfHexEditor.Core.BinaryAnalysis.Services
{
    /// <summary>
    /// Service for calculating comprehensive data statistics
    /// Generates entropy, byte distribution, and data type estimates
    /// </summary>
    public class DataStatisticsService
    {
        /// <summary>
        /// Maximum sample size for analysis (default: 10MB)
        /// </summary>
        public int MaxSampleSize { get; set; } = 10 * 1024 * 1024;

        /// <summary>
        /// Calculate comprehensive file statistics
        /// </summary>
        /// <param name="data">Binary data to analyze</param>
        /// <returns>File statistics</returns>
        public FileStatistics CalculateStatistics(byte[] data)
        {
            if (data == null || data.Length == 0)
                return new FileStatistics { FileSize = 0 };

            var stopwatch = Stopwatch.StartNew();
            var stats = new FileStatistics
            {
                FileSize = data.Length
            };

            // Limit analysis to MaxSampleSize
            var analyzeLength = Math.Min(data.Length, MaxSampleSize);

            // Calculate byte frequency distribution
            for (int i = 0; i < analyzeLength; i++)
            {
                stats.ByteFrequency[data[i]]++;
            }

            // Find most/least common bytes
            long maxFreq = 0;
            long minFreq = long.MaxValue;
            int uniqueCount = 0;

            for (int i = 0; i < 256; i++)
            {
                if (stats.ByteFrequency[i] > 0)
                {
                    uniqueCount++;

                    if (stats.ByteFrequency[i] > maxFreq)
                    {
                        maxFreq = stats.ByteFrequency[i];
                        stats.MostCommonByte = (byte)i;
                    }

                    if (stats.ByteFrequency[i] < minFreq)
                    {
                        minFreq = stats.ByteFrequency[i];
                        stats.LeastCommonByte = (byte)i;
                    }
                }
            }

            stats.MostCommonByteCount = maxFreq;
            stats.UniqueBytesCount = uniqueCount;

            // Calculate percentages
            stats.NullBytePercentage = (stats.ByteFrequency[0] / (double)analyzeLength) * 100.0;

            // Calculate printable ASCII percentage (0x20-0x7E)
            long printableCount = 0;
            for (int i = 0x20; i <= 0x7E; i++)
            {
                printableCount += stats.ByteFrequency[i];
            }
            stats.PrintableAsciiPercentage = (printableCount / (double)analyzeLength) * 100.0;

            // Calculate Shannon entropy
            stats.Entropy = CalculateEntropy(stats.ByteFrequency, analyzeLength);

            // Estimate data type
            stats.EstimatedDataType = EstimateDataType(stats);

            stopwatch.Stop();
            stats.AnalysisDurationMs = stopwatch.ElapsedMilliseconds;

            return stats;
        }

        /// <summary>
        /// Calculate Shannon entropy from byte frequency distribution
        /// Algorithm from BarChartPanel.CalculateEntropy()
        /// </summary>
        private double CalculateEntropy(long[] frequency, long totalBytes)
        {
            if (totalBytes == 0)
                return 0;

            double entropy = 0;
            for (int i = 0; i < 256; i++)
            {
                if (frequency[i] > 0)
                {
                    double probability = frequency[i] / (double)totalBytes;
                    entropy -= probability * Math.Log(probability, 2);
                }
            }

            return entropy;
        }

        /// <summary>
        /// Estimate data type based on statistics
        /// </summary>
        private DataType EstimateDataType(FileStatistics stats)
        {
            // Very low entropy with high null bytes = Sparse
            if (stats.Entropy < 1.0 && stats.NullBytePercentage > 50)
                return DataType.Sparse;

            // High printable ASCII = Text
            if (stats.PrintableAsciiPercentage > 70)
                return DataType.Text;

            // Very high uniform entropy = Encrypted
            if (stats.Entropy > 7.9)
                return DataType.Encrypted;

            // High entropy = Compressed
            if (stats.Entropy > 7.0)
                return DataType.Compressed;

            // Medium entropy with low printable ASCII = Binary/Image/Executable
            if (stats.Entropy > 4.0 && stats.PrintableAsciiPercentage < 30)
                return DataType.Binary;

            return DataType.Unknown;
        }

        /// <summary>
        /// Generate byte distribution chart data
        /// </summary>
        public ChartData GenerateByteDistributionChart(FileStatistics stats)
        {
            var chart = new ChartData("Byte Distribution")
            {
                XAxisLabel = "Byte Value (0x00-0xFF)",
                YAxisLabel = "Frequency",
                ChartType = ChartType.Bar
            };

            for (int i = 0; i < 256; i++)
            {
                if (stats.ByteFrequency[i] > 0)
                {
                    chart.AddPoint(i, stats.ByteFrequency[i], $"0x{i:X2}");
                }
            }

            return chart;
        }

        /// <summary>
        /// Generate entropy chart data over file regions
        /// </summary>
        public ChartData GenerateEntropyChart(byte[] data, int windowSize = 1024)
        {
            var chart = new ChartData("Entropy Distribution")
            {
                XAxisLabel = "File Offset",
                YAxisLabel = "Entropy (0-8)",
                ChartType = ChartType.Line
            };

            if (data == null || data.Length == 0)
                return chart;

            // Calculate entropy for sliding windows
            var analyzeLength = Math.Min(data.Length, MaxSampleSize);
            var stepSize = windowSize / 2; // 50% overlap

            for (int offset = 0; offset < analyzeLength - windowSize; offset += stepSize)
            {
                var frequency = new long[256];
                for (int i = offset; i < offset + windowSize && i < analyzeLength; i++)
                {
                    frequency[data[i]]++;
                }

                var entropy = CalculateEntropy(frequency, windowSize);
                chart.AddPoint(offset, entropy, $"0x{offset:X}");
            }

            return chart;
        }

        /// <summary>
        /// Generate ASCII/Binary distribution chart
        /// </summary>
        public ChartData GenerateDataTypeDistributionChart(FileStatistics stats)
        {
            var chart = new ChartData("Data Type Distribution")
            {
                XAxisLabel = "Category",
                YAxisLabel = "Percentage",
                ChartType = ChartType.Bar
            };

            // Calculate categories
            long printable = 0;
            long control = 0;
            long extended = 0;
            long nullBytes = stats.ByteFrequency[0];

            for (int i = 0; i < 256; i++)
            {
                if (i >= 0x20 && i <= 0x7E) // Printable ASCII
                    printable += stats.ByteFrequency[i];
                else if (i < 0x20 && i != 0) // Control characters (excluding null)
                    control += stats.ByteFrequency[i];
                else if (i > 0x7E) // Extended ASCII
                    extended += stats.ByteFrequency[i];
            }

            double total = stats.FileSize;
            chart.AddPoint(0, (nullBytes / total) * 100, "Null (0x00)");
            chart.AddPoint(1, (control / total) * 100, "Control");
            chart.AddPoint(2, (printable / total) * 100, "Printable");
            chart.AddPoint(3, (extended / total) * 100, "Extended");

            return chart;
        }

        /// <summary>
        /// Get statistics summary as formatted text
        /// </summary>
        public string GetStatisticsSummary(FileStatistics stats)
        {
            var summary = $"FILE STATISTICS\n";
            summary += new string('=', 60) + "\n\n";

            summary += $"File Size: {stats.FileSize:N0} bytes ({FormatBytes(stats.FileSize)})\n";
            summary += $"Entropy: {stats.Entropy:F4} (0=uniform, 8=random)\n";
            summary += $"Estimated Type: {stats.EstimatedDataType}\n";
            summary += $"Analysis Time: {stats.AnalysisDurationMs} ms\n\n";

            summary += $"BYTE STATISTICS\n";
            summary += new string('-', 60) + "\n";
            summary += $"Unique Bytes: {stats.UniqueBytesCount} / 256 ({(stats.UniqueBytesCount / 256.0 * 100):F1}%)\n";
            summary += $"Most Common: 0x{stats.MostCommonByte:X2} ({stats.MostCommonByteCount:N0} occurrences, {stats.GetBytePercentage(stats.MostCommonByte):F2}%)\n";
            summary += $"Least Common: 0x{stats.LeastCommonByte:X2}\n";
            summary += $"Null Bytes: {stats.NullBytePercentage:F2}%\n";
            summary += $"Printable ASCII: {stats.PrintableAsciiPercentage:F2}%\n";

            return summary;
        }

        /// <summary>
        /// Format bytes in human-readable form
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }
    }
}
