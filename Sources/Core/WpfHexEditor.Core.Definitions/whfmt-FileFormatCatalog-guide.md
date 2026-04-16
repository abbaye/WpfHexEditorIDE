# whfmt.FileFormatCatalog — Documentation

## Table of Contents

1. [Architecture](#architecture)
2. [API Reference](#api-reference)
3. [Integration Guide — Level 1: Basic Detection](#level-1-basic-detection)
4. [Integration Guide — Level 2: Routing and Syntax](#level-2-routing-and-syntax)
5. [Integration Guide — Level 3: Full Pipeline](#level-3-full-pipeline)
6. [The .whfmt Format](#the-whfmt-format)

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
| `FormatCategory` | Core.Contracts | Enum — 27 known categories |
| `SchemaName` | Core.Contracts | Enum — 5 known embedded schemas |

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

### `FormatSignature`

| Field | Type | Description |
|---|---|---|
| `Value` | `string` | Hex string of expected bytes, e.g. `"504B0304"` |
| `Offset` | `int` | Byte offset in the file where the signature appears |
| `Weight` | `double` | Match confidence (0.0–1.0) used for scoring in `DetectFromBytes` |

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
    Console.WriteLine($"Detected: {entry.Name} (score-based)");
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

### File identification pipeline with fallback chain

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
            if (byBytes is not null) return byBytes; // byte-confirmed result
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
var catalog = EmbeddedFormatCatalog.Instance;

var results = Directory
    .EnumerateFiles(@"C:\Data", "*.*", SearchOption.AllDirectories)
    .AsParallel()
    .WithDegreeOfParallelism(Environment.ProcessorCount)
    .Select(path =>
    {
        EmbeddedFormatEntry? entry = null;
        try
        {
            entry = catalog.GetByExtension(Path.GetExtension(path));
            if (entry is null || entry.Signatures is { Count: > 0 })
            {
                using var fs = File.OpenRead(path);
                var header = new byte[512];
                int read = fs.Read(header, 0, header.Length);
                var detected = catalog.DetectFromBytes(header.AsSpan(0, read));
                entry = detected ?? entry;
            }
        }
        catch { /* skip locked / inaccessible files */ }

        return new
        {
            Path      = path,
            Name      = entry?.Name ?? "Unknown",
            Category  = entry?.Category ?? "Unknown",
            IsSpoofed = entry is not null &&
                        catalog.GetByExtension(Path.GetExtension(path))?.ResourceKey != entry.ResourceKey
        };
    })
    .ToList();

// Summary
foreach (var g in results.GroupBy(r => r.Category).OrderByDescending(g => g.Count()))
    Console.WriteLine($"{g.Key,-20} {g.Count(),5} file(s)  spoofed: {g.Count(f => f.IsSpoofed)}");
```

### Full syntax engine bootstrap

```csharp
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
        // Use enum — all programming language grammars, no string typos
        foreach (var entry in _catalog.GetByCategory(FormatCategory.Programming)
                                      .Where(e => e.HasSyntaxDefinition))
        {
            var grammar = _catalog.GetSyntaxDefinitionJson(entry.ResourceKey);
            if (grammar is null) continue;

            foreach (var ext in entry.Extensions)
                _grammarsByExtension.TryAdd(ext, grammar);
        }
    }

    public string? GetGrammar(string fileExtension)
        => _grammarsByExtension.GetValueOrDefault(fileExtension);

    // Validate a user-supplied .whfmt file against the embedded schema
    public string GetWhfmtSchema()
        => _catalog.GetSchemaJson(SchemaName.Whfmt) ?? throw new InvalidOperationException("Schema not found.");
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

  // Optional: syntax grammar block (only for text/code formats)
  "syntaxDefinition": {
    "name": "...",
    "scopeName": "source.example",
    "patterns": [ ... ]                 // tokenizer rules
  },

  // Quality metadata
  "QualityMetrics": {
    "CompletenessScore": 90             // 0–100
  }
}
```

### Key fields used by the catalog API

| Field | API surface |
|---|---|
| `formatName` | `EmbeddedFormatEntry.Name` |
| `category` | `EmbeddedFormatEntry.Category` / `GetByCategory()` |
| `extensions` | `EmbeddedFormatEntry.Extensions` / `GetByExtension()` |
| `MimeTypes` | `EmbeddedFormatEntry.MimeTypes` / `GetByMimeType()` |
| `detection.signatures` | `EmbeddedFormatEntry.Signatures` / `DetectFromBytes()` |
| `preferredEditor` | `EmbeddedFormatEntry.PreferredEditor` / `GetCompatibleEditorIds()` |
| `detection.isTextFormat` | `EmbeddedFormatEntry.IsTextFormat` |
| `syntaxDefinition` | `EmbeddedFormatEntry.HasSyntaxDefinition` / `GetSyntaxDefinitionJson()` |
| `diffMode` | `EmbeddedFormatEntry.DiffMode` |
| `QualityMetrics.CompletenessScore` | `EmbeddedFormatEntry.QualityScore` |

### Validating your own .whfmt files

```csharp
string? schema = EmbeddedFormatCatalog.Instance.GetSchemaJson(SchemaName.Whfmt);
// Pass to JsonSchema.Net, Newtonsoft.Json.Schema, or any JSON Schema validator
```

The embedded schema (`whfmt.schema.json`) is the authoritative v2.3 schema used internally to validate all 675+ bundled definitions at build time.
