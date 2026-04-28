# WPFHexaEditor

A full-featured WPF hex editor `UserControl` for .NET 8.  
Drop it into any WPF window — no IDE, no plugin host, no external dependencies.

```
dotnet add package WPFHexaEditor
```

> **Full documentation**: [WPFHexaEditor-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Editors/WpfHexEditor.HexEditor/WPFHexaEditor-guide.md) — Architecture, API reference, integration guides (Level 1–4), format detection pipeline, and settings reference.

---

## Quick Start

### 1 — Add the namespace

```xml
<Window
    xmlns:hexe="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor">
```

### 2 — Place the control

```xml
<hexe:HexEditor x:Name="HexEdit" />
```

### 3 — Open a file

```csharp
HexEdit.FileName = @"C:\path\to\file.bin";
```

### 4 — Open a stream

```csharp
HexEdit.Stream = File.OpenRead("data.bin");
```

### 5 — Read or modify bytes

```csharp
// Read
byte b = HexEdit.GetByte(offset);

// Write (adds to undo stack)
HexEdit.SetByte(offset, 0xFF);

// Undo / redo
HexEdit.Undo();
HexEdit.Redo();

// Save
HexEdit.SubmitChanges();          // save to original file
HexEdit.SubmitChanges("out.bin"); // save to new file
```

### Standalone setup (no IDE host)

Merge the resource dictionary so themes and brushes resolve correctly:

```xml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/WpfHexEditor.HexEditor;component/Resources/Dictionary/Generic.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Context menus use opaque backgrounds by default. No extra theming is needed.

---

## Features

### Viewing & Navigation
- Hex + ASCII panels with configurable column count
- Column and row cursor highlighting
- Line numbers and offset display (hex / decimal)
- Scroll marker panel
- Column ruler
- Go to offset (Ctrl+G)
- Read-only mode

### Editing
- In-place byte editing in hex or ASCII panel
- Multi-byte selection with keyboard and mouse
- Drag-selection auto-scroll
- Undo / redo with UndoGroup transactions and coalescence
- Undo history dropdown
- Cut / copy / paste
- Fill selection with value

### Format Detection
- 600+ built-in format definitions (.whfmt) — auto-detection on open
- Format field overlay — semi-transparent colored blocks over detected structures
- Syntax coloring driven by format rules
- Shannon entropy, byte distribution, anomaly detection

### Search
- Find (hex sequence, ASCII text, regex)
- Match case / whole word
- Search result highlighting with scroll-bar tick marks

### Import / Export
- Intel HEX (.hex) import/export
- Motorola S-Record (.srec/.s19) import/export
- Binary template compiler (010 Editor compatible)
- ParsedFields export templates

### UI Controls
- `HexBreadcrumbBar` — visual structure navigator
- `HexScrollMarkerPanel` — overview of bookmarks, search hits, and changes
- Settings panel with auto-generated UI and ColorPicker

### Settings
- Full `DependencyProperty` API for programmatic control
- JSON settings persistence (export / import)
- `ByteToolTipDisplayMode`, `ByteToolTipDetailLevel`, `MouseWheelSpeed`, `FontSize`, `BytePerLine`, and 30+ more

---

## What's New in 3.1.4

- **Feat**: 155+ new `.whfmt` format definitions — total exceeds 790 definitions (schema v2.4).
- **Feat**: `.whfmt` schema v2.4 — `formatId`, `SyntaxDefinition` promoted to first-class property, new block types (`group`, `header`, `data`), `until`/`maxLength` sentinel scanning, `imports` array for cross-format struct references.
- **Feat**: 57 language grammars with `syntaxDefinition` blocks (up from 35).
- **Feat**: `LSP IsFullyLoaded` lifecycle gate — suppresses init-time diagnostic noise before the language server workspace is ready.
- **Fix**: Format detection — `HexEditor.FileOperations` now correctly triggers format detection even when no background blocks are produced by the active `.whfmt`.
- **Fix**: ParsedFields sync — `HexEditor.ParsedFieldsIntegration` reliably re-syncs the parsed-fields panel when the active document changes.
- **Fix**: `ForensicPattern` tolerant converter — invalid pattern values no longer throw; fallback to `null` with log entry.
- **New**: `InputFilter` control — reusable filter-bar `UserControl` for hex/byte input.
- **New**: `HexStringToColorConverter` — XAML binding converter for hex color strings.

## What's New in 3.1.2

- **Fix**: Corrupted or malformed `.whfmt` files no longer crash the IDE — load failures are captured in `FormatLoadFailure` and surfaced in the StatusBar (`⚠ N whfmt failed to load`) instead of propagating exceptions.
- **Perf**: `EmbeddedFormatCatalog` singleton and lazy caches modernized — `LazyInitializer.EnsureInitialized` replaces manual double-checked lock; `GetAll()` / `GetCategories()` now return `IReadOnlySet<T>` backed by `FrozenSet<T>` for better thread safety and lookup performance.
- **Test**: `MakeEntries(rethrow: true)` / `MakeCategories()` exposed as `public static` factory methods — enables `LoadResourcesTest` build gate.
- **Feat**: New format definition `ROM_SNES_SRM` (SNES save RAM).
- **Fix**: `.whfmt` `references` schema v2 standardized across Game and Archives categories.

## What's New in 3.1.1

- **Fix**: `TechnicalDetails.SampleRate` changed from `int?` to `string?`.
- **Fix**: `MOBI.whfmt` references structure corrected.
- **Fix**: `GFX.whfmt`, `TIL.whfmt`, `CHR.whfmt` Platform field corrected.
- **Fix**: 5 `.whfmt` files with invalid `Strength` values corrected.
- **Perf**: `JsonSerializerOptions` in `ImportFromJson` is now a `static readonly` field — avoids 463+ allocations at startup.

## What's New in 3.1.0

- **Fix**: `SignatureStrength` enum now correctly deserialized — was silently falling back to `None(0)`, causing all TIER 1 strong-signature formats to be excluded from detection.
- **Fix**: TIER 1 candidates scored before early-exit check.
- **Fix**: TIER 2 text-heuristic suppressed when TIER 1 has a match.
- **Fix**: Entropy check skipped for `Strong`/`Unique` signatures.
- **Fix**: `EmbeddedFormatCatalog.GetAll()` fully thread-safe.
- **Fix**: 463 `.whfmt` block-comment headers removed (fixes [#229](https://github.com/abbaye/WpfHexEditorIDE/issues/229)).
- **Fix**: `HexBreadcrumbBar` ContextMenu fully opaque in standalone apps.

## What's New in 3.0.4

- **Fix**: `ResourceReferenceKeyNotFoundException` no longer thrown in standalone WPF apps (fixes [#228](https://github.com/abbaye/WpfHexEditorIDE/issues/228)).
- **Fix**: Drag-selection auto-scroll — cross-panel mouse boundary no longer stops scrolling (fixes [#227](https://github.com/abbaye/WpfHexEditorIDE/issues/227)).
- **Fix**: Column/row highlight tracks cursor on vertical scroll.
- **Feat**: Context menu — drop shadow, MDL2 icons, accent band, light theme.

## What's New in 3.0.0 (since WPFHexaEditor 2.1.7)

**Breaking changes**: .NET 8.0-windows only, namespace `WPFHexaEditor` → `WpfHexEditor.HexEditor`, assembly renamed, modular architecture.  
See full changelog in the [GitHub repository](https://github.com/abbaye/WpfHexEditorIDE).

---

## Included Assemblies

All bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|---|---|
| WpfHexEditor.HexEditor | `HexEditor` UserControl — main entry point |
| WpfHexEditor.Core | Byte providers, format detection, search, undo/redo |
| WpfHexEditor.Core.BinaryAnalysis | Cross-platform binary analysis (no WPF dependency) |
| WpfHexEditor.Core.Definitions | 600+ embedded format definitions (.whfmt) |
| WpfHexEditor.Editor.Core | Shared editor abstractions |
| WpfHexEditor.ColorPicker | Color picker control (settings panel) |
| WpfHexEditor.HexBox | Hex display rendering control |
| WpfHexEditor.ProgressBar | Progress bar control |

**Localizations**: ar-SA, de-DE, es-419, es-ES, fr-CA, fr-FR, hi-IN, it-IT, ja-JP, ko-KR, nl-NL, pl-PL, pt-BR, pt-PT, ru-RU, sv-SE, tr-TR, zh-CN

---

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
