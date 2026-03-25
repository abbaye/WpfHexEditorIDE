//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Core.BinaryAnalysis.Models.Visualization
{
    /// <summary>
    /// Comprehensive file statistics for visualization
    /// </summary>
    [Serializable]
    public class FileStatistics
    {
        /// <summary>
        /// Total file size in bytes
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Shannon entropy (0.0 to 8.0)
        /// </summary>
        public double Entropy { get; set; }

        /// <summary>
        /// Byte frequency distribution (256 elements)
        /// </summary>
        public long[] ByteFrequency { get; set; }

        /// <summary>
        /// Most common byte value
        /// </summary>
        public byte MostCommonByte { get; set; }

        /// <summary>
        /// Frequency of most common byte
        /// </summary>
        public long MostCommonByteCount { get; set; }

        /// <summary>
        /// Least common byte value (excluding zero-frequency bytes)
        /// </summary>
        public byte LeastCommonByte { get; set; }

        /// <summary>
        /// Number of unique byte values present
        /// </summary>
        public int UniqueBytesCount { get; set; }

        /// <summary>
        /// Percentage of null bytes (0x00)
        /// </summary>
        public double NullBytePercentage { get; set; }

        /// <summary>
        /// Percentage of printable ASCII characters (0x20-0x7E)
        /// </summary>
        public double PrintableAsciiPercentage { get; set; }

        /// <summary>
        /// Analysis timestamp
        /// </summary>
        public DateTime AnalysisDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Time taken for analysis in milliseconds
        /// </summary>
        public long AnalysisDurationMs { get; set; }

        /// <summary>
        /// Data type classification
        /// </summary>
        public DataType EstimatedDataType { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public FileStatistics()
        {
            ByteFrequency = new long[256];
        }

        /// <summary>
        /// Get percentage for a specific byte value
        /// </summary>
        public double GetBytePercentage(byte value)
        {
            if (FileSize == 0)
                return 0;

            return (ByteFrequency[value] / (double)FileSize) * 100.0;
        }

        /// <summary>
        /// Get summary string
        /// </summary>
        public string GetSummary()
        {
            return $"File: {FileSize:N0} bytes | Entropy: {Entropy:F2} | Type: {EstimatedDataType} | Unique: {UniqueBytesCount}/256";
        }
    }

    /// <summary>
    /// Estimated data type based on statistics
    /// </summary>
    public enum DataType
    {
        /// <summary>
        /// Unknown or mixed data
        /// </summary>
        Unknown,

        /// <summary>
        /// Text data (high printable ASCII)
        /// </summary>
        Text,

        /// <summary>
        /// Binary data
        /// </summary>
        Binary,

        /// <summary>
        /// Compressed data (high entropy)
        /// </summary>
        Compressed,

        /// <summary>
        /// Encrypted data (very high uniform entropy)
        /// </summary>
        Encrypted,

        /// <summary>
        /// Mostly null bytes
        /// </summary>
        Sparse,

        /// <summary>
        /// Image data
        /// </summary>
        Image,

        /// <summary>
        /// Executable code
        /// </summary>
        Executable
    }
}
