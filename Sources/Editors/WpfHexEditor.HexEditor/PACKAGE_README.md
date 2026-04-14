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

## What's New in 3.0.5

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
| WpfHexEditor.Core.Definitions | 400+ embedded format definitions (.whfmt) |
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
