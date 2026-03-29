// ==========================================================
// Project: WpfHexEditor.Core
// File: FormatMatchCandidate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Represents a potential format match candidate produced by the multi-tier
//     format detection system, carrying confidence score, matched format definition,
//     and detection evidence for final ranking and selection.
//
// Architecture Notes:
//     Data container returned by FormatDetectionService. Ranked by confidence
//     score (0.0–1.0). The highest-confidence candidate triggers editor selection.
//     No WPF dependencies.
//
// ==========================================================

using System.Collections.Generic;
using WpfHexEditor.Core;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Represents a potential format match with confidence scoring.
    /// Used by the multi-tier detection system to rank and select the best format match.
    /// </summary>
    public class FormatMatchCandidate
    {
        /// <summary>
        /// The format definition that matched
        /// </summary>
        public FormatDefinition Format { get; set; }

        /// <summary>
        /// Generated custom background blocks from this format
        /// </summary>
        public List<CustomBackgroundBlock> Blocks { get; set; }

        /// <summary>
        /// Overall confidence score (0.0 - 1.0)
        /// Computed from individual confidence components
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Which detection tier found this match
        /// </summary>
        public MatchTier Tier { get; set; }

        /// <summary>
        /// Human-readable reasons for the confidence score
        /// </summary>
        public List<string> ConfidenceFactors { get; set; } = new List<string>();

        // Individual confidence components (0.0 - 1.0)

        /// <summary>
        /// Confidence from signature/magic bytes match
        /// </summary>
        public double SignatureConfidence { get; set; }

        /// <summary>
        /// Confidence from file extension match
        /// </summary>
        public double ExtensionConfidence { get; set; }

        /// <summary>
        /// Confidence from content analysis (text patterns, etc.)
        /// </summary>
        public double ContentConfidence { get; set; }

        /// <summary>
        /// Confidence from successful structure parsing
        /// </summary>
        public double StructureConfidence { get; set; }

        /// <summary>
        /// Variables populated by built-in functions (e.g., encoding, lineCount, etc.)
        /// </summary>
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// D3 — Assertion results collected during block execution.
        /// Failed entries are shown in the Forensic Alerts section.
        /// </summary>
        public List<AssertionResult> AssertionResults { get; set; } = new List<AssertionResult>();

        public override string ToString()
        {
            return $"{Format?.FormatName} (Tier {(int)Tier}, Confidence: {ConfidenceScore:P0})";
        }
    }

    /// <summary>
    /// Detection tier priority levels.
    /// Lower number = higher priority/confidence.
    /// </summary>
    public enum MatchTier
    {
        /// <summary>
        /// Strong signature match (required: true, unique signature like PNG/ZIP)
        /// </summary>
        StrongSignature = 1,

        /// <summary>
        /// Weak signature match (required: true, but common signature like 0x00)
        /// </summary>
        WeakSignature = 2,

        /// <summary>
        /// Extension-based match with content heuristics
        /// </summary>
        ExtensionBased = 3,

        /// <summary>
        /// Content-based detection only (text format patterns)
        /// </summary>
        ContentBased = 4,

        /// <summary>
        /// Fallback weak match (required: false, last resort)
        /// </summary>
        FallbackWeak = 5
    }
}
