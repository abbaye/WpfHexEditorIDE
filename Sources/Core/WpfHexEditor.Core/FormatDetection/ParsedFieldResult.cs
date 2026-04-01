// ==========================================================
// Project: WpfHexEditor.Core
// File: ParsedFieldResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-25
// Description:
//     Structured output model produced by FormatScriptInterpreter for each
//     parsed field.  Carries decoded display value, mapped value, bitfield
//     breakdowns, child fields (for repeating/nested), and validation state.
//
// Architecture Notes:
//     Immutable record — created by interpreter, consumed by ParsedFieldsPanel
//     and Format Structure Tree panel.  No WPF dependencies.
//     Children list supports arbitrary nesting depth (tree model).
// ==========================================================

using System.Collections.Generic;

namespace WpfHexEditor.Core.FormatDetection
{
    /// <summary>
    /// Validation state of a parsed field.
    /// </summary>
    public enum ValidationState
    {
        /// <summary>No validation issues.</summary>
        Pass,
        /// <summary>Validation warning (non-blocking).</summary>
        Warning,
        /// <summary>Validation error (field value is suspect).</summary>
        Error
    }

    /// <summary>
    /// Result of extracting one bitfield from a packed byte field.
    /// </summary>
    public sealed class BitfieldResult
    {
        /// <summary>Display name from the bitfield definition.</summary>
        public string Name       { get; init; }

        /// <summary>Bit range string (e.g. "7-6").</summary>
        public string Bits       { get; init; }

        /// <summary>Extracted integer value.</summary>
        public long   Value      { get; init; }

        /// <summary>Human-readable mapped name (from ValueMap), or null.</summary>
        public string MappedName { get; init; }

        /// <summary>Human-readable description from the definition.</summary>
        public string Description { get; init; }
    }

    /// <summary>
    /// Structured output produced by <see cref="FormatScriptInterpreter"/> for every
    /// parsed field, metadata entry, repeating-block entry, or union variant.
    /// </summary>
    public sealed class ParsedFieldResult
    {
        // ── Identity ────────────────────────────────────────────────────────────

        /// <summary>Field name from block definition.</summary>
        public string Name        { get; init; }

        /// <summary>Block type (field, signature, metadata, repeating, ...).</summary>
        public string BlockType   { get; init; }

        // ── Location ────────────────────────────────────────────────────────────

        /// <summary>Absolute file offset of this field.</summary>
        public long   Offset      { get; init; }

        /// <summary>Length of this field in bytes.</summary>
        public long   Length      { get; init; }

        // ── Values ──────────────────────────────────────────────────────────────

        /// <summary>Value type token (uint32, ascii8, guid, ...).</summary>
        public string ValueType   { get; init; }

        /// <summary>Raw bytes as uppercase hex string (e.g. "4D 5A").</summary>
        public string RawHex      { get; init; }

        /// <summary>Human-readable decoded display string (from TypeDecoderRegistry).</summary>
        public string DisplayValue { get; init; }

        /// <summary>Mapped value from valueMap lookup (e.g. "AMD64"), or null.</summary>
        public string MappedValue { get; init; }

        // ── Structure ───────────────────────────────────────────────────────────

        /// <summary>Bitfield breakdowns, or empty list if none defined.</summary>
        public IReadOnlyList<BitfieldResult> Bitfields { get; init; }
            = System.Array.Empty<BitfieldResult>();

        /// <summary>Child fields (repeating block entries, nested struct fields).</summary>
        public IReadOnlyList<ParsedFieldResult> Children { get; init; }
            = System.Array.Empty<ParsedFieldResult>();

        /// <summary>True when this result represents a repeating group header.</summary>
        public bool IsRepeatingGroup { get; init; }

        /// <summary>0-based iteration index within a repeating block, -1 otherwise.</summary>
        public int  RepeatIndex      { get; init; } = -1;

        // ── Display ─────────────────────────────────────────────────────────────

        /// <summary>Hex color string (#RRGGBB) used by the overlay block.</summary>
        public string Color     { get; init; }

        /// <summary>Opacity (0.0–1.0) for the overlay block.</summary>
        public double Opacity   { get; init; } = 0.3;

        /// <summary>Description text shown in the panel.</summary>
        public string Description { get; init; }

        // ── Validation ──────────────────────────────────────────────────────────

        /// <summary>Pass / Warning / Error result from field validation.</summary>
        public ValidationState Validation { get; init; } = ValidationState.Pass;

        /// <summary>Human-readable validation message (null when Pass).</summary>
        public string ValidationMessage   { get; init; }

        // ── Factory ─────────────────────────────────────────────────────────────

        /// <summary>Creates a group header result for a repeating block.</summary>
        public static ParsedFieldResult RepeatingGroup(
            string name, long offset, long totalLength, string color, double opacity,
            IReadOnlyList<ParsedFieldResult> children)
            => new ParsedFieldResult
            {
                Name             = name,
                BlockType        = "repeating",
                Offset           = offset,
                Length           = totalLength,
                Color            = color,
                Opacity          = opacity,
                IsRepeatingGroup = true,
                Children         = children ?? System.Array.Empty<ParsedFieldResult>()
            };
    }
}
