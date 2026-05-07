# whfmt.CodeGen

**Generate strongly-typed parsers** from `.whfmt` binary format definitions — in C#, F#, or Rust.

```bash
dotnet tool install -g whfmt.CodeGen

# Generate a PNG parser
whfmt-codegen generate PNG --namespace My.Parsers --output PngParser.cs

# Dump a binary file's structure
whfmt-codegen dump myfile.png

# List all 757 supported binary formats
whfmt-codegen list
```

Every `BlockDefinition` in a `.whfmt` file becomes a **typed property**.  
`valueMap` entries become **C# enums** (or F# discriminated unions / Rust enums).  
`bitflags: true` blocks emit `[Flags] enum`.  
Big-endian fields emit BSwap helpers automatically.  
`--validate` adds `InvalidSignatureException`, `ChecksumMismatchException`, `TruncatedFileException`.

Powered by **757 binary format definitions** from `whfmt.FileFormatCatalog` (790+ total; 757 binary formats available for code generation — text-format grammars excluded).

---

## Install

```bash
dotnet tool install -g whfmt.CodeGen
```

---

## Commands

### `whfmt-codegen generate`

```
whfmt-codegen generate <format> [options]
```

| Option | Short | Default | Description |
|---|---|---|---|
| `<format>` | | *required* | Format name, extension, or path to `.whfmt` file |
| `--namespace` | `-n` | `Generated.Parsers` | C# namespace |
| `--class` | `-c` | `<FormatName>Parser` | Class name override |
| `--output` | `-o` | stdout | Output file path |
| `--project` | `-p` | — | Output **directory** for a complete multi-file C# project |
| `--validate` | | false | Emit typed exceptions for signature / checksum failures |
| `--async` | | false | Generate `async Task<T>` overloads |
| `--lang` | `-l` | `csharp` | Output language: `csharp`, `csharp-span`, `fsharp`, `rust` |

### `whfmt-codegen dump`

Parse a binary file and display its structured field values in a table.

```
whfmt-codegen dump <file> [options]
```

| Option | Short | Default | Description |
|---|---|---|---|
| `<file>` | | *required* | Binary file to parse |
| `--format` | `-f` | auto-detect | Force format (name or extension) |
| `--verbose` | `-v` | false | Show reserved/padding fields |
| `--hex` | `-x` | false | Force hex for all fields |
| `--limit` | `-l` | 64 | Max byte[] size to render as hex |

### `whfmt-codegen list`

```
whfmt-codegen list [--search <term>] [--category <name>]
```

Lists all 757 binary formats available for code generation grouped by category.

---

## Examples

### Generate a PNG parser with validation

```bash
whfmt-codegen generate PNG \
  --namespace My.Imaging \
  --output src/Parsers/PngParser.cs \
  --validate
```

**Output** (excerpt):

```csharp
public enum ColorTypeType
{
    Grayscale = 0,
    Rgb = 2,
    Indexed = 3,
    GrayscaleAlpha = 4,
    Rgba = 6,
}

public sealed class PngParser
{
    /// <summary>PNG 8-byte file signature</summary>
    public byte[] PngSignature { get; private set; }

    /// <summary>Image width in pixels</summary>
    public uint Width { get; private set; }

    /// <summary>Color type</summary>
    public ColorTypeType ColorType { get; private set; }

    public static PngParser Parse(Stream stream) { ... }
    public static PngParser ParseFile(string path) { ... }
}
```

Validation mode emits:

```csharp
public sealed class InvalidSignatureException : Exception
{
    public byte[] Actual   { get; }
    public byte[] Expected { get; }
}
public sealed class ChecksumMismatchException : Exception
{
    public string Algorithm { get; }
    public byte[] Computed  { get; }
    public byte[] Stored    { get; }
}
public sealed class TruncatedFileException : Exception
{
    public string FieldName      { get; }
    public long   RequiredOffset { get; }
    public long   ActualLength   { get; }
}
```

### Generate a complete C# project

```bash
whfmt-codegen generate ZIP --project src/ZipParser/ --validate --async
```

Emits:
```
src/ZipParser/
  ZipParser.csproj
  ZipParserTypes.cs       — enums, value types
  ZipParser.cs            — parser class
  ZipParserExceptions.cs  — typed exceptions
```

### Dump a binary file

```bash
whfmt-codegen dump firmware.bin --format UEFI
```

```
  File    : firmware.bin  (16,777,216 bytes)
  Format  : UEFI Firmware

  Field                      Offset   Len  Hex                       Interpreted
  ─────────────────────────────────────────────────────────────────────────────
  FirmwareVolumeSignature         0     4  5F 46 56 48               _FVH            ✓ sig
  Attributes                      8     4  FF FE 00 00               4294901248
  HeaderLength                   48     2  48 00                      72
  Checksum                        50     2  35 BA                      47669
  Revision                       55     1  02                         2

  Checksums:
    CRC32      @ 50     stored=35BA  computed=35BA  ✓
```

### Generate an async PE parser

```bash
whfmt-codegen generate PE_EXE \
  --namespace My.Native \
  --class PortableExecutableParser \
  --async --validate \
  --output src/Parsers/PortableExecutableParser.cs
```

### Generate zero-alloc Span-based parser

```bash
whfmt-codegen generate SQLite --lang csharp-span --output SqliteSpanReader.cs
```

```csharp
public ref struct SQLiteParser
{
    private readonly ReadOnlySpan<byte> _data;

    public SQLiteParser(ReadOnlySpan<byte> data) => _data = data;

    public ReadOnlySpan<byte> Header => _data.Length >= 16 ? _data.Slice(0, 16) : ReadOnlySpan<byte>.Empty;
    public ushort PageSize => _data.Length < 18 ? default : ReverseBytes(MemoryMarshal.Read<ushort>(_data.Slice(16, 2)));
    public byte FileFormatWriteVersion => _data.Length > 18 ? _data[18] : default;
    ...
}
```

### Generate an F# parser

```bash
whfmt-codegen generate MP3 --lang fsharp --output Mp3Parser.fs
```

```fsharp
type Id3MajorVersionKind =
    | ID3v22Obsolete3CharFrameIDs
    | ID3v23MostCommon4CharFrameIDs
    | ID3v24LatestUTF8NativeSupport
    | Unknown of uint16

type MP3AudioParser = {
    Id3Signature     : byte[]
    Id3MajorVersion  : Id3MajorVersionKind
    ...
}

let parse (stream: Stream) : MP3AudioParser = ...
let parseFile path = File.OpenRead(path) |> parse
```

### Generate a Rust parser

```bash
whfmt-codegen generate PNG --lang rust --output png_parser.rs
```

```rust
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ColorTypeKind {
    Grayscale,
    Rgb,
    Indexed,
    GrayscaleAlpha,
    Rgba,
    Unknown(u8),
}

#[derive(Debug, Clone)]
pub struct Png {
    pub png_signature: Vec<u8>,
    pub width:         u32,
    pub height:        u32,
    pub color_type:    ColorTypeKind,
    ...
}

impl TryFrom<&[u8]> for Png {
    type Error = ParseError;
    fn try_from(data: &[u8]) -> Result<Self, ParseError> { ... }
}
```

### MSBuild integration

```xml
<!-- MyProject.csproj -->
<Target Name="GenerateParsers" BeforeTargets="Build">
  <Exec Command="whfmt-codegen generate PNG -n $(RootNamespace).Parsers -o $(ProjectDir)Generated/PngParser.cs" />
  <Exec Command="whfmt-codegen generate MP3 -n $(RootNamespace).Parsers -o $(ProjectDir)Generated/Mp3Parser.cs" />
</Target>
```

### From a local `.whfmt` file

```bash
whfmt-codegen generate ./MyFormat.whfmt --namespace Acme.Formats --output MyFormatParser.cs
```

---

## Generated Class Shape

| .whfmt block attribute | Generated C# |
|---|---|
| `type: uint8`…`uint64` | Typed property: `byte`, `ushort`, `uint`, `ulong` |
| `type: string/ascii/utf8` | `string` property with null-trim |
| `valueMap` (≥2 entries) | C# `enum` type + property typed as that enum |
| `bitflags: true` | `[Flags] enum` |
| `repeating: true` | `List<T>` property |
| `conditional: true` | Nullable `T?` property |
| `isSignature: true` + `--validate` | `InvalidSignatureException` on mismatch |
| `checksums[]` + `--validate` | `ChecksumMismatchException` on CRC32/MD5/SHA1/SHA256 mismatch |
| `endian: "big"` | BSwap16/32/64 inline helper |

### Async overloads (`--async`)

```csharp
Task<PngParser> ParseAsync(Stream stream, CancellationToken ct = default)
Task<PngParser> ParseFileAsync(string path, CancellationToken ct = default)
```

---

## Output Languages

| `--lang` | Description |
|---|---|
| `csharp` (default) | Standard C# with `BinaryReader` |
| `csharp-span` | Zero-alloc `ref struct` using `ReadOnlySpan<byte>` + `MemoryMarshal` |
| `fsharp` | F# record + discriminated unions + pattern matching |
| `rust` | Rust `struct` + `impl TryFrom<&[u8]>` with `u16::from_le/be_bytes` |

---

## Supported Formats (757 binary)

Run `whfmt-codegen list` to browse all 757 binary formats by category. Key formats:

| Category | Formats |
|---|---|
| Archives | ZIP, 7-ZIP, RAR, TAR, GZ, BZ2, XZ, LZ4, ZSTD, CAB, ISO, DMG... |
| Images | PNG, JPEG, BMP, GIF, TIFF, WebP, HEIF, ICO, TGA, PSD, DDS... |
| Executables | PE/EXE, ELF, Mach-O, WASM, Java Class, DEX, COM... |
| Audio | MP3, FLAC, WAV, OGG, AAC, AIFF, DSF, OPUS... |
| Video | MP4, MKV, AVI, MOV, WebM, FLV, MPEG-TS... |
| Documents | PDF, DOCX, XLSX, ODT, EPUB, RTF... |
| Database | SQLite, MDB, DuckDB... |
| Game/ROM | Unity, Unreal, PSX, N64, GBA, NDS, GB, GBC... |
| Crypto | PEM, DER, P12, GPG... |
| Firmware | UEFI, BIOS, U-Boot... |
| 3D | BLEND, FBX, GLB, STL, OBJ, PLY... |
| Fonts | TTF, OTF, WOFF, WOFF2... |
| Disk | VMDK, VHD, QCOW2, ISO... |
| Network | PCAP, PCAPNG, DNS... |

> **790+ total definitions** in `whfmt.FileFormatCatalog`. 757 binary formats are available for code generation; 33 text-format grammars (C#, Python, Rust, etc.) are excluded from `list` and `generate`.

---

## Architecture

```
whfmt.CodeGen (dotnet global tool: whfmt-codegen)
├── Commands/
│   ├── GenerateCommand   — format resolution → ParserGenerator → write output / project
│   ├── ListCommand       — catalog browser with search/category filters
│   └── DumpCommand       — binary file → structured field table
└── Generator/
    ├── ParserGenerator   — JSON → C# BinaryReader parser (enums, flags, exceptions)
    ├── SpanGenerator     — JSON → zero-alloc ref struct (ReadOnlySpan<byte>)
    ├── FSharpGenerator   — JSON → F# record + DU
    ├── RustGenerator     — JSON → Rust struct + TryFrom
    ├── ProjectEmitter    — multi-file project scaffolding (--project)
    └── OutputLanguage    — enum: CSharp | CSharpSpan | FSharp | Rust
```

Depends on: `whfmt.FileFormatCatalog 1.3.1+` · `System.CommandLine 2.0.0-beta4` — cross-platform net8.0.

---

## Related Packages

| Package | Description |
|---|---|
| [whfmt.FileFormatCatalog](https://www.nuget.org/packages/whfmt.FileFormatCatalog) | 790+ format definitions — required dependency |
| [whfmt.Validate](https://www.nuget.org/packages/whfmt.Validate) | `dotnet tool` — validate + repair binary files from the CLI |
| [whfmt.Analysis](https://www.nuget.org/packages/whfmt.Analysis) | Semantic field-level diff between binary files |
| [whfmt.Fuzz](https://www.nuget.org/packages/whfmt.Fuzz) | Format-aware binary fuzzer for parser testing |

---

## License

GNU AGPL v3.0 — © 2016–2026 Derek Tremblay / Pulsar Informatique
