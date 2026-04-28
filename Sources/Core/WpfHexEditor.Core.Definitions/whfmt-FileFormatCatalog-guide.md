# whfmt.FileFormatCatalog — Documentation

## Table of Contents

1. [Architecture](#architecture)
2. [API Reference](#api-reference)
3. [Utility Layer](#utility-layer)
4. [Integration Guide — Level 1: Basic Detection](#level-1-basic-detection)
5. [Integration Guide — Level 2: Routing and Syntax](#level-2-routing-and-syntax)
6. [Integration Guide — Level 3: Full Pipeline](#level-3-full-pipeline)
7. [Integration Guide — Level 4: Rich Metadata](#level-4-rich-metadata)
8. [The .whfmt Format](#the-whfmt-format)

---

## Architecture

### Assembly structure

The package ships two assemblies:

```
whfmt.FileFormatCatalog.nupkg
└── lib/net8.0/
    ├── WpfHexEditor.Core.Definitions.dll   — catalog engine + 675+ embedded resources
    └── WpfHexEditor.Core.Contracts.dll     — public types (interfaces, records, enums)
```

Consumers only need to reference `WpfHexEditor.Core.Definitions`. `WpfHexEditor.Core.Contracts` is bundled and flows transitively — no separate package reference needed.

### Type ownership

| Type | Assembly | Purpose |
|---|---|---|
| `EmbeddedFormatCatalog` | Core.Definitions | Singleton — catalog engine |
| `IEmbeddedFormatCatalog` | Core.Contracts | Interface — injectable abstraction |
| `EmbeddedFormatEntry` | Core.Contracts | Immutable record — one format definition |
| `FormatSignature` | Core.Contracts | Immutable record — one magic-byte signature |
| `FormatMatchResult` | Core.Contracts | Immutable record — scored detection result |
| `MatchSource` | Core.Contracts | Flags enum — Extension / MagicBytes / MimeType / Combined |
| `FormatCategory` | Core.Contracts | Enum — 27 known categories |
| `SchemaName` | Core.Contracts | Enum — 5 known embedded schemas |
| `FormatMatcher` | Core.Definitions | Stateless detection façade with confidence scoring |
| `FormatFileAnalyzer` | Core.Definitions | I/O helper — file path / FileInfo / Stream / async |
| `CatalogQuery` | Core.Definitions | Fluent query builder |
| `FormatMetadataExtensions` | Core.Definitions | Extension methods — forensic, AI hints, assertions… |
| `FormatSummaryBuilder` | Core.Definitions | Human-readable summaries — plain text / Markdown / dump |

### Initialization and caching

```
First call to EmbeddedFormatCatalog.Instance
    └── LazyInitializer creates the singleton (thread-safe)
        └── First call to GetAll()
            └── Scans all manifest resource names in the assembly
                └── Filters *.whfmt and *.grammar resources
                    └── LoadHeader() / LoadGrammarHeader() per resource
                        └── Extracts: name, category, extensions, MIME,
                                      signatures, preferredEditor, flags
                    └── Sorted by category then name
                    └── Stored as FrozenSet<EmbeddedFormatEntry>
```

`GetJson(resourceKey)` uses a separate `Dictionary<string, string>` cache — each resource is read from the embedded stream exactly once, then served from memory on all subsequent calls.

**Consequence:** the first call to `GetAll()` is the expensive one (~10–50 ms depending on hardware). All subsequent calls are O(1). Call `PreWarm()` on a background thread at startup to absorb this cost before the first user action.

### Thread safety

- `Instance` — safe to access from any thread (backed by `LazyInitializer`)
- `GetAll()` / `GetCategories()` — safe; backed by immutable `FrozenSet<T>`
- `GetJson()` — safe; uses `lock` + `TryAdd` pattern
- `DetectFromBytes()` — safe; read-only enumeration over immutable entries
- `FormatMatcher`, `FormatFileAnalyzer`, `CatalogQuery` — stateless; safe by construction
- All other methods — safe; delegate to the above

### Dependency graph

```
Your app
  └── WpfHexEditor.Core.Definitions   (net8.0)
        └── WpfHexEditor.Core.Contracts  (net8.0)
              └── [BCL only — System.*]
```

Zero external NuGet dependencies. No WPF, no platform-specific APIs.

---

## API Reference

### `EmbeddedFormatCatalog`

| Member | Returns | Description |
|---|---|---|
| `Instance` | `EmbeddedFormatCatalog` | Singleton — thread-safe, lazy |
| `GetAll()` | `IReadOnlySet<EmbeddedFormatEntry>` | All entries, sorted by category then name |
| `GetCategories()` | `IReadOnlySet<string>` | Distinct category names, alphabetical |
| `GetByExtension(string)` | `EmbeddedFormatEntry?` | First match by extension (case-insensitive, leading dot optional) |
| `GetByMimeType(string)` | `EmbeddedFormatEntry?` | First match by MIME type (case-insensitive) |
| `GetByCategory(string)` | `IReadOnlyList<EmbeddedFormatEntry>` | All entries in a category (string overload) |
| `GetByCategory(FormatCategory)` | `IReadOnlyList<EmbeddedFormatEntry>` | All entries in a category (enum overload) |
| `DetectFromBytes(ReadOnlySpan<byte>)` | `EmbeddedFormatEntry?` | Best match by magic-byte scoring |
| `GetCompatibleEditorIds(string)` | `IReadOnlyList<string>` | Compatible editor IDs for a file path |
| `GetJson(string)` | `string` | Full .whfmt JSON for a resource key (cached) |
| `GetSyntaxDefinitionJson(string)` | `string?` | Raw `syntaxDefinition` block JSON |
| `GetSchemaJson(string)` | `string?` | Embedded schema JSON (string overload) |
| `GetSchemaJson(SchemaName)` | `string?` | Embedded schema JSON (enum overload) |
| `PreWarm()` | `void` | Pre-load all JSON into memory cache |
| `.Query()` | `CatalogQuery` | Begin a fluent query (extension method) |

### `EmbeddedFormatEntry`

Immutable positional record. All fields populated from the `.whfmt` header at catalog load time.

| Field | Type | Notes |
|---|---|---|
| `ResourceKey` | `string` | Assembly manifest resource name — pass to `GetJson()` / `GetSyntaxDefinitionJson()` |
| `Name` | `string` | Human-readable format name, e.g. `"ZIP Archive"` |
| `Category` | `string` | Logical category, e.g. `"Archives"` |
| `Description` | `string` | Short description of the format |
| `Extensions` | `IReadOnlyList<string>` | File extensions with leading dot, e.g. `[".zip", ".jar"]` |
| `MimeTypes` | `IReadOnlyList<string>?` | MIME types, e.g. `["application/zip"]`. Null when not declared. |
| `Signatures` | `IReadOnlyList<FormatSignature>?` | Magic-byte signatures. Null when not declared. |
| `QualityScore` | `int` | 0–100 completeness score from `QualityMetrics.CompletenessScore` |
| `Version` | `string` | Format spec version, e.g. `"1.14"`. Empty when not specified. |
| `Author` | `string` | Author/organization. Empty when not specified. |
| `Platform` | `string` | Target platform for ROM/game formats, e.g. `"Nintendo Entertainment System"`. Empty otherwise. |
| `PreferredEditor` | `string?` | Recommended editor ID. Null when not declared. Typical values: `"hex-editor"`, `"code-editor"`, `"structure-editor"`. |
| `IsTextFormat` | `bool` | True when `detection.isTextFormat` is set in the .whfmt file |
| `HasSyntaxDefinition` | `bool` | True when the .whfmt file contains a `syntaxDefinition` block |
| `DiffMode` | `string?` | Preferred diff algorithm: `"text"`, `"semantic"`, `"binary"`. Null when absent. |

### `FormatMatchResult`

Immutable scored detection result. Produced by `FormatMatcher` and `FormatFileAnalyzer`.

| Member | Type | Description |
|---|---|---|
| `Entry` | `EmbeddedFormatEntry` | The matched format |
| `Confidence` | `double` | Normalised 0.0–1.0 confidence score |
| `Source` | `MatchSource` | Which strategy produced this match |
| `RawScore` | `double` | Accumulated signature weight before normalisation |
| `IsConfirmed` | `bool` | True when Source == Combined (extension + magic bytes both matched) |

### `MatchSource` flags enum

| Value | Meaning |
|---|---|
| `Extension` | Matched by file extension |
| `MagicBytes` | Matched by magic-byte signature scoring |
| `MimeType` | Matched by MIME type string |
| `Combined` | Extension + MagicBytes — highest confidence |

### `FormatCategory` enum

| Value | Category string |
|---|---|
| `Archives` | `"Archives"` |
| `Audio` | `"Audio"` |
| `CAD` | `"CAD"` |
| `Certificates` | `"Certificates"` |
| `Crypto` | `"Crypto"` |
| `Data` | `"Data"` |
| `Database` | `"Database"` |
| `Disk` | `"Disk"` |
| `Documents` | `"Documents"` |
| `Executables` | `"Executables"` |
| `Firmware` | `"Firmware"` |
| `Fonts` | `"Fonts"` |
| `GIS` | `"GIS"` |
| `Game` | `"Game"` |
| `Images` | `"Images"` |
| `MachineLearning` | `"MachineLearning"` |
| `Medical` | `"Medical"` |
| `Network` | `"Network"` |
| `Programming` | `"Programming"` |
| `RomHacking` | `"RomHacking"` |
| `Science` | `"Science"` |
| `Subtitles` | `"Subtitles"` |
| `Synalysis` | `"Synalysis"` |
| `System` | `"System"` |
| `Text` | `"Text"` |
| `Video` | `"Video"` |
| `_3D` | `"3D"` |
| `Other` | `"Other"` |

> `FormatCategory._3D` maps to the string `"3D"` — the enum overload handles this automatically.

### `SchemaName` enum

| Value | Schema file | Use case |
|---|---|---|
| `Whfmt` | `whfmt.schema.json` | Validate `.whfmt` format definitions |
| `Whcd` | `whcd.schema.json` | Class diagram visual state |
| `Whdbg` | `whdbg.schema.json` | Debug launch configuration |
| `Whidews` | `whidews.schema.json` | Workspace archive manifest |
| `Whscd` | `whscd.schema.json` | Solution-wide class diagram |

---

## Utility Layer

The utility layer sits on top of `IEmbeddedFormatCatalog` and covers four concerns:
**matching with confidence scores**, **I/O-free file analysis**, **fluent filtering**, and **rich metadata extraction**.
All utilities are cross-platform (`net8.0`, zero external dependencies).

### Namespaces

```csharp
using WpfHexEditor.Core.Definitions.Matching;   // FormatMatcher, FormatFileAnalyzer
using WpfHexEditor.Core.Definitions.Query;      // CatalogQuery, CatalogQueryExtensions
using WpfHexEditor.Core.Definitions.Metadata;   // FormatMetadataExtensions, FormatSummaryBuilder
```

---

### `FormatMatcher` — Scored detection façade

`FormatMatcher` is a stateless class whose methods combine extension + magic-byte + MIME-type
detection into a single `FormatMatchResult` with a normalised confidence score.

#### Confidence scale

| Confidence | Source | Meaning |
|---|---|---|
| `1.0` | `Combined` | Extension + magic bytes both agree |
| `0.51–0.99` | `MagicBytes` | Byte scoring only (no extension, or extension disagreed) |
| `0.5` | `Extension` | Extension matched, no signatures to confirm |
| `0.4` | `MimeType` | MIME type matched only |

#### Single best match

```csharp
using WpfHexEditor.Core.Definitions.Matching;

var catalog = EmbeddedFormatCatalog.Instance;
byte[] header = File.ReadAllBytes("myfile.zip")[..512];

var result = FormatMatcher.Match(catalog, ".zip", header);
// result.Entry.Name    → "ZIP Archive"
// result.Confidence    → 1.0
// result.Source        → MatchSource.Combined
// result.IsConfirmed   → true
```

Match from a full file path (extension extracted automatically):

```csharp
var result = FormatMatcher.Match(catalog, @"C:\uploads\archive.zip", header);
```

#### Top-N ranked candidates

Useful when debugging ambiguous files or building a "pick format" UI:

```csharp
var candidates = FormatMatcher.GetTopMatches(catalog, header, maxResults: 5);

foreach (var match in candidates)
    Console.WriteLine(match); // "ZIP Archive [MagicBytes] 99% (raw=1.00)"
```

#### All entries for an extension

Some extensions map to multiple formats (e.g. `.bin`). Get all candidates ranked by quality:

```csharp
var all = FormatMatcher.GetMatchesByExtension(catalog, ".bin");
// Returns multiple FormatMatchResult sorted by QualityScore descending
```

#### MIME-type match

```csharp
var result = FormatMatcher.MatchMime(catalog, "application/pdf");
// result.Entry.Name   → "PDF Document"
// result.Confidence   → 0.4   (MIME types are often ambiguous)
// result.Source       → MatchSource.MimeType
```

---

### `FormatFileAnalyzer` — I/O without boilerplate

`FormatFileAnalyzer` reads the first 512 bytes from a file (or stream) and delegates to
`FormatMatcher`. It eliminates the byte-reading boilerplate from every call site.

#### From a file path

```csharp
using WpfHexEditor.Core.Definitions.Matching;

var catalog = EmbeddedFormatCatalog.Instance;

var result = FormatFileAnalyzer.Analyze(catalog, @"C:\files\document.pdf");
Console.WriteLine(result?.Entry.Name);      // "PDF Document"
Console.WriteLine(result?.Confidence);      // 1.0
Console.WriteLine(result?.IsConfirmed);     // true
```

#### From a `FileInfo`

```csharp
var fi = new FileInfo(@"C:\files\archive.7z");
var result = FormatFileAnalyzer.Analyze(catalog, fi);
```

#### From a `Stream` (extension hint optional)

```csharp
using var stream = assembly.GetManifestResourceStream("MyApp.Resources.data.bin")!;
var result = FormatFileAnalyzer.Analyze(catalog, stream, extension: ".bin");
```

#### Async — from a file path

```csharp
var result = await FormatFileAnalyzer.AnalyzeAsync(catalog, @"C:\uploads\file.unknown");
if (result is null)
    Console.WriteLine("Format not recognised.");
else
    Console.WriteLine($"{result.Entry.Name} ({result.Source}, {result.Confidence:P0})");
```

#### Async — from a stream

```csharp
await using var fs = File.OpenRead(uploadedFile);
var result = await FormatFileAnalyzer.AnalyzeAsync(catalog, fs,
    extension: Path.GetExtension(uploadedFile),
    cancellationToken: ct);
```

#### Batch — scan a directory

```csharp
var catalog = EmbeddedFormatCatalog.Instance;

foreach (var (path, match) in FormatFileAnalyzer.AnalyzeDirectory(
    catalog,
    directory: @"C:\Data",
    searchPattern: "*.*",
    recursive: true))
{
    var name = match?.Entry.Name ?? "Unknown";
    var conf = match?.Confidence.ToString("P0") ?? "—";
    Console.WriteLine($"{Path.GetFileName(path),-30}  {name,-25}  {conf}");
}
```

---

### `CatalogQuery` — Fluent filtering

`CatalogQuery` is a composable query builder. Obtain one via the `.Query()` extension method
on any `IEmbeddedFormatCatalog` instance.

#### Basic example

```csharp
using WpfHexEditor.Core.Definitions.Query;

var catalog = EmbeddedFormatCatalog.Instance;

var highQualityDiskFormats = catalog
    .Query()
    .InCategory(FormatCategory.Disk)
    .WithMinQuality(80)
    .HasMagicBytes()
    .OrderByQuality()
    .Execute();

foreach (var fmt in highQualityDiskFormats)
    Console.WriteLine($"{fmt.Name,30}  {fmt.QualityScore}%");
```

#### Filter methods

| Method | Description |
|---|---|
| `.InCategory(FormatCategory)` | Restrict to a category (enum — compile-time safe) |
| `.InCategory(string)` | Restrict to a category (string overload) |
| `.WithMinQuality(int)` | Keep entries with QualityScore ≥ threshold |
| `.PriorityOnly()` | Shortcut: QualityScore ≥ 85 |
| `.HasMagicBytes()` | Only entries with at least one signature |
| `.WithExtension(string)` | Only entries that handle a given extension |
| `.TextFormatsOnly()` | Only text-based formats |
| `.BinaryFormatsOnly()` | Only binary formats |
| `.HasSyntaxDefinition()` | Only entries with a grammar block |
| `.WithPreferredEditor(string)` | e.g. `"code-editor"` or `"structure-editor"` |
| `.HasMimeType()` | Only entries that declare a MIME type |
| `.ForPlatform(string)` | e.g. `"Nintendo"` or `"SNES"` |
| `.WithDiffMode(string)` | `"text"`, `"binary"`, or `"semantic"` |
| `.Containing(string)` | Full-text search in name + description |
| `.Where(predicate)` | Custom predicate |

#### Ordering methods

| Method | Description |
|---|---|
| `.OrderByQuality()` | Highest quality score first |
| `.OrderByName()` | Alphabetical by name |
| `.OrderByCategoryThenName()` | Category, then name within category |

#### Terminal methods

| Method | Returns | Description |
|---|---|---|
| `.Execute()` | `IReadOnlyList<EmbeddedFormatEntry>` | All matching entries |
| `.First()` | `EmbeddedFormatEntry?` | First match or null |
| `.Count()` | `int` | Count without materialising the list |

#### Real-world examples

**All code-editor grammars with a syntax definition:**

```csharp
var grammars = catalog
    .Query()
    .InCategory(FormatCategory.Programming)
    .HasSyntaxDefinition()
    .OrderByName()
    .Execute();

Console.WriteLine($"{grammars.Count} language grammars available.");
```

**ROM formats for Nintendo platforms:**

```csharp
var nintendoRoms = catalog
    .Query()
    .InCategory(FormatCategory.RomHacking)
    .ForPlatform("Nintendo")
    .OrderByQuality()
    .Execute();
```

**High-risk crypto formats:**

```csharp
// Extension method on the entry — requires catalog for JSON parsing
var cryptoFormats = catalog.Query()
    .InCategory(FormatCategory.Crypto)
    .WithMinQuality(50)
    .Execute();

var highRisk = cryptoFormats
    .Where(e => e.IsHighRisk(catalog))
    .ToList();
```

**Structure-editor formats with export templates:**

```csharp
var structured = catalog
    .Query()
    .WithPreferredEditor("structure-editor")
    .WithMinQuality(70)
    .Execute()
    .Where(e => e.GetExportTemplates(catalog).Count > 0)
    .ToList();
```

---

### `FormatMetadataExtensions` — Rich whfmt metadata

These extension methods on `EmbeddedFormatEntry` surface the deep metadata blocks from
the full `.whfmt` JSON — forensic, AI hints, navigation, assertions, inspector groups,
export templates, and technical details.

> All methods take the catalog as a second parameter to load the JSON on demand.
> The JSON is cached by the catalog after the first call.

#### Forensic data

```csharp
using WpfHexEditor.Core.Definitions.Metadata;

var catalog = EmbeddedFormatCatalog.Instance;
var entry = catalog.GetByExtension(".jks")!;  // Java KeyStore

var forensic = entry.GetForensicSummary(catalog);
// forensic.Category           → "crypto"
// forensic.RiskLevel          → "medium"
// forensic.IsHighRisk         → false
// forensic.SuspiciousPatterns → list of SuspiciousPattern records

foreach (var p in forensic!.SuspiciousPatterns)
    Console.WriteLine($"⚠ {p.Name}: {p.Description}  (condition: {p.Condition})");

// Quick boolean check:
bool dangerous = entry.IsHighRisk(catalog);
```

#### AI analysis hints

```csharp
var ai = entry.GetAiHints(catalog);
// ai.AnalysisContext       → "Java KeyStore format..."
// ai.SuggestedInspections  → ["Check version field", "Inspect alias count", ...]
// ai.KnownVulnerabilities  → []

Console.WriteLine(ai?.AnalysisContext);
foreach (var hint in ai?.SuggestedInspections ?? [])
    Console.WriteLine($"  → {hint}");
```

#### Navigation bookmarks

```csharp
var apfs = catalog.GetByExtension("")
    ?? catalog.Query().InCategory(FormatCategory.Disk).Containing("APFS").First()!;

var bookmarks = apfs.GetNavigationBookmarks(catalog);
// [
//   NavigationBookmark("Object Header",        Offset=null, OffsetVar="currentOffset", Icon="header"),
//   NavigationBookmark("NXSB Magic",           Offset=32,   OffsetVar=null,            Icon="signature"),
//   NavigationBookmark("Container UUID",       Offset=72,   OffsetVar=null,            Icon="id"),
// ]

foreach (var b in bookmarks)
    Console.WriteLine($"  [{b.Icon}] {b.Name} @ {b.Offset?.ToString("X4") ?? b.OffsetVar}");
```

#### Validation assertions

```csharp
var assertions = apfs.GetAssertions(catalog);
// [
//   AssertionRule("NXSB magic valid", "nxMagic == 'NXSB'", "error", "Invalid APFS magic"),
//   AssertionRule("block size is power of 2", "nxBlockSize >= 512 && ...", "warning", null),
// ]

foreach (var a in assertions)
    Console.WriteLine($"  [{a.Severity.ToUpperInvariant()}] {a.Name}: {a.Expression}");
```

#### Inspector groups

```csharp
var groups = apfs.GetInspectorGroups(catalog);
// [
//   InspectorGroup("Object Header",      "header", ["objChecksum","objOid","objXid",...]),
//   InspectorGroup("Container Superblock","disk",  ["nxMagic","nxBlockSize",...]),
//   InspectorGroup("Feature Flags",      "flag",  ["nxFeatures",...]),
// ]

foreach (var g in groups)
    Console.WriteLine($"  [{g.Icon}] {g.Title}: {string.Join(", ", g.Fields)}");
```

#### Export templates

```csharp
var templates = apfs.GetExportTemplates(catalog);
// [
//   ExportTemplate("APFS Container Header (JSON)", "json", ["nxMagic","nxBlockSize",...]),
//   ExportTemplate("APFS Summary (CSV)",           "csv",  ["nxMagic","nxBlockSize",...]),
// ]

foreach (var t in templates)
    Console.WriteLine($"  Export as {t.Format.ToUpperInvariant()}: {t.Name}  ({t.Fields.Count} fields)");
```

#### Technical details

```csharp
var td = apfs.GetTechnicalDetails(catalog);
// td.Endianness         → "little"
// td.DataStructure      → "Copy-on-write B-tree filesystem..."
// td.SupportsEncryption → true
// td.Encryption         → "Per-file or per-volume encryption..."

Console.WriteLine($"Endianness: {td?.Endianness}");
Console.WriteLine($"Encrypted : {td?.SupportsEncryption}");

// Quick boolean check:
bool encrypted = apfs.SupportsEncryption(catalog);
```

---

### `FormatSummaryBuilder` — Human-readable summaries

Generates formatted summaries without any WPF or MVVM dependency.

#### One-liner (status bar / tooltip)

```csharp
using WpfHexEditor.Core.Definitions.Metadata;

var entry = EmbeddedFormatCatalog.Instance.GetByExtension(".zip")!;

string label = FormatSummaryBuilder.BuildOneLiner(entry);
// "ZIP Archive (Archives) — .zip .jar .apk — Quality: 92%"
```

#### Plain text (multi-line)

```csharp
string text = FormatSummaryBuilder.BuildPlainText(entry);
// Name        : ZIP Archive
// Category    : Archives
// Description : ZIP archive format (PKZIP)...
// Extensions  : .zip  .jar  .apk
// MIME        : application/zip
// Quality     : 92/100
// Signatures  :
//    @   0  50 4B 03 04  weight=1.00
//    @   0  50 4B 05 06  weight=0.80
```

Pass the catalog to include rich metadata in the output:

```csharp
string richText = FormatSummaryBuilder.BuildPlainText(entry, catalog);
// … includes Forensic, AI context, TechnicalDetails
```

#### Markdown card

```csharp
string md = FormatSummaryBuilder.BuildMarkdown(entry, catalog);
// ## ZIP Archive
// > ZIP archive format (PKZIP)...
// | Field | Value |
// |---|---|
// | Category | `Archives` |
// | Extensions | `.zip` `.jar` `.apk` |
// | MIME | `application/zip` |
// | Quality | 92/100 |
// ...
// ### Magic Bytes
// | Offset | Signature | Weight |
// |---|---|---|
// | `0x0000` | `50 4B 03 04` | 1.00 |
// ...
// ### Forensic (low risk)
// ### Navigation Bookmarks
// ### Validation Assertions
```

#### Diagnostic dump (debugging)

```csharp
string dump = FormatSummaryBuilder.BuildDiagnosticDump(entry, catalog);
// === FORMAT DIAGNOSTIC DUMP ===
// ResourceKey         : WpfHexEditor.Core.Definitions.FormatDefinitions.Archives.ZIP.whfmt
// Name                : ZIP Archive
// QualityScore        : 92
// Forensic.RiskLevel  : low  (high=False)
// Assertions (3):
//   [ERROR] NXSB magic valid: nxMagic == 'NXSB'
// Bookmarks (3):
//   Object Header  offset=currentOffset  icon=header
// ==============================
```

#### Signature formatting utility

```csharp
string hex = FormatSummaryBuilder.FormatHex("504B0304");
// "50 4B 03 04"
```

---

## Level 1: Basic Detection

This level covers the most common scenario: identifying a file and deciding what to do with it.

### Extension lookup

The fastest lookup — no I/O, pure in-memory scan.

```csharp
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Contracts;

var catalog = EmbeddedFormatCatalog.Instance;

var entry = catalog.GetByExtension(".zip");
// entry.Name          → "ZIP Archive"
// entry.Category      → "Archives"
// entry.Description   → "ZIP archive format (PKZIP)..."
// entry.PreferredEditor → "hex-editor"
// entry.MimeTypes[0]  → "application/zip"
```

Extensions are case-insensitive and the leading dot is optional:

```csharp
catalog.GetByExtension(".ZIP");  // same result
catalog.GetByExtension("zip");   // same result
```

### Magic-byte detection

Use when the extension is missing, untrusted, or ambiguous. Pass as many bytes as you can — at least 16, ideally 512.

```csharp
byte[] header = new byte[512];
using (var fs = File.OpenRead(filePath))
    fs.Read(header, 0, header.Length);

var entry = catalog.DetectFromBytes(header);
if (entry is not null)
    Console.WriteLine($"Detected: {entry.Name}");
```

#### How scoring works

Each entry's `Signatures` list is evaluated. For every signature where the bytes match at the declared offset, its `Weight` (0.0–1.0) is added to a running score. The entry with the highest total score wins. Entries with no signatures are skipped entirely.

```
ZIP has 3 signatures:
  "504B0304" offset 0  weight 1.0  ← Local File Header
  "504B0506" offset 0  weight 0.8  ← End of Central Directory
  "504B0708" offset 0  weight 0.5  ← Data Descriptor

A normal .zip file matches the first → score 1.0 → wins if no other format scores higher.
```

### MIME type lookup

```csharp
var entry = catalog.GetByMimeType("image/png");
// entry.Name       → "PNG Image"
// entry.Extensions → [".png"]
```

### Category browsing

```csharp
// Enum overload — recommended
var archives = catalog.GetByCategory(FormatCategory.Archives);

// Iterate
foreach (var fmt in archives)
    Console.WriteLine($"{fmt.Name}: {string.Join(", ", fmt.Extensions)}");
```

---

## Level 2: Routing and Syntax

This level covers editor routing and syntax highlighting integration.

### Editor routing

`GetCompatibleEditorIds` returns all editor IDs that can open a given file. `"hex-editor"` is always included as the universal fallback.

```csharp
var editors = catalog.GetCompatibleEditorIds("report.pdf");
// ["hex-editor", "structure-editor"]

var editors2 = catalog.GetCompatibleEditorIds("main.cs");
// ["hex-editor", "code-editor", "text-editor"]
```

Routing logic applied internally:

| Condition | Editor added |
|---|---|
| Always | `"hex-editor"` |
| `PreferredEditor` is set | that value |
| `IsTextFormat == true` | `"code-editor"`, `"text-editor"` |
| `Category == "Images"` | `"image-viewer"` |
| `Category == "Audio"` | `"audio-viewer"` |
| `DiffMode == "text"` | `"diff-viewer"` |

### Registering grammars for a syntax engine

`GetSyntaxDefinitionJson` returns the raw `syntaxDefinition` JSON block from the `.whfmt` file. Feed it into your tokenizer or highlighter.

```csharp
var catalog = EmbeddedFormatCatalog.Instance;

// Load all programming language grammars
foreach (var entry in catalog.GetByCategory(FormatCategory.Programming)
                              .Where(e => e.HasSyntaxDefinition))
{
    string? grammar = catalog.GetSyntaxDefinitionJson(entry.ResourceKey);
    if (grammar is null) continue;

    // Example: register with a hypothetical tokenizer registry
    // MyTokenizer.Register(entry.Name, grammar);
    Console.WriteLine($"  {entry.Name} → {entry.Extensions.FirstOrDefault()}");
}
```

Available language grammars (35): Assembly, Batch, C, C++, C#, C# Script, CSS, Dart, F#, Go, HTML, Java, JavaScript, JSON, Kotlin, Lua, Markdown, PHP, Perl, PowerShell, Python, Ruby, Rust, Shell, SQL, Swift, TOML, TypeScript, VB.NET, WHFMT, XAML, XML, XMLMarkup, YAML.

### Detecting extension spoofing

```csharp
bool IsExtensionSpoofed(string filePath)
{
    var byExtension = catalog.GetByExtension(Path.GetExtension(filePath));
    if (byExtension is null) return false;

    using var fs = File.OpenRead(filePath);
    var header = new byte[512];
    int read = fs.Read(header, 0, header.Length);
    var byBytes = catalog.DetectFromBytes(header.AsSpan(0, read));

    return byBytes is not null && byBytes.ResourceKey != byExtension.ResourceKey;
}

if (IsExtensionSpoofed(@"uploads\document.pdf"))
    throw new SecurityException("File content does not match declared extension.");
```

Or use the confidence-aware version via `FormatMatcher`:

```csharp
using WpfHexEditor.Core.Definitions.Matching;

byte[] header = File.ReadAllBytes(filePath)[..512];
var result = FormatMatcher.Match(catalog, filePath, header);

bool spoofed = result is not null
    && result.Source == MatchSource.MagicBytes
    && !result.IsConfirmed;
```

### MIME negotiation for HTTP

```csharp
// Extension → MIME (Content-Type header)
string? GetContentType(string extension)
    => catalog.GetByExtension(extension)?.MimeTypes?.FirstOrDefault()
       ?? "application/octet-stream";

// MIME → extension (download filename)
string GetExtensionForMime(string mimeType)
    => catalog.GetByMimeType(mimeType)?.Extensions.FirstOrDefault()
       ?? ".bin";

// In an ASP.NET controller:
Response.ContentType = GetContentType(Path.GetExtension(fileName));
```

### Accessing embedded JSON schemas

Use `SchemaName` enum for compile-time safety:

```csharp
// Get the whfmt schema to validate user-provided format definitions
string? schema = catalog.GetSchemaJson(SchemaName.Whfmt);

// Pass to a JSON schema validator (e.g. JsonSchema.Net)
// var jsonSchema = JsonSchema.FromText(schema);
// var result = jsonSchema.Evaluate(JsonNode.Parse(userWhfmt));
```

---

## Level 3: Full Pipeline

This level covers production-grade integration patterns.

### File identification with `FormatFileAnalyzer` (recommended)

```csharp
using WpfHexEditor.Core.Definitions.Matching;

var catalog = EmbeddedFormatCatalog.Instance;

// One line — no byte-reading boilerplate
var result = FormatFileAnalyzer.Analyze(catalog, filePath);

if (result is null)
{
    Console.WriteLine("Format not recognised.");
}
else
{
    Console.WriteLine($"Format    : {result.Entry.Name}");
    Console.WriteLine($"Category  : {result.Entry.Category}");
    Console.WriteLine($"Confidence: {result.Confidence:P0}");
    Console.WriteLine($"Source    : {result.Source}");
    Console.WriteLine($"Confirmed : {result.IsConfirmed}");
}
```

### File identification pipeline with fallback chain (low-level)

```csharp
public sealed class FileIdentifier
{
    private readonly IEmbeddedFormatCatalog _catalog;

    public FileIdentifier(IEmbeddedFormatCatalog catalog) => _catalog = catalog;

    public EmbeddedFormatEntry? Identify(string filePath)
    {
        // 1 — Fast path: extension lookup
        var byExt = _catalog.GetByExtension(Path.GetExtension(filePath));
        if (byExt?.Signatures is { Count: > 0 })
        {
            // Confirm with magic bytes when signatures are available
            using var fs = File.OpenRead(filePath);
            var header = new byte[512];
            int read = fs.Read(header, 0, header.Length);
            var byBytes = _catalog.DetectFromBytes(header.AsSpan(0, read));
            if (byBytes is not null) return byBytes;
        }

        // 2 — Extension match without signatures (text formats, config files)
        if (byExt is not null) return byExt;

        // 3 — Pure magic-byte scan (no extension, or unknown extension)
        {
            using var fs = File.OpenRead(filePath);
            var header = new byte[512];
            int read = fs.Read(header, 0, header.Length);
            return _catalog.DetectFromBytes(header.AsSpan(0, read));
        }
    }
}
```

### Dependency injection setup

`IEmbeddedFormatCatalog` is the injectable interface. Register the singleton in your DI container:

```csharp
// Microsoft.Extensions.DependencyInjection
services.AddSingleton<IEmbeddedFormatCatalog>(EmbeddedFormatCatalog.Instance);

// Then inject normally
public class MyService(IEmbeddedFormatCatalog catalog) { ... }
```

### Background pre-warming

```csharp
public static class AppStartup
{
    public static Task PreWarmCatalogAsync()
        => Task.Run(() =>
        {
            // Forces singleton creation + full entry scan + JSON cache fill
            EmbeddedFormatCatalog.Instance.PreWarm();
        });
}

// In Program.cs or App.xaml.cs — fire and forget, don't await
_ = AppStartup.PreWarmCatalogAsync();
```

### Batch folder scanner with parallel processing

```csharp
using WpfHexEditor.Core.Definitions.Matching;

var catalog = EmbeddedFormatCatalog.Instance;

var results = Directory
    .EnumerateFiles(@"C:\Data", "*.*", SearchOption.AllDirectories)
    .AsParallel()
    .WithDegreeOfParallelism(Environment.ProcessorCount)
    .Select(path =>
    {
        FormatMatchResult? match = null;
        try { match = FormatFileAnalyzer.Analyze(catalog, path); }
        catch { /* skip locked / inaccessible files */ }

        return new
        {
            Path      = path,
            Name      = match?.Entry.Name ?? "Unknown",
            Category  = match?.Entry.Category ?? "Unknown",
            Confidence= match?.Confidence ?? 0,
            IsSpoofed = match?.Source == MatchSource.MagicBytes && !match.IsConfirmed,
        };
    })
    .ToList();

// Summary by category
foreach (var g in results.GroupBy(r => r.Category).OrderByDescending(g => g.Count()))
    Console.WriteLine($"{g.Key,-20} {g.Count(),5} file(s)  spoofed: {g.Count(f => f.IsSpoofed)}");
```

### Full syntax engine bootstrap

```csharp
using WpfHexEditor.Core.Definitions.Query;

public sealed class SyntaxEngineBootstrapper
{
    private readonly IEmbeddedFormatCatalog _catalog;
    private readonly Dictionary<string, string> _grammarsByExtension = new(StringComparer.OrdinalIgnoreCase);

    public SyntaxEngineBootstrapper(IEmbeddedFormatCatalog catalog)
    {
        _catalog = catalog;
        LoadAll();
    }

    private void LoadAll()
    {
        // Fluent query — all programming language grammars
        var entries = _catalog
            .Query()
            .InCategory(FormatCategory.Programming)
            .HasSyntaxDefinition()
            .Execute();

        foreach (var entry in entries)
        {
            var grammar = _catalog.GetSyntaxDefinitionJson(entry.ResourceKey);
            if (grammar is null) continue;
            foreach (var ext in entry.Extensions)
                _grammarsByExtension.TryAdd(ext, grammar);
        }
    }

    public string? GetGrammar(string fileExtension)
        => _grammarsByExtension.GetValueOrDefault(fileExtension);

    public string GetWhfmtSchema()
        => _catalog.GetSchemaJson(SchemaName.Whfmt) ?? throw new InvalidOperationException("Schema not found.");
}
```

---

## Level 4: Rich Metadata

This level covers access to the deep metadata blocks embedded in each `.whfmt` file —
forensic intelligence, AI hints, navigation, assertions, inspector groups, and export templates.

### Security scanner — detect high-risk formats

```csharp
using WpfHexEditor.Core.Definitions.Metadata;
using WpfHexEditor.Core.Definitions.Matching;
using WpfHexEditor.Core.Definitions.Query;

var catalog = EmbeddedFormatCatalog.Instance;

// Identify file
var result = FormatFileAnalyzer.Analyze(catalog, filePath);
if (result is null) return;

var entry = result.Entry;

// Forensic check
var forensic = entry.GetForensicSummary(catalog);
if (forensic?.IsHighRisk == true)
{
    Console.WriteLine($"⛔ HIGH RISK: {entry.Name} ({forensic.RiskLevel})");
    foreach (var p in forensic.SuspiciousPatterns)
        Console.WriteLine($"   ⚠ {p.Name}: {p.Description}");
}

// AI-assisted inspection hints
var ai = entry.GetAiHints(catalog);
if (ai?.SuggestedInspections.Count > 0)
{
    Console.WriteLine("Suggested inspections:");
    foreach (var hint in ai.SuggestedInspections)
        Console.WriteLine($"   → {hint}");
}
```

### Binary validator — run format assertions

```csharp
// Get assertion rules for the detected format
var assertions = entry.GetAssertions(catalog);

Console.WriteLine($"Validation rules for {entry.Name} ({assertions.Count} assertions):");
foreach (var a in assertions)
    Console.WriteLine($"  [{a.Severity,-7}] {a.Name}: {a.Expression}");

// assertions can be passed to a rule-engine / expression evaluator
// e.g. MyRuleEngine.Evaluate(parsedFields, assertions);
```

### Structure viewer — build inspector groups

```csharp
// Inspector groups define how parsed fields are visually organised
var groups = entry.GetInspectorGroups(catalog);

foreach (var group in groups)
{
    Console.WriteLine($"[{group.Icon}] {group.Title}");
    foreach (var field in group.Fields)
        Console.WriteLine($"    {field}");
}
```

### Export pipeline — enumerate available templates

```csharp
var templates = entry.GetExportTemplates(catalog);

foreach (var tmpl in templates)
{
    Console.WriteLine($"Export: {tmpl.Name} [{tmpl.Format.ToUpperInvariant()}]");
    Console.WriteLine($"  Fields: {string.Join(", ", tmpl.Fields)}");
}
```

### Navigation — jump to known offsets

```csharp
var bookmarks = entry.GetNavigationBookmarks(catalog);

foreach (var b in bookmarks)
{
    var position = b.Offset.HasValue
        ? $"0x{b.Offset:X4}"
        : $"${b.OffsetVar}";
    Console.WriteLine($"  [{b.Icon,-12}] {b.Name,-25} @ {position}");
}
```

### Diagnostic report — full entry dump

```csharp
using WpfHexEditor.Core.Definitions.Metadata;

var entry = catalog.GetByExtension(".jks")!;
string dump = FormatSummaryBuilder.BuildDiagnosticDump(entry, catalog);
Console.WriteLine(dump);
```

### Format report — Markdown card for documentation

```csharp
var entry = catalog.Query()
    .InCategory(FormatCategory.Disk)
    .Containing("APFS")
    .First()!;

string markdown = FormatSummaryBuilder.BuildMarkdown(entry, catalog);
File.WriteAllText("apfs-format-card.md", markdown);
```

### All-in-one: catalogue of high-quality formats with rich metadata

```csharp
using WpfHexEditor.Core.Definitions.Query;
using WpfHexEditor.Core.Definitions.Metadata;

var catalog = EmbeddedFormatCatalog.Instance;

var formats = catalog
    .Query()
    .WithMinQuality(80)
    .HasMagicBytes()
    .OrderByQuality()
    .Execute();

Console.WriteLine($"{"Format",-30} {"Category",-15} {"Q",3}  {"Risk",-8}  {"Assertions",10}  {"Exports",7}");
Console.WriteLine(new string('-', 85));

foreach (var fmt in formats)
{
    var forensic   = fmt.GetForensicSummary(catalog);
    var assertions = fmt.GetAssertions(catalog);
    var exports    = fmt.GetExportTemplates(catalog);

    Console.WriteLine(
        $"{fmt.Name,-30} {fmt.Category,-15} {fmt.QualityScore,3}  " +
        $"{(forensic?.RiskLevel ?? "—"),-8}  {assertions.Count,10}  {exports.Count,7}");
}
```

---

## The .whfmt Format

A `.whfmt` file is a JSONC document (JSON with `//` comments and trailing commas allowed) that describes a binary or text file format. Here is an annotated example:

```jsonc
{
  // Root identification fields
  "formatName": "ZIP Archive",           // Human-readable name
  "version": "1.14",                     // Format spec version
  "category": "Archives",               // Must match a FormatCategory value
  "description": "...",                 // Short description
  "author": "WPFHexaEditor Team",
  "preferredEditor": "hex-editor",      // "hex-editor" | "code-editor" | "structure-editor" | etc.
  "diffMode": "binary",                 // "text" | "semantic" | "binary"

  // File association
  "extensions": [ ".zip", ".jar", ".apk" ],
  "MimeTypes":  [ "application/zip" ],

  // Detection rules
  "detection": {
    "signatures": [
      { "value": "504B0304", "offset": 0, "weight": 1.0 },  // hex bytes, byte offset, confidence
      { "value": "504B0506", "offset": 0, "weight": 0.8 }
    ],
    "matchMode": "any",                  // "any" | "all"
    "required": true,                   // whether a signature match is mandatory
    "EntropyHint": { "min": 5.0, "max": 8.0 },
    "isTextFormat": false               // true for plain-text formats (CSV, INI, etc.)
  },

  // Quality metadata
  "QualityMetrics": {
    "CompletenessScore": 90             // 0–100
  },

  // Forensic intelligence
  "forensic": {
    "category": "archive",
    "riskLevel": "low",                 // "low" | "medium" | "high" | "critical"
    "suspiciousPatterns": [
      {
        "name": "Zip bomb",
        "description": "Deeply nested archives with extreme compression ratio",
        "condition": "compressionRatio > 1000"
      }
    ]
  },

  // AI-assisted analysis hints
  "aiHints": {
    "analysisContext": "ZIP is a widely-used container format...",
    "suggestedInspections": [
      "Check local file header counts",
      "Verify central directory offset"
    ]
  },

  // Navigation bookmarks
  "navigation": {
    "bookmarks": [
      { "name": "Local File Header", "offset": 0,  "icon": "signature" },
      { "name": "Central Directory", "offsetVar": "cdOffset", "icon": "directory" }
    ]
  },

  // Validation assertions
  "assertions": [
    {
      "name": "Magic bytes valid",
      "expression": "signature == 'PK'",
      "severity": "error",
      "message": "Missing ZIP magic bytes"
    }
  ],

  // Inspector layout (field grouping for UI)
  "inspector": {
    "groups": [
      {
        "title": "File Header",
        "icon": "header",
        "fields": ["signature", "version", "flags", "compression"]
      }
    ]
  },

  // Export templates
  "exportTemplates": [
    {
      "name": "ZIP Summary (JSON)",
      "format": "json",
      "fields": ["signature", "version", "compression", "crc32"]
    }
  ],

  // Technical details
  "TechnicalDetails": {
    "endianness": "little",
    "compressionMethod": "DEFLATE / Store / BZip2 / LZMA",
    "supportsEncryption": true,
    "encryption": "Traditional PKWARE, AES-256"
  },

  // Optional: syntax grammar block (only for text/code formats)
  "syntaxDefinition": {
    "name": "...",
    "scopeName": "source.example",
    "patterns": [ ]                     // tokenizer rules
  }
}
```

### Key fields used by the catalog API

| Field | API surface |
|---|---|
| `formatName` | `EmbeddedFormatEntry.Name` |
| `category` | `EmbeddedFormatEntry.Category` / `GetByCategory()` / `.Query().InCategory()` |
| `extensions` | `EmbeddedFormatEntry.Extensions` / `GetByExtension()` / `.Query().WithExtension()` |
| `MimeTypes` | `EmbeddedFormatEntry.MimeTypes` / `GetByMimeType()` / `FormatMatcher.MatchMime()` |
| `detection.signatures` | `EmbeddedFormatEntry.Signatures` / `DetectFromBytes()` / `FormatMatcher.Match()` |
| `preferredEditor` | `EmbeddedFormatEntry.PreferredEditor` / `GetCompatibleEditorIds()` |
| `detection.isTextFormat` | `EmbeddedFormatEntry.IsTextFormat` / `.Query().TextFormatsOnly()` |
| `syntaxDefinition` | `EmbeddedFormatEntry.HasSyntaxDefinition` / `GetSyntaxDefinitionJson()` / `.Query().HasSyntaxDefinition()` |
| `diffMode` | `EmbeddedFormatEntry.DiffMode` / `.Query().WithDiffMode()` |
| `QualityMetrics.CompletenessScore` | `EmbeddedFormatEntry.QualityScore` / `.Query().WithMinQuality()` |
| `forensic` | `entry.GetForensicSummary(catalog)` / `entry.IsHighRisk(catalog)` |
| `aiHints` | `entry.GetAiHints(catalog)` |
| `navigation.bookmarks` | `entry.GetNavigationBookmarks(catalog)` |
| `assertions` | `entry.GetAssertions(catalog)` |
| `inspector.groups` | `entry.GetInspectorGroups(catalog)` |
| `exportTemplates` | `entry.GetExportTemplates(catalog)` |
| `TechnicalDetails` | `entry.GetTechnicalDetails(catalog)` / `entry.SupportsEncryption(catalog)` |

### Validating your own .whfmt files

```csharp
string? schema = EmbeddedFormatCatalog.Instance.GetSchemaJson(SchemaName.Whfmt);
// Pass to JsonSchema.Net, Newtonsoft.Json.Schema, or any JSON Schema validator
```

The embedded schema (`whfmt.schema.json`) is the authoritative v2.4 schema used internally to validate all 675+ bundled definitions at build time.
