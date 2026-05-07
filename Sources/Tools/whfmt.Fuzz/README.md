# whfmt.Fuzz

**Format-aware binary fuzzer** that generates structured mutant files for testing parsers, decoders, and file readers.

Unlike naive byte-flippers, `whfmt.Fuzz` understands the *structure* of binary files:

- Targets semantically significant fields (magic bytes, size fields, enum values, checksums)
- Applies format-specific mutation strategies declared in `.whfmt` definitions
- Optionally recomputes checksums after mutation so parsers see plausible-looking corrupt data
- Weighted random strategy picker ensures coverage of the most dangerous fields first

Powered by **790+ whfmt format definitions** with dedicated `fuzz` blocks for ZIP, PNG, PE/EXE, PDF, MP3, SQLite, and more.

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
    int? seed = null)

// From raw bytes — format detected from extension
IReadOnlyList<FuzzVariant> Generate(
    IEmbeddedFormatCatalog catalog,
    byte[] inputData,
    string fileName,
    int count = 10,
    string? forcedFormat = null,
    int? seed = null)
```

| Parameter | Description |
|---|---|
| `catalog` | `EmbeddedFormatCatalog.Instance` |
| `inputFile` / `inputData` | Source file to mutate |
| `count` | Number of variants to generate |
| `forcedFormat` | Override auto-detection (e.g. `"ZIP"`, `".zip"`) |
| `seed` | Fixed seed for reproducible corpus |

### `FuzzVariant`

| Property | Type | Description |
|---|---|---|
| `Index` | `int` | Zero-based index in the batch |
| `OriginalFile` | `string` | Source file name |
| `FormatName` | `string` | Detected format |
| `Strategy` | `string` | Mutation type applied |
| `Field` | `string` | Target field name from the whfmt definition |
| `Description` | `string` | Why this field is interesting to fuzz |
| `Data` | `byte[]` | Mutated file bytes |
| `MutationCount` | `int` | Mutations applied (always 1 in single-mutation corpus) |
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

### Force format detection

```csharp
// File has no extension — force format
var variants = FormatFuzzer.Generate(catalog, rawBytes, "mystery_file", forcedFormat: "SQLite");
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
├── FormatFuzzer       — entry point, strategy picker, mutation engine
├── FuzzVariant        — immutable result record
├── MutationType       — enum of 9 mutation strategies
└── FuzzStrategy       — internal weighted strategy record
```

Checksum recomputation: CRC32 (poly 0xEDB88320), MD5, SHA1, SHA256 — all built-in, zero external dependencies.

Depends on: `whfmt.FileFormatCatalog 1.3.0+` — cross-platform net8.0.

---

## Related Packages

| Package | Description |
|---|---|
| [whfmt.FileFormatCatalog](https://www.nuget.org/packages/whfmt.FileFormatCatalog) | 790+ format definitions — required dependency |
| [whfmt.Validate](https://www.nuget.org/packages/whfmt.Validate) | `dotnet tool` — validate binary files from the CLI |
| [whfmt.Analysis](https://www.nuget.org/packages/whfmt.Analysis) | Semantic field-level diff between binary files |
| [whfmt.CodeGen](https://www.nuget.org/packages/whfmt.CodeGen) | `dotnet tool` — generate C# parser classes from .whfmt |

---

## License

GNU AGPL v3.0 — © 2016–2026 Derek Tremblay / Pulsar Informatique
