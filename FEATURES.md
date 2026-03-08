# WpfHexEditor — Complete Feature Reference

> **Platform:** Windows · .NET 8.0 · Native WPF
> **Architecture:** VS-style IDE with plugin system, dockable panels, multi-editor workspace

---

## Table of Contents

- [IDE Shell](#ide-shell)
- [Project System](#project-system)
- [Editors](#editors)
- [Plugins](#plugins)
- [IDE Panels](#ide-panels)
- [Integrated Terminal](#integrated-terminal)
- [HexEditor Control](#hexeditor-control)
- [Reusable Controls & Libraries](#reusable-controls--libraries)
- [Performance Architecture](#performance-architecture)
- [Developer & SDK](#developer--sdk)
- [Legend](#legend)

---

## IDE Shell

### Application Shell

| Feature | Status | Notes |
|---------|--------|-------|
| VS-style docking (float, dock, auto-hide, tab groups) | ✅ | Custom engine — zero third-party docking dependency |
| 8 built-in visual themes | ✅ | Dark · Light · VS2022Dark · DarkGlass · Minimal · Office · Cyberpunk · VisualStudio |
| Runtime theme switching | ✅ | Live, no restart required |
| Colored tabs with `TabSettingsDialog` | ✅ | Per-tab color + left/right placement |
| VS2022-style status bar | ✅ | Edit mode · bytes/line · caret offset · plugin personality |
| Output panel | ✅ | Session log and operation messages |
| Error/Diagnostics panel | ✅ | Severity filter, navigation to offset |
| Toolbar overflow manager | ✅ | All panels collapse toolbar groups on resize |
| Plugin monitor panel | ✅ | Per-plugin CPU %, RAM, load state, priority |
| Plugin manager UI | ✅ | Load/unload/inspect plugins at runtime |
| In-IDE plugin development (#138) | 🔧 Planned | Write, compile and hot-reload plugins directly inside the IDE |
| Command palette | 🔧 Planned | Keyboard-driven access to all IDE commands |
| Global options / settings dialog | 🔧 Planned | Centralized settings with per-plugin sections |
| Workspace-scoped settings | 🔧 Planned | Per-project overrides for themes, encoding, layout |

### Keyboard Shortcuts (IDE-level)

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open file in new editor tab |
| Ctrl+S | Save current editor |
| Ctrl+W | Close current tab |
| Ctrl+Tab | Cycle editor tabs |
| Ctrl+V | Paste (also opens assembly dialog in AssemblyExplorer) |
| F4 | Open Properties panel |
| F7 / F8 | Navigate diffs |

---

## Project System

| Feature | Status | Notes |
|---------|--------|-------|
| Solution management (`.whsln`) | ✅ | Create, open, save, close |
| Project management (`.whproj`) | ✅ | Multiple projects per solution |
| VS `.sln` / `.csproj` import | 🔧 Planned | Read-only parsing via MSBuild |
| Virtual folders | ✅ | Logical grouping without disk structure |
| Physical folders | ✅ | Mirrors disk directory tree |
| Show All Files mode | ✅ | Reveals untracked files in project directories |
| Per-file state persistence | ✅ | Bookmarks, caret position, scroll, encoding |
| Typed item links | ✅ | e.g. `.bin` linked to `.tbl` → auto-applied on open |
| Format versioning + auto-migration | ✅ | In-memory format upgrade on open with automatic backup |
| File templates | ✅ | Binary · TBL · JSON · Text |

---

## Editors

All editors implement `IDocumentEditor` and integrate with docking, undo/redo, status bar, search, and the options system.

| Editor | Status | Key Capabilities |
|--------|--------|-----------------|
| **Hex Editor** | ✅ | Insert/overwrite, 400+ format detection, SIMD search, TBL, bookmarks, BarChart, scroll markers |
| **TBL Editor** | ✅ | Character table editing for custom encodings and ROM hacking, DTE/MTE support |
| **JSON Editor** | ✅ | Real-time validation, diagnostics, syntax coloring |
| **Text Editor** | ✅ | Syntax highlighting, multi-encoding |
| **Code Editor** | 🔧 In dev | VS-like: IntelliSense, folding, gutter, multi-caret, multi-language syntax, diagnostics |
| **Script Editor** | 🔧 Stub | Planned scripting host |
| **Image Viewer** | 🔧 Stub | Planned |
| **Audio Viewer** | 🔧 Stub | Planned |
| **Diff Viewer** | 🔧 Stub | Side-by-side binary/text comparison |
| **Disassembly Viewer** | 🔧 Stub | x86/x64 disassembly display |
| **Entropy Viewer** | 🔧 Stub | Block entropy visualization |
| **Structure Editor** | 🔧 Stub | Binary structure definition & overlay |
| **Tile Editor** | 🔧 Stub | Pixel/tile editing for ROM graphics |
| **Changeset Editor** | 🔧 Stub | Edit history and patch management |

### Code Editor — Feature Set

| Feature | Status | Notes |
|---------|--------|-------|
| Multi-language syntax highlighting | 🔧 Planned | C#, JSON, XML, XAML, Lua, Python, and more |
| IntelliSense / autocomplete / snippets | 🔧 Planned | Language-server-style suggestions |
| Multi-caret and multi-selection | 🔧 Planned | VS-like editing ergonomics |
| Code folding / collapse | 🔧 Planned | Block indicators in gutter |
| Gutter: line numbers, breakpoint markers, error indicators | 🔧 Planned | Full VS-like gutter |
| Virtual scroll for large files (>1 GB) | 🔧 Planned | Render only visible lines |
| Diagnostics integration | 🔧 Planned | Errors/warnings pushed to Error panel |
| Command system integration (#78) | 🔧 Planned | Keyboard-bound, palette-accessible scripted commands |
| Event system (#80) | 🔧 Planned | Editor events exposed for plugin subscription |
| Scripting host (#79) | 🔧 Planned | Embedded script execution (Lua / C# scripting) |
| Plugin sandbox per-editor (#81) | 🔧 Planned | Isolated plugin execution per document |
| Workspace & project integration | 🔧 Planned | Respects `.whsln` / `.whproj` context |
| Undo/Redo + diff tracking | 🔧 Planned | IDE-level undo stack, changeset history |
| Theme & options integration | 🔧 Planned | Follows global IDE theme and options system |

---

## Plugins

Plugins are loaded via `WpfHexEditor.PluginHost` with priority-based ordering and optional sandboxing. All plugins expose dockable panels conforming to the VS-Like standard.

### Assembly Explorer (`WpfHexEditor.Plugins.AssemblyExplorer`)

| Feature | Status | Notes |
|---------|--------|-------|
| Open assembly via dialog / drag-drop / Ctrl+V | ✅ | Supports .dll, .exe, .winmd |
| Namespace / type / member tree | ✅ | Classes, interfaces, structs, enums, delegates |
| Method, field, property, event nodes | ✅ | Full member breakdown |
| Colored semantic icons | ✅ | VS Code color palette per node type |
| Lock badge for non-public members | ✅ | Visual access-modifier indicator |
| C# skeleton decompiler | ✅ | `CSharpSkeletonEmitter` — BCL-only, zero NuGet |
| IL text emitter | ✅ | Full ECMA-335 IL via `IlTextEmitter` |
| 4-tab Detail pane (Code / IL / Info / Hex) | ✅ | IL tab auto-selected for method nodes |
| Open in Code Editor | ✅ | Via `IUIRegistry.RegisterDocumentTab` |
| Live tree filter / search | ✅ | Bottom-up `SetNodeVisibility`, parent auto-expand |
| "Inherits From" group | ✅ | Shows base type and interfaces per class |
| Framework badge on root nodes | ✅ | Displays `[.NET X.X]` target |
| Show non-public members toggle | ✅ | Options page |
| Show inherited members toggle | ✅ | Options page |
| Recent files list (max 20) | ✅ | Persisted in options |
| Pin assemblies across file change | ✅ | Options page |
| Core library: BCL-only, no NuGet | ✅ | `System.Reflection.Metadata` + `PEReader` inbox in .NET 8 |
| Full method body decompilation | 🔧 Planned | Emit full C# code (not just skeleton) |
| Cross-assembly reference navigation | 🔧 Planned | Jump to definition across loaded assemblies |
| Attribute and custom metadata display | 🔧 Planned | Show custom attributes on any node |
| Export decompiled output | 🔧 Planned | Save C# / IL to file |

### Data Inspector (`WpfHexEditor.Plugins.DataInspector`)

| Feature | Status | Notes |
|---------|--------|-------|
| 40+ byte type interpretations at caret | ✅ | Int8/16/32/64, Float, Double, GUID, Dates, Flags, ... |
| Scope: Caret / Selection / Active View / Whole File | ✅ | Switchable from toolbar |
| Byte distribution BarChart | ✅ | Byte frequency histogram |
| Lazy whole-file load (one-shot) | ✅ | `_wholeFileChartLoaded` guard — no reload on each selection |
| Endianness toggle | ✅ | Little / Big Endian |
| Toolbar overflow support | ✅ | 5 collapsible toolbar groups |

### Parsed Fields (`WpfHexEditor.Plugins.ParsedFields`)

| Feature | Status | Notes |
|---------|--------|-------|
| 400+ binary format detection | ✅ | PE, ELF, ZIP, PNG, MP3, SQLite, PDF, ... |
| Field list with type and offset | ✅ | Hierarchical field tree |
| Inline field value editing | ✅ | Edit parsed values directly |
| Type overlay on hex grid | ✅ | Visual highlight per field |
| Export fields | ✅ | Toolbar export action |

### Structure Overlay (`WpfHexEditor.Plugins.StructureOverlay`)

| Feature | Status | Notes |
|---------|--------|-------|
| Visual field highlighting on hex grid | ✅ | Color-coded regions |
| Add structure overlay manually | ✅ | Via toolbar |
| Overlay from parsed format | ✅ | Auto-generated from Parsed Fields |

### Pattern Analysis (`WpfHexEditor.Plugins.PatternAnalysis`)

| Feature | Status | Notes |
|---------|--------|-------|
| Byte pattern detection | ✅ | Statistical analysis of byte sequences |
| Refresh from toolbar | ✅ | On-demand re-analysis |

### File Statistics (`WpfHexEditor.Plugins.FileStatistics`)

| Feature | Status | Notes |
|---------|--------|-------|
| Byte frequency histogram | ✅ | Full 0x00–0xFF distribution |
| Entropy calculation | ✅ | Shannon entropy per block |
| Null / printable / high byte ratios | ✅ | Summary statistics |

### File Comparison (`WpfHexEditor.Plugins.FileComparison`)

| Feature | Status | Notes |
|---------|--------|-------|
| Binary file diff | ✅ | Byte-level comparison |
| SIMD-accelerated comparison | ✅ | 3 variants: Basic / Parallel / SIMD |
| Similarity percentage | ✅ | `CalculateSimilarity()` 0–100% |
| Difference count | ✅ | `CountDifferences()` with SIMD |

### Archive Structure (`WpfHexEditor.Plugins.ArchiveStructure`)

| Feature | Status | Notes |
|---------|--------|-------|
| Archive format tree display | ✅ | ZIP, RAR, 7z, CAB structural view |
| Entry navigation | ✅ | Jump to entry offset in hex editor |

### Format Info (`WpfHexEditor.Plugins.FormatInfo`)

| Feature | Status | Notes |
|---------|--------|-------|
| Detected format metadata display | ✅ | MIME type, version, encoding info |
| Format confidence score | ✅ | Detection certainty indicator |

### Custom Parser Template (`WpfHexEditor.Plugins.CustomParserTemplate`)

| Feature | Status | Notes |
|---------|--------|-------|
| User-defined field parser | 🔧 In dev | Template-based binary parsing |
| Script-driven field definitions | 🔧 In dev | Extensible format description |
| Visual template designer | 🔧 Planned | Drag-and-drop field layout editor |
| Export as C struct / Go struct | 🔧 Planned | Generate native struct definitions from template |
| Share / import templates | 🔧 Planned | Community template exchange |

---

## IDE Panels

Built-in panels that ship with `WpfHexEditor.Panels.IDE`. All follow the VS-Like dockable panel standard.

| Panel | Status | Description |
|-------|--------|-------------|
| Solution Explorer | ✅ | Project tree with virtual/physical folders, file operations |
| Properties Panel | ✅ | Context-aware F4 panel via `IPropertyProvider` |
| Error/Diagnostics Panel | ✅ | Severity filter, navigate-to-offset from any `IDiagnosticSource` |
| File Diff Panel | ✅ | Side-by-side binary comparison, F7/F8 navigation |
| Plugin Monitor Panel | ✅ | Per-plugin CPU %, RAM, load state, execution metrics |
| Plugin Manager | ✅ | Load/unload/inspect plugins, version and priority info |
| Output Panel | ✅ | Session log, operation messages from all components |

---

## Integrated Terminal

Multi-tab terminal panel (`WpfHexEditor.Terminal`) with macro recording and shell session management.

| Feature | Status | Notes |
|---------|--------|-------|
| Multi-tab shell sessions | ✅ | Unlimited tabs, each with independent process |
| Shell types: HxTerminal / PowerShell / Bash / CMD | ✅ | Per-session shell selection |
| New session via "+" menu | ✅ | Choose shell type on creation |
| Close session (last tab protected) | ✅ | Cannot close the last remaining tab |
| Session command history | ✅ | Per-session history |
| Macro recording | ✅ | `record start` / `record stop` / `record save <path>` |
| Macro replay | ✅ | `replay-history [N]` command |
| Built-in commands: `record`, `replay-history` | ✅ | Registered via `TerminalCommandRegistry` |
| Ctrl+L to clear terminal | ✅ | Keyboard shortcut |
| Toolbar overflow: 5 collapsible groups | ✅ | Scroll nav · history · filters · recording · save |
| Theme compliance | ✅ | Follows global IDE theme |
| Save session output to file | 🔧 Planned | Export full session transcript |
| Split terminal panes | 🔧 Planned | Side-by-side sessions in the same panel |
| Environment variable editor | 🔧 Planned | Per-session environment configuration |
| Auto-attach to running process | 🔧 Planned | Pipe into an existing process stdio |

---

## HexEditor Control

`WpfHexEditor.HexEditor` is a standalone, reusable WPF UserControl targeting `net48` and `net8.0-windows`. It is the core editing engine for the IDE Hex Editor tab, but can be embedded in any WPF application independently.

### Core Editing

| Feature | Status | Notes |
|---------|--------|-------|
| Overwrite mode | ✅ | Standard byte editing |
| Insert mode | ✅ | Fixed (#145) — `PositionMapper.PhysicalToVirtual()` corrected |
| Delete bytes | ✅ | Single and range |
| Append bytes | ✅ | Add at end of file |
| Fill selection with byte/pattern | ✅ | Repeating value fill |
| Unlimited Undo/Redo | ✅ | `EditsManager` — memory-efficient virtual edits |
| Read-only mode | ✅ | `ReadOnly` property |
| Multi-format input (Hex / Dec / Oct / Bin) | ✅ | All numeric bases |
| Multi-byte modes (8 / 16 / 32-bit) | ✅ | Byte, Word, DWord |
| Endianness (Little / Big Endian) | ✅ | Configurable |

### Search & Find

| Feature | Status | Notes |
|---------|--------|-------|
| FindFirst / Next / Last / All | ✅ | All directions |
| Byte array and string search | ✅ | Multiple pattern types |
| Replace First / Next / All | ✅ | Find-and-replace |
| LRU search cache | ✅ | 20-entry cache, O(1) repeat lookup |
| Parallel multi-core search | ✅ | Auto for files > 100 MB |
| SIMD vectorization (AVX2/SSE2) | ✅ | 16–32 bytes per instruction |
| Async search with progress | ✅ | `IProgress<int>` + `CancellationToken` |
| Scrollbar markers for results | ✅ | Bright orange markers |
| Search cache invalidation | ✅ | Fixed at all 11 modification points |

### Display & Visualization

| Feature | Status | Notes |
|---------|--------|-------|
| DrawingContext rendering | ✅ | Custom GPU-accelerated `DrawingVisual` pipeline |
| BarChart byte frequency view | ✅ | Full 0x00–0xFF histogram |
| Scrollbar markers | ✅ | Bookmarks (blue) · Modified (orange) · Search (bright orange) · Added (green) · Deleted (red) |
| Byte grouping (2/4/6/8/16 bytes) | ✅ | Configurable visual grouping |
| Line addressing (Hex / Decimal) | ✅ | Offset display format |
| Show deleted bytes | ✅ | Strikethrough visual diff |
| Mouse hover byte preview | ✅ | Value tooltip on hover |
| Bold SelectionStart indicator | ✅ | Visual emphasis on anchor |
| Dual-color selection | ✅ | Active/inactive panel distinction |
| Font customization | ✅ | Family + size (`Courier New` default) |
| Highlight colors (14 brushes) | ✅ | All fully customizable |

### File Operations

| Feature | Status | Notes |
|---------|--------|-------|
| Open file | ✅ | `OpenFile(path)` |
| Open stream | ✅ | `Stream` property |
| Save | ✅ | Full write-back with change tracking |
| Save As | ✅ | `SaveAs(newPath)` |
| Large file support (GB+) | ✅ | Memory-mapped files |
| Async file operations | ✅ | Non-blocking load/save |
| File locking detection | ✅ | `IsLockedFile` property |

### Character Encoding & TBL

| Feature | Status | Notes |
|---------|--------|-------|
| 20+ built-in encodings | ✅ | ASCII · UTF-8 · UTF-16 · EBCDIC · Shift-JIS · EUC-KR · … |
| Custom `Encoding` property | ✅ | Windows-1252, ISO-8859-1, and any `System.Text.Encoding` |
| TBL file loading | ✅ | `LoadTBLFile(path)` |
| Unicode TBL (DTE/MTE) | ✅ | Multi-byte character support |
| TBL color customization | ✅ | `TbldteColor`, `TblmteColor`, `TblEndBlockColor`, `TblEndLineColor` |
| TBL MTE display toggle | ✅ | `TblShowMte` property |
| ASCII/TBL mode switching | ✅ | `CloseTBL()` |
| TBL string copy mode | ✅ | `CopyPasteMode.TblString` |

### Copy, Paste & Export

| Feature | Status | Notes |
|---------|--------|-------|
| Standard clipboard (Ctrl+C/V/X) | ✅ | Windows clipboard |
| Copy as code — 19 languages | ✅ | C# · VB.NET · Java · Python · C++ · Go · … |
| Multiple formats (Hex / ASCII / Binary) | ✅ | Flexible representation |
| Copy to stream | ✅ | Stream-based export for large selections |
| 7 copy modes | ✅ | HexaString · AsciiString · CSharpCode · TblString · … |
| Paste Insert / Overwrite | ✅ | Configurable paste mode |
| `GetCopyData(start, stop, copyChange)` | ✅ | Programmatic selection extraction |

### Events (21+)

| Event | Description |
|-------|-------------|
| `SelectionChanged` | Selection start/stop/length changed |
| `PositionChanged` | Caret position changed |
| `ByteModified` | Byte modified (with `ByteEventArgs`) |
| `BytesDeleted` | Bytes deleted |
| `DataCopied` | Data copied to clipboard |
| `ChangesSubmited` | Changes saved to file/stream |
| `FileOpened` / `FileClosed` | File lifecycle |
| `Undone` / `Redone` | Undo/Redo executed |
| `UndoCompleted` / `RedoCompleted` | Operation complete |
| `LongProcessProgressChanged` | Progress 0–100% |
| `LongProcessProgressStarted/Completed` | Long op lifecycle |
| `ReplaceByteCompleted` | Replace finished |
| `FillWithByteCompleted` | Fill finished |
| `ByteClick` / `ByteDoubleClick` | Mouse events with position |
| `ZoomScaleChanged` | Zoom level changed |
| `VerticalScrollBarChanged` | Scrollbar position |
| `ReadOnlyChanged` | Read-only mode toggled |

### Keyboard Shortcuts (HexEditor)

| Shortcut | Action |
|----------|--------|
| Ctrl+C/V/X | Copy / Paste / Cut |
| Ctrl+Z / Y | Undo / Redo |
| Ctrl+A | Select all |
| Ctrl+F | Find |
| Ctrl+H | Replace |
| Ctrl+G | Go to offset |
| Ctrl+B | Toggle bookmark |
| Delete / Backspace | Delete byte at / before cursor |
| Arrow keys | Navigate |
| Page Up/Down | Fast scroll |
| Home / End | Line start/end |
| Ctrl+Home / End | File start/end |
| Ctrl+MouseWheel | Zoom in/out |
| ESC | Clear selection / close find panel |

All shortcuts configurable via `AllowBuildin*` properties.

---

## Reusable Controls & Libraries

All controls target `net48` and `net8.0-windows` unless noted.

| Library | Target | Status | Description |
|---------|--------|--------|-------------|
| `WpfHexEditor.HexEditor` | net48 · net8 | ✅ | Full hex editor UserControl |
| `WpfHexEditor.HexBox` | net48 · net8 | ✅ | Standalone hex value input control |
| `WpfHexEditor.ColorPicker` | net48 · net8 | ✅ | RGBA color picker with theme support |
| `WpfHexEditor.BarChart` | net48 · net8 | ✅ | Byte distribution histogram control |
| `WpfHexEditor.Docking.Wpf` | net8 | ✅ | VS-style docking engine (custom, no AvalonDock) |
| `WpfHexEditor.BinaryAnalysis` | net8 | ✅ | 400+ format detection engine |
| `WpfHexEditor.Core.AssemblyAnalysis` | net8 | ✅ | BCL-only .NET assembly analysis (no NuGet) |
| `WpfHexEditor.Core.Terminal` | net8 | ✅ | Shell session management, macro engine |
| `WpfHexEditor.SDK` | net8 | ✅ | Plugin + editor contracts for third-party extensions |
| `WpfHexEditor.Definitions` | net8 | ✅ | Shared types and format definitions |

---

## Performance Architecture

The HexEditor control and binary analysis engine are built around six performance tiers.

| Tier | Technique | Gain |
|------|-----------|------|
| **1 — Rendering** | `DrawingContext` + `DrawingVisual`, GPU-accelerated custom pipeline | **5–10× faster** than a naive WPF layout |
| **2 — Search Cache** | LRU 20-entry cache, O(1) repeat lookup | **10–100× faster** repeated searches |
| **3 — Parallel Search** | Multi-core, auto-enabled > 100 MB | **2–4× faster** |
| **4 — SIMD Vectorization** | AVX2/SSE2, 16–32 bytes/instruction | **4–8× faster** single-byte search |
| **5 — Memory** | `Span<T>` + `ArrayPool<T>`, zero-copy ops | **80–90% less GC pressure** |
| **6 — Position Mapping** | True O(log m) binary search in `PositionMapper` | **100–5,882× faster** for heavily edited files |

**Combined peak:** all six tiers compound — up to **6,000× faster** throughput for large, heavily edited files.

Additional optimizations:
- `Typeface` / glyph-width render cache (static `Dictionary`)
- `BeginBatch` / `EndBatch` bulk update pattern
- `HashSet<long>` for highlights (2–3× faster, 50% less memory than `Dictionary`)
- Memory-mapped files for GB+ binary files
- Profile-Guided Optimization (PGO) + ReadyToRun — .NET 8 only

---

## Developer & SDK

| Feature | Status | Notes |
|---------|--------|-------|
| `IDocumentEditor` plugin contract | ✅ | Implement to create a new editor tab type |
| `IPluginPanel` dockable panel contract | ✅ | VS-Like panel standard |
| `IUIRegistry` | ✅ | Register tabs, panels, status bar segments |
| `IPropertyProvider` | ✅ | Expose properties to the F4 Properties panel |
| `IDiagnosticSource` | ✅ | Push errors/warnings to the Error panel |
| `ITerminalService` | ✅ | Open sessions, send commands from plugins |
| `ToolbarOverflowManager` | ✅ | Drop-in toolbar collapse for any panel |
| Plugin sandboxing (`WpfHexEditor.PluginSandbox`) | ✅ | Isolated plugin execution |
| Plugin priority system | ✅ | Load order and resource scheduling |
| 60+ dependency properties on `HexEditor` | ✅ | Full XAML / data-binding support |
| MVVM-ready `HexEditorViewModel` | ✅ | `INotifyPropertyChanged`, `RelayCommand<T>` |
| Async APIs throughout | ✅ | `IProgress<int>` + `CancellationToken` |
| Localization — 9 languages | ✅ | Runtime language switching, no restart |
| Unit tests (`WpfHexEditor.Tests`) | ✅ | ByteProvider, PositionMapper, BinaryAnalysis |
| BenchmarkDotNet suite | ✅ | Performance regression tracking |

---

## Roadmap Highlights

Major features currently tracked or in active planning.

| Feature | Issue | Priority | Notes |
|---------|-------|----------|-------|
| Code Editor — VS-like full experience | #84 | High | IntelliSense, folding, multi-caret, scripting |
| In-IDE Plugin Development | #138 | High | Write + hot-reload plugins without leaving the IDE |
| Command System | #78 | High | Palette, keyboard bindings, scripted commands |
| Event System | #80 | High | IDE-wide observable event bus for plugins |
| Scripting Host | #79 | High | Embedded Lua / C# scripting in editors |
| Plugin Sandbox isolation | #81 | High | Crash-proof per-plugin process boundary |
| Full method body decompiler | — | Medium | Complete C# decompilation in Assembly Explorer |
| VS `.sln` / `.csproj` import | — | Medium | Read-only MSBuild solution support |
| Global options dialog | — | Medium | Centralized settings with per-plugin pages |
| Split terminal panes | — | Medium | Side-by-side sessions |
| Command palette | — | Medium | Fuzzy-search over all IDE commands |
| Disassembly Viewer (x86/x64) | — | Medium | Full disassembly editor with symbol support |
| Entropy Viewer | — | Medium | Block entropy map with anomaly detection |
| Image Viewer | — | Low | Common image formats (PNG, BMP, DDS, …) |
| Audio Viewer | — | Low | Waveform display for embedded audio assets |
| Tile Editor | — | Low | ROM tile / palette editing |
| Structure Editor | — | Low | Visual binary structure authoring |

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Implemented and tested |
| 🔧 | In development or planned |
| ⚡ | Performance-critical path |

> Features marked 🔧 represent the active development direction. See [ROADMAP.md](ROADMAP.md) for milestone tracking.

---

📖 **See also:** [README](README.md) · [Getting Started](GETTING_STARTED.md) · [Roadmap](ROADMAP.md) · [Contributing](CONTRIBUTING.md)
