//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7 (1M context)
//////////////////////////////////////////////
// Project: WpfHexEditor.Core.Definitions
// File: Models/WhfmtValueType.cs
// Description: Canonical value type enum for whfmt variables and block fields.
//              Maps the string "type" / "valueType" found in .whfmt files to a
//              typed C# enum used by the variables engine, expression evaluator,
//              and (later) the function registry.
// Architecture notes (ADR-038 D5):
//              Closed enum — new types require an explicit code change. This is
//              intentional: every type carries an implied size + parser pair in
//              the runtime, so we keep the set finite.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Definitions.Models;

/// <summary>
/// Canonical value types recognized by the whfmt variables engine.
/// Maps the lowercase strings ("uint8", "ascii", "float32", ...) found in
/// <c>.whfmt</c> files to a typed enum. <see cref="Unknown"/> is returned for
/// values not in the canonical set.
/// </summary>
public enum WhfmtValueType
{
    /// <summary>Unrecognized or missing type string.</summary>
    Unknown,

    UInt8,
    UInt16,
    UInt32,
    UInt64,

    Int8,
    Int16,
    Int32,
    Int64,

    Float32,
    Float64,

    /// <summary>ASCII string. Length given by parent field's length.</summary>
    Ascii,
    /// <summary>UTF-8 string.</summary>
    Utf8,
    /// <summary>UTF-16 little-endian string.</summary>
    Utf16Le,
    /// <summary>UTF-16 big-endian string.</summary>
    Utf16Be,

    /// <summary>Raw byte array.</summary>
    Bytes,
    /// <summary>Hex-encoded byte sequence (display only).</summary>
    Hex,
}

/// <summary>
/// Helpers for parsing the string form of a whfmt value type into <see cref="WhfmtValueType"/>.
/// </summary>
public static class WhfmtValueTypes
{
    /// <summary>
    /// Parses the lowercase canonical string form. Accepts the casing variants
    /// observed across the catalog (e.g. "UInt32", "uint32", "UINT32").
    /// Returns <see cref="WhfmtValueType.Unknown"/> for null, empty, or unrecognized input.
    /// </summary>
    public static WhfmtValueType Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return WhfmtValueType.Unknown;
        return raw.Trim().ToLowerInvariant() switch
        {
            "uint8"   or "u8"  or "byte"   => WhfmtValueType.UInt8,
            "uint16"  or "u16" or "ushort" => WhfmtValueType.UInt16,
            "uint32"  or "u32" or "uint"   => WhfmtValueType.UInt32,
            "uint64"  or "u64" or "ulong"  => WhfmtValueType.UInt64,
            "int8"    or "i8"  or "sbyte"  => WhfmtValueType.Int8,
            "int16"   or "i16" or "short"  => WhfmtValueType.Int16,
            "int32"   or "i32" or "int"    => WhfmtValueType.Int32,
            "int64"   or "i64" or "long"   => WhfmtValueType.Int64,
            "float32" or "f32" or "single" or "float" => WhfmtValueType.Float32,
            "float64" or "f64" or "double" => WhfmtValueType.Float64,
            "ascii"   => WhfmtValueType.Ascii,
            "utf8"    or "utf-8"  => WhfmtValueType.Utf8,
            "utf16le" or "utf-16le" => WhfmtValueType.Utf16Le,
            "utf16be" or "utf-16be" => WhfmtValueType.Utf16Be,
            "bytes"   => WhfmtValueType.Bytes,
            "hex"     => WhfmtValueType.Hex,
            _         => WhfmtValueType.Unknown,
        };
    }

    /// <summary>
    /// Byte size of a fixed-width numeric type, or 0 for variable-width / unknown types.
    /// </summary>
    public static int FixedSizeBytes(WhfmtValueType type) => type switch
    {
        WhfmtValueType.UInt8  or WhfmtValueType.Int8  => 1,
        WhfmtValueType.UInt16 or WhfmtValueType.Int16 => 2,
        WhfmtValueType.UInt32 or WhfmtValueType.Int32 or WhfmtValueType.Float32 => 4,
        WhfmtValueType.UInt64 or WhfmtValueType.Int64 or WhfmtValueType.Float64 => 8,
        _ => 0,
    };
}
