# WpfHexEditor.Definitions

Embedded file-format catalog (400+ signatures, `.whfmt`) and syntax-highlight definitions (27 languages, `.whlang`) shipped as assembly resources inside the IDE.

**License:** GNU Affero General Public License v3.0
**Target:** net8.0-windows

---

## Architecture / Modules

### Format Catalog (`FormatDefinitions/`)

Binary file signatures and structured field definitions in the proprietary `.whfmt` JSON format, compiled as embedded resources under the namespace `WpfHexEditor.Definitions.FormatDefinitions.*`.

**Categories and representative formats:**

| Category | Examples |
|---|---|
| 3D | 3DS, BLEND, FBX, GLTF, OBJ, STL, USD |
| Archives | 7Z, BZIP2, GZIP, RAR, TAR, ZIP, ZSTD, LZ4 |
| Audio | AAC, FLAC, MP3, OGG, WAV, MIDI, DFF/DSF |
| CAD | DWG, DXF, STEP, SKP, SLDPRT/SLDASM |
| Certificates | DER, P12, PEM |
| Crypto | KEYSTORE (and others) |
| (+ many more categories: Databases, Documents, Executables, Fonts, Images, Video, etc.) |

- **`EmbeddedFormatCatalog`** — singleton; performs lazy scan of all manifest resources matching `*.whfmt` on first call to `GetAll()`. Returns a lightweight `IReadOnlyList<EmbeddedFormatEntry>` (header only; full block definitions loaded on demand).
- **`IEmbeddedFormatCatalog`** — interface exposed to other modules; enables unit-test substitution.

### Syntax Catalog (`SyntaxDefinitions/`)

Tokenizer rules for the code editor in `.whlang` format, compiled as embedded resources.

**Languages (27 total):**

| Group | Languages |
|---|---|
| Assembly | 6502, ARM, MIPS, x86, Z80 |
| C-like | C/C++, C#, Dart, Go, Java, JavaScript, Kotlin, PHP, Rust, Swift |
| Data | INI, JSON, SQL, XML, YAML |
| Script | Lua, Perl, Python, Ruby, Shell |
| Misc | Markdown, Plain Text |

- **`EmbeddedSyntaxCatalog`** — singleton; lists all registered `.whlang` entries.
- **`EmbeddedSyntaxEntry`** — record: `LanguageId`, `DisplayName`, `FileExtensions[]`, resource key.

---

## Design Notes

- All definitions are compiled as `EmbeddedResource` items in the `.csproj`; no file-system access is needed at runtime.
- `EmbeddedFormatCatalog` uses a lazy double-checked initialization pattern; thread-safe via `??=` assignment on an immutable reference.
- The `WpfHexEditor.ProjectSystem` depends on this library to associate project items with their format/language for syntax highlighting and structure overlay.

---

## Usage

```csharp
// Format detection
var entry = EmbeddedFormatCatalog.Instance
    .GetAll()
    .FirstOrDefault(e => e.Extensions.Contains(".zip"));

// Syntax language lookup
var lang = EmbeddedSyntaxCatalog.Instance
    .GetAll()
    .FirstOrDefault(e => e.LanguageId == "csharp");
```
