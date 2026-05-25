// ==========================================================
// Project: whfmt.Analysis.Tests
// File: WhfmtAnalysisFixtures.cs
// Description: Inline whfmt JSON fixtures and minimal binary builders for unit tests.
// ==========================================================

namespace WhfmtAnalysis.Tests;

/// <summary>Shared fixtures: whfmt JSON strings and binary helpers for Analysis tests.</summary>
internal static class WhfmtAnalysisFixtures
{
    // ── Minimal PNG-like whfmt definition (diff + checksum sections) ──────────
    // Fields: magic(4), width(4), height(4), color_type(1), padding(3)
    // Checksum: crc32 over bytes 0..11, stored at offset 12
    internal const string PngLike = """
        {
          "formatId": "test-png",
          "formatName": "Test PNG-like",
          "category": "image",
          "blocks": [
            { "name": "Magic",      "offset": 0,  "length": 4, "storeAs": "magic",      "valueType": "hex",    "expected": "89504E47" },
            { "name": "Width",      "offset": 4,  "length": 4, "storeAs": "image_width","valueType": "uint32" },
            { "name": "Height",     "offset": 8,  "length": 4, "storeAs": "image_height","valueType": "uint32" },
            { "name": "ColorType",  "offset": 12, "length": 1, "storeAs": "color_type", "valueType": "uint8",
              "valueMap": { "0": "Greyscale", "2": "RGB", "3": "Indexed", "4": "GreyscaleAlpha", "6": "RGBA" } },
            { "name": "Padding",    "offset": 13, "length": 3, "storeAs": "padding",    "valueType": "hex" }
          ],
          "checksums": [
            {
              "algorithm": "crc32",
              "storedAt":  { "fixedOffset": 16, "length": 4 },
              "dataRange": { "fixedOffset": 0,  "fixedLength": 16 }
            }
          ],
          "diff": {
            "keyFields":    ["magic", "image_width", "image_height", "color_type"],
            "ignoreFields": ["padding"],
            "groupBy":      "category"
          }
        }
        """;

    // ── Tiny whfmt with no diff block (fallback to variables) ─────────────────
    internal const string NoDiff = """
        {
          "formatId": "test-nodiff",
          "formatName": "No Diff",
          "category": "misc",
          "blocks": [
            { "name": "Value", "offset": 0, "length": 4, "storeAs": "value", "valueType": "uint32" }
          ]
        }
        """;

    // ── whfmt with no blocks at all ───────────────────────────────────────────
    internal const string Empty = """
        {
          "formatId": "test-empty",
          "formatName": "Empty",
          "category": "misc",
          "blocks": []
        }
        """;

    // ── Build a 20-byte PNG-like binary ────────────────────────────────────────
    // Layout: magic(4) + width(4 LE) + height(4 LE) + color_type(1) + padding(3) + crc32(4)
    internal static byte[] BuildPngLike(
        byte[]? magic     = null,
        uint   width      = 640,
        uint   height     = 480,
        byte   colorType  = 2,
        byte[]? padding   = null,
        bool   correctCrc = true)
    {
        var buf = new byte[20];

        var mg = magic ?? [0x89, 0x50, 0x4E, 0x47];
        Buffer.BlockCopy(mg, 0, buf, 0, Math.Min(4, mg.Length));

        Buffer.BlockCopy(BitConverter.GetBytes(width),  0, buf, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(height), 0, buf, 8, 4);
        buf[12] = colorType;

        var pad = padding ?? [0x00, 0x00, 0x00];
        Buffer.BlockCopy(pad, 0, buf, 13, Math.Min(3, pad.Length));

        if (correctCrc)
        {
            uint crc = Crc32(buf.AsSpan(0, 16));
            Buffer.BlockCopy(BitConverter.GetBytes(crc), 0, buf, 16, 4);
        }
        else
        {
            buf[16] = 0xDE; buf[17] = 0xAD; buf[18] = 0xBE; buf[19] = 0xEF;
        }

        return buf;
    }

    // ── Build a 4-byte NoDiff binary ──────────────────────────────────────────
    internal static byte[] BuildNoDiff(uint value = 42)
    {
        var buf = new byte[4];
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buf, 0, 4);
        return buf;
    }

    // ── CRC32 (standard IEEE 802.3 poly) ─────────────────────────────────────
    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data) crc = (crc >> 8) ^ _table[(crc & 0xFF) ^ b];
        return ~crc;
    }

    private static readonly uint[] _table = BuildTable();
    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++) { uint c = i; for (int j = 8; j > 0; j--) c = (c & 1) != 0 ? (c >> 1) ^ 0xEDB88320 : c >> 1; t[i] = c; }
        return t;
    }
}
