// Project      : whfmt.SourceGenerator.Tests
// File         : WhfmtFixtures.cs
// Description  : Inline .whfmt JSON strings used as test fixtures.

namespace whfmt.SourceGenerator.Tests;

internal static class WhfmtFixtures
{
    /// <summary>Minimal valid .whfmt — simple fields, no signature, no checksum.</summary>
    internal const string Simple = """
        {
          "formatName": "Simple Test",
          "formatId": "SIMPLE",
          "version": "1.0",
          "category": "Test",
          "description": "Minimal fixture for unit tests.",
          "blocks": [
            { "name": "Magic",    "offset": 0, "length": 4, "type": "uint32" },
            { "name": "Version",  "offset": 4, "length": 2, "type": "uint16" },
            { "name": "DataSize", "offset": 6, "length": 4, "type": "uint32" }
          ]
        }
        """;

    /// <summary>Fixture with a hex signature and CRC32 checksum — exercises validation codepath.</summary>
    internal const string WithSignatureAndChecksum = """
        {
          "formatName": "PNG-like",
          "formatId": "PNGLIKE",
          "version": "1.0",
          "category": "Test",
          "description": "Fixture with signature and checksum for validation tests.",
          "blocks": [
            { "name": "Signature", "offset": 0, "length": 8, "type": "bytes", "isSignature": true,
              "value": "89504E470D0A1A0A" },
            { "name": "Width",  "offset": 16, "length": 4, "type": "uint32", "endian": "big" },
            { "name": "Height", "offset": 20, "length": 4, "type": "uint32", "endian": "big" }
          ]
        }
        """;

    /// <summary>Fixture with an enum (valueMap) field.</summary>
    internal const string WithEnum = """
        {
          "formatName": "Enum Test",
          "formatId": "ENUMTEST",
          "version": "1.0",
          "category": "Test",
          "description": "Fixture with a valueMap field that becomes an enum.",
          "blocks": [
            { "name": "Type", "offset": 0, "length": 1, "type": "byte",
              "valueMap": { "Text": "0", "Binary": "1", "Mixed": "2" } }
          ]
        }
        """;

    /// <summary>Fixture with a repeating field (becomes List&lt;T&gt;).</summary>
    internal const string WithRepeating = """
        {
          "formatName": "Repeating Test",
          "formatId": "REPTEST",
          "version": "1.0",
          "category": "Test",
          "description": "Fixture with a repeating block.",
          "blocks": [
            { "name": "Count",   "offset": 0, "length": 4, "type": "uint32" },
            { "name": "Entries", "offset": 4, "length": 8, "type": "uint64", "repeating": true }
          ]
        }
        """;

    /// <summary>Fixture with an async-worthy structure — used to test WhfmtAsync=true.</summary>
    internal const string ForAsync = Simple;

    /// <summary>Not valid JSON — should trigger WHSG001.</summary>
    internal const string Malformed = "{ this is not valid json :::";

    /// <summary>Empty content — should trigger WHSG001 (whitespace-only check).</summary>
    internal const string Empty = "   ";

    /// <summary>JSONC with comments — must parse correctly.</summary>
    internal const string WithComments = """
        {
          // This is a JSONC comment
          "formatName": "JSONC Test",
          "formatId": "JSONCTEST",
          "version": "1.0",
          "category": "Test",
          "description": "Fixture to verify JSONC comment stripping.",
          /* Multi-line comment */
          "blocks": [
            { "name": "Header", "offset": 0, "length": 4, "type": "uint32" }, // trailing comma ok
          ]
        }
        """;
}
