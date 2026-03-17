// ==========================================================
// Project: WpfHexEditor.Core
// File: SynalysisGrammar/UfwbModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     C# model classes representing a Synalysis / Hexinator UFWB grammar file
//     (.grammar XML). Mirrors the UFWB 1.x element hierarchy without any WPF
//     or JSON dependencies — pure BCL.
//
// Architecture Notes:
//     Pattern: Data model (no logic)
//     - UfwbStructure extends UfwbElement so it can appear both at grammar
//       top level and as a nested child inside another structure.
//     - Length / repeat values are stored as strings because they can be
//       integers, "remaining", "prev.FieldName", or "unlimited".
//     - All id references ("id:45") are stored as raw strings; the interpreter
//       resolves them through a pre-built symbol table.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexEditor.Core.SynalysisGrammar;

/// <summary>Root wrapper for a UFWB grammar file.</summary>
public sealed class UfwbRoot
{
    /// <summary>UFWB spec version, e.g. "1.3.1".</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>The single grammar element contained in this file.</summary>
    public UfwbGrammar Grammar { get; set; } = new();
}

/// <summary>Top-level grammar element — metadata + all structure definitions.</summary>
public sealed class UfwbGrammar
{
    /// <summary>Human-readable name, e.g. "PNG Images".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>ID reference to the root structure, e.g. "id:45".</summary>
    public string Start { get; set; } = string.Empty;

    /// <summary>Author name or email.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated file extension(s) without leading dot, e.g. "png" or "jpg,jpeg".
    /// </summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>UTI identifier on Apple platforms (informational).</summary>
    public string Uti { get; set; } = string.Empty;

    /// <summary>Human-readable description of the format.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>All top-level structure definitions in declaration order.</summary>
    public List<UfwbStructure> Structures { get; set; } = [];

    /// <summary>
    /// Parsed extension list from <see cref="FileExtension"/>.
    /// Each entry is lowercased with a leading dot, e.g. ".png".
    /// </summary>
    public IEnumerable<string> FileExtensions =>
        FileExtension
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant());
}

// ---------------------------------------------------------------------------
// Element hierarchy
// ---------------------------------------------------------------------------

/// <summary>Abstract base for all elements that can appear inside a structure.</summary>
public abstract class UfwbElement
{
    /// <summary>Numeric ID string, e.g. "45". Used as symbol-table key.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable field name displayed in the parsed-fields panel.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description / tooltip text.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// A named structure that can contain child elements.
/// Used both as a top-level grammar definition and as a nested inline structure.
/// </summary>
public sealed class UfwbStructure : UfwbElement
{
    /// <summary>ID reference to a parent structure to inherit from, e.g. "id:46".</summary>
    public string Extends { get; set; } = string.Empty;

    /// <summary>Default character encoding for string fields, e.g. "ISO_8859-1:1987", "UTF-8".</summary>
    public string Encoding { get; set; } = string.Empty;

    /// <summary>Default byte order: "big" or "little".</summary>
    public string Endian { get; set; } = string.Empty;

    /// <summary>Default signed flag for numeric fields: "yes" or "no".</summary>
    public string Signed { get; set; } = string.Empty;

    /// <summary>
    /// Fixed byte length of this structure.
    /// May be an integer, "prev.FieldName", or empty (variable/unlimited).
    /// </summary>
    public string Length { get; set; } = string.Empty;

    /// <summary>Minimum repetition count (default 1).</summary>
    public int RepeatMin { get; set; } = 1;

    /// <summary>
    /// Maximum repetition count. -1 means "unlimited".
    /// Populated from the "repeatmax" attribute ("unlimited" → -1).
    /// </summary>
    public int RepeatMax { get; set; } = 1;

    /// <summary>
    /// When true children may appear in any order (chunk-based formats like PNG).
    /// Corresponds to <c>order="variable"</c>.
    /// </summary>
    public bool VariableOrder { get; set; }

    /// <summary>
    /// When true this structure may appear at an arbitrary file position.
    /// Corresponds to <c>floating="yes"</c>.
    /// </summary>
    public bool Floating { get; set; }

    /// <summary>Background fill colour for the hex view: 6-char hex "RRGGBB" or empty.</summary>
    public string FillColor { get; set; } = string.Empty;

    /// <summary>Child elements declared in document order.</summary>
    public List<UfwbElement> Elements { get; set; } = [];
}

/// <summary>Numeric field — integer, float, or double.</summary>
public sealed class UfwbNumber : UfwbElement
{
    /// <summary>Data type: "integer", "float", "double".</summary>
    public string Type { get; set; } = "integer";

    /// <summary>
    /// Field size in bytes.
    /// Can be an integer string, "prev.FieldName", or empty (inherit from parent).
    /// </summary>
    public string Length { get; set; } = string.Empty;

    /// <summary>Display format hint: "hex", "decimal", "binary". Empty = default decimal.</summary>
    public string Display { get; set; } = string.Empty;

    /// <summary>Background fill colour: 6-char hex "RRGGBB" or empty.</summary>
    public string FillColor { get; set; } = string.Empty;

    /// <summary>Override signed flag for this field: "yes", "no", or empty (inherit).</summary>
    public string Signed { get; set; } = string.Empty;

    /// <summary>
    /// When true the field value must match one of the <see cref="FixedValues"/>.
    /// A mismatch marks the parse as invalid.
    /// </summary>
    public bool MustMatch { get; set; }

    /// <summary>Optional enumeration of expected / named values.</summary>
    public UfwbFixedValues? FixedValues { get; set; }
}

/// <summary>Raw binary data field.</summary>
public sealed class UfwbBinary : UfwbElement
{
    /// <summary>
    /// Field size in bytes.
    /// Can be an integer string, "remaining", or "prev.FieldName".
    /// </summary>
    public string Length { get; set; } = string.Empty;

    /// <summary>Background fill colour: 6-char hex "RRGGBB" or empty.</summary>
    public string FillColor { get; set; } = string.Empty;

    /// <summary>When true the raw bytes must equal the first fixed value.</summary>
    public bool MustMatch { get; set; }

    /// <summary>Expected byte sequences (used for signature validation).</summary>
    public UfwbFixedValues? FixedValues { get; set; }
}

/// <summary>Text / string field.</summary>
public sealed class UfwbString : UfwbElement
{
    /// <summary>String termination strategy: "zero-terminated", "fixed-length", "pascal".</summary>
    public string Type { get; set; } = "zero-terminated";

    /// <summary>Fixed byte length for "fixed-length" strings. Empty otherwise.</summary>
    public string Length { get; set; } = string.Empty;

    /// <summary>Character encoding override, e.g. "UTF-8". Empty = inherit from parent.</summary>
    public string Encoding { get; set; } = string.Empty;
}

/// <summary>Reference to a named structure definition (by id).</summary>
public sealed class UfwbStructRef : UfwbElement
{
    /// <summary>ID reference to the target structure, e.g. "id:58".</summary>
    public string StructureRef { get; set; } = string.Empty;

    /// <summary>Minimum repetition count (default 1).</summary>
    public int RepeatMin { get; set; } = 1;

    /// <summary>Maximum repetition count. -1 means "unlimited".</summary>
    public int RepeatMax { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Fixed value helpers
// ---------------------------------------------------------------------------

/// <summary>Container for a list of named / expected field values.</summary>
public sealed class UfwbFixedValues
{
    /// <summary>Individual named values.</summary>
    public List<UfwbFixedValue> Values { get; set; } = [];
}

/// <summary>A single named constant value within a <see cref="UfwbFixedValues"/> block.</summary>
public sealed class UfwbFixedValue
{
    /// <summary>Optional display name for the value.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Expected value as a string (decimal or "0x…" hex).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string Description { get; set; } = string.Empty;
}
