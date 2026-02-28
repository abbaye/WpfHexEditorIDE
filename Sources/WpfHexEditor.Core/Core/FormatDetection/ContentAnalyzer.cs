//////////////////////////////////////////////
// Apache 2.0  - 2026
// Content Analysis for Format Detection
// Author: Derek Tremblay
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Analyzes file content to distinguish between text and binary files,
    /// and detect specific text format patterns (YAML, JSON, XML, CSV).
    /// </summary>
    public class ContentAnalyzer
    {
        /// <summary>
        /// Analyzes file content and returns detection results
        /// </summary>
        /// <param name="data">File bytes to analyze</param>
        /// <param name="sampleSize">Number of bytes to analyze (default 8KB)</param>
        /// <returns>Content analysis results</returns>
        public ContentAnalysisResult Analyze(byte[] data, int sampleSize = 8192)
        {
            if (data == null || data.Length == 0)
            {
                return new ContentAnalysisResult
                {
                    IsLikelyBinary = true,
                    IsLikelyText = false,
                    TextContentRatio = 0.0,
                    DetectedEncoding = "Empty",
                    HasNullBytes = false
                };
            }

            var sample = data.Take(Math.Min(sampleSize, data.Length)).ToArray();

            var result = new ContentAnalysisResult
            {
                HasNullBytes = ContainsNullBytes(sample),
                TextContentRatio = CalculateTextRatio(sample),
                DetectedEncoding = DetectEncoding(sample),
                TextFormatHints = new List<string>()
            };

            // Determine if text or binary
            // Text files typically have >85% printable chars and no null bytes
            result.IsLikelyText = result.TextContentRatio > 0.85 && !result.HasNullBytes;
            result.IsLikelyBinary = !result.IsLikelyText;

            // Detect specific text formats if it's text
            if (result.IsLikelyText)
            {
                bool specificFormatFound = false;

                if (LooksLikeYaml(sample))
                {
                    result.TextFormatHints.Add("YAML");
                    specificFormatFound = true;
                }
                if (LooksLikeJson(sample))
                {
                    result.TextFormatHints.Add("JSON");
                    specificFormatFound = true;
                }
                if (LooksLikeXml(sample))
                {
                    result.TextFormatHints.Add("XML");
                    specificFormatFound = true;
                }
                if (LooksLikeCsv(sample))
                {
                    result.TextFormatHints.Add("CSV");
                    specificFormatFound = true;
                }

                // If no specific format detected, mark as generic plain text
                if (!specificFormatFound)
                {
                    result.TextFormatHints.Add("Plain");
                }
            }

            return result;
        }

        /// <summary>
        /// Calculates the ratio of printable text characters
        /// </summary>
        private double CalculateTextRatio(byte[] data)
        {
            if (data.Length == 0) return 0.0;

            int printableCount = 0;
            foreach (byte b in data)
            {
                // ASCII printable (0x20-0x7E) + common whitespace (tab, LF, CR)
                if ((b >= 0x20 && b <= 0x7E) || b == 0x09 || b == 0x0A || b == 0x0D)
                    printableCount++;
            }

            return (double)printableCount / data.Length;
        }

        /// <summary>
        /// Checks if data contains null bytes (0x00)
        /// </summary>
        private bool ContainsNullBytes(byte[] data)
        {
            return data.Any(b => b == 0x00);
        }

        /// <summary>
        /// Detects encoding type
        /// </summary>
        private string DetectEncoding(byte[] data)
        {
            if (data.Length == 0) return "Unknown";

            // Check for BOM (Byte Order Mark)
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
                return "UTF-8-BOM";

            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
                return "UTF-16LE";

            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
                return "UTF-16BE";

            // Check if valid UTF-8
            if (HasValidUtf8Sequences(data))
                return "UTF-8";

            // Check if pure ASCII
            if (data.All(b => b < 0x80))
                return "ASCII";

            // Contains high bytes but not valid UTF-8
            if (ContainsNullBytes(data))
                return "Binary";

            return "Extended-ASCII";
        }

        /// <summary>
        /// Validates UTF-8 byte sequences
        /// </summary>
        private bool HasValidUtf8Sequences(byte[] data)
        {
            try
            {
                // Try to decode as UTF-8
                var text = Encoding.UTF8.GetString(data);

                // Re-encode and compare
                var reencoded = Encoding.UTF8.GetBytes(text);

                // If lengths differ significantly, likely not valid UTF-8
                return Math.Abs(reencoded.Length - data.Length) < data.Length * 0.1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects YAML format patterns
        /// </summary>
        private bool LooksLikeYaml(byte[] data)
        {
            try
            {
                var text = Encoding.UTF8.GetString(data.Take(512).ToArray());

                // YAML indicators:
                // - Document separator "---"
                // - Key-value pairs "key: value"
                // - List items starting with "- "
                return text.Contains("---") ||
                       Regex.IsMatch(text, @"^\w+:\s+\S+", RegexOptions.Multiline) ||
                       Regex.IsMatch(text, @"^-\s+\w+", RegexOptions.Multiline);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects JSON format patterns
        /// </summary>
        private bool LooksLikeJson(byte[] data)
        {
            try
            {
                var text = Encoding.UTF8.GetString(data.Take(512).ToArray()).Trim();

                // JSON starts with { or [ and contains quotes
                return (text.StartsWith("{") || text.StartsWith("[")) && text.Contains("\"");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects XML format patterns
        /// </summary>
        private bool LooksLikeXml(byte[] data)
        {
            try
            {
                var text = Encoding.UTF8.GetString(data.Take(512).ToArray()).Trim();

                // XML starts with <?xml or <tag>
                return text.StartsWith("<?xml") ||
                       (text.StartsWith("<") && text.Contains(">") && !text.StartsWith("<!"));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detects CSV format patterns
        /// </summary>
        private bool LooksLikeCsv(byte[] data)
        {
            try
            {
                var text = Encoding.UTF8.GetString(data.Take(512).ToArray());
                var lines = text.Split('\n').Take(3).ToArray();

                if (lines.Length < 2) return false;

                // Check for consistent delimiter count across lines
                char[] delimiters = { ',', ';', '\t' };

                foreach (var delim in delimiters)
                {
                    var counts = lines.Select(l => l.Count(c => c == delim)).ToArray();

                    // All lines should have same delimiter count and at least 1 delimiter
                    if (counts.All(c => c > 0 && c == counts[0]))
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Results from content analysis
    /// </summary>
    public class ContentAnalysisResult
    {
        /// <summary>
        /// File is likely a text file
        /// </summary>
        public bool IsLikelyText { get; set; }

        /// <summary>
        /// File is likely a binary file
        /// </summary>
        public bool IsLikelyBinary { get; set; }

        /// <summary>
        /// Ratio of printable text characters (0.0 - 1.0)
        /// </summary>
        public double TextContentRatio { get; set; }

        /// <summary>
        /// Detected encoding (UTF-8, ASCII, Binary, etc.)
        /// </summary>
        public string DetectedEncoding { get; set; }

        /// <summary>
        /// Contains null bytes (0x00)
        /// </summary>
        public bool HasNullBytes { get; set; }

        /// <summary>
        /// Specific text format hints detected (YAML, JSON, XML, CSV)
        /// </summary>
        public List<string> TextFormatHints { get; set; } = new List<string>();

        public override string ToString()
        {
            var type = IsLikelyText ? "Text" : "Binary";
            var hints = TextFormatHints.Count > 0 ? $" ({string.Join(", ", TextFormatHints)})" : "";
            return $"{type} - {DetectedEncoding} - {TextContentRatio:P0} printable{hints}";
        }
    }
}
