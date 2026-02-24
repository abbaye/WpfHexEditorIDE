//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.FormatDetection;

namespace WpfHexaEditor.Events
{
    /// <summary>
    /// Event args for format detection
    /// </summary>
    public class FormatDetectedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether format detection succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Detected format definition
        /// </summary>
        public FormatDefinition Format { get; set; }

        /// <summary>
        /// Generated custom background blocks
        /// </summary>
        public List<CustomBackgroundBlock> Blocks { get; set; } = new List<CustomBackgroundBlock>();

        /// <summary>
        /// Error message if detection failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Time taken for detection (milliseconds)
        /// </summary>
        public double DetectionTimeMs { get; set; }

        public override string ToString()
        {
            if (Success)
                return $"Format detected: {Format?.FormatName} ({Blocks?.Count ?? 0} blocks, {DetectionTimeMs:F2}ms)";
            else
                return $"Detection failed: {ErrorMessage}";
        }
    }

    /// <summary>
    /// Result of format detection operation
    /// </summary>
    public class FormatDetectionResult
    {
        /// <summary>
        /// Whether detection succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Detected format
        /// </summary>
        public FormatDefinition Format { get; set; }

        /// <summary>
        /// Generated blocks
        /// </summary>
        public List<CustomBackgroundBlock> Blocks { get; set; } = new List<CustomBackgroundBlock>();

        /// <summary>
        /// Error message if failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Detection time in milliseconds
        /// </summary>
        public double DetectionTimeMs { get; set; }

        /// <summary>
        /// All candidate matches found (for ambiguous cases)
        /// Used when multiple formats match with similar confidence
        /// </summary>
        public List<FormatMatchCandidate> Candidates { get; set; } = new List<FormatMatchCandidate>();

        /// <summary>
        /// Confidence score of the selected format (0.0 - 1.0)
        /// Higher values indicate stronger match confidence
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Whether user selection is recommended due to ambiguity
        /// True when multiple candidates have similar confidence scores
        /// </summary>
        public bool RequiresUserSelection { get; set; }

        /// <summary>
        /// Results from content analysis (text vs binary, encoding, etc.)
        /// </summary>
        public ContentAnalysisResult ContentAnalysis { get; set; }

        public override string ToString()
        {
            if (Success)
            {
                var confidence = Confidence > 0 ? $", {Confidence:P0} confidence" : "";
                var ambiguous = RequiresUserSelection ? " [AMBIGUOUS]" : "";
                return $"✓ {Format?.FormatName}: {Blocks.Count} blocks ({DetectionTimeMs:F2}ms{confidence}){ambiguous}";
            }
            else
                return $"✗ Failed: {ErrorMessage}";
        }
    }
}
