# WPFHexaEditor

A full-featured WPF hex editor `UserControl` for .NET 8.  
Drop it into any WPF window — no IDE, no plugin host, no external dependencies.

```
dotnet add package WPFHexaEditor
```

---

## Quick Start

### 1 — Add the namespace

```xml
<Window
    xmlns:hex="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor">
```

Both `HexEditor` and `HexEditorSplitHost` live in the same assembly — one declaration covers both.

### 2 — Place the control

**Single editor:**
```xml
<hex:HexEditor x:Name="HexEdit" />
```

**With built-in split view:**
```xml
<hex:HexEditorSplitHost x:Name="HexEdit" />
```

The split button is built into the toolbar — the user clicks it to open/close the second pane.  
Access the underlying editor via `HexEdit.PrimaryEditor`.

### 3 — Open a file

```csharp
// HexEditor
HexEdit.FileName = @"C:\path\to\file.bin";

// HexEditorSplitHost
HexEdit.OpenFile(@"C:\path\to\file.bin");
HexEdit.PrimaryEditor.FileName = @"C:\path\to\file.bin"; // equivalent
```

### 4 — Open a byte array (no file needed)

```xml
<!-- XAML binding -->
<hex:HexEditor ByteArray="{Binding MyBytes}" />
```

```csharp
// Code-behind
HexEdit.ByteArray = new byte[] { 0x00, 0xFF, 0xDE, 0xAD };

// Async load (same result, returns Task.CompletedTask)
await HexEdit.OpenByteArrayAsync(myBuffer, readOnly: false);

// Retrieve the (possibly edited) buffer
byte[] modified = HexEdit.GetCurrentBytes();

// Save without a file — fires the ByteArraySaved event
HexEdit.ByteArraySaved += (_, bytes) => ProcessBytes(bytes);
HexEdit.SaveByteArray();  // or Ctrl+S in byte-array mode
```

### 5 — Open a stream

```csharp
HexEdit.Stream = File.OpenRead("data.bin");         // HexEditor
HexEdit.OpenStream(File.OpenRead("data.bin"));       // HexEditorSplitHost
```

### 6 — Read or modify bytes

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

### 7 — Resource dictionary (required)

Merge once in `App.xaml` so themes and brushes resolve correctly:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="pack://application:,,,/WpfHexEditor.HexEditor;component/Resources/Dictionary/Generic.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

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
- 856 built-in format definitions (.whfmt) — auto-detection on open
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

### In-Memory / Byte Array Mode
- `ByteArray` DependencyProperty — bind a `byte[]` directly in XAML, no file required
- `OpenByteArrayAsync(byte[], readOnly)` — programmatic load
- `GetCurrentBytes()` — snapshot of the current buffer including edits
- `SaveByteArray()` / `ByteArraySaved` event — save-without-file workflow
- `IsByteArrayMode` — flag to detect current backing mode
- `IsDirty` correctly tracks in-memory changes

### UI Controls
- `HexEditorSplitHost` — synchronized split-view host with built-in toolbar toggle
- `HexEditorSettings` — auto-generated settings panel with live binding and JSON persistence
- `HexBreadcrumbBar` — visual structure navigator
- `HexScrollMarkerPanel` — overview of bookmarks, search hits, and changes

### Parsed Fields Integration
- `IParsedFieldsPanel` — bridge interface for custom parsed-fields side panels
- `ConnectParsedFieldsPanel()` / `DisconnectParsedFieldsPanel()` — attach/detach at runtime
- `GetByteProvider()` — access the raw byte provider for custom analysis
- `FindSelect(position, length)` — jump to and select any byte range

### Settings
- Full `DependencyProperty` API for programmatic control
- JSON settings persistence (export / import)
- `ByteToolTipDisplayMode`, `ByteToolTipDetailLevel`, `MouseWheelSpeed`, `FontSize`, `BytePerLine`, and 30+ more

---

## What's New in 3.4.5

- **New**: First-class `byte[]` support — bind or set `ByteArray` directly in XAML without a file (issue #253).
- **New**: `OpenByteArrayAsync(byte[], readOnly)` — loads an in-memory buffer programmatically.
- **New**: `GetCurrentBytes()` — retrieves the current buffer (including unsaved edits) as `byte[]`.
- **New**: `ByteArraySaved` event — fires on `SaveByteArray()` or Ctrl+S in byte-array mode; subscriber receives the full modified buffer.
- **New**: `IsByteArrayMode` property — `true` when the editor is backed by a `byte[]` rather than a file.
- **Fix**: `IsDirty` no longer returns `true` for a freshly loaded byte-array document with no edits.
- **Fix**: Save pipeline (`SaveOrSaveAs`) routes correctly through `ByteArraySaved` when in byte-array mode instead of opening a `SaveFileDialog`.
- **No breaking API changes** — drop-in upgrade from 3.4.0.

---

## What's New in 3.4.0

- **New**: Entropy heatmap — per-byte Shannon entropy rendered as a color gradient directly on the hex grid. Toggle via the context menu or the `ShowEntropyHeatmap` dependency property; configure palette and block size from `HexEditorSettings`.
- **New**: `HexEditorSettings` entropy options page — `EntropyBlockSize`, `EntropyColorLow`, `EntropyColorHigh`, theme-aware palette.
- **Updated**: `whfmt.FileFormatCatalog` → **1.5.0** (856 format definitions, schema v3.1 with `embeddedLanguages[]` support).
- **Fix**: Undo/redo fully wired in standalone mode — `Ctrl+Z` / `Ctrl+Y` no longer swallowed when the control is hosted outside a docking shell.
- **Fix**: `IsDirty` correctly resets to `false` after undoing all changes back to the original state.
- **No breaking API changes** — drop-in upgrade from 3.3.x.

---

## What's New in 3.3.2

- **Fix**: `HexEditorLocalizedDictionary` is now merged into `UserControl.Resources` — resolves `XamlParseException` at startup when localization `StaticResource` keys are used outside the `ContextMenu` tree (standalone and IDE mode both affected).
- **No public API changes** — drop-in upgrade from 3.3.1.

---

## What's New in 3.3.1

- **Updated** to `whfmt.FileFormatCatalog 1.3.2` (whfmt v3 GA) — 799 format definitions, runtime expression engine, schema v3 canonical.
- **Fix**: Split-view focus border hidden when the split is closed (was leaving a stale frame around the empty secondary slot).
- **Fix**: Breadcrumb bar synced in the secondary pane after layout reload.
- **Fix**: All mutation types + initial paint + standalone-mode split correctly synced between primary and secondary editors.
- **Fix**: Module panels now return singleton instances — prevents the dual-cache stale-placeholder bug when reopening a tool panel after it was closed.
- **No public API changes** — drop-in upgrade from 3.3.0.

## What's New in 3.3.0

- **New**: `HexEditorSplitHost` — drop-in split-view host wrapping a primary + optional secondary `HexEditor`; synchronized scrolling, mutations, and breadcrumb.
- **New**: `HexEditorSettings` — auto-generated settings panel `UserControl`; exposes every `HexEditor` `DependencyProperty` with live binding, color picker, JSON export/import.
- **New**: `IParsedFieldsPanel` integration API — `ConnectParsedFieldsPanel()` / `DisconnectParsedFieldsPanel()`, `GetByteProvider()`, `FindSelect(position, length)`.
- **New**: Themed `MessageBox` / `IDialogService` — drop-in replacement for `MessageBox.Show`; injectable, themeable via `DynamicResource`, usable in any WPF host.
- **New**: +10 localizations — uk-UA, cs-CZ, vi-VN, hu-HU, ro-RO, id-ID, th-TH, el-GR, da-DK, fi-FI (now 28 languages total).
- **Fix**: Split-view secondary pane — breadcrumb bar now syncs correctly; all mutation types (insert, delete, replace) propagated to secondary.
- **Fix**: Focus border hidden when split panel is closed.

## What's New in 3.2.0

- **New**: Go to position dialog — jump directly to any byte offset via `Ctrl+G`.
- **New**: Unified `UndoEngine` — `Ctrl+Z`/`Ctrl+Y` history now uses a shared undo engine; undo groups and coalescence work consistently across sessions.
- **Fix**: Drag-selection auto-scroll — window-level mouse capture ensures scrolling continues when the cursor leaves the control boundary.
- **Fix**: Column highlight defaults — `ShowColumnHighlight` and `ShowAsciiColumnHighlight` now default to `false` to reduce visual noise out-of-the-box.
- **Fix**: `HexBreadcrumbBar` freeze — Render-priority dispatcher guard prevents mouse re-dispatch loop during visual tree rebuild on rapid navigation.
- **Fix**: `HexBreadcrumbBar` double rebuild — phantom empty row caused by XAML/code-behind duplication resolved; bookmark chip re-render only fires on actual bookmark set changes.
- **Perf**: `ByteProvider` extracted to `WpfHexEditor.Core.ByteProvider` standalone library.

## What's New in 3.1.3

- **Feat**: 155+ new `.whfmt` format definitions added (Groups C–J) — total now exceeds 600 definitions.
- **Feat**: `FormatSchemaValidator` wired — `.whfmt` files are now validated against schema v3 at load time; violations are reported via `FormatLoadFailure`.
- **Feat**: `.whfmt` schema bumped to v2.3 — `references` and `detection` fields unified across all categories.
- **Fix**: Stream operations — contributor enhancements to stream-backed byte provider edge cases.
- **Fix**: `ForensicPattern` tolerant converter — invalid pattern values no longer throw; fallback to `null` with log entry.
- **New**: `InputFilter` control — reusable filter-bar `UserControl` for hex/byte input.
- **New**: `HexStringToColorConverter` — XAML binding converter for hex color strings.

## What's New in 3.1.2

- **Fix**: Corrupted or malformed `.whfmt` files no longer crash the host application — load failures are captured in `FormatLoadFailure` and surfaced in the StatusBar (`⚠ N whfmt failed to load`) instead of propagating exceptions.
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
| WpfHexEditor.Core.Definitions | 856 embedded format definitions (.whfmt, schema v3.1) |
| WpfHexEditor.Editor.Core | Shared editor abstractions |
| WpfHexEditor.ColorPicker | Color picker control (settings panel) |
| WpfHexEditor.HexBox | Hex display rendering control |
| WpfHexEditor.ProgressBar | Progress bar control |

**Localizations** (28): ar-SA, cs-CZ, da-DK, de-DE, el-GR, es-419, es-ES, fi-FI, fr-CA, fr-FR, hi-IN, hu-HU, id-ID, it-IT, ja-JP, ko-KR, nl-NL, pl-PL, pt-BR, pt-PT, ro-RO, ru-RU, sv-SE, th-TH, tr-TR, uk-UA, vi-VN, zh-CN

---

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- **Full documentation**: [WPFHexaEditor-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Editors/WpfHexEditor.HexEditor/WPFHexaEditor-guide.md) — Architecture, API reference, integration guides (Level 1–4), format detection, search, export, and settings reference.
- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
