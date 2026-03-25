//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Core.BinaryAnalysis.Models.Patterns
{
    /// <summary>
    /// Represents a detected pattern in binary data
    /// </summary>
    [Serializable]
    public class DetectedPattern
    {
        /// <summary>
        /// Type of pattern detected
        /// </summary>
        public PatternType Type { get; set; }

        /// <summary>
        /// Start offset in the file
        /// </summary>
        public long StartOffset { get; set; }

        /// <summary>
        /// Length of the pattern in bytes
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// End offset (StartOffset + Length)
        /// </summary>
        public long EndOffset => StartOffset + Length;

        /// <summary>
        /// Confidence score (0.0 to 1.0)
        /// </summary>
        public double Confidence { get; set; } = 1.0;

        /// <summary>
        /// Pattern description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Detected file type (for EmbeddedFile patterns)
        /// </summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// Magic bytes signature (for EmbeddedFile patterns)
        /// </summary>
        public byte[] MagicBytes { get; set; }

        /// <summary>
        /// Number of occurrences (for RepeatedSequence patterns)
        /// </summary>
        public int Occurrences { get; set; } = 1;

        /// <summary>
        /// Entropy value (for entropy-based patterns)
        /// </summary>
        public double? Entropy { get; set; }

        /// <summary>
        /// Additional metadata about the pattern
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Severity level (for Corruption/Anomaly patterns)
        /// 0 = Info, 1 = Low, 2 = Medium, 3 = High, 4 = Critical
        /// </summary>
        public int Severity { get; set; } = 0;

        /// <summary>
        /// Sample data (first bytes of the pattern, max 256 bytes)
        /// </summary>
        public byte[] SampleData { get; set; }

        /// <summary>
        /// Timestamp when pattern was detected
        /// </summary>
        public DateTime DetectedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Pattern visualization color (hex string, e.g., "#FF0000")
        /// </summary>
        public string VisualizationColor { get; set; } = "#0078D4"; // Default blue

        /// <summary>
        /// Constructor
        /// </summary>
        public DetectedPattern()
        {
        }

        /// <summary>
        /// Constructor with basic properties
        /// </summary>
        public DetectedPattern(PatternType type, long startOffset, long length, string description = "")
        {
            Type = type;
            StartOffset = startOffset;
            Length = length;
            Description = description;
        }

        /// <summary>
        /// Get pattern summary string
        /// </summary>
        public override string ToString()
        {
            return $"{Type} at 0x{StartOffset:X} ({Length} bytes)" +
                   (string.IsNullOrEmpty(Description) ? "" : $" - {Description}");
        }

        /// <summary>
        /// Check if this pattern overlaps with another
        /// </summary>
        public bool OverlapsWith(DetectedPattern other)
        {
            if (other == null)
                return false;

            return !(EndOffset <= other.StartOffset || StartOffset >= other.EndOffset);
        }

        /// <summary>
        /// Check if this pattern contains a specific offset
        /// </summary>
        public bool ContainsOffset(long offset)
        {
            return offset >= StartOffset && offset < EndOffset;
        }

        /// <summary>
        /// Clone this pattern
        /// </summary>
        public DetectedPattern Clone()
        {
            return new DetectedPattern
            {
                Type = this.Type,
                StartOffset = this.StartOffset,
                Length = this.Length,
                Confidence = this.Confidence,
                Description = this.Description,
                FileType = this.FileType,
                MagicBytes = this.MagicBytes != null ? (byte[])this.MagicBytes.Clone() : null,
                Occurrences = this.Occurrences,
                Entropy = this.Entropy,
                Metadata = new Dictionary<string, string>(this.Metadata),
                Severity = this.Severity,
                SampleData = this.SampleData != null ? (byte[])this.SampleData.Clone() : null,
                DetectedDate = this.DetectedDate,
                VisualizationColor = this.VisualizationColor
            };
        }
    }
}
