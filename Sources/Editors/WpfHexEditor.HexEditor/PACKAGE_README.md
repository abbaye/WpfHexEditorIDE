# WPFHexaEditor

A full-featured WPF hex editor UserControl for .NET 8. Successor to [WPFHexaEditor](https://www.nuget.org/packages/WPFHexaEditor/).

## Quick Start

```xml
<Window xmlns:hexe="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor">
    <hexe:HexEditor FileName="C:\path\to\file.bin" />
</Window>
```

```
dotnet add package WPFHexaEditor
```

## What's New in 3.1.2

- **Fix**: Invalid `\x` JSON escape sequences in `HDF5.whfmt`, `NETCDF.whfmt`, `NPY.whfmt` — `\x89`, `\x01`, `\x02`, `\x93` are not valid JSON escapes and caused `JsonReaderException` in `EmbeddedFormatCatalog.LoadHeader`. Replaced with human-readable hex notation.

## What's New in 3.1.1

- **Fix**: `TechnicalDetails.SampleRate` changed from `int?` to `string?` — AMR and OPUS had descriptive values like `"8000 Hz (AMR-NB) / 16000 Hz (AMR-WB)"` that were silently dropped
- **Fix**: `MOBI.whfmt` references structure corrected — was an array wrapping an object, must be a plain object
- **Fix**: `GFX.whfmt`, `TIL.whfmt`, `CHR.whfmt` Platform field corrected — was a string array, must be a single string
- **Fix**: 5 `.whfmt` files with invalid `Strength` values corrected (`"strong"` → `"Strong"`, `"moderate"` → `"Medium"`)
- **Perf**: `JsonSerializerOptions` in `ImportFromJson` is now a `static readonly` field — avoids 463+ allocations at startup

## What's New in 3.1.0

### Format Detection — Major Overhaul

- **Fix**: `SignatureStrength` enum now correctly deserialized from `.whfmt` files — `"Strength": "Strong"` was silently falling back to `None(0)` due to missing `JsonStringEnumConverter`, causing all TIER 1 strong-signature formats (PE, ELF, RTF, and hundreds more) to be excluded from detection. Root cause of all format mis-detections since 3.0.0.
- **Fix**: TIER 1 candidates are now scored before the early-exit check — confidence threshold was evaluated on unscored candidates (all `ConfidenceScore = 0`), so the early-exit never triggered and TIER 2 (plain-text heuristic) displaced correct TIER 1 matches (e.g. RTF detected as "Plain Text").
- **Fix**: TIER 2 text-heuristic detection is now suppressed when TIER 1 has a match — prevents plain-text fallback from overriding a verified magic-byte signature match.
- **Fix**: Entropy check is skipped for `Strong`/`Unique` signatures — a verified magic byte sequence is definitive proof of identity; entropy filtering only added false negatives for text-based formats with strong signatures (RTF, XML, SVG).
- **Fix**: `EmbeddedFormatCatalog.GetAll()` is now fully thread-safe (double-checked lock + `volatile`) — race condition between `PreWarm()` background thread and UI thread could produce an empty catalog on first access.
- **Fix**: 5 `.whfmt` definitions with invalid `Strength` values corrected (`"strong"` → `"Strong"`, `"moderate"` → `"Medium"`).
- **Fix**: `required: true` added to 10 `.whfmt` definitions that were missing it (RTF, DJVU, AIFF, OPUS, AVIF, JFIF, JPEG2000, PCX, TGA, TIFF).
- **Robustness**: `SignatureStrengthConverter` now accepts case-insensitive strings (`"strong"`, `"Strong"`, `"STRONG"`), integer values (`80`), and falls back to `Medium` for any unknown value — never throws.

### Format Definitions — 463 Files Updated

- **Fix**: Removed `/* */` block-comment file headers from all 463 `.whfmt` definitions (fixes [#229](https://github.com/abbaye/WpfHexEditorIDE/issues/229)) — these headers preceded the opening `{` and could cause `JsonReaderException` on first load.
- **Feat**: Version numbers bumped across all 455+ `.whfmt` definitions.
- **Feat**: `.whfmt` enrichment pass — improved descriptions, detection rules, and export templates across multiple categories.

### UI / Controls

- **Fix**: `HexBreadcrumbBar` segment dropdown (ContextMenu) is now fully opaque in standalone WPF apps — `BC_Background` brush alpha is forced to 255 before assignment, preventing see-through popup in apps without the IDE theme host.
- **Fix**: ScrollBar theming consistency fix.
- **Feat**: `IconGlyphs` constants class for Segoe MDL2 Assets glyph codes.
- **Feat**: `ParsedFields` panel export templates.

## What's New in 3.0.8

- **Fix**: `JsonReaderException` no longer thrown in `EmbeddedFormatCatalog.LoadHeader` on first run — removed fragile `/* */` file headers from all 463 `.whfmt` format definitions (fixes [#229](https://github.com/abbaye/WpfHexEditorIDE/issues/229))
- **Fix**: BCB (Custom Background Block) visual tree locked during navigation to prevent mouse re-dispatch loop
- **Fix**: Bookmark chip `MouseDown` re-dispatch loop eliminated
- **Fix**: Bookmark chips no longer re-render on every navigation — repaint only on actual changes

## What's New in 3.0.4

- **Fix**: `ResourceReferenceKeyNotFoundException` no longer thrown in standalone WPF apps — `GetThemeColor` now uses `TryFindResource` instead of `FindResource` (fixes [#228](https://github.com/abbaye/WpfHexEditorIDE/issues/228))
- **Fix**: Drag-selection auto-scroll — cross-panel mouse boundary no longer stops scrolling in HexEditor (fixes [#227](https://github.com/abbaye/WpfHexEditorIDE/issues/227))
- **Fix**: Column/row highlight tracks cursor position correctly on vertical scroll
- **Feat**: Context menu — drop shadow, MDL2 icons, accent band, light theme styling

## What's New in 3.0.3

- **Fix**: `AllowCustomBackgroundBlock` dependency property is now obsolete and has no effect. Custom background blocks render automatically when blocks are added via `AddCustomBackgroundBlock()`. Use `AddCustomBackgroundBlock()` / `ClearCustomBackgroundBlock()` as the sole control point.
- **Localization**: 18 satellite assemblies now bundled in the package (ar-SA, de-DE, es-ES, fr-FR, ja-JP, zh-CN, and 12 more). No separate install required.

## What's New in 3.0.2

- Column ruler support
- Scroll marker panel improvements
- Format detection performance improvements

## What's New in 3.0.0 (since WPFHexaEditor 2.1.7)

### Breaking Changes
- **Target framework**: .NET 8.0-windows (dropped .NET Framework 4.7 and .NET Core 3.1)
- **Namespace renamed**: `WPFHexaEditor` → `WpfHexEditor.HexEditor`
- **Assembly renamed**: `WPFHexaEditor.dll` → `WpfHexEditor.HexEditor.dll`
- **Modular architecture**: core logic extracted into separate assemblies (Core, BinaryAnalysis, Definitions, Editor.Core)

### Performance
- GlyphRun-based text renderer — replaces FormattedText, eliminates per-glyph allocation
- LineVisualPool — object pooling for visible line elements, zero GC pressure on scroll
- HexLookup table — O(1) byte-to-hex conversion via precomputed 256-entry table
- TBL key buffer — zero-allocation custom table encoding
- DrawingContext renderers for HexBox and ProgressBar controls
- Cached DPI, FormattedText, and Pens — 25-55ms → 5-8ms render time in docking scenarios
- Dirty-line tracking — only re-render lines that changed, not the full viewport
- Mouse hover overlay — eliminated full re-render on mouse move

### New Features
- **400+ built-in format definitions** (.whfmt) — automatic format detection and syntax coloring for PE, ELF, ZIP, PNG, PDF, MP3, SQLite, and hundreds more
- **Column and row highlighting** — visual cursor tracking across hex and ASCII panels
- **Undo/redo overhaul** — UndoGroup composite, transactions, coalescence, history dropdown
- **Binary analysis** — Shannon entropy, byte distribution, anomaly detection, data type estimation
- **Intel HEX / S-Record** — import/export support
- **Binary template compiler** — 010 Editor compatible C-like templates
- **Format field overlay** — semi-transparent colored blocks over detected format structures
- **Settings UI** — dynamic property editor with ColorPicker for all editor properties
- **IDocumentEditor interface** — standardized editor contract for hosting frameworks

### Bug Fixes
- Row highlight now updates on vertical scroll
- Null guard on UpdateColumnHighlight
- FileSystemWatcher re-fire suppressed during ReloadFromDisk
- ByteToolTipDisplayMode/DetailLevel DP defaults synced to HexViewport at init
- MouseWheelSpeed DP implemented in scroll handler

## Included Assemblies

All bundled inside the package — zero external NuGet dependencies:

| Assembly | Purpose |
|----------|---------|
| WpfHexEditor.HexEditor | HexEditor UserControl (main entry point) |
| WpfHexEditor.Core | Byte providers, format detection, search, undo/redo |
| WpfHexEditor.Core.BinaryAnalysis | Cross-platform binary analysis (no WPF dependency) |
| WpfHexEditor.Core.Definitions | 463 embedded format definitions (.whfmt) |
| WpfHexEditor.Editor.Core | Shared editor abstractions |
| WpfHexEditor.ColorPicker | Color picker control for settings |
| WpfHexEditor.HexBox | Hex display control |
| WpfHexEditor.ProgressBar | Progress bar control |

**Localizations**: ar-SA, de-DE, es-419, es-ES, fr-CA, fr-FR, hi-IN, it-IT, ja-JP, ko-KR, nl-NL, pl-PL, pt-BR, pt-PT, ru-RU, sv-SE, tr-TR, zh-CN

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorIDE)
- [Report Issues](https://github.com/abbaye/WpfHexEditorIDE/issues)
