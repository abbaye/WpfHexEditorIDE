# WpfHexEditorControl

A full-featured WPF hex editor UserControl for .NET 8. Successor to [WPFHexaEditor](https://www.nuget.org/packages/WPFHexaEditor/).

## Quick Start

```xml
<Window xmlns:hexe="clr-namespace:WpfHexEditor.HexEditor;assembly=WpfHexEditor.HexEditor">
    <hexe:HexEditor FileName="C:\path\to\file.bin" />
</Window>
```

```
dotnet add package WpfHexEditorControl
```

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

| Assembly | Purpose |
|----------|---------|
| WpfHexEditor.HexEditor | HexEditor UserControl (main entry point) |
| WpfHexEditor.Core | Byte providers, format detection, search, undo/redo |
| WpfHexEditor.Core.BinaryAnalysis | Cross-platform binary analysis (no WPF dependency) |
| WpfHexEditor.Core.Definitions | 300+ embedded format definitions (.whfmt) |
| WpfHexEditor.Editor.Core | Shared editor abstractions |
| WpfHexEditor.ColorPicker | Color picker control for settings |
| WpfHexEditor.HexBox | Hex display control |
| WpfHexEditor.ProgressBar | Progress bar control |

## License

GNU Affero General Public License v3.0 (AGPL-3.0)

## Links

- [GitHub Repository](https://github.com/abbaye/WpfHexEditorControl)
- [Report Issues](https://github.com/abbaye/WpfHexEditorControl/issues)
