# whfmt.FileFormatCatalog

790+ embedded file format and language definitions for automatic format detection and syntax highlighting.  
Cross-platform `net8.0` — works in any .NET 8 application. Zero external NuGet dependencies.

```
dotnet add package whfmt.FileFormatCatalog
```

> **Full documentation**: [whfmt-FileFormatCatalog-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Core/WpfHexEditor.Core.Definitions/whfmt-FileFormatCatalog-guide.md) — API reference, architecture, integration guides (Level 1–4), and .whfmt format specification.

---

## What's New in 1.1.0

### Catalog — 790+ definitions, schema v2.4, 57 language grammars

- **790+ definitions** (782 `.whfmt` + 10 `.grammar`) — up from 675 in v1.0
- **57 language grammars** with `syntaxDefinition` blocks — up from 35 (+22 new: Dockerfile, `.env`, Nginx, HCL/Terraform, WAT, MSBuild, SourceMap, WebManifest, CSON, NDJSON, iCal, vCard, DocBook, AbiWord, WML, FODT, FB2, MHT, OpenDoc Flat, Config/INI, RESW, RESX)
- **`formatId` field** — every `.whfmt` now carries a stable machine-readable identifier (e.g. `"APFS"`, `"ZIP"`) for unambiguous cross-reference
- **whfmt schema v2.4** — new block types (`group`, `header`, `data`), `until` / `maxLength` / `untilInclusive` sentinel scanning, `imports` array for cross-format struct references, `SyntaxDefinition` promoted to first-class property
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

Over time the catalog grew to 790+ definitions covering everything from Nintendo ROMs and audio codecs to machine learning models and certificate formats. The syntax grammar side expanded to 57 languages to drive the built-in code editor.

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
| `GetAll()` | `IReadOnlySet<EmbeddedFormatEntry>` | All 790+ entries |
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

// Other filters
.PriorityOnly()              // QualityScore ≥ 85
.WithExtension(".cs")        // extension match
.TextFormatsOnly()           // IsTextFormat == true
.HasSyntaxDefinition()       // grammar block present
.WithPreferredEditor("code-editor")
.ForPlatform("Nintendo")     // platform substring
.WithDiffMode("binary")
.Containing("APFS")          // full-text search in name + description
.Where(e => e.Author == "WPFHexaEditor Team")  // custom predicate

// Terminal operations
.Execute()   → IReadOnlyList<EmbeddedFormatEntry>
.First()     → EmbeddedFormatEntry?
.Count()     → int
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
- **790+ embedded definitions** (782 `.whfmt` + 10 `.grammar`) — extension, MIME type, and magic-byte lookup
- `DetectFromBytes(ReadOnlySpan<byte>)` — zero-alloc magic-byte scoring
- `formatId` field on every entry — stable machine-readable identifier for cross-reference
- 27 categories: Archives, Audio, Images, Game, Documents, Video, System, 3D, Disk, Crypto, and more

### Utility Layer *(v1.1)*
- `FormatFileAnalyzer` — one-line file analysis from path / `FileInfo` / `Stream` / bytes, sync and async
- `FormatMatcher` — scored multi-strategy detection with confidence, ranked top-N candidates
- `CatalogQuery` — fluent builder: 14 filter methods, 3 ordering modes, 3 terminal operations
- `FormatMetadataExtensions` — forensic data, AI hints, assertions, bookmarks, inspector groups, export templates, technical details
- `FormatSummaryBuilder` — one-liner, plain text, Markdown card, diagnostic dump

### Syntax Highlighting
- **57 language grammars** with `syntaxDefinition` blocks — C#, Python, JS/TS, Go, Rust, Java, Kotlin, Swift, YAML, TOML, Markdown, Dockerfile, HCL/Terraform, Nginx, WAT, MSBuild, iCal, vCard, DocBook, and more
- `GetSyntaxDefinitionJson(resourceKey)` — raw grammar JSON ready for a tokenizer
- `HasSyntaxDefinition` flag + `.Query().HasSyntaxDefinition()` for fast filtering

### whfmt Schema v2.4
- `formatId` — stable machine-readable identifier on every definition
- `SyntaxDefinition` — promoted to first-class property; drives code-editor grammar registration
- New block types: `group`, `header`, `data` — structural grouping for binary parsers
- `until` / `maxLength` / `untilInclusive` — sentinel-based field scanning (Boyer-Moore-Horspool)
- `imports` — cross-format struct references (`$ref` + alias)
- `forensic`, `aiHints`, `assertions`, `navigation.bookmarks`, `inspector.groups`, `exportTemplates`, `TechnicalDetails` — full rich metadata surface

### Quality & Performance
- `FormatCategory` and `SchemaName` enums — IntelliSense, no string typos
- Singleton backed by `LazyInitializer` — thread-safe, zero lock contention after init
- `FrozenSet<T>` — O(1) set operations on the entry index
- JSON cache — each resource key loaded once, then served from memory
- `PreWarm()` — absorb startup cost on a background thread

---

## Changelog

### 1.1.0

#### Catalog
- **790+ definitions** — 782 `.whfmt` + 10 `.grammar` (up from 675 in v1.0)
- **57 language grammars** — `syntaxDefinition` blocks added to 22 new formats: Dockerfile, `.env`, Nginx, HCL/Terraform, WAT, MSBuild, SourceMap, WebManifest, CSON, NDJSON, iCal, vCard, DocBook, AbiWord, WML, FODT, FB2, MHT, OpenDoc Flat, Config/INI, RESW, RESX
- **`formatId`** — stable machine-readable identifier injected into all 788 `.whfmt` files
- **Schema v2.4** — new block types (`group`, `header`, `data`), `until`/`maxLength`/`untilInclusive` sentinel fields, `imports` array, `SyntaxDefinition` as first-class property; `whfmt.schema.json` updated accordingly
- **Duplicate cleanup** — removed 7 redundant entries: `Firmware/CPIO`, `Firmware/NRG`, `Firmware/SQUASHFS`, `Game/PATCH_IPS`, `Game/PATCH_UPS`, `Programming/Markdown`, `Programming/TOML`
- **Tolerant JSON deserialisation** — `FormatRelationshipsConverter` (array→object), `TechnicalDetailsConverter` (string→RawDescription), `BoolFromAnyConverter`, `BlockDefinitionListFromMixedConverter` — all 6 EmbeddedWhfmt_Tests green
- **`System/JOURNAL`** renamed to `"systemd Journal (Legacy)"` to disambiguate from `SYSTEMD_JOURNAL`
- **Extensionless formats** — `FAT_BINARY`, `SHEBANG`, `ELF` now declare `extensions: [""]` for consistent catalog lookup

#### Utility Layer (new)
- **`FormatMatcher`** — stateless scored detection façade: `Match()` (extension + magic bytes + MIME combined), `GetTopMatches()` (ranked top-N), `GetMatchesByExtension()`, `MatchMime()`
- **`FormatFileAnalyzer`** — zero-boilerplate I/O: accepts `string path`, `FileInfo`, `Stream`, `ReadOnlyMemory<byte>`; full `async` variants; `AnalyzeDirectory()` lazy batch scan
- **`CatalogQuery`** — fluent builder via `.Query()` on `IEmbeddedFormatCatalog`: 14 filter methods, 3 ordering modes, `.Execute()` / `.First()` / `.Count()`
- **`FormatMetadataExtensions`** — extension methods on `EmbeddedFormatEntry`: `GetForensicSummary()`, `GetAiHints()`, `GetNavigationBookmarks()`, `GetAssertions()`, `GetInspectorGroups()`, `GetExportTemplates()`, `GetTechnicalDetails()`, `IsHighRisk()`, `SupportsEncryption()`
- **`FormatSummaryBuilder`** — human-readable output without WPF: `BuildOneLiner()`, `BuildPlainText()`, `BuildMarkdown()`, `BuildDiagnosticDump()`, `FormatHex()`
- **`FormatMatchResult`** record — `Entry`, `Confidence` (0.0–1.0), `Source`, `RawScore`, `IsConfirmed`
- **`MatchSource`** flags enum — `Extension`, `MagicBytes`, `MimeType`, `Combined`

#### Documentation
- Guide expanded with Level 4 (Rich Metadata) section, full utility layer examples, updated `.whfmt` format reference for v2.4 fields

### 1.0.0

- Initial NuGet release — cross-platform `net8.0`
- `EmbeddedFormatCatalog` singleton: `GetAll`, `GetByExtension`, `GetByMimeType`, `GetByCategory`, `DetectFromBytes`, `GetCompatibleEditorIds`, `GetJson`, `GetSyntaxDefinitionJson`, `GetSchemaJson`, `PreWarm`
- `FormatCategory` enum — 27 categories with type-safe overload
- `SchemaName` enum — 5 embedded JSON schemas
- 675 `.whfmt` definitions + 35 language grammars

---

## Included Assemblies

Both bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|---|---|
| WpfHexEditor.Core.Definitions | `EmbeddedFormatCatalog` + utility layer + 790+ embedded definitions (782 `.whfmt` + 10 `.grammar`) |
| WpfHexEditor.Core.Contracts | `IEmbeddedFormatCatalog`, `EmbeddedFormatEntry`, `FormatMatchResult`, `MatchSource`, `FormatCategory`, `SchemaName` |

---

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
