//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
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

        public override string ToString()
        {
            if (Success)
                return $"✓ {Format?.FormatName}: {Blocks.Count} blocks ({DetectionTimeMs:F2}ms)";
            else
                return $"✗ Failed: {ErrorMessage}";
        }
    }
}
