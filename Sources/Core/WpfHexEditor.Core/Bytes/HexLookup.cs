// ==========================================================
// Project: WpfHexEditor.Core
// File: Bytes/HexLookup.cs
// Description:
//     Static 256-entry lookup table for byte-to-hex-string conversion.
//     Replaces byte.ToString("X2") allocations in render-critical hot paths.
//
// Architecture Notes:
//     ToHex2(byte) returns a pre-interned string — zero allocation.
//     WriteHex2(Span<char>, int, byte) writes directly into caller-owned
//     buffers — used by the TBL key builder (Phase 3).
// ==========================================================

using System;

namespace WpfHexEditor.Core.Bytes;

/// <summary>
/// Zero-allocation byte-to-hex conversion utilities.
/// </summary>
public static class HexLookup
{
    // Pre-computed uppercase hex strings "00" through "FF" (256 entries, all interned).
    private static readonly string[] _upper;

    static HexLookup()
    {
        _upper = new string[256];
        for (int i = 0; i < 256; i++)
            _upper[i] = i.ToString("X2");
    }

    /// <summary>
    /// Returns the uppercase two-character hex string for <paramref name="value"/>.
    /// Example: ToHex2(0xFF) → "FF". Zero allocation — returns a pre-computed string.
    /// </summary>
    public static string ToHex2(byte value) => _upper[value];

    /// <summary>
    /// Writes the two-character uppercase hex representation of <paramref name="value"/>
    /// directly into <paramref name="dest"/> at <paramref name="offset"/>.
    /// Example: WriteHex2(buf, 4, 0xAB) writes 'A','B' at buf[4] and buf[5].
    /// </summary>
    public static void WriteHex2(Span<char> dest, int offset, byte value)
    {
        const string hex = "0123456789ABCDEF";
        dest[offset]     = hex[value >> 4];
        dest[offset + 1] = hex[value & 0xF];
    }
}
