// ==========================================================
// Project: whfmt.Fuzz.Tests
// File: WhfmtFuzzFixtures.cs
// Description: Inline whfmt JSON fixtures and helpers for Fuzz unit tests.
// ==========================================================

using WpfHexEditor.Core.Contracts;

namespace WhfmtFuzz.Tests;

/// <summary>Shared fixtures for whfmt.Fuzz tests.</summary>
internal static class WhfmtFuzzFixtures
{
    // ── PNG-like whfmt with fuzz block ─────────────────────────────────────────
    internal const string PngLikeWithFuzz = """
        {
          "formatId": "test-png-fuzz",
          "formatName": "Test PNG Fuzz",
          "category": "image",
          "blocks": [
            { "name": "Magic",     "offset": 0, "length": 4, "storeAs": "magic",       "valueType": "hex",    "expected": "89504E47" },
            { "name": "Width",     "offset": 4, "length": 4, "storeAs": "image_width", "valueType": "uint32" },
            { "name": "ColorType", "offset": 8, "length": 1, "storeAs": "color_type",  "valueType": "uint8",
              "valueMap": { "0": "Grey", "2": "RGB", "6": "RGBA" } }
          ],
          "checksums": [
            {
              "algorithm": "crc32",
              "storedAt":  { "fixedOffset": 9,  "length": 4 },
              "dataRange": { "fixedOffset": 0,  "fixedLength": 9 }
            }
          ],
          "fuzz": {
            "preserveChecksums": true,
            "strategies": [
              { "field": "magic",       "mutation": "CorruptSignature", "rate": 1.0,  "weight": 3, "description": "Corrupt magic bytes" },
              { "field": "image_width", "mutation": "BoundaryValues",  "rate": 1.0,  "weight": 4, "description": "Width boundary values" },
              { "field": "color_type",  "mutation": "EnumSweep",       "rate": 1.0,  "weight": 3, "description": "Sweep color type values" }
            ]
          }
        }
        """;

    // ── whfmt with no fuzz block ──────────────────────────────────────────────
    internal const string NoFuzzBlock = """
        {
          "formatId": "test-nofuzz",
          "formatName": "No Fuzz",
          "category": "misc",
          "blocks": [
            { "name": "Value", "offset": 0, "length": 4, "storeAs": "value", "valueType": "uint32" }
          ]
        }
        """;

    // ── 13-byte PNG-like binary (no CRC stored) ───────────────────────────────
    // magic(4) + width(4 LE) + color_type(1) + crc(4)
    internal static byte[] BuildPngFuzz(uint width = 640, byte colorType = 2)
    {
        var buf = new byte[13];
        buf[0] = 0x89; buf[1] = 0x50; buf[2] = 0x4E; buf[3] = 0x47;
        Buffer.BlockCopy(BitConverter.GetBytes(width), 0, buf, 4, 4);
        buf[8] = colorType;
        // CRC placeholder (not computed for simplicity in tests)
        buf[9] = 0x00; buf[10] = 0x00; buf[11] = 0x00; buf[12] = 0x00;
        return buf;
    }

    // ── 4-byte minimal binary for NoFuzz ─────────────────────────────────────
    internal static byte[] BuildSimple(uint value = 42) =>
        BitConverter.GetBytes(value);
}

/// <summary>Minimal IEmbeddedFormatCatalog stub for Fuzz tests.</summary>
internal sealed class FuzzStubCatalog : IEmbeddedFormatCatalog
{
    private readonly Dictionary<string, (EmbeddedFormatEntry Entry, string Json)> _map = new(StringComparer.OrdinalIgnoreCase);

    internal FuzzStubCatalog Register(string name, string json,
        string[]? extensions = null, string formatId = "")
    {
        var entry = new EmbeddedFormatEntry(
            ResourceKey:     name,
            Name:            name,
            Category:        "misc",
            Description:     name,
            Extensions:      extensions ?? [],
            QualityScore:    80,
            Version:         "1.0",
            Author:          "test",
            Platform:        "",
            PreferredEditor: null,
            IsTextFormat:    false,
            FormatId:        formatId);
        _map[name] = (entry, json);
        return this;
    }

    public IReadOnlySet<EmbeddedFormatEntry>      GetAll()            => _map.Values.Select(v => v.Entry).ToHashSet();
    public IReadOnlySet<string>                   GetCategories()     => _map.Values.Select(v => v.Entry.Category).ToHashSet();
    public string                                 GetJson(string key) => _map.TryGetValue(key, out var v) ? v.Json : "";
    public EmbeddedFormatEntry?                   GetByExtension(string ext) => _map.Values.FirstOrDefault(v => v.Entry.Extensions.Contains(ext)).Entry;
    public IReadOnlyList<EmbeddedFormatEntry>     GetByCategory(string cat) => _map.Values.Select(v => v.Entry).Where(e => e.Category.Equals(cat, StringComparison.OrdinalIgnoreCase)).ToList();
    public IReadOnlyList<string>                  GetCompatibleEditorIds(string _) => ["hex-editor"];
    public EmbeddedFormatEntry?                   DetectFromBytes(ReadOnlySpan<byte> _) => null;
    public EmbeddedFormatEntry?                   GetByMimeType(string _) => null;
    public string?                                GetSchemaJson(string _) => null;
}
