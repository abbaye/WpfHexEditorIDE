# whfmt.Analysis

**Field-level semantic diff between binary files** using [whfmt.FileFormatCatalog](https://www.nuget.org/packages/whfmt.FileFormatCatalog) definitions.

Instead of comparing raw bytes, `whfmt.Analysis` understands the *structure* of a file:

- Groups entries by logical key (e.g. filename inside a ZIP)
- Ignores noise fields (timestamps, padding, calculated offsets)
- Surfaces only meaningful structural changes
- Side-by-side **hex diff** per changed field (BytesA / BytesB / DiffMask)
- **Checksum validation** — detects corrupted checksums (CRC32 / MD5 / SHA1 / SHA256)
- **Structural diff** — block-level OnlyInA / OnlyInB / InBoth using MD5 hashes
- Outputs rich text, JSON, CSV, Markdown, or dark-themed HTML reports

Powered by **757 binary whfmt format definitions** covering Archives, Images, Executables, Documents, Audio, Databases, and more.

---

## Install

```bash
dotnet add package whfmt.Analysis
```

---

## Quick Start

```csharp
using WhfmtAnalysis;
using WpfHexEditor.Core.Definitions;

var catalog = EmbeddedFormatCatalog.Instance;

// Compare two ZIP files semantically
var result = FormatDiff.Compare(catalog, "v1.zip", "v2.zip");

if (result.IsIdentical)
{
    Console.WriteLine("Files are semantically identical.");
}
else
{
    Console.WriteLine($"Format: {result.FormatName}");
    Console.WriteLine($"Changed fields: {result.ChangedCount}");

    foreach (var change in result.FieldChanges.Where(c => c.IsChanged))
        Console.WriteLine($"  {change.FieldName}: {change.ValueA} → {change.ValueB}");
}
```

---

## API Reference

### `FormatDiff.Compare()`

```csharp
// From file paths — format auto-detected
DiffResult Compare(IEmbeddedFormatCatalog catalog, string fileA, string fileB)

// From byte arrays — format auto-detected from extension
DiffResult Compare(
    IEmbeddedFormatCatalog catalog,
    byte[] dataA, string nameA,
    byte[] dataB, string nameB)
```

### `FormatDiff.CompareAsync()`

```csharp
// Async from file paths
Task<DiffResult> CompareAsync(IEmbeddedFormatCatalog catalog, string fileA, string fileB)

// Async from streams
Task<DiffResult> CompareAsync(
    IEmbeddedFormatCatalog catalog,
    Stream streamA, string nameA,
    Stream streamB, string nameB)
```

### `DiffResult`

| Property | Type | Description |
|---|---|---|
| `FileA` / `FileB` | `string` | File names |
| `SizeA` / `SizeB` | `long` | File sizes in bytes |
| `FormatName` | `string` | Detected format name |
| `FieldChanges` | `IReadOnlyList<FieldChange>` | All field comparisons |
| `ChangedCount` | `int` | Number of changed fields (excl. ignored) |
| `IsIdentical` | `bool` | True when all key fields match |
| `RawByteDelta` | `long` | Size difference in bytes |
| `ChecksumsA` / `ChecksumsB` | `IReadOnlyList<ChecksumStatus>` | Per-checksum validation |
| `StructuralDiff` | `StructuralDiff?` | Block-level diff (OnlyInA/B/InBoth) |

### `FieldChange`

| Property | Type | Description |
|---|---|---|
| `FieldName` | `string` | Field name from the whfmt definition |
| `ValueA` / `ValueB` | `string` | Hex-formatted values |
| `IsChanged` | `bool` | True when the values differ |
| `IsIgnored` | `bool` | True for noise fields (timestamps, etc.) |
| `IsCorrupted` | `bool` | True when a checksum field is invalid |
| `HexDiff` | `HexDiff?` | Per-byte diff mask for binary fields |

### `DiffRenderer`

```csharp
// Plain text (console-friendly, includes hex diff + checksum + structural sections)
string text = DiffRenderer.RenderText(result);

// JSON (for tooling / CI pipelines)
string json = DiffRenderer.RenderJson(result);

// CSV (Field,ValueA,ValueB,IsChanged,IsIgnored,IsCorrupted,DifferentBytes)
string csv = DiffRenderer.ToCsv(result);

// GitHub Markdown table with emoji status
string md = DiffRenderer.ToMarkdown(result);

// Dark-themed HTML (for reports)
string html = DiffRenderer.RenderHtml(result);
```

---

## Output Examples

### Text

```
whfmt.Analysis — Semantic Binary Diff
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  Format  : ZIP
  File A  : build-v1.zip (1,204,812 bytes)
  File B  : build-v2.zip (1,209,044 bytes)
  Delta   : +4,232 bytes

  Key fields:
  ✓ entry_count         00000023  →  00000024   CHANGED
  ✓ compression_method  00000008  →  00000008   unchanged
  ✓ crc32               A3F2019C  →  B7C3A4D1   CHANGED

  Ignored: timestamps (2), offsets (4)
  Result  : 2 field(s) changed
```

### JSON

```json
{
  "formatName": "ZIP",
  "fileA": "build-v1.zip",
  "fileB": "build-v2.zip",
  "sizeA": 1204812,
  "sizeB": 1209044,
  "rawByteDelta": 4232,
  "isIdentical": false,
  "changedCount": 2,
  "fieldChanges": [
    { "fieldName": "entry_count", "valueA": "00000023", "valueB": "00000024", "isChanged": true, "isIgnored": false },
    { "fieldName": "crc32",       "valueA": "A3F2019C", "valueB": "B7C3A4D1", "isChanged": true, "isIgnored": false }
  ]
}
```

---

## Supported Formats with Semantic Diff

| Format | Key Fields | Ignored |
|---|---|---|
| **ZIP** | entry_name, compression_method, crc32, entry_count | timestamps, offsets |
| **PNG** | width, height, bit_depth, ihdr_crc | - |
| **PE/EXE** | machine_type, entry_point_rva, size_of_image | time_date_stamp, checksum |
| **PDF** | pdf_version, page_count, root_obj_id | creation_date, producer |
| **MP3** | mpeg_version, bitrate, sample_rate, id3 tags | - |
| **SQLite** | page_size, schema_format, user_version | change_counter |

All 757 binary catalog formats are supported for raw-byte fallback. Formats with a `diff` block in their .whfmt definition get full semantic key-field comparison.

---

## CI Integration

```yaml
# .github/workflows/binary-diff.yml
- name: Semantic binary diff
  run: |
    dotnet script diff.csx artifacts/v1/app.exe artifacts/v2/app.exe
```

```csharp
// diff.csx
#r "nuget: whfmt.Analysis, 1.0.0"
using WhfmtAnalysis;
using WpfHexEditor.Core.Definitions;

var result = FormatDiff.Compare(EmbeddedFormatCatalog.Instance, Args[0], Args[1]);
Console.WriteLine(DiffRenderer.RenderText(result));
if (!result.IsIdentical) Environment.Exit(1);
```

---

## Architecture

```
whfmt.Analysis
├── FormatDiff         — entry point, format detection, single-pass field extraction
├── DiffResult         — immutable value-object result model
├── FieldChange        — per-field comparison (IsCorrupted, HexDiff)
├── ChecksumStatus     — stored vs computed checksum per entry
├── StructuralDiff     — block-level OnlyInA / OnlyInB / InBoth
└── DiffRenderer       — text / JSON / CSV / Markdown / HTML output
```

Depends on: `whfmt.FileFormatCatalog 1.3.0+` (zero other dependencies, cross-platform net8.0).

---

## Related Packages

| Package | Description |
|---|---|
| [whfmt.FileFormatCatalog](https://www.nuget.org/packages/whfmt.FileFormatCatalog) | 790+ format definitions — required dependency |
| [whfmt.Validate](https://www.nuget.org/packages/whfmt.Validate) | `dotnet tool` — validate binary files from the CLI |
| [whfmt.Fuzz](https://www.nuget.org/packages/whfmt.Fuzz) | Format-aware binary fuzzer for parser testing |
| [whfmt.CodeGen](https://www.nuget.org/packages/whfmt.CodeGen) | `dotnet tool` — generate C# parser classes from .whfmt |

---

## License

GNU AGPL v3.0 — © 2016–2026 Derek Tremblay / Pulsar Informatique
