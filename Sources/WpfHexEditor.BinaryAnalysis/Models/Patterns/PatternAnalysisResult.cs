//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexEditor.BinaryAnalysis.Models.Patterns
{
    /// <summary>
    /// Result of pattern analysis on binary data
    /// Contains all detected patterns and statistics
    /// </summary>
    [Serializable]
    public class PatternAnalysisResult
    {
        /// <summary>
        /// All detected patterns
        /// </summary>
        public List<DetectedPattern> Patterns { get; set; } = new List<DetectedPattern>();

        /// <summary>
        /// Total number of patterns detected
        /// </summary>
        public int TotalPatterns => Patterns.Count;

        /// <summary>
        /// Offset where analysis started
        /// </summary>
        public long StartOffset { get; set; } = 0;

        /// <summary>
        /// Length of data analyzed
        /// </summary>
        public long AnalyzedLength { get; set; } = 0;

        /// <summary>
        /// End offset of analysis
        /// </summary>
        public long EndOffset => StartOffset + AnalyzedLength;

        /// <summary>
        /// Analysis duration in milliseconds
        /// </summary>
        public long AnalysisDurationMs { get; set; } = 0;

        /// <summary>
        /// Timestamp when analysis was performed
        /// </summary>
        public DateTime AnalysisDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Overall file entropy (0.0 to 8.0)
        /// </summary>
        public double OverallEntropy { get; set; } = 0;

        /// <summary>
        /// Whether analysis completed successfully
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Error message if analysis failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Analysis statistics by pattern type
        /// </summary>
        public Dictionary<PatternType, int> PatternTypeCount { get; set; } = new Dictionary<PatternType, int>();

        /// <summary>
        /// Warnings encountered during analysis
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Constructor
        /// </summary>
        public PatternAnalysisResult()
        {
        }

        /// <summary>
        /// Constructor with basic properties
        /// </summary>
        public PatternAnalysisResult(long startOffset, long analyzedLength)
        {
            StartOffset = startOffset;
            AnalyzedLength = analyzedLength;
        }

        /// <summary>
        /// Add a detected pattern to the results
        /// </summary>
        public void AddPattern(DetectedPattern pattern)
        {
            if (pattern == null)
                return;

            Patterns.Add(pattern);

            // Update statistics
            if (PatternTypeCount.ContainsKey(pattern.Type))
                PatternTypeCount[pattern.Type]++;
            else
                PatternTypeCount[pattern.Type] = 1;
        }

        /// <summary>
        /// Get patterns of a specific type
        /// </summary>
        public List<DetectedPattern> GetPatternsByType(PatternType type)
        {
            return Patterns.Where(p => p.Type == type).ToList();
        }

        /// <summary>
        /// Get patterns at a specific offset
        /// </summary>
        public List<DetectedPattern> GetPatternsAtOffset(long offset)
        {
            return Patterns.Where(p => p.ContainsOffset(offset)).ToList();
        }

        /// <summary>
        /// Get patterns in a range
        /// </summary>
        public List<DetectedPattern> GetPatternsInRange(long startOffset, long endOffset)
        {
            return Patterns
                .Where(p => p.StartOffset < endOffset && p.EndOffset > startOffset)
                .ToList();
        }

        /// <summary>
        /// Get high-severity patterns (corruption, anomalies)
        /// </summary>
        public List<DetectedPattern> GetHighSeverityPatterns()
        {
            return Patterns.Where(p => p.Severity >= 3).ToList();
        }

        /// <summary>
        /// Get patterns sorted by offset
        /// </summary>
        public List<DetectedPattern> GetPatternsByOffset()
        {
            return Patterns.OrderBy(p => p.StartOffset).ToList();
        }

        /// <summary>
        /// Get patterns sorted by confidence (descending)
        /// </summary>
        public List<DetectedPattern> GetPatternsByConfidence()
        {
            return Patterns.OrderByDescending(p => p.Confidence).ToList();
        }

        /// <summary>
        /// Get total coverage (percentage of analyzed data covered by patterns)
        /// </summary>
        public double GetCoveragePercentage()
        {
            if (AnalyzedLength == 0)
                return 0;

            // Calculate non-overlapping coverage
            var coveredBytes = new HashSet<long>();
            foreach (var pattern in Patterns)
            {
                for (long offset = pattern.StartOffset; offset < pattern.EndOffset && offset < EndOffset; offset++)
                {
                    coveredBytes.Add(offset);
                }
            }

            return (coveredBytes.Count / (double)AnalyzedLength) * 100.0;
        }

        /// <summary>
        /// Get summary statistics
        /// </summary>
        public string GetSummary()
        {
            var summary = $"Pattern Analysis Summary:\n";
            summary += $"- Analyzed: {AnalyzedLength:N0} bytes (0x{StartOffset:X} to 0x{EndOffset:X})\n";
            summary += $"- Duration: {AnalysisDurationMs} ms\n";
            summary += $"- Total Patterns: {TotalPatterns}\n";
            summary += $"- Overall Entropy: {OverallEntropy:F2}\n";
            summary += $"- Coverage: {GetCoveragePercentage():F1}%\n";

            if (PatternTypeCount.Count > 0)
            {
                summary += "\nPattern Types:\n";
                foreach (var kvp in PatternTypeCount.OrderByDescending(x => x.Value))
                {
                    summary += $"  - {kvp.Key}: {kvp.Value}\n";
                }
            }

            if (Warnings.Count > 0)
            {
                summary += $"\nWarnings: {Warnings.Count}\n";
            }

            return summary;
        }

        /// <summary>
        /// Export patterns to formatted string for display
        /// </summary>
        public string ExportToText()
        {
            var text = GetSummary() + "\n\nDetected Patterns:\n";
            text += new string('-', 80) + "\n";

            foreach (var pattern in GetPatternsByOffset())
            {
                text += $"0x{pattern.StartOffset:X8} - 0x{pattern.EndOffset:X8} ({pattern.Length:N0} bytes)\n";
                text += $"  Type: {pattern.Type}\n";
                text += $"  Description: {pattern.Description}\n";

                if (!string.IsNullOrEmpty(pattern.FileType))
                    text += $"  File Type: {pattern.FileType}\n";

                if (pattern.Confidence < 1.0)
                    text += $"  Confidence: {pattern.Confidence:P0}\n";

                if (pattern.Entropy.HasValue)
                    text += $"  Entropy: {pattern.Entropy.Value:F2}\n";

                if (pattern.Severity > 0)
                    text += $"  Severity: {pattern.Severity}\n";

                text += "\n";
            }

            return text;
        }
    }
}
