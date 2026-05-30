# whfmt.SourceGenerator

Roslyn Source Generator that turns `.whfmt` binary format definitions into strongly-typed C# parsers **at compile time** — no CLI step, no checked-in generated files, full IntelliSense.

---

## What's New

### v1.0.2 — Stability + schema v3.1 alignment

- **Fix**: Generator now tolerates `embeddedLanguages[]` entries in `syntaxDefinition` blocks (schema v3.1) without crashing — fields not relevant to C# parsing are silently skipped.
- **Fix**: Shared `ParserGenerator` / `SpanGenerator` sources updated in sync with `whfmt.CodeGen 1.1.5`.
- **No generated-API changes** — all previously generated parsers remain source-compatible.

### v1.0.1 — Initial public release

- Roslyn `IIncrementalGenerator` wired to `AdditionalFiles` ending in `.whfmt`.
- Generates `{FormatName}Parser` (BinaryReader-based) and `{FormatName}SpanParser` (zero-alloc `Span<byte>`) per input file.
- MSBuild `.props` file exposes `WhfmtXxx` metadata as compiler-visible item metadata for `AnalyzerConfigOptionsProvider`.

---

## Quick start

### 1. Install

```xml
<PackageReference Include="whfmt.SourceGenerator" Version="1.0.0" />
```

### 2. Declare your `.whfmt` file as an `AdditionalFile`

```xml
<ItemGroup>
  <AdditionalFiles Include="Formats\PNG.whfmt" />
</ItemGroup>
```

### 3. Use the generated parser — no other step needed

```csharp
using WhfmtGenerated;

var png = PngParser.ParseFile("image.png");
Console.WriteLine($"{png.Width} x {png.Height}");  // strongly-typed fields
```

The class `PngParser` (and its typed fields) appear automatically every time the project builds. The `.g.cs` file is never committed to source control.

---

## Configuration

Each `AdditionalFiles` entry accepts per-file MSBuild metadata to control generation:

| Metadata key       | Default                  | Description |
|--------------------|--------------------------|-------------|
| `WhfmtNamespace`   | `WhfmtGenerated`         | C# namespace for the generated class |
| `WhfmtClass`       | File name in PascalCase  | Class name override |
| `WhfmtLanguage`    | `CSharp`                 | Output language (see below) |
| `WhfmtValidate`    | `true`                   | Emit signature + checksum validation |
| `WhfmtAsync`       | `false`                  | Emit `ParseAsync(Stream, CancellationToken)` overloads |

### Example — custom namespace + async overloads

```xml
<AdditionalFiles Include="Formats\ZIP.whfmt">
  <WhfmtNamespace>MyApp.Formats</WhfmtNamespace>
  <WhfmtClass>ZipParser</WhfmtClass>
  <WhfmtValidate>true</WhfmtValidate>
  <WhfmtAsync>true</WhfmtAsync>
</AdditionalFiles>
```

```csharp
var zip = await ZipParser.ParseAsync(stream, cancellationToken);
Console.WriteLine(zip.LocalFileHeaders.Count);
```

---

## Supported output languages

| `WhfmtLanguage` value | Generated output |
|-----------------------|-----------------|
| `CSharp` *(default)*  | `class` with `BinaryReader`, rich enums, nullable conditionals, `List<T>` repeating fields |
| `CSharpSpan`          | `ref struct` with `ReadOnlySpan<byte>` + `MemoryMarshal` — **zero heap allocations** |
| `FSharp`              | F# module with discriminated unions and `parse` function |
| `Rust`                | Rust `struct` + `impl TryFrom<&[u8]>` |
| `VisualBasic`         | VB.NET `Class` with `BinaryReader` |

> **Note:** F#, Rust and VB output is supported but the generated `.g.cs` file will contain the foreign-language source as a string literal comment — use the [whfmt-codegen CLI](https://www.nuget.org/packages/whfmt.CodeGen) for those targets instead.

---

## Multiple formats at once

Apply settings to all `.whfmt` files in a folder:

```xml
<ItemGroup>
  <AdditionalFiles Include="Formats\*.whfmt">
    <WhfmtNamespace>MyApp.Formats</WhfmtNamespace>
    <WhfmtValidate>true</WhfmtValidate>
  </AdditionalFiles>
</ItemGroup>
```

Each file gets its own generated class named after the file (PascalCase).

---

## Zero-alloc Span variant

For performance-critical paths (hot loops, network buffers):

```xml
<AdditionalFiles Include="Formats\PNG.whfmt">
  <WhfmtNamespace>MyApp.Formats</WhfmtNamespace>
  <WhfmtLanguage>CSharpSpan</WhfmtLanguage>
</AdditionalFiles>
```

```csharp
ReadOnlySpan<byte> buffer = stackalloc byte[128];
// ... fill buffer ...
var png = new PngParser(buffer);
Console.WriteLine(png.Width);   // no allocation
```

---

## Global configuration via Directory.Build.props

Apply settings to all `.whfmt` files across the entire project tree without repeating metadata on every `<AdditionalFiles>` entry:

```xml
<!-- Directory.Build.props at solution root -->
<Project>
  <ItemGroup>
    <AdditionalFiles Include="$(MSBuildProjectDirectory)\Formats\*.whfmt">
      <WhfmtNamespace>$(RootNamespace).Formats</WhfmtNamespace>
      <WhfmtValidate>true</WhfmtValidate>
    </AdditionalFiles>
  </ItemGroup>
</Project>
```

Individual projects can still override per-file:

```xml
<!-- In a specific project's .csproj — overrides the Directory.Build.props default -->
<AdditionalFiles Include="Formats\Internal.whfmt">
  <WhfmtNamespace>$(RootNamespace).Internal</WhfmtNamespace>
  <WhfmtAsync>true</WhfmtAsync>
</AdditionalFiles>
```

---

## What gets generated

For a `.whfmt` file with `blocks` like:

```jsonc
{
  "formatName": "PNG",
  "blocks": [
    { "name": "Signature", "offset": 0, "length": 8, "type": "bytes", "isSignature": true },
    { "name": "Width",     "offset": 16, "length": 4, "type": "uint32", "endian": "big" },
    { "name": "Height",    "offset": 20, "length": 4, "type": "uint32", "endian": "big" },
    { "name": "BitDepth",  "offset": 24, "length": 1, "type": "byte" }
  ]
}
```

The generator emits:

```csharp
// <auto-generated/>
// Source: PNG.whfmt (whfmt.SourceGenerator)
namespace WhfmtGenerated;

public sealed class PngParser
{
    public byte[]  Signature { get; }
    public uint    Width     { get; }
    public uint    Height    { get; }
    public byte    BitDepth  { get; }

    private PngParser(BinaryReader r) { /* ... */ }

    public static PngParser Parse(Stream stream)   { /* ... */ }
    public static PngParser Parse(byte[] data)     { /* ... */ }
    public static PngParser ParseFile(string path) { /* ... */ }
}
```

Fields with `valueMap` become enums. `isRepeating` fields become `List<T>`. `isConditional` fields become nullable.

---

## Diagnostics

| Code     | Severity | Meaning |
|----------|----------|---------|
| `WHSG001` | Error   | The `.whfmt` file could not be parsed or contains an unsupported schema construct. Check the file follows whfmt v3 schema. |

---

## Relationship to `whfmt-codegen` CLI

This package and [`whfmt.CodeGen`](https://www.nuget.org/packages/whfmt.CodeGen) (the global CLI tool `whfmt-codegen`) share the same generation engine.

| | `whfmt.SourceGenerator` | `whfmt.CodeGen` CLI |
|---|---|---|
| Trigger | Automatic at build | Manual (`whfmt-codegen generate`) |
| Generated files committed? | No — lives in `obj/` | Yes — explicit output |
| Best for | C# / C#Span in .NET projects | F#, Rust, VB, one-shot generation |
| IntelliSense | ✅ Full | ✅ After running CLI |
| Multi-format glob | ✅ Via `<AdditionalFiles>` | One at a time |

---

## Requirements

- **.NET SDK 6.0 or later** (Roslyn incremental generator support)
- **`.whfmt` files following whfmt v3 schema** — see [whfmt.FileFormatCatalog](https://www.nuget.org/packages/whfmt.FileFormatCatalog) for 800+ ready-to-use definitions

---

## License

GNU AGPL v3.0 — see [LICENSE](https://github.com/abbaye/WpfHexEditorControl/blob/master/LICENSE).
