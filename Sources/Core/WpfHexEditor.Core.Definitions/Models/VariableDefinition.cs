//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/VariableDefinition.cs
// Description: Typed declaration of a single variable from a whfmt "variables" block.
//              Supports both whfmt v2 schemas (dict + typed-array) — see WhfmtVariableParser
//              for the schema normalization logic.
// Architecture notes (ADR-038 D7):
//              The typed-array schema is canonical in v3. The dict schema is
//              accepted via auto-migration in WhfmtVariableParser.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models;

/// <summary>
/// Endianness of a numeric variable.
/// </summary>
public enum WhfmtEndian
{
    /// <summary>Inherited from the parent block / format default (little-endian when absent).</summary>
    Inherit,
    Little,
    Big,
}

/// <summary>
/// Typed declaration of a single whfmt variable.
/// <para>
/// Variables are populated at parse time by reading bytes at <see cref="Offset"/>/<see cref="Length"/>
/// according to <see cref="Type"/> and <see cref="Endian"/>, and stored by <see cref="Name"/>
/// in a <see cref="WhfmtVariableStore"/> for later use by expressions, assertions, and functions.
/// </para>
/// </summary>
public sealed record VariableDefinition(
    /// <summary>Variable name (key used by storeAs / expression references).</summary>
    string Name,
    /// <summary>Canonical type. <see cref="WhfmtValueType.Unknown"/> when not declared.</summary>
    WhfmtValueType Type,
    /// <summary>Byte offset, or -1 when computed at runtime.</summary>
    int Offset,
    /// <summary>Byte length. 0 means "fixed by Type" (use <see cref="WhfmtValueTypes.FixedSizeBytes"/>).</summary>
    int Length,
    /// <summary>Endianness, or <see cref="WhfmtEndian.Inherit"/> when not declared.</summary>
    WhfmtEndian Endian,
    /// <summary>Optional human description.</summary>
    string Description,
    /// <summary>Initial value (dict-schema variables carry a literal initialValue; typed schema uses null).</summary>
    object? InitialValue);
