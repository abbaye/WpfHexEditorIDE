# whfmt.Fuzz — Documentation (v1.1.1)

> **What you get** — A format-aware binary fuzzer that reads the `fuzz`
> strategies declared in each of the 799 `.whfmt` definitions (schema v3,
> shipped by `whfmt.FileFormatCatalog` 1.3.2) and generates targeted mutant
> files for parser hardening, decoder fuzzing, and security research.
> Twelve mutation primitives, compound mutations, reproducible seeded
> sessions, automatic checksum recomputation, JSON corpus manifests, and a
> coverage report so you know which fields are still untested. Cross-platform
> `net8.0`, zero external dependencies beyond the catalog.

## Table of Contents

1. [Installation](#installation)
2. [Architecture & dependencies](#architecture--dependencies)
3. [Mutation strategies](#mutation-strategies)
4. [Public API reference](#public-api-reference)
5. [Usage examples](#usage-examples)
6. [Integration with other whfmt.* packages](#integration-with-other-whfmt-packages)
7. [Schema v3 fields consumed](#schema-v3-fields-consumed)
8. [Reproducibility & corpus management](#reproducibility--corpus-management)
9. [Troubleshooting](#troubleshooting)
10. [License](#license)

---

## Installation

```bash
dotnet add package whfmt.Fuzz --version 1.1.1
```

```xml
<PackageReference Include="whfmt.Fuzz" Version="1.1.1" />
```

Target framework: `net8.0`. `whfmt.FileFormatCatalog` 1.3.2 is pulled
transitively.

---

## Architecture & dependencies

```
whfmt.Fuzz.nupkg
└── lib/net8.0/
    └── whfmt.Fuzz.dll
            └─ depends on → whfmt.FileFormatCatalog 1.3.2
                              ├─ WpfHexEditor.Core.Definitions.dll
                              └─ WpfHexEditor.Core.Contracts.dll
```

### Type ownership

| Type | Namespace | Purpose |
|---|---|---|
| `FormatFuzzer` | `WhfmtFuzz` | Static engine — `Generate`, `GenerateAsync`, `GenerateWithReport` |
| `FuzzVariant` | `WhfmtFuzz` | One generated mutant (`Data`, `Strategy`, `MutationLog`) |
| `MutationType` | `WhfmtFuzz` | The 12 mutation primitives (enum) |
| `MutationLogEntry` | `WhfmtFuzz` | One mutation step inside a compound variant |
| `FuzzReport` | `WhfmtFuzz` | Coverage report — field histogram, untested fields, dominant strategy |
| `FuzzSession` | `WhfmtFuzz` | Multi-generation reproducible session with corpus persistence |

### Pipeline

```
Generate(input, count, compound)
    └── Resolve(catalog, data, fileName, forcedFormat)
            └── parse .whfmt JSON
                    └── ParseStrategies(root)        ─── fuzz.strategies[]
                    └── for v in 0..count:
                          for m in 0..compound:
                            WeightedPick(strategies)
                            Mutate(data, strategy, root, rng)
                            log.Add(...)
                          if preserveChecksums: RecomputeChecksums(data, root)
                          → FuzzVariant
```

---

## Mutation strategies

The `MutationType` enum defines all 12 primitives. Strategies declared in
`.whfmt → fuzz.strategies[].mutation` are matched case-insensitively (snake or
camel — `bit_flip`, `bitFlip`, `BitFlip` all parse).

| `MutationType` | Description |
|---|---|
| `BoundaryValues` | Writes 0, 1, 127, 128, 255, 256, 32767, 32768, 65535, 65536, `int.MaxValue`, `uint.MaxValue` (random pick) into the target field. |
| `EnumSweep` | Walks the field's `valueMap` keys + random invalid bytes. |
| `CorruptSignature` | XORs each magic byte with `rng.Next(1, 255)`. |
| `BitFlip` | Flips one random bit in one random byte of the field. |
| `ZeroField` | Fills the field with `0x00`. |
| `Overflow` | Fills the field with `0xFF` (integer overflow). |
| `RandomBytes` | Replaces the field with cryptographically-random bytes. |
| `Truncate` | Cuts the file at `offset + length/2`. Output is shorter. |
| `Duplicate` | Inserts a copy of the field's bytes right after it. Output is longer. |
| `InsertBytes` | Inserts N random bytes (1 ≤ N ≤ length) at the field offset. |
| `SliceRepeat` | Repeats the field 2–4 times in place. Output is longer. |
| `NegateField` | XORs every byte of the field with `0xFF`. |

Unknown strings fall back to `RandomBytes`.

---

## Public API reference

### `FormatFuzzer` (static)

| Signature | Returns | Notes |
|---|---|---|
| `Generate(IEmbeddedFormatCatalog catalog, string inputFile, int count = 10, string? forcedFormat = null, int? seed = null, int compound = 1)` | `IReadOnlyList<FuzzVariant>` | File-path overload. |
| `Generate(IEmbeddedFormatCatalog catalog, byte[] inputData, string fileName, int count = 10, string? forcedFormat = null, int? seed = null, int compound = 1)` | `IReadOnlyList<FuzzVariant>` | Byte-array overload. |
| `GenerateAsync(IEmbeddedFormatCatalog, string inputFile, int, string?, int?, int, CancellationToken)` | `Task<IReadOnlyList<FuzzVariant>>` | Async file read. |
| `GenerateAsync(IEmbeddedFormatCatalog, Stream, string fileName, int, string?, int?, int, CancellationToken)` | `Task<IReadOnlyList<FuzzVariant>>` | Async stream read. |
| `GenerateWithReport(IEmbeddedFormatCatalog, string inputFile, int, string?, int?, int)` | `(IReadOnlyList<FuzzVariant>, FuzzReport)` | Also emits coverage stats. |
| `GenerateWithReport(IEmbeddedFormatCatalog, byte[], string fileName, int, string?, int?, int)` | `(IReadOnlyList<FuzzVariant>, FuzzReport)` | Byte-array + report. |

Parameters:

- **`count`** — target number of variants. Generation gives up after `count * 20` attempts.
- **`forcedFormat`** — skip autodetection by format name or extension.
- **`seed`** — pass an `int` for deterministic corpora across CI runs.
- **`compound`** — number of mutations per variant (compound mutations target distinct fields when possible).

### `FuzzVariant`

| Member | Type | Description |
|---|---|---|
| `Index` | `int` | Position in the batch. |
| `OriginalFile` | `string` | Source file name. |
| `FormatName` | `string` | Detected (or forced) format name. |
| `Strategy` | `string` | Name of the first mutation applied. |
| `Field` | `string` | First mutated field. |
| `Description` | `string` | Strategy description from the `.whfmt`. |
| `Data` | `byte[]` | Mutated bytes. |
| `MutationCount` | `int` | `compound` value used (≥ 1). |
| `MutationLog` | `IReadOnlyList<MutationLogEntry>` | Per-step provenance. |
| `Error` | `string?` | Set when generation failed (format unknown, no `.whfmt`). |
| `IsError` | `bool` | `Error is not null`. |
| `SuggestedFileName` | `string` | `"<stem>_fuzz<NNNN>_<Strategy><ext>"`. |

### `FuzzReport`

| Member | Type | Description |
|---|---|---|
| `FormatName` | `string` | Session format. |
| `TotalVariants` / `ErrorCount` | `int` | Includes failures. |
| `FieldCoverage` | `IReadOnlyDictionary<string,int>` | How many mutations hit each field. |
| `StrategyDistribution` | `IReadOnlyDictionary<MutationType,int>` | Per-strategy histogram. |
| `UntestedFields` | `IReadOnlyList<string>` | Fields with no strategy entry. |
| `AverageMutationsPerVariant` | `double` | Useful in compound mode. |
| `Seed` | `int?` | Reproducibility token. |
| `MostTargetedField` | `string?` | Most-touched field name. |
| `DominantStrategy` | `MutationType?` | Most-frequent mutation. |
| `ToString()` | `string` | Compact text summary. |

### `FuzzSession`

| Member | Type / Signature | Description |
|---|---|---|
| `ctor(IEmbeddedFormatCatalog catalog, int? seed = null)` | — | Seed is offset by `+ generation * 397` each round. |
| `Corpus` | `IReadOnlyList<FuzzVariant>` | All accumulated variants. |
| `Generation` | `int` | 0-based generation counter. |
| `NextGeneration(string inputFile, int count = 10, string? forcedFormat = null, int compound = 1)` | `IReadOnlyList<FuzzVariant>` | Append a batch from a file. |
| `NextGeneration(byte[] inputData, string fileName, int count = 10, string? forcedFormat = null, int compound = 1)` | `IReadOnlyList<FuzzVariant>` | Byte-array overload. |
| `SaveCorpusAsync(string directory)` | `Task` | Writes each variant + `manifest.json`. |
| `SaveCorpus(string directory)` | `void` | Synchronous wrapper. |
| `static LoadCorpusAsync(IEmbeddedFormatCatalog catalog, string directory, int? seed = null)` | `Task<FuzzSession>` | Re-hydrates a previously saved corpus. |

---

## Usage examples

### 1. Generate 50 PNG mutants

```csharp
using WhfmtFuzz;
using WpfHexEditor.Core.Definitions;

var catalog = EmbeddedFormatCatalog.Instance;
var variants = FormatFuzzer.Generate(catalog, "golden.png", count: 50);

foreach (var v in variants.Where(v => !v.IsError))
    File.WriteAllBytes(Path.Combine("corpus", v.SuggestedFileName), v.Data);
```

### 2. Reproducible CI corpus

```csharp
// Same seed → byte-identical corpus across runs / machines / OSes.
var variants = FormatFuzzer.Generate(catalog, "input.zip", count: 200, seed: 0xC0FFEE);
```

### 3. Compound mutations (stress multi-field invariants)

```csharp
var variants = FormatFuzzer.Generate(catalog, "input.bmp", count: 30, compound: 3, seed: 42);
foreach (var v in variants)
{
    Console.WriteLine($"Variant {v.Index}: {v.MutationCount} mutations on {v.MutationLog.Count} fields");
    foreach (var step in v.MutationLog)
        Console.WriteLine($"   - {step.Mutation,-16} → {step.Field}");
}
```

### 4. Coverage report

```csharp
var (variants, report) = FormatFuzzer.GenerateWithReport(catalog, "input.png", count: 100, seed: 1);
Console.Write(report);  // ToString() is informative
Console.WriteLine($"Untested fields: {string.Join(", ", report.UntestedFields)}");
```

### 5. Multi-generation session with corpus persistence

```csharp
var session = new FuzzSession(catalog, seed: 1);
session.NextGeneration("a.png", count: 20);
session.NextGeneration("b.jpg", count: 20);
session.NextGeneration("c.bmp", count: 20, compound: 2);
await session.SaveCorpusAsync("./corpus-v1");
// later …
var reloaded = await FuzzSession.LoadCorpusAsync(catalog, "./corpus-v1");
Console.WriteLine($"Loaded {reloaded.Corpus.Count} variants");
```

### 6. Streaming source (e.g. fuzz S3 blobs without staging to disk)

```csharp
await using var s = await s3.GetObjectStreamAsync(bucket, key);
var variants = await FormatFuzzer.GenerateAsync(catalog, s, key, count: 25, seed: 7);
```

---

## Integration with other whfmt.* packages

| Package | Synergy |
|---|---|
| `whfmt.FileFormatCatalog` | Provides the `fuzz` strategies and the autodetection used to pick the right `.whfmt`. |
| `whfmt.Validate` | Feed each `FuzzVariant.Data` to the validator and assert that the generated mutants trigger the expected failure modes (`forcedFormat:` on both sides). |
| `whfmt.Analysis` | `FormatDiff.Compare(catalog, original, variant.Data, "v")` shows exactly which field a mutant disturbed — invaluable when investigating parser crashes. |
| `whfmt.CodeGen` | Generated parsers can be the unit-under-fuzz: run the corpus through your generated `Parse(byte[])` and watch the typed exceptions surface. |

---

## Schema v3 fields consumed

```jsonc
{
  "fuzz": {
    "preserveChecksums": true,
    "strategies": [
      { "field": "width",  "mutation": "boundary_values", "rate": 1.0, "weight": 1.0,
        "description": "Width is a common integer-overflow target." },
      { "field": "magic",  "mutation": "corrupt_signature", "weight": 0.2,
        "description": "Magic-byte tampering should hit the rejection path." },
      { "field": "format", "mutation": "enum_sweep", "weight": 0.5 }
    ]
  },
  "blocks": [ /* offset/length resolution */ ],
  "checksums": [ /* recomputed when preserveChecksums = true */ ]
}
```

- **`fuzz.strategies[]`** — list of `(field, mutation, rate, weight, description)`. Strategies are picked by weighted random; if no strategies are declared the engine falls back to a single `BitFlip` on `raw_data`.
- **`fuzz.preserveChecksums`** — when `true`, every supported checksum is recomputed after mutation so the only thing the parser will reject is the *field* change, not a stale CRC.
- **`blocks[].name` / `storeAs` / `offset` / `length`** — resolve the byte range to mutate.
- **`blocks[].valueMap`** — drives `EnumSweep`; missing → falls back to random byte.
- **`checksums[].algorithm`** (`crc32` / `md5` / `sha1` / `sha256`) — recomputed when `preserveChecksums`.

### `rate` vs `weight`

- **`weight`** — relative probability the strategy is picked.
- **`rate`** — gate applied *after* selection in non-compound mode. `rate=0.001` means even when picked the mutation is applied only 1 time in 1000 (use for "rarely" strategies). Ignored in compound mode.

---

## Reproducibility & corpus management

- `seed = null` → `Random.Shared`, fast but non-reproducible.
- `seed = N` → `FuzzSession.NextSeed()` increments by `N + generation * 397`. Identical input + identical seed → identical bytes.
- `SaveCorpusAsync` writes one file per non-error variant plus `manifest.json`:

```json
[
  {
    "index": 0,
    "file": "golden_fuzz0000_BitFlip.png",
    "format": "PNG",
    "strategy": "BitFlip",
    "field": "width",
    "description": "Width overflow target",
    "mutations": 1,
    "mutationLog": [ { "mutation": "BitFlip", "field": "width" } ],
    "sizeBytes": 12576
  }
]
```

- `LoadCorpusAsync` re-hydrates the `Corpus` from `manifest.json` + sibling files.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Single variant with `IsError == true` and `Error = "Could not detect format."` | Autodetect failed. | Pass `forcedFormat:`. |
| Single variant with `Error = "No full definition for this format."` | Format is signature-only in the catalog. | File an issue against `whfmt.FileFormatCatalog`. |
| `count` requested but fewer variants returned | All candidate strategies are `rate`-gated, attempts exhausted (`count*20`). | Lower `rate` thresholds upstream or bump `compound > 1` (skips rate gate). |
| Variants all hit the same field | Only one strategy declared, or one weight dominates. | Add more `fuzz.strategies[]` to the `.whfmt`. |
| Variants larger than input | `Duplicate` / `InsertBytes` / `SliceRepeat` are size-changing. | Expected. |
| Variants smaller than input | `Truncate` is size-shrinking. | Expected. |
| `UntestedFields` non-empty | Some declared blocks have no matching `fuzz.strategies[].field`. | Add strategies, or accept the gap. |
| Checksums still invalid after mutation | `preserveChecksums == false` or unsupported algorithm. | Set the flag in the `.whfmt`, or accept that the parser will reject the variant on the CRC check first. |

---

## License

GNU AGPL v3.0. See `https://www.gnu.org/licenses/agpl-3.0.html`.

Copyright © 2016-2026 Derek Tremblay.
