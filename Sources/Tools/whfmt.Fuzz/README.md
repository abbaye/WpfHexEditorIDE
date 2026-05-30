# whfmt.Fuzz

**Format-aware binary fuzzer** that generates structured mutant files for testing parsers, decoders, and file readers.

Unlike naive byte-flippers, `whfmt.Fuzz` understands the *structure* of binary files:

- Targets semantically significant fields (magic bytes, size fields, enum values, checksums)
- Applies format-specific mutation strategies declared in `.whfmt` definitions
- **Compound mutations** — apply N independent mutations per variant for deeper coverage
- **Mutation log** — every applied mutation recorded (`MutationLogEntry`: type + field)
- Optionally recomputes checksums after mutation so parsers see plausible-looking corrupt data
- **`FuzzSession`** — reproducible multi-generation corpus with `manifest.json` save/load
- **`FuzzReport`** — field coverage, strategy distribution, and untested fields analysis
- Weighted random strategy picker ensures coverage of the most dangerous fields first

Powered by **856+ whfmt format definitions** (schema v3.1) with dedicated `fuzz` blocks for ZIP, PNG, PE/EXE, PDF, MP3, SQLite, and more.


> **Full documentation**: [whfmt-Fuzz-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Tools/whfmt.Fuzz/whfmt-Fuzz-guide.md) — API reference, architecture, integration guides, and usage examples.

---

## What's New

### v1.1.2 — Catalog 1.5.0 alignment

- **Catalog bump** to `whfmt.FileFormatCatalog 1.5.0` — schema v3.1, PHP/Markdown embedded language injection, `CSS.whfmt` fix, 856+ definitions.
- **No Fuzz API changes** — drop-in upgrade from 1.1.1.

### v1.1.1 — Catalog 1.3.2 alignment + extended mutations

- **Catalog bump** to `whfmt.FileFormatCatalog 1.3.2` (Phase B audit + 2 bug fixes, 799 definitions, schema v3 canonical).
- **`FuzzMutation` enum extended** (Lot 4) with `byte_swap` / `truncate` / `duplicate` mutations.
- **No Fuzz API changes** — drop-in upgrade from 1.1.0.

### v1.1.0 — Sessions, reports, compound mutations

- **`FuzzSession`** — multi-generation reproducible corpus with `manifest.json` save/restore (`SaveCorpusAsync` / `LoadCorpusAsync`)
- **`FuzzReport`** — coverage analysis: `FieldCoverage`, `StrategyDistribution`, `UntestedFields`, `AverageMutationsPerVariant`
- **Compound mutations** — `compound: N` parameter applies N independent mutations per variant
- **`MutationLog`** — every applied mutation recorded as `MutationLogEntry` (type + field)
- **3 new mutation strategies** — `InsertBytes`, `SliceRepeat`, `NegateField` (12 total)
- **Async overloads** — `GenerateAsync(string file, …)` and `GenerateAsync(Stream stream, …)`
- **`GenerateWithReport()`** — single call returns `(variants, report)` with one Resolve pass
- **`Random.Shared`** replaces `new Random()` for stronger weak seeding

---

## Install

```bash
dotnet add package whfmt.Fuzz
```

---

## Quick Start

```csharp
using WhfmtFuzz;
using WpfHexEditor.Core.Definitions;

var catalog = EmbeddedFormatCatalog.Instance;

// Generate 20 mutant variants of a PNG file
var variants = FormatFuzzer.Generate(catalog, "sample.png", count: 20);

foreach (var v in variants)
{
    if (v.IsError) { Console.Error.WriteLine(v.Error); continue; }

    File.WriteAllBytes($"corpus/{v.SuggestedFileName}", v.Data);
    Console.WriteLine($"  [{v.Index:D4}] {v.Strategy} on '{v.Field}' — {v.Description}");
}
```

---

## API Reference

### `FormatFuzzer.Generate()`

```csharp
// From a file — format auto-detected
IReadOnlyList<FuzzVariant> Generate(
    IEmbeddedFormatCatalog catalog,
    string inputFile,
    int count = 10,
    string? forcedFormat = null,
    int? seed = null,
    int compound = 1)   // NEW: mutations per variant

// From raw bytes — format detected from extension
IReadOnlyList<FuzzVariant> Generate(
    IEmbeddedFormatCatalog catalog,
    byte[] inputData,
    string fileName,
    int count = 10,
    string? forcedFormat = null,
    int? seed = null,
    int compound = 1)

// Async overloads (from file path or stream)
Task<IReadOnlyList<FuzzVariant>> GenerateAsync(string inputFile, ...)
Task<IReadOnlyList<FuzzVariant>> GenerateAsync(Stream stream, string fileName, ...)
```

### `FormatFuzzer.GenerateWithReport()`

```csharp
// Returns variants + coverage report in one call
(IReadOnlyList<FuzzVariant>, FuzzReport) GenerateWithReport(
    IEmbeddedFormatCatalog catalog,
    string inputFile,
    int count = 10,
    string? forcedFormat = null,
    int? seed = null,
    int compound = 1)
```

### `FuzzSession` — multi-generation corpus

```csharp
var session = new FuzzSession(catalog, seed: 42);

// Generate batches iteratively
session.NextGeneration("sample.png", count: 50);
session.NextGeneration("sample.png", count: 50, compound: 3);

Console.WriteLine($"Corpus: {session.Corpus.Count} variants");

// Save with manifest.json
await session.SaveCorpusAsync("corpus/");

// Restore from disk
var restored = await FuzzSession.LoadCorpusAsync(catalog, "corpus/", seed: 42);
```

### `FuzzReport` — coverage analysis

| Property | Type | Description |
|---|---|---|
| `FormatName` | `string` | Format used for the session |
| `TotalVariants` | `int` | Total generated |
| `ErrorCount` | `int` | Failed variants |
| `FieldCoverage` | `IReadOnlyDictionary<string, int>` | Hits per field |
| `UntestedFields` | `IReadOnlyList<string>` | Fields never mutated |
| `StrategyDistribution` | `IReadOnlyDictionary<MutationType, int>` | Hits per strategy |
| `AverageMutationsPerVariant` | `double` | Compound mutation rate |

### `FuzzVariant`

| Property | Type | Description |
|---|---|---|
| `Index` | `int` | Zero-based index in the batch |
| `OriginalFile` | `string` | Source file name |
| `FormatName` | `string` | Detected format |
| `Strategy` | `string` | Primary mutation type applied |
| `Field` | `string` | Target field name from the whfmt definition |
| `Description` | `string` | Why this field is interesting to fuzz |
| `Data` | `byte[]` | Mutated file bytes |
| `MutationCount` | `int` | Total mutations applied (≥1 with compound) |
| `MutationLog` | `IReadOnlyList<MutationLogEntry>` | Per-mutation type + field |
| `IsError` | `bool` | True if generation failed |
| `Error` | `string?` | Error message if `IsError` |
| `SuggestedFileName` | `string` | e.g. `sample_fuzz0003_BitFlip.png` |

---

## Mutation Strategies

| Strategy | Description |
|---|---|
| `BoundaryValues` | Apply boundary integers (0, 1, 127, 255, 65535, 2³¹-1) |
| `EnumSweep` | Iterate all valid enum values + 5 invalid ones |
| `CorruptSignature` | XOR magic/signature bytes with random values |
| `BitFlip` | Flip one random bit in the target field |
| `ZeroField` | Fill the field with 0x00 |
| `Overflow` | Fill the field with 0xFF |
| `RandomBytes` | Overwrite with cryptographically random bytes |
| `Truncate` | Cut the file at the midpoint of the target field |
| `Duplicate` | Inline-duplicate the field bytes |
| `InsertBytes` | Insert random bytes at a random offset |
| `SliceRepeat` | Repeat a random slice of the file 2–8 times |
| `NegateField` | Bitwise-NOT all bytes in the target field |

Strategies are selected by **weighted random pick** using weights declared in the `.whfmt` definition. Rate controls per-strategy acceptance probability.

---

## Format-Specific Strategies

### ZIP

| Field | Strategies |
|---|---|
| `local_file_header_sig` | CorruptSignature (weight 3) |
| `compression_method` | EnumSweep (weight 2) |
| `compressed_size` | BoundaryValues (weight 2) |
| `crc32` | RandomBytes (weight 1) |
| `entry_count` | Overflow (weight 2) |

### PNG

| Field | Strategies |
|---|---|
| `signature` | CorruptSignature (weight 3) |
| `width` | BoundaryValues + Overflow (weight 2 each) |
| `bit_depth` | EnumSweep (weight 2) |
| `ihdr_crc` | RandomBytes (weight 1) |
| `idat_length` | BoundaryValues (weight 2) |

### PE/EXE

| Field | Strategies |
|---|---|
| `mz_signature` | CorruptSignature (weight 3) |
| `machine_type` | EnumSweep (weight 2) |
| `size_of_image` | BoundaryValues + Overflow (weight 2 each) |
| `entry_point_rva` | BoundaryValues (weight 2) |
| `pe_signature` | CorruptSignature (weight 3) |

---

## Examples

### Reproducible corpus generation

```csharp
// Fixed seed — same corpus every CI run
var variants = FormatFuzzer.Generate(catalog, "test.zip", count: 100, seed: 42);
```

### Compound mutations (deeper coverage)

```csharp
// Apply 3 independent mutations per variant
var variants = FormatFuzzer.Generate(catalog, "sample.png", count: 50, compound: 3);
foreach (var v in variants)
    Console.WriteLine($"  {v.MutationCount} mutations: {string.Join(", ", v.MutationLog.Select(m => m.Mutation))}");
```

### Coverage-aware generation with report

```csharp
var (variants, report) = FormatFuzzer.GenerateWithReport(catalog, "sample.zip", count: 200, seed: 42);
Console.WriteLine($"Untested fields: {string.Join(", ", report.UntestedFields)}");
```

### Multi-generation session with save/restore

```csharp
var session = new FuzzSession(catalog, seed: 42);
session.NextGeneration("sample.zip", count: 50);
session.NextGeneration("sample.zip", count: 50, compound: 2);
await session.SaveCorpusAsync("corpus/");
// corpus/ contains variant files + manifest.json
```

### Save full corpus

```csharp
var variants = FormatFuzzer.Generate(catalog, "sample.pdf", count: 500);
Directory.CreateDirectory("corpus");
foreach (var v in variants.Where(v => !v.IsError))
    File.WriteAllBytes(Path.Combine("corpus", v.SuggestedFileName), v.Data);

Console.WriteLine($"Generated {variants.Count(v => !v.IsError)} variants");
```

### Integration with libFuzzer / AFL

```csharp
// whfmt.Fuzz generates the initial seed corpus
// libFuzzer takes over for coverage-guided evolution
var seeds = FormatFuzzer.Generate(catalog, "golden.mp3", count: 1000, seed: 0);
foreach (var s in seeds.Where(v => !v.IsError))
    File.WriteAllBytes($"seeds/{s.SuggestedFileName}", s.Data);

// Then: afl-fuzz -i seeds/ -o findings/ -- ./my_parser @@
```

---

## CI Integration

```yaml
# .github/workflows/fuzz-corpus.yml
- name: Generate fuzz corpus
  run: dotnet run --project FuzzRunner -- --input golden/ --output corpus/ --count 200 --seed 42
- name: Run parser against corpus
  run: find corpus/ -name "*.png" | xargs -I{} ./tests/run_parser {}
```

---

## Architecture

```
whfmt.Fuzz
├── FormatFuzzer       — entry point, strategy picker, compound mutation engine
├── FuzzSession        — multi-generation reproducible corpus with manifest.json
├── FuzzVariant        — immutable result record with MutationLog
├── FuzzReport         — field coverage, strategy distribution, untested fields
├── MutationType       — enum of 12 mutation strategies
└── MutationLogEntry   — per-mutation type + field record
```

Checksum recomputation: CRC32 (poly 0xEDB88320), MD5, SHA1, SHA256 — all built-in, zero external dependencies.

Depends on: `whfmt.FileFormatCatalog 1.3.2+` — cross-platform net8.0.

---

## Related Packages

| Package | Description |
|---|---|
| [whfmt.FileFormatCatalog](https://www.nuget.org/packages/whfmt.FileFormatCatalog) | 799 format definitions — required dependency |
| [whfmt.Validate](https://www.nuget.org/packages/whfmt.Validate) | `dotnet tool` — validate binary files from the CLI |
| [whfmt.Analysis](https://www.nuget.org/packages/whfmt.Analysis) | Semantic field-level diff between binary files |
| [whfmt.CodeGen](https://www.nuget.org/packages/whfmt.CodeGen) | `dotnet tool` — generate C# parser classes from .whfmt |

---

## License

GNU AGPL v3.0 — © 2016–2026 Derek Tremblay / Pulsar Informatique
