# whfmt.CodeGen — Documentation (v1.1.2)

> **What you get** — A **dotnet global tool** (`whfmt-codegen`) that turns
> any of the 799 `.whfmt` definitions shipped with
> `whfmt.FileFormatCatalog` 1.3.2 into a strongly-typed parser in **C#**,
> **zero-alloc C# (`ReadOnlySpan<byte>` + `MemoryMarshal`)**, **F#**, **Rust**,
> or **VB.NET**. Rich types out of the box — `enum` for `valueMap`, `[Flags]`
> for bit-fields, `List<T>` for repeating sections, nullable for conditional
> fields. Typed exceptions for principled error handling.
> Plus a `dump` subcommand that parses a binary and tabulates its decoded
> fields directly from the `.whfmt` blocks, and a `list` subcommand that
> enumerates the binary formats available for code-gen.

## Table of Contents

1. [Installation](#installation)
2. [Architecture & dependencies](#architecture--dependencies)
3. [CLI reference](#cli-reference)
4. [Internal API (Generators)](#internal-api-generators)
5. [Usage examples](#usage-examples)
6. [Integration with other whfmt.* packages](#integration-with-other-whfmt-packages)
7. [Schema v3 fields consumed](#schema-v3-fields-consumed)
8. [Generated output anatomy](#generated-output-anatomy)
9. [Troubleshooting](#troubleshooting)
10. [License](#license)

---

## Installation

```bash
dotnet tool install --global whfmt.CodeGen --version 1.1.2
# verify
whfmt-codegen --help
```

Update / uninstall:

```bash
dotnet tool update --global whfmt.CodeGen
dotnet tool uninstall --global whfmt.CodeGen
```

The tool requires the **.NET 8** runtime (the catalog is `net8.0`).

---

## Architecture & dependencies

```
whfmt.CodeGen.nupkg     (PackAsTool=true, ToolCommandName=whfmt-codegen)
└── tools/net8.0/any/
    └── whfmt-codegen.dll
            └─ depends on → whfmt.FileFormatCatalog 1.3.2
            └─ depends on → System.CommandLine 2.0.0-beta4
```

### Pipeline

```
generate <format>
    └── catalog.GetAll().FirstOrDefault(name/ext/key)
    └── catalog.GetJson(entry.ResourceKey)
    └── ParserGenerator.GenerateFromJson(json, ns, class, validate, async, language)
            ├── --lang csharp       → GenerateCSharp(...)            (default)
            ├── --lang csharp-span  → SpanGenerator.Generate(...)    (zero-alloc)
            ├── --lang fsharp       → FSharpGenerator.Generate(...)
            ├── --lang rust         → RustGenerator.Generate(...)
            └── --lang vb           → VBGenerator.Generate(...)
    └── --project DIR → ProjectEmitter.EmitAsync(json, ns, class, dir, ...)
```

---

## CLI reference

### `whfmt-codegen generate`

Generate a strongly-typed parser from a `.whfmt` definition (catalog name,
extension, or path to a `.whfmt` file on disk).

```
whfmt-codegen generate <format> [options]
```

| Argument / option | Short | Default | Description |
|---|---|---|---|
| `<format>` (positional) | — | required | Format name (`PNG`), extension (`.png`), or path to a `.whfmt`. |
| `--namespace` | `-n` | `Generated.Parsers` | C# namespace for the generated class. |
| `--class` | `-c` | `<FormatName>Parser` | Class name. |
| `--output` | `-o` | stdout | File to write to (ignored when `--project` is used). |
| `--project` | `-p` | — | Output directory for a complete multi-file project (overrides `--output`). |
| `--validate` | — | `false` | Include signature assertions + checksum validation in the generated parser. |
| `--async` | — | `false` | Emit async `Parse` methods using `Stream.ReadExactlyAsync`. |
| `--lang` | `-l` | `csharp` | Output language: `csharp`, `csharp-span`/`span`, `fsharp`/`fs`, `rust`/`rs`, `vb`/`vbnet`/`visualbasic`. |

Exit codes: `0` = success, `2` = format not found or no full definition.

#### Examples

```bash
# Default C# parser to stdout
whfmt-codegen generate PNG

# C# parser to file, with validation + async
whfmt-codegen generate PNG -n MyApp.Parsers -c PngReader \
                            --validate --async -o PngReader.cs

# Zero-alloc Span parser
whfmt-codegen generate ZIP --lang span -o ZipParser.cs

# F# discriminated unions
whfmt-codegen generate BMP --lang fsharp -o Bmp.fs

# Rust struct
whfmt-codegen generate GIF --lang rust -o gif.rs

# VB.NET class
whfmt-codegen generate DDS --lang vb -o DdsParser.vb

# Full multi-file C# project
whfmt-codegen generate PNG --project ./PngLib --validate --async

# Generate from a local .whfmt instead of the catalog
whfmt-codegen generate ./Custom.whfmt -n Custom -c CustomParser
```

### `whfmt-codegen list`

Enumerate the binary formats available for code-generation (text formats are
excluded — they have no field schema).

```
whfmt-codegen list [options]
```

| Option | Short | Description |
|---|---|---|
| `--search` | `-s` | Filter by name substring (case-insensitive). |
| `--category` | `-c` | Filter by category (exact, case-insensitive). |

```bash
whfmt-codegen list                       # all binary formats
whfmt-codegen list --category Archives
whfmt-codegen list --search png
```

### `whfmt-codegen dump`

Parse a binary file and print a structured field table by walking the
`.whfmt`'s `blocks[]` definition (no parser code is generated).

```
whfmt-codegen dump <file> [options]
```

| Argument / option | Short | Default | Description |
|---|---|---|---|
| `<file>` | — | required | Binary file to parse. |
| `--format` | `-f` | autodetect | Force format (name or extension). |
| `--verbose` | `-v` | `false` | Include `reserved` / `padding` / `unused` fields. |
| `--hex` | `-x` | `false` | Show raw hex bytes for all fields (not just `byte[]`). |
| `--limit` | `-l` | `64` | Maximum byte-length to render as hex before switching to `[N bytes]`. |

Output columns: `Field | Offset | Len | Hex | Interpreted [✓ sig | ✗ sig MISMATCH]`.
Trailing section verifies every entry in `.whfmt → checksums[]`.

```bash
whfmt-codegen dump golden.png
whfmt-codegen dump -f BMP unknown.bin
whfmt-codegen dump --verbose --hex tiny.dds
```

---

## Internal API (Generators)

These types are `internal` — they are the implementation behind the CLI and
are documented here for contributors.

| Type | Purpose |
|---|---|
| `ParserGenerator` (`internal static`) | `GenerateFromJson(json, ns, className, includeValidation, generateAsync, OutputLanguage)` → `string`. Reads the JSON, builds `BlockDef` / `ChecksumDef` / `EnumDef` lists, dispatches by language. |
| `OutputLanguage` (`public enum`) | `CSharp`, `CSharpSpan`, `FSharp`, `Rust`, `VisualBasic`. |
| `SpanGenerator` | Zero-alloc C# emitter: `ReadOnlySpan<byte>` + `MemoryMarshal.Read<T>` + manual `BinaryPrimitives.ReverseEndianness` for big-endian fields. |
| `FSharpGenerator` | F# `type ... = { ... }` records + `module Parser` with `parseBytes : byte[] -> Result<T, string>`. |
| `RustGenerator` | `struct` + `impl TryFrom<&[u8]> for T` with explicit endian conversion. |
| `VBGenerator` | `Public Class … BinaryReader`, `<Flags>` enums, `List(Of T)`, big-endian helpers. |
| `ProjectEmitter` | Multi-file C# project emitter — splits the result into `Parser.cs`, `Models.cs`, `Exceptions.cs`, and emits a minimal `.csproj`. |
| `BlockDef` / `ChecksumDef` / `EnumDef` (internal records) | Parsed shape of `blocks[]`, `checksums[]`, and synthesized enums (one per `valueMap`). |

---

## Usage examples

### 1. Quick start — parse a known format in your app

```bash
whfmt-codegen generate PNG -n MyApp.Parsers -c PngParser -o PngParser.cs
```

```csharp
using MyApp.Parsers;

var png = PngParser.Parse(File.ReadAllBytes("hello.png"));
Console.WriteLine($"{png.Width} × {png.Height}, {png.ColorType}");
```

### 2. CI-friendly multi-file project

```bash
whfmt-codegen generate PNG --project ./gen/Png --validate --async
dotnet build ./gen/Png
```

### 3. Zero-alloc decode in a hot path

```bash
whfmt-codegen generate ZIP --lang span -o ZipParser.cs
```

```csharp
ReadOnlySpan<byte> buf = stackalloc byte[256];
stream.ReadExactly(buf);
var hdr = ZipParser.Parse(buf);
```

### 4. F# from the same definition

```bash
whfmt-codegen generate BMP --lang fsharp -o Bmp.fs
```

```fsharp
open Bmp
match Bmp.Parser.parseBytes data with
| Ok bmp -> printfn "%d x %d" bmp.Width bmp.Height
| Error e -> eprintfn "%s" e
```

### 5. Rust ffi-friendly parser

```bash
whfmt-codegen generate GIF --lang rust -o src/gif.rs
```

```rust
let gif = Gif::try_from(bytes.as_slice())?;
println!("{}x{} @ {} frames", gif.width, gif.height, gif.frame_count);
```

### 6. Dump-first, code-gen-later workflow

```bash
whfmt-codegen dump sample.png        # eyeball the fields
whfmt-codegen generate PNG -o Png.cs # commit the parser
```

---

## Integration with other whfmt.* packages

| Package | Synergy |
|---|---|
| `whfmt.FileFormatCatalog` | Source of the `.whfmt` JSON and embedded format entries — `EmbeddedFormatCatalog.Instance.GetJson(entry.ResourceKey)`. |
| `whfmt.Validate` | Use `whfmt validate` to sanity-check files before parsing them with the generated parser. |
| `whfmt.Fuzz` | Generate a mutant corpus with `whfmt.Fuzz`, then run it through the generated `Parse(byte[])` to harden it — typed exceptions (`InvalidSignatureException`, `ChecksumMismatchException`, `TruncatedFileException`) make crash-vs-graceful-reject easy to distinguish. |
| `whfmt.Analysis` | `FormatDiff.Compare` consumes the same `.whfmt` definition — parser and diff stay in lock-step. |

---

## Schema v3 fields consumed

```jsonc
{
  "name": "PNG",
  "category": "Images",
  "version": "1.2",
  "description": "Portable Network Graphics",

  "blocks": [
    {
      "type": "field", "name": "magic", "offset": 0, "length": 8,
      "valueType": "bytes", "isSignature": true,
      "value": "89 50 4E 47 0D 0A 1A 0A"
    },
    {
      "type": "field", "name": "width", "storeAs": "imageWidth",
      "offset": 16, "length": 4, "valueType": "uint32", "endian": "big",
      "description": "Image width in pixels"
    },
    {
      "type": "field", "name": "colorType", "offset": 25, "length": 1,
      "valueType": "uint8",
      "valueMap": { "0": "Grayscale", "2": "RGB", "3": "Indexed", "4": "GrayscaleAlpha", "6": "RGBA" }
    },
    {
      "type": "field", "name": "flags", "offset": 26, "length": 1,
      "valueType": "uint8", "bitFlags": true,
      "valueMap": { "1": "Interlaced", "2": "Filtered", "4": "Compressed" }
    },
    {
      "type": "field", "name": "chunks", "repeating": true,
      "dependsOn": "chunkCount"
    }
  ],

  "checksums": [
    { "algorithm": "crc32",
      "storedAt":  { "fixedOffset": -4, "length": 4 },
      "dataRange": { "fixedOffset": 0,  "fixedLength": -4 } }
  ]
}
```

| `.whfmt` key | Code-gen effect |
|---|---|
| `blocks[].name` / `storeAs` | C#/VB property name; PascalCased. |
| `blocks[].valueType` | Mapped to native scalar: `uint8→byte`, `uint16→ushort`, `uint32→uint`, `uint64→ulong`, `int*→s/short/int/long`, `float→float`, `double→double`, `string/ascii/utf8→string`, anything else → `byte[]`. |
| `blocks[].endian` | `big` → emits byte-swap (`BinaryPrimitives.ReverseEndianness` in span mode, manual `BSwap32/64` elsewhere). |
| `blocks[].isSignature` + `value` | Emits a `[ExpectedBytes]`-backed assertion when `--validate`. |
| `blocks[].valueMap` | Generates a typed `enum` (with C# value-comments). |
| `blocks[].bitFlags` | Generates a `[Flags]` enum. |
| `blocks[].repeating` | Generates a `List<T>` property; size driven by `dependsOn`. |
| `blocks[].conditional` / `dependsOn` | Generates a nullable property. |
| `checksums[]` | When `--validate`: emits `ChecksumMismatchException` per algorithm. |

---

## Generated output anatomy

For C# (`--lang csharp`, the default), the emitted file follows:

```
// header (project / file / description / namespace)
namespace <Namespace>
{
    // typed enums per valueMap
    public enum PngColorType : byte { Grayscale = 0, Rgb = 2, … }
    [Flags] public enum PngFlags : byte { Interlaced = 1, Filtered = 2, Compressed = 4 }

    // typed exceptions (--validate only)
    public sealed class InvalidSignatureException : InvalidDataException { … }
    public sealed class ChecksumMismatchException : InvalidDataException { … }
    public sealed class TruncatedFileException    : InvalidDataException { … }

    public sealed class PngParser
    {
        public uint           Width        { get; init; }
        public uint           Height       { get; init; }
        public PngColorType   ColorType    { get; init; }
        public PngFlags       Flags        { get; init; }
        public List<PngChunk> Chunks       { get; init; } = [];

        public static PngParser Parse(byte[] data) { … }
        public static PngParser Parse(Stream stream) { … }
        public static async Task<PngParser> ParseAsync(Stream stream, CancellationToken ct = default) { … }
    }
}
```

Span mode replaces `byte[]` / `Stream` with `ReadOnlySpan<byte>`. F# emits
records + `Result<_, string>`. Rust emits `struct` + `impl TryFrom`. VB.NET
emits `Public Class … BinaryReader` and `<Flags>` enums.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `Format '…' not found in catalog and not a valid .whfmt path.` (exit 2) | Format name / extension wrong, no `.whfmt` at that path. | Run `whfmt-codegen list --search <name>` to discover it. |
| `No full definition available for '…'.` (exit 2) | Format is detection-only (signature-only) in the catalog. | Use a different format or contribute a full `.whfmt` definition. |
| Empty `list` output for a category | The category contains only text formats (excluded — they have no field schema). | Use `--category` matching a binary group. |
| Generated VB has duplicate enum names | Two `valueMap`s share keys without `storeAs`. | Add `storeAs` upstream in the `.whfmt` to disambiguate. |
| `dump` shows fields beyond EOF | The `.whfmt` declares blocks longer than the actual file. | Pass `--format` to force the right format, or trust that EOF-spilling fields are silently skipped. |
| Big-endian fields look wrong in dumps | The `.whfmt` is missing the `endian: big` attribute. | File an issue against `whfmt.FileFormatCatalog`. |
| `whfmt-codegen` not found after install | Global tools path not on `PATH`. | Add `~/.dotnet/tools` (`%USERPROFILE%\.dotnet\tools` on Windows). |

---

## License

GNU AGPL v3.0. See `https://www.gnu.org/licenses/agpl-3.0.html`.

Copyright © 2016-2026 Derek Tremblay.
