# whfmt.Analysis — Documentation (v1.1.1)

> **What you get** — Field-level **semantic** binary diff over the 799 file-format
> definitions shipped in `whfmt.FileFormatCatalog` 1.3.2. Instead of byte-by-byte
> noise, you get a diff scoped to the `diff.keyFields` / `diff.ignoreFields`
> contract declared in each `.whfmt`, plus inline hex deltas, checksum
> validation, structural block diffing, and renderers for plain text, JSON,
> CSV, GitHub-Markdown and self-contained dark-themed HTML. Cross-platform
> `net8.0`, zero external dependencies beyond the catalog.

## Table of Contents

1. [Installation](#installation)
2. [Architecture & dependencies](#architecture--dependencies)
3. [Public API reference](#public-api-reference)
4. [Usage examples](#usage-examples)
5. [Integration with other whfmt.* packages](#integration-with-other-whfmt-packages)
6. [Schema v3 fields consumed](#schema-v3-fields-consumed)
7. [Output formats](#output-formats)
8. [Troubleshooting](#troubleshooting)
9. [License](#license)

---

## Installation

```bash
dotnet add package whfmt.Analysis --version 1.1.1
```

`whfmt.FileFormatCatalog` 1.3.2 is pulled transitively. No other runtime
dependency.

```xml
<PackageReference Include="whfmt.Analysis" Version="1.1.1" />
```

Target framework: `net8.0` (cross-platform — Windows, Linux, macOS).

---

## Architecture & dependencies

```
whfmt.Analysis.nupkg
└── lib/net8.0/
    └── whfmt.Analysis.dll
            └─ depends on → whfmt.FileFormatCatalog 1.3.2
                              ├─ WpfHexEditor.Core.Definitions.dll   (engine + 799 .whfmt)
                              └─ WpfHexEditor.Core.Contracts.dll     (records, enums)
```

### Type ownership

| Type | Namespace | Purpose |
|---|---|---|
| `FormatDiff` | `WhfmtAnalysis` | Static entry point — `Compare` / `CompareAsync` |
| `DiffResult` | `WhfmtAnalysis` | Top-level result aggregate |
| `FieldChange` | `WhfmtAnalysis` | One field comparison row |
| `HexDiff` | `WhfmtAnalysis` | Per-field byte-level diff with mask |
| `ChecksumStatus` | `WhfmtAnalysis` | One checksum-block verification result |
| `StructuralDiff` | `WhfmtAnalysis` | OnlyInA / OnlyInB / InBoth block sets |
| `StructuralBlock` | `WhfmtAnalysis` | Detected chunk (name, offset, length, MD5-4 hash) |
| `DiffRenderer` | `WhfmtAnalysis` | Renders `DiffResult` to text / JSON / CSV / Markdown / HTML |

### Pipeline

```
Compare(A, B)
    └── format detection per file (FormatFileAnalyzer.Analyze)
            └── load .whfmt JSON via catalog.GetJson(entry.ResourceKey)
                    └── ParseDiffConfig          ─── keyFields / ignoreFields / groupBy
                    └── ExtractFields × (A, B)   ─── values + raw bytes per block
                    └── FillChecksumStatus × (A, B)
                    └── BuildHexDiff per changed key field
                    └── BuildStructuralDiff      ─── MD5-4 block hashes
                    └── → DiffResult
```

---

## Public API reference

### `FormatDiff` (static)

| Signature | Description |
|---|---|
| `DiffResult Compare(IEmbeddedFormatCatalog catalog, string fileA, string fileB, string? forcedFormat = null)` | Compare two files on disk. |
| `DiffResult Compare(IEmbeddedFormatCatalog catalog, byte[] dataA, string nameA, byte[] dataB, string nameB, string? forcedFormat = null)` | Compare two in-memory buffers. |
| `Task<DiffResult> CompareAsync(IEmbeddedFormatCatalog catalog, string fileA, string fileB, string? forcedFormat = null, CancellationToken cancellationToken = default)` | Async file overload. |
| `Task<DiffResult> CompareAsync(IEmbeddedFormatCatalog catalog, Stream streamA, string nameA, Stream streamB, string nameB, string? forcedFormat = null, CancellationToken cancellationToken = default)` | Async stream overload. |

When `forcedFormat` is `null`, each file is autodetected independently (one
side may detect, the other may not — see `FormatsMatch`).

### `DiffResult`

| Member | Type | Description |
|---|---|---|
| `FileA`, `FileB` | `string` | Display paths. |
| `SizeA`, `SizeB` | `long` | Byte sizes. |
| `FormatName` | `string` | Definitive format used for the field schema (A wins, then B). |
| `FormatDetectedA`, `FormatDetectedB` | `string` | Per-side detection. |
| `FormatsMatch` | `bool` | Both sides resolved to the same format. |
| `KeyFields`, `IgnoreFields` | `List<string>` | From `.whfmt → diff`. |
| `GroupBy` | `string?` | From `.whfmt → diff.groupBy`. |
| `FieldChanges` | `List<FieldChange>` | All key + ignored fields. |
| `RawByteDelta` | `long` | `SizeB - SizeA`. |
| `IsIdentical` | `bool` | All key fields equal AND sizes equal. |
| `Error` | `string?` | Set when detection or definition lookup fails. |
| `ChecksumsA`, `ChecksumsB` | `List<ChecksumStatus>` | One entry per `.whfmt → checksums[]`. |
| `StructuralDiff` | `StructuralDiff?` | Set when blocks have a fixed length. |
| `ChangedCount` / `UnchangedCount` | `int` | Excludes ignored fields. |
| `CorruptedCountA` / `CorruptedCountB` | `int` | Number of failing checksums. |

### `FieldChange`

| Member | Type | Description |
|---|---|---|
| `FieldName` | `string` | Block `name` or `storeAs`. |
| `ValueA`, `ValueB` | `string` | Interpreted value (via `valueType` map). |
| `IsChanged` | `bool` | Case-insensitive string mismatch. |
| `IsIgnored` | `bool` | Listed in `diff.ignoreFields`. |
| `IsCorrupted` | `bool` | Checksum valid in A, invalid in B (corruption forensic). |
| `HexDiff` | `HexDiff?` | Populated when both sides have raw bytes for the field. |

### `HexDiff`

| Member | Type | Description |
|---|---|---|
| `Offset` | `long` | Field offset in the file. |
| `BytesA`, `BytesB` | `byte[]` | Raw bytes from each side. |
| `DiffMask` | `bool[]` | `true` where bytes differ (length = `max(BytesA, BytesB)`). |
| `DifferentBytes` | `int` | Count of `true` in `DiffMask`. |

### `ChecksumStatus`

| Member | Type | Description |
|---|---|---|
| `Algorithm` | `string` | Upper-cased — `CRC32`, `MD5`, `SHA1`, `SHA256`. |
| `StoredOffset` | `long` | Offset where the checksum lives. |
| `StoredHex`, `ComputedHex` | `string` | Hex strings (no separators). |
| `IsValid` | `bool` | Stored == computed (length-trimmed compare). |

### `StructuralDiff` / `StructuralBlock`

| Member | Type | Description |
|---|---|---|
| `OnlyInA`, `OnlyInB` | `IReadOnlyList<StructuralBlock>` | Blocks unique to one side (by 4-byte MD5 prefix). |
| `InBoth` | `IReadOnlyList<StructuralBlock>` | Blocks identical on both sides. |
| `TotalA`, `TotalB` | `int` | Convenience counters. |
| `StructuralBlock.Name` / `Offset` / `Length` / `Hash` | — | Block identity. |

### `DiffRenderer` (static)

| Method | Returns | Description |
|---|---|---|
| `RenderText(DiffResult)` | `string` | Human-readable ANSI-free console output, includes inline hex tape + checksum table + structural section. |
| `RenderJson(DiffResult)` | `string` | Pretty-printed System.Text.Json model — CI-friendly. |
| `ToCsv(DiffResult)` | `string` | One row per `FieldChange`. |
| `ToMarkdown(DiffResult)` | `string` | GitHub-flavoured Markdown tables with emoji status. |
| `RenderHtml(DiffResult)` | `string` | Self-contained dark-themed HTML page. |

---

## Usage examples

### 1. Minimal diff

```csharp
using WhfmtAnalysis;
using WpfHexEditor.Core.Definitions;

var catalog = EmbeddedFormatCatalog.Instance;
var result  = FormatDiff.Compare(catalog, "before.png", "after.png");

Console.WriteLine(DiffRenderer.RenderText(result));
```

### 2. Async batch

```csharp
var tasks = pairs.Select(p => FormatDiff.CompareAsync(catalog, p.A, p.B));
var results = await Task.WhenAll(tasks);
int regressions = results.Count(r => !r.IsIdentical);
```

### 3. Force a format (when autodetect fails or fights you)

```csharp
var result = FormatDiff.Compare(catalog, a, b, forcedFormat: "PNG");
// or by extension
var result2 = FormatDiff.Compare(catalog, a, b, forcedFormat: ".dds");
```

### 4. CI gate — fail the build if anything but the timestamp changed

```csharp
var r = FormatDiff.Compare(catalog, golden, candidate);
bool onlyTimestamp = r.FieldChanges
    .Where(f => f.IsChanged && !f.IsIgnored)
    .All(f => f.FieldName.Equals("timestamp", StringComparison.OrdinalIgnoreCase));

if (!onlyTimestamp) { Console.Error.WriteLine(DiffRenderer.RenderText(r)); return 1; }
```

### 5. Detect tampered files (checksum corruption forensic)

```csharp
var r = FormatDiff.Compare(catalog, original, suspicious);
var tampered = r.FieldChanges.Where(f => f.IsCorrupted).ToList();
foreach (var t in tampered)
    Console.WriteLine($"Field {t.FieldName} was valid in A but corrupt in B");
```

### 6. Stream-based diff (download + diff without ever writing to disk)

```csharp
await using var streamA = await httpClient.GetStreamAsync(urlA);
await using var streamB = await httpClient.GetStreamAsync(urlB);
var r = await FormatDiff.CompareAsync(catalog, streamA, "a.zip", streamB, "b.zip");
File.WriteAllText("diff.html", DiffRenderer.RenderHtml(r));
```

### 7. Export to multiple formats at once

```csharp
var r = FormatDiff.Compare(catalog, a, b);
File.WriteAllText("diff.txt",  DiffRenderer.RenderText(r));
File.WriteAllText("diff.json", DiffRenderer.RenderJson(r));
File.WriteAllText("diff.md",   DiffRenderer.ToMarkdown(r));
File.WriteAllText("diff.csv",  DiffRenderer.ToCsv(r));
File.WriteAllText("diff.html", DiffRenderer.RenderHtml(r));
```

---

## Integration with other whfmt.* packages

| Package | Synergy |
|---|---|
| `whfmt.FileFormatCatalog` 1.3.2 | Primary dependency — provides the catalog, the JSON definitions, and the runtime expression engine consumed indirectly through `FormatFileAnalyzer.Analyze`. |
| `whfmt.Fuzz` | Pipe `FuzzVariant.Data` arrays directly into `FormatDiff.Compare(byte[]…)` to verify each mutant still passes / breaks the format as expected. |
| `whfmt.Validate` | Run `whfmt validate` first to catch malformed files; then use `whfmt.Analysis` to diff the surviving valid pairs. |
| `whfmt.CodeGen` | Generated parsers and `whfmt.Analysis` can share the same `.whfmt` definition — write once, parse and diff for free. |

---

## Schema v3 fields consumed

The diff engine reads the following keys from each `.whfmt`:

```jsonc
{
  "diff": {
    "keyFields":    ["width", "height", "colorType"],
    "ignoreFields": ["timestamp", "creationDate"],
    "groupBy":      "chunkType"
  },
  "blocks": [
    { "type": "field", "name": "width", "storeAs": "imageWidth",
      "offset": 16, "length": 4, "valueType": "uint32" }
  ],
  "checksums": [
    { "algorithm": "crc32",
      "storedAt":  { "fixedOffset": -4, "length": 4 },
      "dataRange": { "fixedOffset": 0,  "fixedLength": -4 } }
  ]
}
```

- **`diff.keyFields`** — must compare; missing → fall back to every variable in `variables{}`.
- **`diff.ignoreFields`** — listed but excluded from `IsIdentical`.
- **`diff.groupBy`** — surfaced on `DiffResult.GroupBy` for UI grouping (not enforced by the engine).
- **`blocks[].offset` / `length`** — drives raw-byte slicing for `HexDiff`.
- **`blocks[].valueType`** — drives `ValueA` / `ValueB` interpretation (`uint8` / `uint16` / `uint32` / `uint64` / `int*` / `ascii8` / `utf8` / `string`; anything else → hex string).
- **`checksums[].algorithm`** — `crc32`, `md5`, `sha1`, `sha256`.
- **`checksums[].storedAt.fixedOffset` / `length`** — where the checksum lives (negative offsets unsupported here — file the diff before fixing, or pre-trim).

---

## Output formats

### Text (default for CLIs)

```
  whfmt diff — PNG
  ────────────────────────────────────────
  A: golden.png   (12.3 KB)
  B: rebuilt.png  (12.4 KB)
  Format: PNG  |  same format
  Size delta: +132 bytes

  Changed fields (1):
    ≠  timestamp                      A: 1715000000  →  B: 1715004422
       Hex A: 66 24 11 80
       Hex B: 66 24 22 86
       Diff:  ^^             ^^ ^^
```

### JSON (CI)

Pretty-printed, contains everything in `DiffResult` plus per-field hex
spaces-separated, suitable for `jq` / GitHub Actions outputs.

### Markdown (PR comments)

Tables with emoji status (`🔴 changed`, `🟢 same`, `☠️ corrupted`, `⬜ ignored`),
plus optional Checksums and Structural-diff sub-tables.

### HTML

Self-contained, dark theme, monospace hex tape with red highlighted diff
bytes. Safe to drop into a build artifact and email.

### CSV

`Field,ValueA,ValueB,IsChanged,IsIgnored,IsCorrupted,DifferentBytes` — one
row per field, RFC-4180 quoting.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Error = "Could not detect format for either file."` | Neither side matched a signature, no `forcedFormat`. | Pass `forcedFormat: "PNG"` or similar. |
| `Error = "No full definition available for this format."` | Format known by extension only, no embedded `.whfmt`. | Open an issue or fall back to a raw `byte[]` diff outside this package. |
| `FormatsMatch == false` | A and B detected as different formats. | Pass `forcedFormat:` or verify the files. |
| `KeyFields.Count == 0` | The `.whfmt` declares no `diff.keyFields` AND no `variables{}`. | Engine defaults to no key comparison — file appears identical. Add `keyFields` upstream. |
| `HexDiff == null` despite a changed field | Field has no fixed `offset` + `length` (variable-size or computed). | Expected; the field name comparison is still authoritative. |
| Checksum reported `IsValid = false` even on the golden file | The `.whfmt`'s `dataRange.fixedLength` is wrong, or the algorithm is non-standard. | Open an issue against `whfmt.FileFormatCatalog`. |
| `StructuralDiff` always empty | Format has no fixed-length blocks (`length <= 0` after parse). | Expected. |

---

## License

GNU AGPL v3.0. See `https://www.gnu.org/licenses/agpl-3.0.html`.

Copyright © 2016-2026 Derek Tremblay.
