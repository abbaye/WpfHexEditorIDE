# WHFMT Format Definition Authoring Guide

This guide describes how to create `.whfmt` format definition files for WpfHexEditor.
Format definitions enable automatic detection, parsing, colorization, and forensic
analysis of binary file structures.

## Quick Start

A minimal `.whfmt` file requires only 3 fields:

```json
{
  "formatName": "My Format",
  "detection": {
    "signature": "4D5A",
    "offset": 0
  },
  "blocks": [
    {
      "type": "signature",
      "name": "Magic Number",
      "offset": 0,
      "length": 2,
      "color": "#FF6B6B"
    }
  ]
}
```

Place the file in `FormatDefinitions/<Category>/` (e.g., `FormatDefinitions/Archives/MyFormat.whfmt`).
The category is auto-detected from the folder name.

---

## File Structure

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `formatName` | string | Human-readable name (e.g., "ZIP Archive") |
| `detection` | object | How to identify this format in binary data |
| `blocks` | array | Fields to parse and colorize |

### Optional Metadata

| Field | Type | Description |
|-------|------|-------------|
| `version` | string | Definition version (e.g., "2.0") |
| `extensions` | string[] | File extensions (e.g., [".zip", ".jar"]) |
| `description` | string | Format description |
| `category` | string | Category (auto-set from folder) |
| `author` | string | Author of this definition |
| `preferredEditor` | string | Editor ID: "hex-editor", "code-editor", "text-editor", "structure-editor" |
| `diffMode` | string | Preferred diff: "text", "semantic", "binary" |
| `mimeTypes` | string[] | MIME types |
| `references` | object | Specification URLs and web links |

---

## Detection Rules

```json
"detection": {
  "signature": "504B0304",
  "offset": 0,
  "required": true,
  "validation": {
    "minFileSize": 22,
    "maxFileSize": 4294967295,
    "maxSignatureOffset": 0
  }
}
```

- `signature` — Hex bytes to match (e.g., "504B0304" for ZIP)
- `offset` — Byte offset where signature starts
- `required` — If true, signature must match exactly
- Multi-signature detection: Use `"signatures"` array for formats with multiple magic bytes

---

## Block Definitions

Each block describes a field in the binary structure.

### Block Types

| Type | Purpose |
|------|---------|
| `signature` | Magic number / identifier bytes |
| `field` | Data field (integer, string, etc.) |
| `reserved` | Reserved / padding bytes |
| `computed` | Derived from other fields (not read from file) |

### Block Properties

```json
{
  "type": "field",
  "name": "Image Width",
  "offset": 16,
  "length": 4,
  "color": "#4ECDC4",
  "opacity": 0.4,
  "description": "Width of the image in pixels",
  "storeAs": "width",
  "valueType": "uint32",
  "bigEndian": false,
  "valueMap": {
    "0": "Invalid"
  }
}
```

| Property | Type | Description |
|----------|------|-------------|
| `type` | string | Block type (see above) |
| `name` | string | Display name |
| `offset` | int/string | Byte offset (number or variable expression) |
| `length` | int/string | Byte length (number or variable expression) |
| `color` | string | Hex color for overlay (e.g., "#FF6B6B") |
| `opacity` | float | Overlay opacity (0.0-1.0) |
| `description` | string | Tooltip description |
| `storeAs` | string | Variable name to store decoded value |
| `valueType` | string | Decoder type (see below) |
| `bigEndian` | bool | Use big-endian byte order |
| `valueMap` | object | Map decoded values to display strings |

### Value Types

| Type | Size | Description |
|------|------|-------------|
| `uint8` | 1 | Unsigned 8-bit integer |
| `uint16` | 2 | Unsigned 16-bit integer |
| `uint32` | 4 | Unsigned 32-bit integer |
| `uint64` | 8 | Unsigned 64-bit integer |
| `int8`-`int64` | 1-8 | Signed integers |
| `float32` | 4 | IEEE 754 single-precision |
| `float64` | 8 | IEEE 754 double-precision |
| `ascii` | N | ASCII string |
| `utf8` | N | UTF-8 string |
| `utf16le`/`utf16be` | N | UTF-16 string |
| `guid` | 16 | GUID (standard format) |
| `ipv4` | 4 | IPv4 address |
| `ipv6` | 16 | IPv6 address |
| `unixtime32` | 4 | Unix timestamp (seconds) |
| `unixtime64` | 8 | Unix timestamp (milliseconds) |
| `filetime` | 8 | Windows FILETIME |
| `dosdate`/`dostime` | 2 | MS-DOS date/time |
| `bytes`/`raw` | N | Raw hex display |

---

## Variables & Expressions

Variables capture decoded field values for use in later blocks:

```json
"variables": {
  "currentOffset": 0,
  "headerSize": 0
},
"blocks": [
  {
    "name": "Header Size",
    "offset": 4,
    "length": 4,
    "storeAs": "headerSize",
    "valueType": "uint32"
  },
  {
    "name": "Payload",
    "offset": "headerSize",
    "length": "fileSize - headerSize"
  }
]
```

Expressions support: `+`, `-`, `*`, `/`, parentheses, and variable references.

---

## v2.0 Features

### Repeating Blocks

Parse arrays/tables with a known count:

```json
{
  "type": "field",
  "name": "Section Entry",
  "offset": "sectionTableOffset",
  "length": 40,
  "repeating": {
    "countVar": "numberOfSections",
    "strideBytes": 40
  }
}
```

### Union Blocks

Parse different structures based on a discriminator:

```json
{
  "type": "field",
  "name": "Record",
  "union": {
    "discriminatorVar": "recordType",
    "cases": {
      "1": [ /* blocks for type 1 */ ],
      "2": [ /* blocks for type 2 */ ]
    }
  }
}
```

### Nested Blocks

Embed sub-structures:

```json
{
  "type": "field",
  "name": "Sub-Header",
  "nested": {
    "structName": "SubHeaderDef",
    "blocks": [ /* inner blocks */ ]
  }
}
```

### Versioned Blocks

Different field layouts per format version:

```json
"versionDetection": {
  "field": "version",
  "offsetInFile": 4,
  "length": 2,
  "valueType": "uint16"
},
"versionedBlocks": {
  "1": [ /* v1 blocks */ ],
  "2": [ /* v2 blocks with new fields */ ]
}
```

### Checksums

Validate data integrity:

```json
"checksums": [
  {
    "name": "Header CRC",
    "algorithm": "crc32",
    "dataRange": {
      "fixedOffset": 0,
      "fixedLength": 26
    },
    "storedAt": {
      "fixedOffset": 26,
      "length": 4,
      "endianness": "little"
    },
    "severity": "error"
  }
]
```

Algorithms: `crc32`, `crc16`, `adler32`, `md5`, `sha1`, `sha256`, `sum8`, `sum16`, `sum32`.

### Assertions

Validate field values:

```json
"assertions": [
  {
    "name": "Valid image dimensions",
    "expression": "width > 0",
    "severity": "error",
    "message": "Image width must be positive"
  },
  {
    "name": "Sane file size",
    "expression": "headerSize <= fileSize",
    "severity": "warning"
  }
]
```

Failed assertions appear in the Forensic Alerts section of the ParsedFields panel.

### Navigation Bookmarks

Auto-create bookmarks at key offsets:

```json
"navigation": {
  "bookmarks": [
    { "name": "File Header", "offsetVar": "0", "description": "Start of file header" },
    { "name": "Data Section", "offsetVar": "dataOffset", "description": "Payload data" }
  ]
}
```

### Forensic Metadata

```json
"forensic": {
  "category": "executable",
  "riskLevel": "medium",
  "suspiciousPatterns": [
    { "name": "Packed executable", "signature": "UPX", "description": "UPX packer detected" }
  ]
}
```

### AI Hints

Context for AI-assisted analysis:

```json
"aiHints": {
  "analysisContext": "PE executable with optional .NET metadata",
  "suggestedInspections": [
    "Check PE header for anomalous section characteristics",
    "Verify import table entries against known DLLs"
  ]
}
```

---

## Syntax Highlighting (for text formats)

Text-based formats can include syntax rules for the CodeEditor:

```json
"syntaxDefinition": {
  "id": "json",
  "name": "JSON",
  "extensions": [".json"],
  "lineCommentPrefix": "//",
  "blockCommentStart": "/*",
  "blockCommentEnd": "*/",
  "rules": [
    { "type": "Keyword", "pattern": "\\b(true|false|null)\\b", "colorKey": "TE_Keyword" },
    { "type": "String",  "pattern": "\"(?:[^\"\\\\]|\\\\.)*\"", "colorKey": "TE_String" },
    { "type": "Number",  "pattern": "-?\\d+\\.?\\d*", "colorKey": "TE_Number" }
  ],
  "foldingRules": {
    "startPatterns": ["\\{", "\\["],
    "endPatterns": ["\\}", "\\]"]
  }
}
```

---

## File Organization

Place formats in the category folder matching their type:

```
FormatDefinitions/
  Archives/     ZIP.whfmt, RAR.whfmt, 7Z.whfmt
  Audio/        MP3.whfmt, FLAC.whfmt, WAV.whfmt
  Data/         JSON.whfmt, XML.whfmt, YAML.whfmt
  Database/     SQLITE.whfmt
  Executables/  PE_EXE.whfmt, ELF.whfmt
  Images/       PNG.whfmt, JPEG.whfmt, GIF.whfmt
  Video/        MP4.whfmt, MKV.whfmt
  Programming/  (syntax-only definitions)
```

---

## Schema Validation

A JSON Schema is available at `whfmt.schema.json` in the Core.Definitions project.
Use it in your editor for autocompletion:

```json
{
  "$schema": "../whfmt.schema.json",
  "formatName": "My Format",
  ...
}
```
