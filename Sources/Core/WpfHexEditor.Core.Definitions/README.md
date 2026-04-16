# whfmt.FileFormatCatalog

675+ embedded file format and language definitions for automatic format detection and syntax highlighting.  
Cross-platform `net8.0` — works in any .NET 8 application.

```
dotnet add package whfmt.FileFormatCatalog
```

> **Full documentation**: [whfmt-FileFormatCatalog-guide.md](whfmt-FileFormatCatalog-guide.md) — API reference, architecture, integration guides (Level 1–3), and .whfmt format specification.

---

## About

This catalog grew out of the format detection engine inside **WpfHexEditorIDE** — a full-featured binary/text IDE built on WPF. Every time a file is opened, the IDE needs to know what it is, which editor to route it to, and how to syntax-highlight it. Rather than hardcoding rules, we built a declarative `.whfmt` format — a JSON definition file that captures magic bytes, extensions, MIME types, entropy hints, quality scores, and syntax grammars in one place.

Over time the catalog grew to 675+ definitions covering everything from Nintendo ROMs and audio codecs to machine learning models and certificate formats. The syntax grammar side expanded to 35 languages to drive the built-in code editor.

This package extracts that catalog as a standalone, cross-platform library — useful for any application that needs to identify files, route them to the right handler, or provide syntax highlighting without taking a dependency on a full IDE framework.

---

## Quick Start

### 1 — Add the using directives

```csharp
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Contracts;

var catalog = EmbeddedFormatCatalog.Instance;
```

### 2 — Detect a format by extension

```csharp
EmbeddedFormatEntry? entry = catalog.GetByExtension(".zip");
Console.WriteLine(entry?.Name);            // "ZIP Archive"
Console.WriteLine(entry?.PreferredEditor); // "structure-editor"
```

### 3 — Detect by magic bytes

```csharp
// Pass at least the first 16 bytes — 512 bytes recommended
byte[] header = File.ReadAllBytes("unknown.bin")[..512];
EmbeddedFormatEntry? detected = catalog.DetectFromBytes(header);
Console.WriteLine(detected?.Name);         // e.g. "PNG Image"
```

### 4 — Detect by MIME type

```csharp
EmbeddedFormatEntry? entry = catalog.GetByMimeType("image/png");
```

### 5 — Browse a category

```csharp
// Enum overload — IntelliSense, no typos
IReadOnlyList<EmbeddedFormatEntry> games = catalog.GetByCategory(FormatCategory.Game);

// String overload — for dynamic/runtime scenarios
IReadOnlyList<EmbeddedFormatEntry> same = catalog.GetByCategory("Game");
```

### 6 — Extract a syntax grammar for a code editor

```csharp
EmbeddedFormatEntry? cs = catalog.GetByExtension(".cs");
if (cs?.HasSyntaxDefinition == true)
{
    string? grammar = catalog.GetSyntaxDefinitionJson(cs.ResourceKey);
    // Feed grammar into your tokenizer / syntax highlighter
}
```

### 7 — Access the full JSON or schema

```csharp
// Full .whfmt JSON for any entry (cached)
string json = catalog.GetJson(entry.ResourceKey);

// Embedded JSON schema — enum overload (recommended)
string? schema = catalog.GetSchemaJson(SchemaName.Whfmt);

// String overload
string? same = catalog.GetSchemaJson("whfmt");
```

### 8 — Route to the right editor

```csharp
IReadOnlyList<string> editors = catalog.GetCompatibleEditorIds("report.pdf");
// ["hex-editor", "structure-editor"]
```

### Fast Startup — PreWarm

```csharp
// Call once from a background thread at startup to pre-load all JSON into cache
await Task.Run(() => EmbeddedFormatCatalog.Instance.PreWarm());
```

---

## Advanced Examples

### Batch folder scanner — group files by detected category

```csharp
var catalog = EmbeddedFormatCatalog.Instance;

var byCategory = Directory
    .EnumerateFiles(@"C:\Downloads", "*.*", SearchOption.AllDirectories)
    .Select(path =>
    {
        // Try extension first (fast), fall back to magic bytes (accurate)
        var entry = catalog.GetByExtension(Path.GetExtension(path));
        if (entry is null)
        {
            using var fs = File.OpenRead(path);
            var header = new byte[512];
            int read = fs.Read(header, 0, header.Length);
            entry = catalog.DetectFromBytes(header.AsSpan(0, read));
        }
        return (Path: path, Category: entry?.Category ?? "Unknown", Entry: entry);
    })
    .GroupBy(f => f.Category)
    .OrderByDescending(g => g.Count());

foreach (var group in byCategory)
    Console.WriteLine($"{group.Key}: {group.Count()} file(s)");

// Pull only the game ROMs using the enum
var roms = catalog.GetByCategory(FormatCategory.Game);
Console.WriteLine($"Known game formats: {roms.Count}");
```

---

### Magic-byte validator — detect extension spoofing

```csharp
var catalog = EmbeddedFormatCatalog.Instance;

bool IsExtensionSpoofed(string filePath)
{
    var byExtension = catalog.GetByExtension(Path.GetExtension(filePath));
    if (byExtension is null) return false; // unknown format — skip

    using var fs = File.OpenRead(filePath);
    var header = new byte[512];
    int read = fs.Read(header, 0, header.Length);
    var byBytes = catalog.DetectFromBytes(header.AsSpan(0, read));

    // Spoofed if bytes point to a different known format
    return byBytes is not null && byBytes.ResourceKey != byExtension.ResourceKey;
}

// Usage
if (IsExtensionSpoofed(@"C:\uploads\invoice.pdf"))
    Console.WriteLine("Warning: file content does not match its extension.");
```

---

### Grammar loader — wire syntax highlighting into a custom editor

```csharp
var catalog = EmbeddedFormatCatalog.Instance;

// Load grammars only for the Programming category (enum — no typo risk)
var languages = catalog.GetByCategory(FormatCategory.Programming)
    .Where(e => e.HasSyntaxDefinition)
    .OrderBy(e => e.Name);

foreach (var lang in languages)
{
    string? grammarJson = catalog.GetSyntaxDefinitionJson(lang.ResourceKey);
    if (grammarJson is null) continue;

    // Deserialize into your tokenizer model and register
    // MyTokenizerRegistry.Register(lang.Name, grammarJson);
    Console.WriteLine($"Loaded grammar: {lang.Name} ({lang.Extensions.FirstOrDefault()})");
}
// Output: Loaded grammar: C# (.cs), Loaded grammar: Python (.py), ...

// Validate your own .whfmt file against the embedded schema
string? whfmtSchema = catalog.GetSchemaJson(SchemaName.Whfmt);
// Pass whfmtSchema to your JSON schema validator (e.g. JsonSchema.Net)
```

---

### MIME negotiation — extension ↔ MIME bidirectional mapping

```csharp
var catalog = EmbeddedFormatCatalog.Instance;

// Extension → MIME (e.g. for HTTP Content-Type)
string? GetMimeType(string extension)
    => catalog.GetByExtension(extension)?.MimeTypes?.FirstOrDefault();

// MIME → canonical extension (e.g. for file download naming)
string? GetExtension(string mimeType)
    => catalog.GetByMimeType(mimeType)?.Extensions.FirstOrDefault();

// Examples
Console.WriteLine(GetMimeType(".png"));          // "image/png"
Console.WriteLine(GetMimeType(".zip"));          // "application/zip"
Console.WriteLine(GetExtension("image/png"));    // ".png"
Console.WriteLine(GetExtension("audio/mpeg"));   // ".mp3"
```

---

## Features

### Format Detection
- 675+ embedded `.whfmt` definitions — extension, MIME type, and magic-byte lookup
- `DetectFromBytes(ReadOnlySpan<byte>)` — zero-alloc magic-byte scoring across all signatures
- `GetByExtension`, `GetByMimeType`, `GetByCategory` — multiple lookup strategies
- `GetCompatibleEditorIds` — returns all compatible editor IDs for a given file path
- 27 categories: Archives, Audio, Images, Game, Documents, Video, System, 3D, and more

### Syntax Highlighting
- 35 language grammars with `syntaxDefinition` blocks (C#, Python, JS/TS, Go, Rust, Java, Kotlin, Swift, Dart, PHP, Ruby, Lua, SQL, YAML, TOML, Markdown, and more)
- `GetSyntaxDefinitionJson(resourceKey)` — raw grammar JSON ready for a tokenizer
- `HasSyntaxDefinition` flag for fast filtering

### Enum API
- `FormatCategory` enum — all 27 categories with IntelliSense, no string typos
- `SchemaName` enum — `Whfmt`, `Whcd`, `Whdbg`, `Whidews`, `Whscd`
- All enum overloads delegate to string overloads — both variants always available

### Performance
- Singleton with lazy thread-safe initialization (`LazyInitializer`)
- Entries backed by `FrozenSet<T>` — O(1) set operations
- JSON cache — each resource key read once, then served from memory
- `PreWarm()` — pre-load all JSON on a background thread before first use

---

## What's New in 1.0.0

- **New**: Initial NuGet release — cross-platform `net8.0`.
- **New**: `FormatCategory` enum — type-safe overload for `GetByCategory()`.
- **New**: `SchemaName` enum — type-safe overload for `GetSchemaJson()`.
- **New**: `DetectFromBytes(ReadOnlySpan<byte>)` — magic-byte detection across all 675+ signatures.
- **New**: `GetByMimeType(string)` — MIME type lookup.
- **New**: `GetByCategory(string/FormatCategory)` — category browsing.
- **New**: `GetSchemaJson(string/SchemaName)` — access to 5 embedded JSON schemas.
- **New**: `MimeTypes` and `Signatures` fields on `EmbeddedFormatEntry`.

---

## Included Assemblies

Both bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|---|---|
| WpfHexEditor.Core.Definitions | `EmbeddedFormatCatalog` singleton + 675+ embedded `.whfmt` definitions |
| WpfHexEditor.Core.Contracts | `IEmbeddedFormatCatalog`, `EmbeddedFormatEntry`, `FormatCategory`, `SchemaName` |

---

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
