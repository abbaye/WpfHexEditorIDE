// ==========================================================
// Project: WpfHexEditor.Core
// File: SynalysisGrammar/SynalysisParseResult.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Output DTOs produced by SynalysisGrammarInterpreter after executing a
//     UFWB grammar against a binary buffer.
//     Consumers (plugin bridge layer) convert these into ParsedFieldViewModel
//     and CustomBackgroundBlock instances.
//
// Architecture Notes:
//     No WPF dependency — pure BCL records.
//     SynalysisField  → ParsedFieldViewModel (via SynalysisToFieldViewModelBridge)
//     SynalysisColorRegion → CustomBackgroundBlock (via SynalysisToBackgroundBlockBridge)
// ==========================================================

using System.Collections.Generic;

namespace WpfHexEditor.Core.SynalysisGrammar;

/// <summary>
/// Aggregate result of executing a UFWB grammar against a binary buffer.
/// </summary>
public sealed class SynalysisParseResult
{
    /// <summary>Name of the grammar that produced this result (from UfwbGrammar.Name).</summary>
    public string GrammarName { get; init; } = string.Empty;

    /// <summary>
    /// All parsed fields in document / traversal order, including nested fields.
    /// Suitable for direct display in the Parsed Fields panel.
    /// </summary>
    public IReadOnlyList<SynalysisField> Fields { get; init; } = [];

    /// <summary>
    /// Coloured regions for the hex-view overlay.
    /// Only fields with a non-empty <c>fillcolor</c> attribute generate an entry.
    /// </summary>
    public IReadOnlyList<SynalysisColorRegion> ColorRegions { get; init; } = [];

    /// <summary>Non-fatal warnings emitted during parsing (e.g. unsupported script blocks).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>True when at least one <c>mustmatch</c> constraint failed.</summary>
    public bool HasValidationErrors { get; init; }
}

/// <summary>
/// A single parsed field produced during grammar execution.
/// Maps directly to one row in the Parsed Fields panel.
/// </summary>
public sealed class SynalysisField
{
    /// <summary>Field name as declared in the grammar (e.g. "Image Width").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Absolute byte offset in the file where this field starts.</summary>
    public long Offset { get; init; }

    /// <summary>Field byte length. 0 for zero-length / variable strings.</summary>
    public int Length { get; init; }

    /// <summary>Decoded display value, e.g. "800" or "0x0320" or "deflate/inflate".</summary>
    public string ValueDisplay { get; init; } = string.Empty;

    /// <summary>
    /// Background colour declared in the grammar as 6-char hex "RRGGBB", or empty.
    /// Empty fields get no overlay block.
    /// </summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Nesting depth for tree indentation in the panel (0 = top level).</summary>
    public int IndentLevel { get; init; }

    /// <summary>
    /// Group / section header — the name of the enclosing structure, e.g. "IHDR Data".
    /// Used to visually separate chunks in the panel.
    /// </summary>
    public string GroupName { get; init; } = string.Empty;

    /// <summary>Optional description / tooltip text.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Data kind used to select the correct icon in the panel.</summary>
    public SynalysisFieldKind Kind { get; init; }

    /// <summary>False when a mustmatch constraint failed for this field.</summary>
    public bool IsValid { get; init; } = true;
}

/// <summary>
/// A colour region to be overlaid on the hex editor view.
/// Corresponds to one <c>fillcolor</c>-bearing element in the grammar.
/// </summary>
public sealed class SynalysisColorRegion
{
    /// <summary>Absolute byte offset where the region starts.</summary>
    public long Offset { get; init; }

    /// <summary>Region byte length.</summary>
    public long Length { get; init; }

    /// <summary>Background colour as 6-char hex "RRGGBB".</summary>
    public string Color { get; init; } = string.Empty;

    /// <summary>Description used as the CustomBackgroundBlock description prefix.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Overlay opacity (0.0 – 1.0). Default 0.3.</summary>
    public double Opacity { get; init; } = 0.3;
}

/// <summary>Classifies the kind of element that produced a <see cref="SynalysisField"/>.</summary>
public enum SynalysisFieldKind
{
    /// <summary>Numeric field (<c>&lt;number&gt;</c>).</summary>
    Number,

    /// <summary>Raw binary / byte array (<c>&lt;binary&gt;</c>).</summary>
    Binary,

    /// <summary>Text string (<c>&lt;string&gt;</c>).</summary>
    String,

    /// <summary>Named structure group (<c>&lt;structure&gt;</c> / <c>&lt;structref&gt;</c>).</summary>
    Structure,
}
