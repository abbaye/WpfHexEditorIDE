//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using WpfHexEditor.Core.BinaryAnalysis.Models.Patterns;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service for detecting patterns in binary data
    /// Detects repeated sequences, embedded files (using FormatDetectionService),
    /// padding, strings, and other patterns
    /// </summary>
    public class PatternRecognitionService
    {
        private readonly FormatDetectionService _formatDetectionService;

        /// <summary>
        /// Minimum length for repeated sequence detection
        /// </summary>
        public int MinRepeatedSequenceLength { get; set; } = 4;

        /// <summary>
        /// Minimum occurrences for repeated sequence detection
        /// </summary>
        public int MinRepeatedSequenceOccurrences { get; set; } = 3;

        /// <summary>
        /// Minimum length for padding detection
        /// </summary>
        public int MinPaddingLength { get; set; } = 16;

        /// <summary>
        /// Minimum length for ASCII string detection
        /// </summary>
        public int MinAsciiStringLength { get; set; } = 10;

        /// <summary>
        /// Maximum bytes to sample for analysis (to limit memory usage)
        /// </summary>
        public int MaxSampleSize { get; set; } = 1024 * 1024; // 1MB default

        public PatternRecognitionService()
        {
            _formatDetectionService = new FormatDetectionService();
        }

        /// <summary>
        /// Analyze binary data for patterns
        /// </summary>
        /// <param name="data">Binary data to analyze</param>
        /// <param name="startOffset">Start offset in original file</param>
        /// <param name="detectEmbeddedFiles">Whether to detect embedded files</param>
        /// <param name="detectStrings">Whether to detect ASCII/Unicode strings</param>
        /// <param name="detectPadding">Whether to detect null/FF padding</param>
        /// <param name="detectRepeatedSequences">Whether to detect repeated sequences</param>
        /// <returns>Pattern analysis result</returns>
        public PatternAnalysisResult AnalyzePatterns(byte[] data, long startOffset = 0,
            bool detectEmbeddedFiles = true,
            bool detectStrings = true,
            bool detectPadding = true,
            bool detectRepeatedSequences = true)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new PatternAnalysisResult(startOffset, data.Length);

            try
            {
                // Limit data size to prevent memory issues
                var analyzedData = data;
                if (data.Length > MaxSampleSize)
                {
                    result.Warnings.Add($"Data truncated to {MaxSampleSize:N0} bytes for analysis");
                    analyzedData = new byte[MaxSampleSize];
                    Array.Copy(data, analyzedData, MaxSampleSize);
                }

                // 1. Detect embedded files (magic bytes)
                if (detectEmbeddedFiles)
                {
                    var embeddedFiles = FindEmbeddedFiles(analyzedData, startOffset);
                    foreach (var pattern in embeddedFiles)
                        result.AddPattern(pattern);
                }

                // 2. Detect padding
                if (detectPadding)
                {
                    var padding = FindPadding(analyzedData, startOffset);
                    foreach (var pattern in padding)
                        result.AddPattern(pattern);
                }

                // 3. Detect ASCII/Unicode strings
                if (detectStrings)
                {
                    var strings = FindStrings(analyzedData, startOffset);
                    foreach (var pattern in strings)
                        result.AddPattern(pattern);
                }

                // 4. Detect repeated sequences
                if (detectRepeatedSequences)
                {
                    var repeated = FindRepeatedSequences(analyzedData, startOffset);
                    foreach (var pattern in repeated)
                        result.AddPattern(pattern);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Pattern analysis failed: {ex.Message}";
            }

            stopwatch.Stop();
            result.AnalysisDurationMs = stopwatch.ElapsedMilliseconds;

            return result;
        }

        #region Embedded File Detection

        /// <summary>
        /// Find embedded files using magic bytes signatures (via FormatDetectionService)
        /// </summary>
        private List<DetectedPattern> FindEmbeddedFiles(byte[] data, long baseOffset)
        {
            var patterns = new List<DetectedPattern>();

            // Scan for magic bytes at various offsets
            // Sample every 512 bytes to balance thoroughness vs performance
            var sampleInterval = 512;

            for (int offset = 0; offset < data.Length - 16; offset += sampleInterval)
            {
                // Extract sample (first 512 bytes from this offset)
                var sampleLength = Math.Min(512, data.Length - offset);
                var sample = new byte[sampleLength];
                Array.Copy(data, offset, sample, 0, sampleLength);

                // Try to detect format
                var detectionResult = _formatDetectionService.DetectFormat(sample);

                if (detectionResult.Success && detectionResult.Format != null)
                {
                    // Check if we already detected this file (avoid duplicates)
                    var absoluteOffset = baseOffset + offset;
                    if (!patterns.Any(p => p.StartOffset == absoluteOffset && p.Type == PatternType.EmbeddedFile))
                    {
                        var pattern = new DetectedPattern(PatternType.EmbeddedFile, absoluteOffset, sampleLength)
                        {
                            Description = $"Embedded {detectionResult.Format.FormatName} file",
                            FileType = detectionResult.Format.FormatName,
                            Confidence = 0.9,
                            VisualizationColor = "#FF6B35" // Orange for embedded files
                        };

                        // Store magic bytes from signature
                        if (detectionResult.Format.Detection != null && !string.IsNullOrEmpty(detectionResult.Format.Detection.Signature))
                        {
                            // Convert hex string to bytes
                            var hexString = detectionResult.Format.Detection.Signature;
                            var magicBytes = new byte[hexString.Length / 2];
                            for (int j = 0; j < magicBytes.Length; j++)
                            {
                                magicBytes[j] = Convert.ToByte(hexString.Substring(j * 2, 2), 16);
                            }
                            pattern.MagicBytes = magicBytes;
                        }

                        patterns.Add(pattern);
                    }
                }
            }

            return patterns;
        }

        #endregion

        #region Padding Detection

        /// <summary>
        /// Find null (00) and xFF padding
        /// </summary>
        private List<DetectedPattern> FindPadding(byte[] data, long baseOffset)
        {
            var patterns = new List<DetectedPattern>();

            int i = 0;
            while (i < data.Length)
            {
                byte currentByte = data[i];

                // Check for null or 0xFF padding
                if (currentByte == 0x00 || currentByte == 0xFF)
                {
                    int startOffset = i;
                    byte paddingByte = currentByte;

                    // Count consecutive identical bytes
                    while (i < data.Length && data[i] == paddingByte)
                        i++;

                    int length = i - startOffset;

                    // Only report if padding is significant
                    if (length >= MinPaddingLength)
                    {
                        var patternType = paddingByte == 0x00 ? PatternType.NullPadding : PatternType.FFPadding;
                        var description = paddingByte == 0x00
                            ? $"Null byte padding ({length} bytes)"
                            : $"0xFF padding ({length} bytes)";

                        var pattern = new DetectedPattern(patternType, baseOffset + startOffset, length)
                        {
                            Description = description,
                            Confidence = 1.0,
                            VisualizationColor = paddingByte == 0x00 ? "#CCCCCC" : "#FFCCCC"
                        };

                        patterns.Add(pattern);
                    }
                }
                else
                {
                    i++;
                }
            }

            return patterns;
        }

        #endregion

        #region String Detection

        /// <summary>
        /// Find ASCII and Unicode strings
        /// </summary>
        private List<DetectedPattern> FindStrings(byte[] data, long baseOffset)
        {
            var patterns = new List<DetectedPattern>();

            // Detect ASCII strings
            patterns.AddRange(FindAsciiStrings(data, baseOffset));

            // Detect Unicode (UTF-16) strings
            patterns.AddRange(FindUnicodeStrings(data, baseOffset));

            return patterns;
        }

        private List<DetectedPattern> FindAsciiStrings(byte[] data, long baseOffset)
        {
            var patterns = new List<DetectedPattern>();
            int i = 0;

            while (i < data.Length)
            {
                if (IsPrintableAscii(data[i]))
                {
                    int startOffset = i;
                    var sb = new StringBuilder();

                    // Collect consecutive printable ASCII characters
                    while (i < data.Length && IsPrintableAscii(data[i]))
                    {
                        sb.Append((char)data[i]);
                        i++;
                    }

                    int length = i - startOffset;

                    if (length >= MinAsciiStringLength)
                    {
                        var stringValue = sb.ToString();
                        var pattern = new DetectedPattern(PatternType.AsciiString, baseOffset + startOffset, length)
                        {
                            Description = $"ASCII string: \"{TruncateString(stringValue, 50)}\"",
                            Confidence = 0.8,
                            VisualizationColor = "#4CAF50" // Green for strings
                        };

                        pattern.Metadata["StringValue"] = stringValue;
                        patterns.Add(pattern);
                    }
                }
                else
                {
                    i++;
                }
            }

            return patterns;
        }

        private List<DetectedPattern> FindUnicodeStrings(byte[] data, long baseOffset)
        {
            var patterns = new List<DetectedPattern>();

            for (int i = 0; i < data.Length - 2; i += 2)
            {
                // UTF-16 LE pattern: printable char followed by 0x00
                if (IsPrintableAscii(data[i]) && data[i + 1] == 0x00)
                {
                    int startOffset = i;
                    var sb = new StringBuilder();

                    // Collect consecutive UTF-16 characters
                    while (i < data.Length - 1 && IsPrintableAscii(data[i]) && data[i + 1] == 0x00)
                    {
                        sb.Append((char)data[i]);
                        i += 2;
                    }

                    int length = i - startOffset;

                    if (length >= MinAsciiStringLength * 2) // UTF-16 uses 2 bytes per char
                    {
                        var stringValue = sb.ToString();
                        var pattern = new DetectedPattern(PatternType.UnicodeString, baseOffset + startOffset, length)
                        {
                            Description = $"Unicode string: \"{TruncateString(stringValue, 50)}\"",
                            Confidence = 0.8,
                            VisualizationColor = "#2196F3" // Blue for Unicode
                        };

                        pattern.Metadata["StringValue"] = stringValue;
                        patterns.Add(pattern);
                    }
                }
            }

            return patterns;
        }

        private bool IsPrintableAscii(byte b)
        {
            // Printable ASCII: space (32) to tilde (126), plus tab (9), newline (10), carriage return (13)
            return (b >= 32 && b <= 126) || b == 9 || b == 10 || b == 13;
        }

        private string TruncateString(string str, int maxLength)
        {
            if (str.Length <= maxLength)
                return str;

            return str.Substring(0, maxLength) + "...";
        }

        #endregion

        #region Repeated Sequence Detection

        /// <summary>
        /// Find repeated byte sequences (simplified version - not full suffix array)
        /// </summary>
        private List<DetectedPattern> FindRepeatedSequences(byte[] data, long baseOffset)
        {
            var patterns = new List<DetectedPattern>();
            var sequenceCounts = new Dictionary<string, List<int>>(); // sequence hash -> list of offsets

            // Scan for repeated sequences (4-16 bytes)
            for (int seqLength = MinRepeatedSequenceLength; seqLength <= 16 && seqLength < data.Length / 4; seqLength++)
            {
                for (int i = 0; i <= data.Length - seqLength; i++)
                {
                    // Create hash of sequence
                    var sequence = new byte[seqLength];
                    Array.Copy(data, i, sequence, 0, seqLength);
                    var hash = Convert.ToBase64String(sequence);

                    if (!sequenceCounts.ContainsKey(hash))
                        sequenceCounts[hash] = new List<int>();

                    sequenceCounts[hash].Add(i);
                }
            }

            // Find sequences that repeat enough times
            foreach (var kvp in sequenceCounts)
            {
                if (kvp.Value.Count >= MinRepeatedSequenceOccurrences)
                {
                    var sequence = Convert.FromBase64String(kvp.Key);
                    var firstOffset = kvp.Value[0];

                    // Check if this is just padding (all same byte)
                    bool isPadding = sequence.All(b => b == sequence[0]);
                    if (isPadding)
                        continue; // Skip, already detected as padding

                    // Check if pattern repeats immediately (ABABAB...)
                    bool isRepeatingPattern = IsImmediatelyRepeating(data, firstOffset, sequence.Length);

                    var patternType = isRepeatingPattern ? PatternType.RepeatingPattern : PatternType.RepeatedSequence;
                    var description = isRepeatingPattern
                        ? $"Repeating pattern ({sequence.Length} bytes Ã— {kvp.Value.Count})"
                        : $"Repeated sequence ({sequence.Length} bytes, {kvp.Value.Count} occurrences)";

                    var pattern = new DetectedPattern(patternType, baseOffset + firstOffset, sequence.Length)
                    {
                        Description = description,
                        Occurrences = kvp.Value.Count,
                        Confidence = 0.7,
                        SampleData = sequence.Take(Math.Min(256, sequence.Length)).ToArray(),
                        VisualizationColor = "#9C27B0" // Purple for repeated sequences
                    };

                    pattern.Metadata["Offsets"] = string.Join(", ", kvp.Value.Take(10).Select(o => $"0x{(baseOffset + o):X}"));
                    patterns.Add(pattern);
                }
            }

            return patterns.OrderByDescending(p => p.Occurrences).Take(20).ToList(); // Top 20 repeated sequences
        }

        private bool IsImmediatelyRepeating(byte[] data, int offset, int seqLength)
        {
            // Check if pattern repeats at least 3 times immediately
            if (offset + seqLength * 3 > data.Length)
                return false;

            for (int repeat = 1; repeat < 3; repeat++)
            {
                for (int i = 0; i < seqLength; i++)
                {
                    if (data[offset + i] != data[offset + repeat * seqLength + i])
                        return false;
                }
            }

            return true;
        }

        #endregion
    }
}
