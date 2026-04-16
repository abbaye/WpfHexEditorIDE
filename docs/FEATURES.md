# WpfHexEditor — Complete Feature Reference

> **Platform:** Windows · .NET 8.0 · Native WPF
> **Architecture:** VS-style IDE with plugin system, dockable panels, multi-editor workspace
> **Last Updated:** 2026-04-15 (v0.6.4.75)

---

## Table of Contents

- [IDE Shell](#ide-shell)
- [Project System & Build](#project-system--build)
- [Editors](#editors)
- [Shared Undo/Redo Engine](#shared-undoredo-engine)
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
| VS-style docking (float, dock, auto-hide, tab groups) | ✅ | Custom engine `WpfHexEditor.Shell` — zero third-party dependency |
| 16 built-in visual themes | ✅ | Dark · Light · VS2022Dark · DarkGlass · Dracula · Nord · Tokyo Night · Catppuccin Mocha/Latte · Gruvbox Dark · Forest · Matrix · Synthwave 84 · Cyberpunk · High Contrast… |
| Runtime theme switching | ✅ | Live, no restart required |
| Colored tabs with `TabSettingsDialog` | ✅ | Per-tab color + left/right/bottom placement |
| VS2022-style status bar | ✅ | Edit mode · bytes/line · caret offset · plugin personality · editor-contributed items |
| Output panel (multi-channel) | ✅ | General · Build · Debug · PluginSystem channels |
| Error/Diagnostics panel | ✅ | Severity filter, navigate to file/line from any `IDiagnosticSource` |
| Build output panel | ✅ | Real-time streamed build output per project |
| Quick Search bar | ✅ | Unified inline search across all editors |
| Plugin monitor panel | ✅ | Per-plugin CPU %, RAM, load state, priority |
| Plugin manager UI | ✅ | Load/unload/inspect plugins at runtime |
| Toolbar overflow manager | ✅ | All panels collapse toolbar groups on resize |
| Welcome panel | ✅ | VS Start Page with recent files/solutions and CHANGELOG preview |
| Document info bar | ✅ | Orange reload/conflict banner for external file changes |
| Plugin quick-status toast | ✅ | Load/unload notifications, auto-dismiss |
| NuGet Solution Manager | ✅ | Browse/Installed/Consolidate/Updates across all projects |
| Command palette | ✅ | `Ctrl+Shift+P`, 9 search modes, ~100 commands, fuzzy scoring, frequency boost |
| Global options / settings dialog | ✅ | 30+ options pages organized in tree, searchable, plugin-extensible |
| Window menu + Win32 Fullscreen | ✅ | Close/Close All, `F11` fullscreen via Win32 MonitorFromWindow |
| Tab Groups | ✅ | Split horizontal/vertical, move between groups, 16 `TG_*` theme tokens |
| AI Assistant | ✅ | 5 providers (Anthropic/OpenAI/Gemini/Ollama/Claude Code CLI), 25 MCP tools |
| Unit Testing | ✅ | Auto-detect xunit/nunit/mstest, TRX parsing, auto-run on build success |
| Workspace-scoped settings | 🔧 Planned | Per-project overrides for themes, encoding, layout |

### Keyboard Shortcuts (IDE-level)

| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open file in new editor tab |
| Ctrl+S | Save current editor |
| Ctrl+Shift+S | Save all |
| Ctrl+W | Close current tab |
| Ctrl+Tab | Cycle editor tabs |
| Ctrl+G | Go to offset / Go to line |
| F4 | Open Properties panel |
| F7 / F8 | Navigate diff regions |
| Ctrl+`` ` `` | Toggle integrated terminal |

---

## Project System & Build

| Feature | Status | Notes |
|---------|--------|-------|
| Solution management (`.whsln`) | ✅ | Create, open, save, close |
| Project management (`.whproj`) | ✅ | Multiple projects per solution |
| VS `.sln` / `.csproj` / `.vbproj` import | ✅ | `VsSolutionLoaderPlugin` — full open + build |
| Folder mode (VS Code–style) | ✅ | `FolderSolutionLoaderPlugin` — gitignore-aware, 500ms debounce refresh |
| MSBuild integration | ✅ | Build / Rebuild / Clean via `dotnet build` — `Build.MSBuild` plugin |
| Build dependency ordering | ✅ | Kahn's topological sort in `BuildDependencyResolver` |
| Build configuration manager | ✅ | Debug / Release / custom configs + platform selector |
| Startup project selection | ✅ | `StartupProjectsPage` in Solution Properties dialog |
| Build output → Output Panel | ✅ | Real-time line streaming via `BuildOutputAdapter` |
| Build errors → Error Panel | ✅ | `BuildErrorListAdapter` with navigate-to-line |
| Status bar build progress | ✅ | `BuildStatusBarAdapter` updates during build |
| Virtual folders | ✅ | Logical grouping without disk structure |
| Physical folders | ✅ | Mirrors disk directory tree |
| Show All Files mode | ✅ | Reveals untracked files in project directories |
| Per-file state persistence | ✅ | Bookmarks, caret position, scroll, encoding |
| Typed item links | ✅ | e.g. `.bin` linked to `.tbl` → auto-applied on open |
| Format versioning + auto-migration | ✅ | In-memory upgrade on open with automatic backup |
| File templates | ✅ | Binary · TBL · JSON · Text · C# (Console/Library/WPF/ASP.NET) |
| NuGet per-project management | ✅ | Browse/install/update/uninstall per project |
| NuGet Solution Manager | ✅ | Consolidate + upgrade across all projects |
| Solution Property Pages dialog | ✅ | Build dependencies, configurations, source files, startup project |
| External file change detection | ✅ | `FileMonitorService` + 500ms debounce + reload/dismiss banner |
| Git Integration | 🔧 ~40% | UI implemented (changes panel, history, blame gutter, branch picker, stash), not yet integration-tested |

---

## Editors

All editors implement `IDocumentEditor` and integrate with the shared docking, undo/redo engine, status bar, search, and options system.

| Editor | Status | Progress | Key Capabilities |
|--------|--------|----------|-----------------|
| **Hex Editor** | ✅ Active | ~65% | Insert/overwrite, 690+ format detection, SIMD search, TBL, bookmarks, BarChart, scroll markers, block undo/redo |
| **Code Editor** | ✅ Active | ~90% | See detailed table below |
| **XAML Designer** | ✅ Active | ~70% | See detailed table below |
| **Text Editor** | ✅ Active | ~50% | 55+ `.whlang` syntax, multi-encoding, rect selection, drag-to-move, shared UndoEngine |
| **TBL Editor** | ✅ Active | ~60% | Character table editing for custom encodings and ROM hacking, DTE/MTE support |
| **JSON Editor** | ✅ Active | ~55% | Real-time validation, diagnostics, syntax coloring |
| **Script Editor** | 🔧 Active | ~45% | `.hxscript` syntax, run-in-terminal, `HxScriptEngine` backend |
| **Image Viewer** | 🔧 Active | ~30% | Zoom/pan, transform pipeline (rotate/flip/crop/resize), `FileShare.ReadWrite` |
| **Entropy Viewer** | 🔧 Active | ~25% | Block entropy graph, anomaly detection |
| **Diff Viewer** | 🔧 Active | ~35% | Side-by-side binary/text comparison, F7/F8 navigation |
| **Structure Editor** | ✅ Active | ~30% | `.whfmt` binary template — block DataGrid, drag-drop, validation, undo/redo, TestTab, SmartComplete, ForensicPattern |
| **Tile Editor** | 🔧 Active | ~30% | Tile-based ROM/binary asset palette + pixel grid |
| **Disassembly Viewer** | 🔧 Stub | ~5% | x86/x64/ARM planned |
| **Audio Viewer** | 🔧 Stub | ~5% | Waveform display planned |
| **Changeset Editor** | 🔧 Active | ~35% | Edit history and patch management |

### Code Editor — Feature Set

| Feature | Status | Notes |
|---------|--------|-------|
| Multi-language syntax highlighting | ✅ | 55+ `.whlang` definitions — C# · XAML · XML · HTML · CSS · JS · Python · Lua · Rust · Go · Java · … |
| VS-Like navigation bar | ✅ | Dual combo (types / members), Segoe MDL2 icons, caret auto-sync |
| Inline Hints inline hints (VS Code–style) | ✅ | Reference counts inline above methods; per-language gated |
| Ctrl+Click Go-to-Definition | ✅ | Workspace scan + LSP multi-location popup; external symbol decompilation |
| Quick Info hover tooltip | ✅ | `QuickInfoPopup` — 400ms debounce, `IQuickInfoProvider` SDK extension |
| Find / Replace panel | ✅ | Tab/Inline Hints-aware highlight alignment (`ComputeVisualX` + `_lineYLookup`) |
| Code folding — brace + `#region` | ✅ | `BraceFoldingStrategy` + `RegionDirectiveFoldingStrategy` + `CompositeFoldingStrategy` |
| Inline `{…}` badge on collapsed regions | ✅ | Non-destructive — gutter triangle unaffected |
| `#region` colorization | ✅ | Dedicated brush tokens; badges on collapse |
| Gutter: line numbers + fold toggles | ✅ | Fold toggles follow smooth-scroll; hide when opener scrolls above viewport |
| Scroll marker panel | ✅ | `CodeScrollMarkerPanel` — bookmarks · modified · errors · search matches |
| Word-under-caret highlight + scrollbar ticks | ✅ | All occurrences, tick marks in scrollbar |
| Scope guide lines (VS Code–style) | ✅ | Vertical indent guides |
| Bracket / brace highlight | ✅ | `_lineYLookup`-aware Y positioning |
| Rectangular selection (Alt+Click) | ✅ | Column-aligned block; single merged rectangle render |
| Text drag-to-move | ✅ | Inline relocate, recorded as compound undo entry |
| Rectangular block drag-to-move | ✅ | Block moves to target column, preserves surrounding columns |
| Shared `UndoEngine` | ✅ | Coalescing (500ms), transactions, save-point, `Ctrl+Z/Y/Shift+Z` |
| Dynamic undo/redo context menu | ✅ | "Undo (N)" / "Redo (N)" headers |
| URL hover + click | ✅ | Detects URLs in code, opens browser on click |
| Context menu with Segoe MDL2 icons | ✅ | Cut/Copy/Paste/Find/Undo/Redo + Outlining submenu |
| Ctrl+Left/Right word jump | ✅ | Standard VS word-boundary navigation |
| Ctrl+Home / End | ✅ | Document start/end |
| Split view | ✅ | `CodeEditorSplitHost` — side-by-side split pane |
| Find References panel | ✅ | `FindReferencesPanel` + `ReferencesPopup` for multi-location results |
| `IEditorPersistable` state | ✅ | Caret, scroll, language persisted per file |
| `IStatusBarContributor` | ✅ | Language · Line/Col · Encoding · Zoom items |
| `IEditorToolbarContributor` | ✅ | Dynamic toolbar strips (view mode, zoom, layout) |
| Diagnostics integration | ✅ | Gutter error/warning icons, scrollbar markers, Error panel push |
| Multi-caret | ✅ | `Ctrl+Alt+Click` multi-caret, `Ctrl+D` select next occurrence |
| Sticky scroll | ✅ | Scope headers pinned at top, line numbers, click-to-navigate |
| Bracket pair colorization | ✅ | 4 bracket depth levels, `CE_Bracket_1/2/3/4` tokens |
| Color swatch preview | ✅ | Inline 12×12 swatch for CSS/XAML/C# color literals |
| Peek definition | ✅ | `Alt+F12`, inline DrawingVisual overlay |
| Code minimap | ✅ | GlyphRun canvas, diagnostic overlay, viewport slider |
| Gutter change markers | ✅ | 4px strip: added/modified/deleted vs save-point |
| Word wrap | ✅ | Prefix-sum wrap map, H-scrollbar auto-hides |
| LSP inlay hints | ✅ | Parameter name pills, `CE_InlayHint*` tokens |
| LSP declaration hints | ✅ | Run\|Debug above test methods, reference counts |
| Ctrl+Click links & emails | ✅ | Toggleable URL/email Ctrl+Click |
| Virtual scroll >1 GB | 🔧 Planned | Render only visible lines |

### XAML Designer — Feature Set

| Feature | Status | Notes |
|---------|--------|-------|
| Live WPF canvas (XamlReader.Parse) | ✅ | Real-time render on code change |
| Bidirectional canvas↔code sync | ✅ | ~95% fidelity; 150ms debounce; feedback-loop guard |
| Move / resize handles | ✅ | `ResizeAdorner` — 8 Thumb handles |
| Rotation handle | ✅ | Arc handle above selection; patches `RenderTransform` in XAML |
| Parent selection via Escape | ✅ | Hierarchical walk-up (VS-Like) |
| Snap-to-grid + element edge snap | ✅ | `SnapEngineService` + `SnapGuideOverlay` visual guides |
| Multi-select + rubber band | ✅ | `RubberBandAdorner`, `MultiSelectionAdorner` |
| 12 alignment / distribution ops | ✅ | Left/Right/Center-H/Top/Bottom/Center-V + 6 distribute/space ops |
| Property Inspector (F4) | ✅ | DP reflection + custom editors (color, enum, font, numeric, thickness) |
| Toolbox with Drag-and-Drop | ✅ | `ToolboxDropService` — insert element at drop position |
| Design History panel (VS-Like) | ✅ | Undo/redo list with jump-to-state; Single/Batch/Snapshot entries |
| Overkill undo/redo | ✅ | `DesignUndoManager` — max 200 entries, batch grouping, full XAML snapshots |
| Error card overlay | ✅ | Inline error on parse failure, auto-dismisses on fix |
| `#region` colorization | ✅ | Dedicated brush tokens in code pane |
| 4 split layouts | ✅ | Right/Left/Bottom/Top; `Ctrl+Shift+L` cycles; persisted in `EditorConfigDto.Extra` |
| Zoom / pan canvas | ✅ | `ZoomPanCanvas` — Ctrl+Wheel zoom, Shift+Wheel/middle-mouse pan |
| Design-time data (d:DataContext) | ✅ | `DesignTimeDataService` + `DesignDataPanel` |
| XAML Outline panel | ✅ | Element tree, sync with canvas selection |
| Live Visual Tree panel | ✅ | Runtime visual tree from rendered canvas |
| Resource Browser panel | ✅ | StaticResource / DynamicResource catalog |
| Binding Inspector panel | ✅ | Binding expression diagnostics |
| Animation Timeline panel | ✅ | Storyboard keyframe editor |
| 30 XD_* theme tokens × 18 themes | ✅ | All surfaces theme-compliant via `SetResourceReference()` |
| Trigger / animation timeline editor | 🔧 Planned | Beyond stub — Phase 2 |
| Data-binding wizard | 🔧 Planned | Visual binding setup |
| Export as standalone `.xaml` | 🔧 Planned | Phase 2 |

---

## Shared Undo/Redo Engine

`WpfHexEditor.Editor.Core.Undo` — shared across CodeEditor, TextEditor, and XAML Designer.

| Feature | Status | Notes |
|---------|--------|-------|
| `UndoEngine` — unified stack | ✅ | Replaces both editors' custom stacks; `List<IUndoEntry>` + split pointer; max 500 entries |
| Coalescing (`TryMerge`) | ✅ | Consecutive same-type edits merged within 500ms window |
| Transactions | ✅ | `BeginTransaction()` / `CommitTransaction()` → `CompositeUndoEntry`; atomic replay |
| Save-point tracking | ✅ | `MarkSaved()` / `IsAtSavePoint` → drives `IsDirty`; `StateChanged` event → IDE title bar |
| `Ctrl+Shift+Z` redo | ✅ | Added alongside `Ctrl+Y` in CodeEditor and TextEditor |
| Dynamic context menu headers | ✅ | "Undo (N)" / "Redo (N)" operation count |
| `UndoCount` / `RedoCount` on `IDocumentEditor` | ✅ | Default interface members for status bar consumption |
| Design undo: `IDesignUndoEntry` hierarchy | ✅ | Single (attr diff) · Batch (grouped) · Snapshot (full XAML) for XAML Designer |
| Jump-to-state (Design History) | ✅ | `DesignUndoManager` computes undo/redo count to reach any history entry |

---

## Plugins

Plugins are loaded via `WpfHexEditor.PluginHost` with priority-based ordering and optional sandboxing. All plugins conform to the VS-Like dockable panel standard.

### Assembly Explorer (`WpfHexEditor.Plugins.AssemblyExplorer`)

| Feature | Status | Notes |
|---------|--------|-------|
| Open assembly via dialog / drag-drop / Ctrl+V | ✅ | .dll, .exe, .winmd |
| Namespace / type / member tree | ✅ | Classes, interfaces, structs, enums, delegates |
| Method, field, property, event nodes | ✅ | Full member breakdown |
| Colored semantic icons | ✅ | VS Code color palette per node type |
| Lock badge for non-public members | ✅ | Visual access-modifier indicator |
| C# skeleton decompiler | ✅ | `CSharpSkeletonEmitter` — BCL-only, zero NuGet |
| IL text emitter | ✅ | Full ECMA-335 IL via `IlTextEmitter` |
| ILSpy decompiler backend | ✅ | `IlSpyDecompilerBackend`; switchable via options |
| VB.NET decompilation | ✅ | `VbNetDecompilationLanguage` in decompiler registry |
| Decompile cache | ✅ | Keyed by `(filePath, tokenHandle, language)` |
| 4-tab Detail pane (Code / IL / Info / Hex) | ✅ | IL tab auto-selected for method nodes |
| Open in Code Editor | ✅ | Via `IUIRegistry.RegisterDocumentTab` |
| Assembly Diff panel | ✅ | Side-by-side comparison with color-coded changes |
| Assembly Search panel | ✅ | Full-text member search across loaded assemblies |
| CFG Canvas | ✅ | Control-flow graph per method (basic blocks + jump edges) |
| XRef View | ✅ | Cross-references (callers/implementors) per member |
| Live tree filter / search | ✅ | Bottom-up `SetNodeVisibility`, parent auto-expand |
| "Inherits From" group | ✅ | Shows base type and implemented interfaces |
| Framework badge on root nodes | ✅ | `[.NET X.X]` target badge |
| Show non-public / inherited members toggles | ✅ | Options page |
| Recent files list (max 20) | ✅ | Persisted in options |
| Ctrl+Click external symbol decompilation | ✅ | `FindAssemblyPath` (AppDomain → runtime → NuGet cache) → read-only tab |
| Full method body decompilation | 🔧 Planned | Complete C# output via ILSpy |
| Cross-assembly reference navigation | 🔧 Planned | Jump-to-definition across loaded assemblies |

### Synalysis Grammar (`WpfHexEditor.Plugins.SynalysisGrammar`) — issue #177

| Feature | Status | Notes |
|---------|--------|-------|
| UFWB (Synalysis/Hexinator) grammar parsing | ✅ | XML-based `.grammar` format |
| 10+ embedded grammars | ✅ | Auto-loaded from `WpfHexEditor.Definitions` |
| Plugin-contributed grammars | ✅ | `IGrammarProvider` SDK extension point |
| Colored hex overlay (CustomBackgroundBlock) | ✅ | `SynalysisToBackgroundBlockBridge` |
| Parsed Fields panel population | ✅ | `SynalysisToFieldViewModelBridge` |
| Auto-apply on file open / editor switch | ✅ | Configurable |
| Grammar Selector dockable panel | ✅ | Right side, 340px |
| `GrammarAppliedEvent` (plugin event bus) | ✅ | Consumed by Parsed Fields panel |
| Options page | ✅ | Auto-apply toggle, max depth, color scheme |

### Data Inspector (`WpfHexEditor.Plugins.DataInspector`)

| Feature | Status | Notes |
|---------|--------|-------|
| 40+ byte type interpretations at caret | ✅ | Int8/16/32/64, Float, Double, GUID, Dates, Flags, … |
| Scope: Caret / Selection / Active View / Whole File | ✅ | Switchable from toolbar |
| Byte distribution BarChart | ✅ | Full 0x00–0xFF histogram |
| Endianness toggle | ✅ | Little / Big Endian |
| Toolbar overflow (5 groups) | ✅ | Collapsible groups |

### Parsed Fields (`WpfHexEditor.Plugins.ParsedFields`)

| Feature | Status | Notes |
|---------|--------|-------|
| 690+ binary format detection | ✅ | PE, ELF, ZIP, PNG, MP3, SQLite, PDF, and 680+ more |
| Field list with type and offset | ✅ | Hierarchical field tree |
| Inline field value editing | ✅ | Edit parsed values directly |
| Type overlay on hex grid | ✅ | Visual highlight per field |
| Export fields | ✅ | Toolbar export action |

### Structure Overlay (`WpfHexEditor.Plugins.StructureOverlay`)

| Feature | Status | Notes |
|---------|--------|-------|
| Visual field highlighting on hex grid | ✅ | Color-coded regions |
| Add overlay manually | ✅ | Via toolbar |
| Overlay from parsed format | ✅ | Auto-generated from Parsed Fields |

### Pattern Analysis (`WpfHexEditor.Plugins.PatternAnalysis`)

| Feature | Status | Notes |
|---------|--------|-------|
| Byte pattern detection | ✅ | Statistical analysis of byte sequences |
| On-demand refresh | ✅ | Toolbar trigger |

### File Statistics (`WpfHexEditor.Plugins.FileStatistics`)

| Feature | Status | Notes |
|---------|--------|-------|
| Byte frequency histogram | ✅ | Full 0x00–0xFF distribution |
| Shannon entropy per block | ✅ | Block-level entropy |
| Null / printable / high byte ratios | ✅ | Summary statistics |

### File Comparison (`WpfHexEditor.Plugins.FileComparison`)

| Feature | Status | Notes |
|---------|--------|-------|
| Binary file diff | ✅ | Byte-level comparison |
| SIMD-accelerated comparison | ✅ | Basic / Parallel / SIMD variants |
| Similarity percentage | ✅ | `CalculateSimilarity()` 0–100% |
| Difference count | ✅ | `CountDifferences()` with SIMD |

### Archive Structure (`WpfHexEditor.Plugins.ArchiveStructure`)

| Feature | Status | Notes |
|---------|--------|-------|
| Archive format tree | ✅ | ZIP, RAR, 7z, CAB structural view |
| Entry navigation | ✅ | Jump to entry offset in hex editor |

### Format Info (`WpfHexEditor.Plugins.FormatInfo`)

| Feature | Status | Notes |
|---------|--------|-------|
| Detected format metadata | ✅ | MIME type, version, encoding info |
| Format confidence score | ✅ | Detection certainty indicator |

### Custom Parser Template (`WpfHexEditor.Plugins.CustomParserTemplate`)

| Feature | Status | Notes |
|---------|--------|-------|
| User-defined field parser | 🔧 In dev | Template-based binary parsing |
| Script-driven field definitions | 🔧 In dev | Extensible format description |
| Visual template designer | 🔧 Planned | Drag-and-drop field layout editor |

### Solution Loaders

| Plugin | Load Priority | Purpose |
|--------|-------------|---------|
| `SolutionLoader.WH` | 95 | Native `.whsln` / `.whproj` files |
| `SolutionLoader.VS` | 90 | Visual Studio `.sln` / `.csproj` / `.vbproj` |
| `SolutionLoader.Folder` | 85 | Any directory — VS Code-style, gitignore-aware |

### Build Adapter

| Plugin | Load Priority | Purpose |
|--------|-------------|---------|
| `Build.MSBuild` | 85 | `dotnet build` CLI — no `Microsoft.Build.*` in-process |

---

## IDE Panels

Built-in panels shipping with `WpfHexEditor.Panels.IDE`. All follow the VS-Like dockable panel standard with VS-style toolbar, drag/float/dock/tab groups.

| Panel | Status | Description |
|-------|--------|-------------|
| Solution Explorer | ✅ | Project tree with virtual/physical folders, file operations, context menus |
| Properties Panel | ✅ | Context-aware F4 panel via `IPropertyProvider` |
| Error/Diagnostics Panel | ✅ | Severity filter, navigate to file+line from any `IDiagnosticSource` |
| File Diff Panel | ✅ | Side-by-side binary comparison, F7/F8 navigation |
| Plugin Monitor Panel | ✅ | Per-plugin CPU %, RAM, load state, execution metrics |
| Plugin Manager | ✅ | Load/unload/inspect plugins, version and priority info |
| Output Panel | ✅ | Multi-channel log (General · Build · Debug · PluginSystem) |
| Build Output | ✅ | Real-time build output with severity coloring |
| NuGet Solution Manager | ✅ | Browse/Installed/Consolidate/Updates across all VS projects |
| Quick Search | ✅ | Unified inline + advanced search bar across all editors |
| Breakpoint Explorer | ✅ | All breakpoints across solution with conditions, hit counts, jump-to-source |
| Call Hierarchy | ✅ | LSP-powered call chain navigator (`Shift+Alt+H`) |
| Type Hierarchy | ✅ | LSP-powered inheritance viewer (`Ctrl+Alt+F12`) |
| Document Structure | ✅ | 8 providers (LSP/JSON/XML/Markdown/INI/Binary/Folding/Outline) |
| WhfmtBrowser | ✅ | Browse 690+ embedded format definitions by category |
| WhfmtCatalog | ✅ | Detailed format inspector with JSON preview |
| AI Assistant | ✅ | Multi-provider AI chat with 25 MCP tools, streaming, inline apply |
| Unit Testing | ✅ | Test runner (xunit/nunit/mstest), TRX parsing, auto-run on build |

---

## Integrated Terminal

Multi-tab terminal panel (`WpfHexEditor.Terminal`) with macro recording and shell session management.

| Feature | Status | Notes |
|---------|--------|-------|
| Multi-tab shell sessions | ✅ | Unlimited tabs, each with independent process |
| Shell types: HxTerminal / PowerShell / Bash / CMD | ✅ | Per-session shell selection |
| New session via "+" menu | ✅ | Choose shell type on creation |
| Close session (last tab protected) | ✅ | Cannot close the last remaining tab |
| Session command history | ✅ | Per-session history with Up/Down navigation |
| Macro recording | ✅ | `record start` / `record stop` / `record save <path>` |
| Macro replay | ✅ | `replay-history [N]` command |
| 31 built-in commands | ✅ | File ops, panel management, format commands |
| Ctrl+L to clear | ✅ | Keyboard shortcut |
| Toolbar overflow (5 collapsible groups) | ✅ | Scroll nav · history · filters · recording · save |
| Theme compliance | ✅ | Follows global IDE theme |
| Save session output | 🔧 Planned | Export full session transcript to file |
| Split terminal panes | 🔧 Planned | Side-by-side sessions in the same panel |
| Environment variable editor | 🔧 Planned | Per-session environment configuration |

---

## HexEditor Control

`WpfHexEditor.HexEditor` — standalone, reusable WPF UserControl targeting `net48` and `net8.0-windows`. Embeddable independently of the IDE.

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
| External file change detection | ✅ | `FileSystemWatcher` + 500ms debounce; auto-reload or status warning |

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
| Custom `Encoding` property | ✅ | Windows-1252, ISO-8859-1, any `System.Text.Encoding` |
| TBL file loading | ✅ | `LoadTBLFile(path)` |
| Unicode TBL (DTE/MTE) | ✅ | Multi-byte character support |
| TBL color customization | ✅ | `TbldteColor`, `TblmteColor`, `TblEndBlockColor`, `TblEndLineColor` |
| TBL MTE display toggle | ✅ | `TblShowMte` property |

### Copy, Paste & Export

| Feature | Status | Notes |
|---------|--------|-------|
| Standard clipboard (Ctrl+C/V/X) | ✅ | Windows clipboard |
| Copy as code — 19 languages | ✅ | C# · VB.NET · Java · Python · C++ · Go · … |
| 7 copy modes | ✅ | HexaString · AsciiString · CSharpCode · TblString · … |
| Paste Insert / Overwrite | ✅ | Configurable paste mode |

### Events (21+)

| Event | Description |
|-------|-------------|
| `SelectionChanged` | Selection start/stop/length changed |
| `PositionChanged` | Caret position changed |
| `ByteModified` | Byte modified |
| `BytesDeleted` | Bytes deleted |
| `DataCopied` | Data copied to clipboard |
| `ChangesSubmited` | Changes saved to file/stream |
| `FileOpened` / `FileClosed` | File lifecycle |
| `Undone` / `Redone` | Undo/Redo executed |
| `LongProcessProgressChanged` | Progress 0–100% |
| `ZoomScaleChanged` | Zoom level changed |
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

---

## Reusable Controls & Libraries

| Library | Target | Status | Description |
|---------|--------|--------|-------------|
| `WpfHexEditor.HexEditor` | net48 · net8 | ✅ | Full hex editor UserControl |
| `WpfHexEditor.HexBox` | net48 · net8 | ✅ | Standalone hex value input control |
| `WpfHexEditor.ColorPicker` | net48 · net8 | ✅ | RGBA color picker with theme support |
| `WpfHexEditor.BarChart` | net48 · net8 | ✅ | Byte distribution histogram control |
| `WpfHexEditor.Shell` | net8 | ✅ | VS-style docking engine (custom, zero AvalonDock) — renamed from `Docking.Wpf` in v0.6.0 |
| `WpfHexEditor.BinaryAnalysis` | net8 | ✅ | 690+ format detection engine |
| `WpfHexEditor.Core.AssemblyAnalysis` | net8 | ✅ | BCL-only .NET assembly analysis (no NuGet) |
| `WpfHexEditor.Core.SourceAnalysis` | net8 | ✅ | BCL-only regex outline engine for `.cs` / `.xaml` |
| `WpfHexEditor.Core.Terminal` | net8 | ✅ | Shell session management, macro engine |
| `WpfHexEditor.LSP` | net8 | ✅ | Language intelligence — lexer, symbols, SmartComplete, refactoring |
| `WpfHexEditor.BuildSystem` | net8 | ✅ | Build orchestration engine + `IBuildAdapter` contracts |
| `WpfHexEditor.Events` | net8 | ✅ | IDE-wide event bus contracts + all domain event records |
| `WpfHexEditor.SDK` | net8 | ✅ | Plugin + editor contracts for third-party extensions |
| `WpfHexEditor.Core.Definitions` | net8.0 | ✅ | 690+ `.whfmt` definitions, 35 syntax grammars; published as `whfmt.FileFormatCatalog` NuGet (cross-platform) |
| `WpfHexEditor.Core.Contracts` | net8.0 | ✅ | `IEmbeddedFormatCatalog`, `EmbeddedFormatEntry`, `FormatCategory`, `SchemaName` — cross-platform |

---

## Performance Architecture

The HexEditor control and binary analysis engine are built around six performance tiers.

| Tier | Technique | Gain |
|------|-----------|------|
| **1 — Rendering** | `DrawingContext` + `DrawingVisual`, GPU-accelerated custom pipeline | **5–10× faster** than naive WPF layout |
| **2 — Search Cache** | LRU 20-entry cache, O(1) repeat lookup | **10–100× faster** repeated searches |
| **3 — Parallel Search** | Multi-core, auto-enabled > 100 MB | **2–4× faster** |
| **4 — SIMD Vectorization** | AVX2/SSE2, 16–32 bytes/instruction | **4–8× faster** single-byte search |
| **5 — Memory** | `Span<T>` + `ArrayPool<T>`, zero-copy ops | **80–90% less GC pressure** |
| **6 — Position Mapping** | True O(log m) binary search in `PositionMapper` | **100–5,882× faster** for heavily edited files |

**Combined peak:** all six tiers compound — up to **6,000× faster** throughput for large, heavily edited files.

Additional optimizations:
- `Typeface` / glyph-width render cache (static `Dictionary`)
- `BeginBatch` / `EndBatch` bulk update pattern
- `HashSet<long>` for highlights (2–3× faster, 50% less memory)
- Memory-mapped files for GB+ binary files
- Profile-Guided Optimization (PGO) + ReadyToRun (.NET 8 only)

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
| `IEditorToolbarContributor` | ✅ | Contribute toolbar strips to any editor |
| `IStatusBarContributor` | ✅ | Add/update status bar items per editor |
| `IQuickInfoProvider` | ✅ | SDK extension point for hover Quick Info tooltips |
| `IGrammarProvider` | ✅ | Contribute Synalysis grammar files from plugins |
| `ISourceOutlineService` | ✅ | BCL-only outline engine for navigation bar |
| `IBuildAdapter` | ✅ | Pluggable build backend (MSBuild, Gradle, …) |
| `ISolutionLoader` | ✅ | Pluggable solution format (WH, VS, Folder, …) |
| `IIDEEventBus` | ✅ | IDE-wide publish/subscribe event bus |
| Plugin sandboxing (`WpfHexEditor.PluginSandbox`) | ✅ | Isolated out-of-process plugin execution |
| Plugin priority system | ✅ | Load order and resource scheduling |
| `ToolbarOverflowManager` | ✅ | Drop-in toolbar collapse for any panel |
| 60+ dependency properties on `HexEditor` | ✅ | Full XAML / data-binding support |
| MVVM-ready `HexEditorViewModel` | ✅ | `INotifyPropertyChanged`, `RelayCommand<T>` |
| Async APIs throughout | ✅ | `IProgress<int>` + `CancellationToken` |
| Localization — 9 languages | ✅ | Runtime language switching, no restart |
| Unit tests (`WpfHexEditor.Tests`) | ✅ | ByteProvider, PositionMapper, BinaryAnalysis |
| BenchmarkDotNet suite | ✅ | Performance regression tracking |

---

## Roadmap Highlights

| Feature | Issue | Status | Notes |
|---------|-------|--------|-------|
| Structure Editor Phase 2 | #172 | 🔧 In Progress | Live binary sync, complex types, template validation |
| Code Editor — inline value hints | #84 | 🔧 In Progress | Debug-session variable overlay |
| .NET Debugger — runtime attach | #44 | 🔧 In Progress | UI complete, runtime launch pending |
| Git Integration — integration testing | #91 | 🔧 ~40% | UI done, real-repo operations unverified |
| XAML Designer Phase 2 | #155 | 🔧 Planned | Trigger/animation timeline, data-binding wizard |
| In-IDE Plugin Development | #138 | 🔧 Planned | Write + hot-reload plugins without leaving the IDE |
| Virtual scroll >1 GB | #97 | 🔧 Planned | Render-only visible lines in Code/Text editors |
| Plugin Marketplace | #41 | 🔧 Planned | Online registry, signed packages, auto-update |
| Plugin Sandbox gRPC migration | #81 | 🔧 Planned | Replace Named Pipe IPC with gRPC transport |

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Implemented and shipped |
| 🔧 | In development or planned |
| ⚡ | Performance-critical path |

> See [ROADMAP.md](ROADMAP.md) for milestone tracking and [CHANGELOG.md](CHANGELOG.md) for full version history.

---

📖 **See also:** [README](../README.md) · [Getting Started](GETTING_STARTED.md) · [Roadmap](ROADMAP.md) · [Contributing](../.github/CONTRIBUTING.md)
