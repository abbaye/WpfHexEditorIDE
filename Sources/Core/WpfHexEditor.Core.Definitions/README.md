# whfmt.FileFormatCatalog

**856** embedded file format and language definitions for automatic format detection and syntax highlighting.  
Cross-platform `net8.0` — works in any .NET 8 application. Zero external NuGet dependencies.

```
dotnet add package whfmt.FileFormatCatalog
```

> **Full documentation**: [whfmt-FileFormatCatalog-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Core/WpfHexEditor.Core.Definitions/whfmt-FileFormatCatalog-guide.md) — API reference, architecture, integration guides (Level 1–4), and .whfmt format specification.

---

## What's New in 1.5.0

### Embedded Language Injection — PHP, Markdown, and more

- **PHP-in-HTML**: `PHP.whfmt` `embeddedLanguages[]` entry wires `<?php … ?>` regions to PHP syntax highlighting in any host that uses the `EmbeddedRangeClassifier` runtime.
- **Markdown fenced blocks**: `` ```lang `` / ```` ``` ```` fences are now declared as embedded zones in `Markdown.whfmt`, enabling per-language syntax coloring inside code blocks.
- **Generic runtime contract**: any `.whfmt` can declare `syntaxDefinition.embeddedLanguages[]` with `startPattern` / `endPattern` / `language`. The runtime resolves them with zero additional changes per language.
- **Fix**: `CSS.whfmt` — broken `Type` pattern corrected (was causing false positives on plain-text files with `:` tokens).
- **No public API changes** — drop-in upgrade from 1.4.1.

---

## What's New in 1.4.0

### Catalog: 856 definitions (+57 since 1.3.2)

New formats added across: 3D, Game, Images, Network, Science, and more — bringing the total to **856 embedded definitions**.

### New companion NuGet: `whfmt.Formats`

[`whfmt.Formats`](https://www.nuget.org/packages/whfmt.Formats) is a content-only NuGet that ships all 856 `.whfmt` files as `AdditionalFiles`.  
Pair it with [`whfmt.SourceGenerator`](https://www.nuget.org/packages/whfmt.SourceGenerator) to get **compile-time C# parsers** for every format — no CLI step, no checked-in generated files:

```xml
<PackageReference Include="whfmt.FileFormatCatalog"  Version="1.4.0" />  <!-- runtime detection -->
<PackageReference Include="whfmt.Formats"             Version="1.0.0" />  <!-- .whfmt as AdditionalFiles -->
<PackageReference Include="whfmt.SourceGenerator"     Version="1.0.0" />  <!-- compile-time parsers -->
```

```csharp
// Runtime detection (whfmt.FileFormatCatalog)
var match = FormatMatcher.Match(fileBytes);

// Compile-time parser (whfmt.SourceGenerator + whfmt.Formats)
var png = PngParser.ParseFile("image.png");   // WhfmtFormats.Images namespace
Console.WriteLine(png.Width);
```

### Ecosystem overview

| Package | Role |
|---------|------|
| `whfmt.FileFormatCatalog` | Runtime — format detection, metadata, expression engine |
| `whfmt.Formats` | Content — ships `.whfmt` files as `AdditionalFiles` for source generation |
| `whfmt.SourceGenerator` | Build-time — Roslyn generator, turns `.whfmt` into typed C# parsers |
| `whfmt.Analysis` | Runtime — semantic diff using the `diff` block |
| `whfmt.Fuzz` | Runtime — format-aware fuzzing using the `fuzz` strategies |
| `whfmt.CodeGen` *(tool)* | CLI — one-shot generation for C#/F#/Rust/VB outside MSBuild |
| `whfmt.Validate` *(tool)* | CLI — validate, repair, lint-expressions |

---

## What's New in 1.3.2 (whfmt v3 GA)

First public NuGet release of the whfmt v3 family.

### Schema v3 — declarative becomes executable

- **Four new root blocks**: `diff`, `repair`, `fuzz`, `migration` — declare format-specific semantics for the companion NuGets.
- **Runtime expression engine** — `assertions[].expression`, `blocks[].condition` / `expression`, and `forensic.suspiciousPatterns[].condition` are now **evaluated at runtime** via `WhfmtExpressionEvaluator` (lexer + parser + AST cache + variable store + function registry). 11 built-in functions, custom function registration via `IWhfmtFunction`.
- **`FormatAssertionEvaluator`** — single-call bridge between the expression engine and a `.whfmt` file's `assertions[]` block. Returns `Pass | Fail | Skipped` per rule.
- **Typed variables** — `variables` can now be a typed array (`{ name, type, offset, length, endian }`) consumed by `WhfmtVariableParser.BuildStore`. The dict form is still accepted.
- **Executable function descriptors** — `functions` block accepts the v3 form `{ params, returns, body, builtIn }`. Doc-strings still accepted for legacy.
- **Negative-offset signatures** — `detection.signatures[].offset` can be negative (offset-from-end). Used by SHEBANG, FAT_BINARY, NE, LE, TRUECRYPT.

### 101 formats enriched + 8 new definitions

Cumulative catalog growth from 1.2.0 → 1.3.2:

- **101 formats enriched** — `diff`, `repair`, `fuzz`, `migration` blocks added across 15 categories: Audio (10), Images (10), Archives (9), Executables (5), Video (10), Documents (10), Game/ROM (10), Crypto (4), Network (3), Firmware (3), 3D (5), Fonts (5), Disk (5), System (3), GIS (2). The 6 priority formats (ZIP, PNG, PE/EXE, PDF, MP3, SQLite) carry complete `diff` keyFields + `repair` rules + 5–7 `fuzz` strategies.
- **8 new format definitions**: PEM, DER, P12, GPG (Crypto), UEFI, BIOS, UBoot (Firmware), DNS (Network).
- **Zero JSON parse errors** — all enriched files validated.

### Companion NuGet packages (now public)

- **`whfmt.Analysis`** — `FormatDiff.Compare()` performs field-level semantic diff using the `diff` block; outputs text / JSON / dark HTML.
- **`whfmt.Fuzz`** — `FormatFuzzer.Generate()` produces format-aware mutant files using the `fuzz` strategies with automatic checksum recomputation.
- **`whfmt.CodeGen`** *(dotnet tool)* — `whfmt-codegen generate <format>` produces a strongly-typed parser in C# / F# / Rust / VB.NET from any `.whfmt` definition.
- **`whfmt.Validate`** *(dotnet tool)* — `whfmt validate | list | info | repair | lint-expressions` CLI. The `repair` command applies `repair` rules (recompute_checksum, set_value, zero_field, truncate, pad). `lint-expressions` runs `WhfmtExpressionValidator` to catch parse errors, undeclared identifiers, and unknown function calls without executing.

### Catalog cleanup (Lots 1-7)

Sweep of the whole catalog for canonicalization before the v3 GA:

- 131 `matchMode` normalizations (`first` → `any`, etc.)
- 32 block-type swaps (`blocks[].type = '<valueType>'` → `type='field' + valueType=X`)
- 20 endian-suffixed valueType splits (`uint32be` → `valueType: uint32, endianness: big`)
- 9 `formatId` collision renames: DER→DER_CRYPTO, PEM→PEM_CRYPTO, P12→P12_CRYPTO, PKCS7→PKCS7_CRYPTO, PAK→PAK_GAME, NSF→NSF_NOTES, YAML→YAML_LANG, SYSLOG→SYSLOG_NET, PDB→PDB_DEBUG
- 8 Unix-style formats given fictive extensions (APFS → `.apfs`, SHEBANG → `.sh`, …)
- 12 exotic valueType mappings to canonical (`uint24` → `uint32`, `vint` → `int64`, `filetime` → `int64`, …)

### Phase B — pre-publication audit

- **2 bug fixes** caught by the new B8 smoke test:
  - `FormatMatcher.ScoreEntry` no longer throws `ArgumentOutOfRangeException` on negative-offset signatures.
  - Assertion engine no longer crashes on `.whfmt` variables of type `bool` or `null` (e.g. `PNG.crc32Valid = false`).
- **AST surface internalized** — `WhfmtExprNode` and the 11 record subtypes are now `internal`. Consumers go through `WhfmtExpressionEvaluator.Evaluate(string)` returning `object?`, so the evaluator can evolve (bytecode lowering, alternative parsers) without ABI break.
- **Schema v3 `category` enum aligned** to 31 values (matches the 29 on-disk category directories + Programming + Other).
- **Direct unit tests** added for `GetJsonV3`, `FormatSummaryBuilder`, `GetDocumentationBundle`, `FormatMatcher` (covers internal `ScoreEntry` via the public surface).

See the [audit-SYNTHESE](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Docs/whfmt-v3/audit-pre-publication/audit-SYNTHESE.md) for the full 4-axis audit report.

## What's New in 1.2.0

- **Catalog**: 799 definitions — schema v3, `formatId` on every entry, 57 language grammars.
- **`FormatFileAnalyzer`**: `AnalyzeDirectory()` lazy batch scan now supports async enumeration (`IAsyncEnumerable`) in addition to the synchronous overload.
- **`CatalogQuery`**: new `WithFormatId(string)` filter for exact `formatId` lookup; `Execute()` now returns `IReadOnlyList<EmbeddedFormatEntry>` (was `List<>`).
- **`FormatMetadataExtensions`**: `GetAllMetadata()` exposed as a public API; `FormatMetadata` record now implements `IEquatable<FormatMetadata>`.
- **Quality**: internal JSON parsers pre-size all list allocations via `GetArrayLength()` — reduced GC pressure on large batch scans.

## What's New in 1.1.1

- **`CatalogQuery`** — 6 new terminal operations: `Any()`, `Select<T>()`, `ToDictionary<K,V>()`, `ToExtensionDictionary()`, `ToExtensionDictionary<V>()`, `GroupByCategory()`; new `HasPreferredEditor()` filter; internal `NormalizeExt` helper ensures consistent `.ext` normalization across all extension-keyed dictionaries
- **`FormatMetadataExtensions`** — `GetAllMetadata()` bulk method: single JSON parse returns all 7 metadata blocks at once; individual methods now share internal parsers — no redundant `JsonDocument.Parse()` calls
- **`FormatSummaryBuilder`** — all `BuildXxx()` methods now call `GetAllMetadata()` once internally; private `AppendHeader` / `AppendMarkdownHeader` helpers eliminate code duplication

---

## What's New in 1.1.0

### Catalog — 799 definitions, schema v3, 57 language grammars

- **799 definitions** (789 `.whfmt` + 10 `.grammar`) — up from 675 in v1.0
- **57 language grammars** with `syntaxDefinition` blocks — up from 35 (+22 new: Dockerfile, `.env`, Nginx, HCL/Terraform, WAT, MSBuild, SourceMap, WebManifest, CSON, NDJSON, iCal, vCard, DocBook, AbiWord, WML, FODT, FB2, MHT, OpenDoc Flat, Config/INI, RESW, RESX)
- **`formatId` field** — every `.whfmt` now carries a stable machine-readable identifier (e.g. `"APFS"`, `"ZIP"`) for unambiguous cross-reference
- **whfmt schema v3** — new block types (`group`, `header`, `data`), `until` / `maxLength` / `untilInclusive` sentinel scanning, `imports` array for cross-format struct references, `SyntaxDefinition` promoted to first-class property
- **Duplicate cleanup** — removed redundant entries: `Firmware/CPIO`, `Firmware/NRG`, `Firmware/SQUASHFS`, `Game/PATCH_IPS`, `Game/PATCH_UPS`, `Programming/Markdown`, `Programming/TOML`
- **Tolerant JSON deserialisation** — new converters (`FormatRelationshipsConverter`, `TechnicalDetailsConverter`, `BoolFromAnyConverter`, `BlockDefinitionListFromMixedConverter`) handle real-world schema variation without throwing
- **Disambiguated entries** — `System/JOURNAL` renamed to `"systemd Journal (Legacy)"` to avoid collision with `SYSTEMD_JOURNAL`; extensionless formats (`FAT_BINARY`, `SHEBANG`, `ELF`) now declare `extensions: [""]` for consistent catalog lookup

### Utility Layer — Format detection is now one line

Before this release, consuming the catalog required 15–20 lines of boilerplate for basic identification. Version 1.1 ships a complete utility layer on top of the catalog:

| Utility | Namespace | Purpose |
|---|---|---|
| `FormatMatcher` | `Matching` | Scored detection façade — Extension + MagicBytes + MIME in one call |
| `FormatFileAnalyzer` | `Matching` | I/O helper — accepts `string path`, `FileInfo`, `Stream`, `ReadOnlyMemory<byte>`, all with async variants |
| `CatalogQuery` | `Query` | Fluent query builder — chain filters, ordering, and terminal operations |
| `FormatMetadataExtensions` | `Metadata` | Extension methods — surfaces forensic data, AI hints, assertions, bookmarks, inspector groups, export templates, and technical details directly from the entry |
| `FormatSummaryBuilder` | `Metadata` | Generates one-liners, plain-text cards, Markdown cards, and diagnostic dumps |
| `FormatMatchResult` | `Contracts` | Immutable scored result — confidence, source strategy, raw score |
| `MatchSource` | `Contracts` | Flags enum — `Extension`, `MagicBytes`, `MimeType`, `Combined` |

---

## About

This catalog grew out of the format detection engine inside **WpfHexEditorIDE** — a full-featured binary/text IDE built on WPF. Every time a file is opened, the IDE needs to know what it is, which editor to route it to, and how to syntax-highlight it. Rather than hardcoding rules, we built a declarative `.whfmt` format — a JSON definition file that captures magic bytes, extensions, MIME types, entropy hints, quality scores, syntax grammars, forensic intelligence, AI hints, and export templates in one place.

Over time the catalog grew to 799 definitions covering everything from Nintendo ROMs and audio codecs to machine learning models and certificate formats. The syntax grammar side expanded to 57 languages to drive the built-in code editor.

This package extracts that catalog as a standalone, cross-platform library — useful for any application that needs to identify files, route them to the right handler, provide syntax highlighting, or perform forensic triage.

---

## Quick Start

### 1 — Add the using directives

```csharp
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Contracts;
```

### 2 — Analyze a file in one line (v1.1)

```csharp
using WpfHexEditor.Core.Definitions.Matching;

var catalog = EmbeddedFormatCatalog.Instance;

// Extension + magic-byte detection with confidence score
var result = FormatFileAnalyzer.Analyze(catalog, @"C:\files\archive.zip");

Console.WriteLine(result?.Entry.Name);    // "ZIP Archive"
Console.WriteLine(result?.Confidence);   // 1.0
Console.WriteLine(result?.IsConfirmed);  // true  (extension + magic bytes agree)
Console.WriteLine(result?.Source);       // Combined
```

### 3 — Or use the raw catalog directly

```csharp
EmbeddedFormatEntry? entry = catalog.GetByExtension(".zip");
Console.WriteLine(entry?.Name);            // "ZIP Archive"
Console.WriteLine(entry?.PreferredEditor); // "structure-editor"

byte[] header = File.ReadAllBytes("unknown.bin")[..512];
EmbeddedFormatEntry? detected = catalog.DetectFromBytes(header);
Console.WriteLine(detected?.Name);         // e.g. "PNG Image"
```

### 4 — Async analysis

```csharp
var result = await FormatFileAnalyzer.AnalyzeAsync(catalog, uploadedFilePath, cancellationToken: ct);
```

### 5 — Fluent query (v1.1)

```csharp
using WpfHexEditor.Core.Definitions.Query;

var highQualityDiskFormats = catalog
    .Query()
    .InCategory(FormatCategory.Disk)
    .WithMinQuality(80)
    .HasMagicBytes()
    .OrderByQuality()
    .Execute();
```

### 6 — Rich metadata (v1.1)

```csharp
using WpfHexEditor.Core.Definitions.Metadata;

var entry = catalog.GetByExtension(".jks")!;  // Java KeyStore

// Forensic intelligence
var forensic = entry.GetForensicSummary(catalog);
Console.WriteLine(forensic?.RiskLevel);     // "medium"
Console.WriteLine(forensic?.IsHighRisk);    // false

// AI-assisted hints
var ai = entry.GetAiHints(catalog);
foreach (var hint in ai?.SuggestedInspections ?? [])
    Console.WriteLine($"  → {hint}");

// Validation assertions
foreach (var a in entry.GetAssertions(catalog))
    Console.WriteLine($"  [{a.Severity}] {a.Name}: {a.Expression}");
```

### 7 — Top-N ranked candidates for ambiguous files

```csharp
byte[] header = File.ReadAllBytes("mystery.bin")[..512];
var candidates = FormatMatcher.GetTopMatches(catalog, header, maxResults: 5);

foreach (var match in candidates)
    Console.WriteLine(match); // "ZIP Archive [MagicBytes] 99% (raw=1.00)"
```

### 8 — Generate a summary card

```csharp
var entry = catalog.GetByExtension(".zip")!;

string oneLiner = FormatSummaryBuilder.BuildOneLiner(entry);
// "ZIP Archive (Archives) — .zip .jar .apk — Quality: 92%"

string markdown = FormatSummaryBuilder.BuildMarkdown(entry, catalog);
// Full Markdown card with magic bytes, forensic section, assertions, bookmarks
```

### 9 — Lookup by `formatId` (v3)

Every entry now carries a stable, machine-readable `formatId` (e.g. `"PNG"`,
`"ZIP"`, `"DER_CRYPTO"`). Use it for O(1) deterministic lookups instead of
extension-based search:

```csharp
var png = catalog.GetByFormatId("PNG");
var der = catalog.GetByFormatId("DER_CRYPTO");   // Lot 5 disambiguated DER → DER_CRYPTO

// Fluent variant
var entry = catalog.Query().WithFormatId("PNG").First();
```

### 10 — Live assertions: validate a parsed file (v3)

`.whfmt` `assertions[]` blocks are now executable expressions evaluated against
a `WhfmtVariableStore` you populate from the parsed payload.

```csharp
using System.Buffers.Binary;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Definitions.Metadata;
using WpfHexEditor.Core.Definitions.Models.Expressions;
using WpfHexEditor.Core.Definitions.Models.Functions;

byte[] data    = File.ReadAllBytes("photo.png");
var    catalog = EmbeddedFormatCatalog.Instance;
var    entry   = catalog.GetByFormatId("PNG")!;

// 1) Populate variables from the parsed bytes.
var store = new WhfmtVariableStore();
store.Set("fileSize",        (long)data.Length);
store.Set("pngSignature",    Convert.ToHexString(data.AsSpan(0, 8)));
store.Set("ihdrChunkLength", BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(8, 4)));
store.Set("imageWidth",      BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4)));
store.Set("imageHeight",     BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4)));
store.Set("crc32Valid",      true);

// 2) Run all assertions in one pass.
var evaluator = new WhfmtExpressionEvaluator(store, WhfmtFunctionRegistry.CreateDefault());
var results   = FormatAssertionEvaluator.EvaluateAll(evaluator, entry.GetAssertions(catalog));

// 3) Surface failures.
foreach (var r in results.Where(r => r.Status == AssertionStatus.Fail))
    Console.WriteLine($"  [{r.Rule.Severity}] {r.Rule.Name}: {r.Rule.Message ?? r.Rule.Expression}");
```

`FormatAssertionEvaluator.EvaluateAll` returns one `AssertionResult` per rule with
`Status = Pass | Fail | Skipped` (skipped when a required variable hasn't been set —
useful when running mid-parse).

### 11 — Evaluate an arbitrary expression (v3)

`WhfmtExpressionEvaluator` is reusable for any `.whfmt` expression — `blocks[].condition`,
`forensic.suspiciousPatterns[].condition`, or your own DSL embedded in metadata.

```csharp
var store = new WhfmtVariableStore();
store.Set("magic",            "PK");
store.Set("compressionRatio", 1500.0);

var ev = new WhfmtExpressionEvaluator(store, WhfmtFunctionRegistry.CreateDefault());

bool   isZip   = ev.EvaluateBool("magic == 'PK'");                     // true
bool   bomb    = ev.EvaluateBool("compressionRatio > 1000");           // true
long   doubled = ev.EvaluateInt ("compressionRatio * 2");              // 3000
object? label  = ev.Evaluate    ("bomb ? 'danger' : 'ok'");            // (set bomb first)
```

Register a custom function:

```csharp
public sealed class Crc32Fn : IWhfmtFunction
{
    public string  Name => "crc32";
    public object? Invoke(IReadOnlyList<object?> args) => Crc32.Compute((byte[])args[0]!);
}

ev.Functions.Register(new Crc32Fn());
ev.EvaluateBool("crc32(payload) == expectedCrc");
```

### 12 — Static expression lint (v3)

Catch parse errors, undeclared identifiers, and unknown function calls **without
executing** — exactly what `whfmt.Validate lint-expressions` does in CI.

```csharp
using WpfHexEditor.Core.Definitions.Models.Validation;
using WpfHexEditor.Core.Definitions.Models.Functions;

var issues = WhfmtExpressionValidator.Validate(
    expression:    "fileSize > 0 && unknownVar < 100 && weirdFn(x)",
    declaredVars:  ["fileSize"],
    declaredFns:   [],
    fnRegistry:    WhfmtFunctionRegistry.CreateDefault());

foreach (var i in issues)
    Console.WriteLine($"  [{i.Severity}] {i.Code}: {i.Message}");
// [Warning] WH-EX-002: Undeclared identifier 'unknownVar'
// [Error]   WH-EX-003: Unknown function 'weirdFn'
```

### Fast Startup — PreWarm

```csharp
// Call once from a background thread at startup to pre-load all JSON into cache
await Task.Run(() => EmbeddedFormatCatalog.Instance.PreWarm());
```

---

## Core API — `EmbeddedFormatCatalog`

| Member | Returns | Description |
|---|---|---|
| `Instance` | `EmbeddedFormatCatalog` | Thread-safe lazy singleton |
| `GetAll()` | `IReadOnlySet<EmbeddedFormatEntry>` | All 799 entries |
| `GetByExtension(string)` | `EmbeddedFormatEntry?` | Extension lookup (case-insensitive, dot optional) |
| `GetByMimeType(string)` | `EmbeddedFormatEntry?` | MIME type lookup |
| `GetByCategory(FormatCategory)` | `IReadOnlyList<EmbeddedFormatEntry>` | Category browsing (enum overload) |
| `DetectFromBytes(ReadOnlySpan<byte>)` | `EmbeddedFormatEntry?` | Magic-byte scoring |
| `GetCompatibleEditorIds(string)` | `IReadOnlyList<string>` | Editor routing for a file path |
| `GetJson(string)` | `string` | Full .whfmt JSON (cached) |
| `GetSyntaxDefinitionJson(string)` | `string?` | Raw grammar JSON block |
| `GetSchemaJson(SchemaName)` | `string?` | Embedded JSON schema |
| `PreWarm()` | `void` | Pre-load all JSON into cache |
| `.Query()` | `CatalogQuery` | Begin a fluent query *(v1.1)* |

---

## Utility Layer — `FormatFileAnalyzer`

```csharp
using WpfHexEditor.Core.Definitions.Matching;

// From file path
FormatMatchResult? result = FormatFileAnalyzer.Analyze(catalog, filePath);

// From FileInfo
FormatMatchResult? result = FormatFileAnalyzer.Analyze(catalog, new FileInfo(path));

// From Stream (preserves stream position)
FormatMatchResult? result = FormatFileAnalyzer.Analyze(catalog, stream, extension: ".zip");

// From raw bytes
FormatMatchResult? result = FormatFileAnalyzer.Analyze(catalog, data.AsMemory(), ".bin");

// Async variants (all of the above)
FormatMatchResult? result = await FormatFileAnalyzer.AnalyzeAsync(catalog, filePath, ct);

// Batch directory scan (lazy enumeration)
foreach (var (path, match) in FormatFileAnalyzer.AnalyzeDirectory(catalog, @"C:\Data", recursive: true))
    Console.WriteLine($"{Path.GetFileName(path),-30}  {match?.Entry.Name}");
```

---

## Utility Layer — `FormatMatcher`

```csharp
using WpfHexEditor.Core.Definitions.Matching;

// Single best match with confidence
FormatMatchResult? result = FormatMatcher.Match(catalog, ".zip", header);
// result.Confidence   → 1.0 (Combined) | 0.5–0.99 (MagicBytes) | 0.5 (Extension) | 0.4 (MimeType)
// result.IsConfirmed  → true when Source == Combined

// Top-N ranked candidates
IReadOnlyList<FormatMatchResult> top = FormatMatcher.GetTopMatches(catalog, header, maxResults: 5);

// All entries for an ambiguous extension
IReadOnlyList<FormatMatchResult> all = FormatMatcher.GetMatchesByExtension(catalog, ".bin");

// MIME-type match
FormatMatchResult? mime = FormatMatcher.MatchMime(catalog, "application/pdf");
```

---

## Utility Layer — `CatalogQuery`

```csharp
using WpfHexEditor.Core.Definitions.Query;

// Composable filter + order + terminal
var results = catalog
    .Query()
    .InCategory(FormatCategory.Executables)   // category filter (enum)
    .WithMinQuality(75)                        // quality threshold
    .HasMagicBytes()                           // must have signatures
    .BinaryFormatsOnly()                       // exclude text formats
    .OrderByQuality()                          // best first
    .Execute();                                // materialise

// Filters
.PriorityOnly()                        // QualityScore ≥ 85
.WithExtension(".cs")                  // extension match (leading dot optional)
.TextFormatsOnly()                     // IsTextFormat == true
.HasSyntaxDefinition()                 // grammar block present
.WithPreferredEditor("code-editor")    // exact editor ID match
.HasPreferredEditor()                  // any preferred editor declared
.HasPlatform()                         // platform field non-empty
.ForPlatform("Nintendo")               // platform substring
.WithDiffMode("binary")
.HasMimeType()                         // at least one MIME type declared
.HasMagicBytes()                       // at least one signature
.WithName("ZIP")                       // exact name match
.Containing("APFS")                    // full-text in name + description
.Where(e => e.Author == "WPFHexaEditor Team")  // custom predicate

// Terminal operations
.Execute()                          → IReadOnlyList<EmbeddedFormatEntry>
.First()                            → EmbeddedFormatEntry?
.Count()                            → int
.Any()                              → bool
.Select(e => e.Name)                → IReadOnlyList<TResult>
.ToDictionary(e => e.Name, e => e)  → Dictionary<TKey, TValue>
.ToExtensionDictionary()            → Dictionary<string, EmbeddedFormatEntry>
.ToExtensionDictionary(e => e.PreferredEditor!)  → Dictionary<string, TValue>
.GroupByCategory()                  → IReadOnlyDictionary<string, IReadOnlyList<EmbeddedFormatEntry>>
```

**Extension→editor routing map** (one line):
```csharp
var editorMap = catalog.Query()
    .HasPreferredEditor()
    .ToExtensionDictionary(e => e.PreferredEditor!);
// { ".cs" → "code-editor", ".zip" → "hex-editor", … }
```

**Group by category** (for tree views / menus):
```csharp
foreach (var (category, entries) in catalog.Query().OrderByName().GroupByCategory())
{
    Console.WriteLine($"{category} ({entries.Count})");
    foreach (var e in entries) Console.WriteLine($"  {e.Name}");
}
```

---

## Utility Layer — Rich Metadata Extensions

```csharp
using WpfHexEditor.Core.Definitions.Metadata;

// All methods take the catalog as a second parameter (JSON loaded on demand, cached)

ForensicSummary?               forensic    = entry.GetForensicSummary(catalog);
AiHints?                       ai          = entry.GetAiHints(catalog);
IReadOnlyList<NavigationBookmark> bookmarks = entry.GetNavigationBookmarks(catalog);
IReadOnlyList<AssertionRule>   assertions  = entry.GetAssertions(catalog);
IReadOnlyList<InspectorGroup>  groups      = entry.GetInspectorGroups(catalog);
IReadOnlyList<ExportTemplate>  exports     = entry.GetExportTemplates(catalog);
TechnicalDetails?              tech        = entry.GetTechnicalDetails(catalog);

// Quick boolean helpers
bool highRisk  = entry.IsHighRisk(catalog);
bool encrypted = entry.SupportsEncryption(catalog);
```

---

## Utility Layer — `FormatSummaryBuilder`

```csharp
using WpfHexEditor.Core.Definitions.Metadata;

string oneLiner = FormatSummaryBuilder.BuildOneLiner(entry);
// "ZIP Archive (Archives) — .zip .jar .apk — Quality: 92%"

string plain    = FormatSummaryBuilder.BuildPlainText(entry, catalog);
// Multi-line: name, category, extensions, MIME, quality, signatures, forensic, technical details

string markdown = FormatSummaryBuilder.BuildMarkdown(entry, catalog);
// Markdown card: table, magic bytes, forensic section, bookmarks, assertions

string dump     = FormatSummaryBuilder.BuildDiagnosticDump(entry, catalog);
// Full debug dump: resource key, all fields, forensic, assertions, bookmarks, exports, technical details

string hex      = FormatSummaryBuilder.FormatHex("504B0304");
// "50 4B 03 04"
```

---

## Advanced Examples

### Security scanner — flag high-risk files

```csharp
using WpfHexEditor.Core.Definitions.Matching;
using WpfHexEditor.Core.Definitions.Metadata;

var catalog = EmbeddedFormatCatalog.Instance;

var result = FormatFileAnalyzer.Analyze(catalog, filePath);
if (result is null) return;

var forensic = result.Entry.GetForensicSummary(catalog);
if (forensic?.IsHighRisk == true)
{
    Console.WriteLine($"⛔ HIGH RISK: {result.Entry.Name} ({forensic.RiskLevel})");
    foreach (var p in forensic.SuspiciousPatterns)
        Console.WriteLine($"   ⚠ {p.Name}: {p.Description}");
}
```

### Batch folder scanner — group files by category

```csharp
using WpfHexEditor.Core.Definitions.Matching;

var catalog = EmbeddedFormatCatalog.Instance;

var summary = FormatFileAnalyzer
    .AnalyzeDirectory(catalog, @"C:\Downloads", recursive: true)
    .GroupBy(r => r.Match?.Entry.Category ?? "Unknown")
    .OrderByDescending(g => g.Count());

foreach (var g in summary)
    Console.WriteLine($"{g.Key,-20}  {g.Count(),5} files  " +
                      $"spoofed: {g.Count(r => r.Match?.Source == MatchSource.MagicBytes && !r.Match.IsConfirmed)}");
```

### Magic-byte validator — detect extension spoofing

```csharp
using WpfHexEditor.Core.Definitions.Matching;

byte[] header = File.ReadAllBytes(filePath)[..512];
var result = FormatMatcher.Match(catalog, filePath, header);

// Spoofed = magic bytes found a format but it doesn't match the extension
bool spoofed = result?.Source == MatchSource.MagicBytes && !result.IsConfirmed;

if (spoofed)
    throw new SecurityException($"Extension mismatch — file is actually: {result!.Entry.Name}");
```

### Grammar loader — wire syntax highlighting

```csharp
using WpfHexEditor.Core.Definitions.Query;

var grammars = catalog
    .Query()
    .InCategory(FormatCategory.Programming)
    .HasSyntaxDefinition()
    .OrderByName()
    .Execute();

foreach (var lang in grammars)
{
    string? grammar = catalog.GetSyntaxDefinitionJson(lang.ResourceKey);
    if (grammar is null) continue;
    // MyTokenizerRegistry.Register(lang.Name, grammar);
    Console.WriteLine($"Loaded: {lang.Name} ({lang.Extensions.FirstOrDefault()})");
}
```

### Dependency injection setup

```csharp
// Register the injectable interface
services.AddSingleton<IEmbeddedFormatCatalog>(EmbeddedFormatCatalog.Instance);

// Inject into services
public class FormatService(IEmbeddedFormatCatalog catalog) { ... }
```

---

## Features

### Core Detection
- **799 embedded definitions** (789 `.whfmt` + 10 `.grammar`) — extension, MIME type, and magic-byte lookup
- `DetectFromBytes(ReadOnlySpan<byte>)` — zero-alloc magic-byte scoring
- `formatId` field on every entry — stable machine-readable identifier for cross-reference
- 29 categories: Archives, Audio, Images, Game, Documents, Video, System, 3D, Disk, Crypto, and more

### Utility Layer *(v1.1)*
- `FormatFileAnalyzer` — one-line file analysis from path / `FileInfo` / `Stream` / bytes, sync and async
- `FormatMatcher` — scored multi-strategy detection with confidence, ranked top-N candidates
- `CatalogQuery` — fluent builder: 15 filter methods, 3 ordering modes, 9 terminal operations (`Execute`, `First`, `Count`, `Any`, `Select<T>`, `ToDictionary`, `ToExtensionDictionary`, `ToExtensionDictionary<V>`, `GroupByCategory`)
- `FormatMetadataExtensions` — forensic data, AI hints, assertions, bookmarks, inspector groups, export templates, technical details
- `FormatSummaryBuilder` — one-liner, plain text, Markdown card, diagnostic dump

### Syntax Highlighting
- **57 language grammars** with `syntaxDefinition` blocks — C#, Python, JS/TS, Go, Rust, Java, Kotlin, Swift, YAML, TOML, Markdown, Dockerfile, HCL/Terraform, Nginx, WAT, MSBuild, iCal, vCard, DocBook, and more
- `GetSyntaxDefinitionJson(resourceKey)` — raw grammar JSON ready for a tokenizer
- `HasSyntaxDefinition` flag + `.Query().HasSyntaxDefinition()` for fast filtering

### whfmt Schema v3
- `formatId` — stable machine-readable identifier on every definition
- `SyntaxDefinition` — promoted to first-class property; drives code-editor grammar registration
- New block types: `group`, `header`, `data` — structural grouping for binary parsers
- `until` / `maxLength` / `untilInclusive` — sentinel-based field scanning (Boyer-Moore-Horspool)
- `imports` — cross-format struct references (`$ref` + alias)
- `forensic`, `aiHints`, `assertions`, `navigation.bookmarks`, `inspector.groups`, `exportTemplates`, `TechnicalDetails` — full rich metadata surface

### What a v3 `.whfmt` looks like

Minimal example highlighting the v3-only additions: `formatId`, typed `variables`
array, executable `functions` block, negative-offset signature, runnable `assertion`,
and the `diff` / `fuzz` blocks consumed by the companion NuGets.

```jsonc
{
  "formatName":    "ZIP Archive",
  "formatId":      "ZIP",                    // v3 — unique, machine-readable
  "version":       "1.14",
  "category":      "Archives",
  "extensions":    [".zip", ".jar", ".apk"],
  "mimeTypes":     ["application/zip"],
  "preferredEditor": "hex-editor",

  // Detection — supports negative offsets in v3 (offset-from-end)
  "detection": {
    "signatures": [
      { "value": "504B0304", "offset":  0, "weight": 1.0, "label": "Local File Header" },
      { "value": "504B0506", "offset":  0, "weight": 0.8, "label": "End of Central Directory" },
      { "value": "55AA",     "offset": -2, "weight": 0.5, "label": "Tail marker" }
    ],
    "matchMode":    "best",
    "minimumScore": 0.5,
    "minFileSize":  22
  },

  // Typed variables (v3 preferred form) — feed WhfmtVariableStore
  "variables": [
    { "name": "magic",             "type": "ascii",  "offset": 0, "length": 4 },
    { "name": "compressionMethod", "type": "uint16", "offset": 8, "length": 2, "endian": "little" }
  ],

  // Executable functions (v3) — invokable from expressions
  "functions": {
    "isZipMagic": { "params": [], "returns": "bool", "body": "magic == 'PK'" },
    "crc32":      { "params": ["bytes"], "returns": "uint32", "builtIn": "crc32" }
  },

  // Runtime assertions — evaluated by FormatAssertionEvaluator
  "assertions": [
    { "name": "magic valid",
      "expression": "magic == 'PK'",
      "severity":   "error",
      "message":    "Missing ZIP magic bytes" },
    { "name": "supported compression",
      "expression": "compressionMethod == 0 || compressionMethod == 8",
      "severity":   "warning" }
  ],

  // Consumed by whfmt.Analysis (semantic diff)
  "diff": { "keyFields": ["centralDirectoryEntries"], "ignoreFields": ["timestamp"] },

  // Consumed by whfmt.Fuzz (mutation engine)
  "fuzz": {
    "preserveChecksums": true,
    "strategies": [
      { "field": "compressionMethod", "mutation": "enum_sweep",      "weight": 1.0 },
      { "field": "fileSize",          "mutation": "boundary_values", "weight": 0.7 }
    ]
  }
}
```

See the [full guide](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Core/WpfHexEditor.Core.Definitions/whfmt-FileFormatCatalog-guide.md#the-whfmt-format-schema-v3) for the complete annotated schema (29 categories,
15 block types, `repair` / `migration` / `formatRelationships` / `syntaxDefinition`).

### Quality & Performance
- `FormatCategory` and `SchemaName` enums — IntelliSense, no string typos
- Singleton backed by `LazyInitializer` — thread-safe, zero lock contention after init
- `FrozenSet<T>` — O(1) set operations on the entry index
- JSON cache — each resource key loaded once, then served from memory
- `PreWarm()` — absorb startup cost on a background thread

---

## Changelog

### 1.3.2 (whfmt v3 GA)

First public release of the whfmt v3 family.

**Schema v3 — declarative becomes executable**

- 4 new root blocks: `diff`, `repair`, `fuzz`, `migration`.
- Runtime expression engine (`WhfmtExpressionEvaluator` + variable store + 11 built-in functions + custom registration via `IWhfmtFunction`).
- `FormatAssertionEvaluator` bridges expressions to `assertions[]` blocks.
- Typed variables array + executable function descriptors.
- Negative-offset signatures (offset-from-end).

**Catalog**

- 101 formats enriched with `diff` / `repair` / `fuzz` / `migration` blocks across 15 categories.
- 8 new format definitions: PEM, DER, P12, GPG, UEFI, BIOS, UBoot, DNS.
- 6 priority formats fully wired (ZIP, PNG, PE/EXE, PDF, MP3, SQLite).
- Cleanup Lots 1-7: 131 matchMode normalizations, 32 block-type swaps, 20 endian splits, 9 formatId collision renames, 8 fictive extensions, 12 exotic valueType mappings.

**Companion NuGets (public)**

- `whfmt.Analysis` — semantic diff via `FormatDiff.Compare()`.
- `whfmt.Fuzz` — format-aware fuzzing via `FormatFuzzer.Generate()`.
- `whfmt.CodeGen` — dotnet tool generating typed parsers in C# / F# / Rust / VB.NET.
- `whfmt.Validate` — dotnet tool with `validate`, `list`, `info`, `repair`, `lint-expressions` commands.

**Phase B (pre-publication audit)**

- Bug fix: `FormatMatcher.ScoreEntry` handles negative-offset signatures.
- Bug fix: assertion engine ingests `bool` and `null` variables without throwing.
- `WhfmtExprNode` AST internalized (consumers go through `Evaluate(string) → object?`).
- Schema v3 `category` enum aligned to 31 values.
- Direct unit tests added for `GetJsonV3`, `FormatSummaryBuilder`, `GetDocumentationBundle`, `FormatMatcher`.

### 1.2.0

- **Catalog**: 799 definitions, schema v3, `formatId` on every entry, 57 language grammars.
- **`FormatFileAnalyzer`**: `AnalyzeDirectory()` supports async enumeration.
- **`CatalogQuery`**: `WithFormatId(string)` filter; `Execute()` returns `IReadOnlyList<>`.
- **`FormatMetadataExtensions`**: `FormatMetadata` record now implements `IEquatable<FormatMetadata>`.
- **Quality**: pre-sized list allocations in all JSON parsers.

### 1.1.1

#### CatalogQuery
- **6 new terminal operations** — `Any()`, `Select<T>()`, `ToDictionary<K,V>()` (auto `OrdinalIgnoreCase` for string keys), `ToExtensionDictionary()`, `ToExtensionDictionary<V>(valueSelector)`, `GroupByCategory()`
- **`HasPreferredEditor()`** — new filter: keeps only entries with a non-null, non-empty `PreferredEditor` field; complements the existing `WithPreferredEditor(editorId)` exact-match filter
- **Internal `NormalizeExt` helper** — consistent `.ext` lowercasing across all `ToExtensionDictionary` overloads
- **`BuildQuery()` pipeline** — single private method eliminates predicate-iteration duplication across all terminals

#### FormatMetadataExtensions
- **`GetAllMetadata()`** — new bulk method on `EmbeddedFormatEntry`: parses the `.whfmt` JSON exactly once and returns a `FormatMetadata` record containing all 7 metadata blocks (`Forensic`, `AiHints`, `Bookmarks`, `Assertions`, `InspectorGroups`, `ExportTemplates`, `TechnicalDetails`)
- **`FormatMetadata`** record — with `IsHighRisk` and `SupportsEncryption` boolean shortcuts
- **Shared internal parsers** — `ParseForensic`, `ParseAiHints`, `ParseBookmarks`, `ParseAssertions`, `ParseInspectorGroups`, `ParseExportTemplates`, `ParseTechnicalDetails` — called by both `GetAllMetadata()` and individual public methods; eliminates redundant `JsonDocument.Parse()` calls
- **Pre-sized list allocations** — `GetArrayLength()` used on all JSON array parsers

#### FormatSummaryBuilder
- **Single-parse rendering** — `BuildPlainText()`, `BuildMarkdown()`, `BuildDiagnosticDump()` now call `GetAllMetadata()` once and pass the result to private rendering helpers
- **`AppendHeader` / `AppendMarkdownHeader`** private helpers — eliminate duplicated header-building logic

### 1.1.0

#### Catalog
- **799 definitions** — 789 `.whfmt` + 10 `.grammar` (up from 675 in v1.0)
- **57 language grammars** — `syntaxDefinition` blocks added to 22 new formats: Dockerfile, `.env`, Nginx, HCL/Terraform, WAT, MSBuild, SourceMap, WebManifest, CSON, NDJSON, iCal, vCard, DocBook, AbiWord, WML, FODT, FB2, MHT, OpenDoc Flat, Config/INI, RESW, RESX
- **`formatId`** — stable machine-readable identifier injected into all 788 `.whfmt` files
- **Schema v3** — new block types (`group`, `header`, `data`), `until`/`maxLength`/`untilInclusive` sentinel fields, `imports` array, `SyntaxDefinition` as first-class property; `whfmt-schema-canonical-v3.json` updated accordingly
- **Duplicate cleanup** — removed 7 redundant entries: `Firmware/CPIO`, `Firmware/NRG`, `Firmware/SQUASHFS`, `Game/PATCH_IPS`, `Game/PATCH_UPS`, `Programming/Markdown`, `Programming/TOML`
- **Tolerant JSON deserialisation** — `FormatRelationshipsConverter` (array→object), `TechnicalDetailsConverter` (string→RawDescription), `BoolFromAnyConverter`, `BlockDefinitionListFromMixedConverter` — all 6 EmbeddedWhfmt_Tests green
- **`System/JOURNAL`** renamed to `"systemd Journal (Legacy)"` to disambiguate from `SYSTEMD_JOURNAL`
- **Extensionless formats** — `FAT_BINARY`, `SHEBANG`, `ELF` now declare `extensions: [""]` for consistent catalog lookup

#### Utility Layer (new)
- **`FormatMatcher`** — stateless scored detection façade: `Match()` (extension + magic bytes + MIME combined), `GetTopMatches()` (ranked top-N), `GetMatchesByExtension()`, `MatchMime()`
- **`FormatFileAnalyzer`** — zero-boilerplate I/O: accepts `string path`, `FileInfo`, `Stream`, `ReadOnlyMemory<byte>`; full `async` variants; `AnalyzeDirectory()` lazy batch scan
- **`CatalogQuery`** — fluent builder via `.Query()` on `IEmbeddedFormatCatalog`: filter, order, and terminal operations (expanded in v1.1.1)
- **`FormatMetadataExtensions`** — extension methods on `EmbeddedFormatEntry`: `GetForensicSummary()`, `GetAiHints()`, `GetNavigationBookmarks()`, `GetAssertions()`, `GetInspectorGroups()`, `GetExportTemplates()`, `GetTechnicalDetails()`, `IsHighRisk()`, `SupportsEncryption()`
- **`FormatSummaryBuilder`** — human-readable output without WPF: `BuildOneLiner()`, `BuildPlainText()`, `BuildMarkdown()`, `BuildDiagnosticDump()`, `FormatHex()`
- **`FormatMatchResult`** record — `Entry`, `Confidence` (0.0–1.0), `Source`, `RawScore`, `IsConfirmed`
- **`MatchSource`** flags enum — `Extension`, `MagicBytes`, `MimeType`, `Combined`

#### Documentation
- Guide expanded with Level 4 (Rich Metadata) section, full utility layer examples, updated `.whfmt` format reference for v2.4 fields

### 1.0.0

- Initial NuGet release — cross-platform `net8.0`
- `EmbeddedFormatCatalog` singleton: `GetAll`, `GetByExtension`, `GetByMimeType`, `GetByCategory`, `DetectFromBytes`, `GetCompatibleEditorIds`, `GetJson`, `GetSyntaxDefinitionJson`, `GetSchemaJson`, `PreWarm`
- `FormatCategory` enum — 29 categories with type-safe overload
- `SchemaName` enum — 5 embedded JSON schemas
- 675 `.whfmt` definitions + 35 language grammars

---

## Included Assemblies

Both bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|---|---|
| WpfHexEditor.Core.Definitions | `EmbeddedFormatCatalog` + utility layer + 799 embedded definitions (789 `.whfmt` + 10 `.grammar`) |
| WpfHexEditor.Core.Contracts | `IEmbeddedFormatCatalog`, `EmbeddedFormatEntry`, `FormatMatchResult`, `MatchSource`, `FormatCategory`, `SchemaName` |

---

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
