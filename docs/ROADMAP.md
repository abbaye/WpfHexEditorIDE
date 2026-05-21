# 🗺️ WpfHexEditor — Roadmap

This document tracks all planned and in-progress features for the WpfHexEditor IDE.
Features already shipped are in [CHANGELOG.md](CHANGELOG.md).

> **Legend:** 🔧 In Progress · 🔜 Planned · ✅ Done (see CHANGELOG)
>
> 📅 *Last revised: 2026-05-20*
>
> **Current IDE build:** `0.6.5.96` — DiffViewer Phase 2 complete (word-level diff, .patch export, 3-way merge), ClassDiagram incremental Roslyn live-sync, Entropy Heatmap viewport rendering.

---

## 🔧 In Progress

| Feature # | Title | Description | Progress |
|-----------|-------|-------------|----------|
| #100 | **IDE Localization** | 27 languages wired — 77.9% DynamicResource coverage across all panels, menus, context menus, dialogs, and toolbar buttons. Per-assembly `LocalizedResourceDictionary` pattern. Remaining: code-behind-set strings (proper nouns, technical configs, icon glyphs intentionally left in English). | ~78% |
| #81 | **Plugin Sandbox** | Out-of-process isolation via HWND embedding + full IPC bridge (menus, toolbar, events). HWND parenting, Job Object resource control, IPC HexEditor event bridge done. Auto-isolation engine done. IDE EventBus IPC bridge done. Plugin signing + signature validation done (ADR-SB-01). Remaining: gRPC migration, hot-reload from sandbox. | ~40% |
| #84 | **Code Editor — VS-Like Advanced** | Full feature set shipped: navigation bar, 57+ language definitions (incl. F# + VB.NET), URL hover/click, find/replace, split view, `IEditorPersistable`, Ctrl+Click cross-file nav, LSP multi-location popup, Alt+Click rect selection, drag-to-move, `#region` colorization, shared `UndoEngine`, data-driven folding (4 strategies), comment-aware brace matching, 790+ `.whfmt` definitions, gutter diagnostics, multi-caret, diagnostics integration, full LSP suite, word wrap, **auto-close brackets/quotes + skip-over + wrap-selection** (#163 ✅), **end-of-block hover hint** ✅, **word highlight** ✅, **sticky scroll with line numbers** (#160 ✅), **Find All References + dockable panel** (#157 ✅), **bracket pair colorization** (#162 ✅), **color swatch preview** (#168 ✅), **format-on-save** (#159 partial ✅), **Ctrl+Click links & emails** (ADR-CTRL-LINK-01 ✅ v0.6.4.6), **Roslyn semantic inline hints upgrade** (ADR-ROSLYN-INLINEHINTS-01 ✅ v0.6.4.6 — `IReferenceCountProvider`, whfmt-driven `CanProvide`), **full LSP code formatting** (`CodeFormattingService` + `CodeFormatter` + `RoslynFormattingProvider` ✅), **code minimap** (`MinimapControl` ✅ #161), **smart indentation** (`SmartIndentService` ✅ #164), **column ruler guides** (`CE_RulerBrush` + options wired ✅ #165), **expand/collapse all folds** (`FoldingEngine.CollapseAll/ExpandAll` ✅ #167), **inline gutter change markers** (`ChangeMarkerGutterControl` + `GutterChangeTracker` ✅ #166 — `CE_GutterAdded/Modified/Deleted` × 18 themes, save-point hash diff), **peek definition** (#158 ✅ — `InlinePeekHost`, `Alt+F12`), **LSP inlay hints layer** (`LspInlayHintsLayer` ✅ — parameter name pills, `CE_InlayHint*` tokens), **LSP declaration hints layer** (`LspDeclarationHintsLayer` ✅ — Run|Debug above test methods, reference counts), **incremental document sync** (`IIncrementalSyncClient` ✅ — `TextDocumentSyncKind=2`). Remaining: inline value hints (debug-session variable overlay, via #85 LSP). | ~90% |
| #101–103 | **MSBuild & VS Solution Support** | Open `.sln` files; build/rebuild/clean via MSBuild API; output routed to Build channel; severity coloring; auto-focus; project templates; error list navigation; incremental build with dirty tracking (`IIncrementalBuildTracker`, FSW per project, `BuildDirtyAsync()`); orange dirty dot in Solution Explorer (`Ctrl+Alt+F7`); **parallel project builds** (`SemaphoreSlim`-gated `Task.WhenAll`, `MaxParallelProjects` from `AppSettings`, `Interlocked`-safe counters); **build progress bar** in status bar ✅; **VB.NET project template write** (`VbNetConsoleAppTemplate` ✅). Remaining: VB.NET item group editing, nested solution folders. | ~70% |
| #104–106 | **Assembly Explorer + Decompilation** | .NET PE tree, C# decompilation → `CodeEditorSplitHost` (syntax-highlighted, read-only) ✅, ILSpy backend, VB.NET decompilation ✅, CFG Canvas, Assembly Diff, Assembly Search, XRef View, Decompile Cache, Ctrl+Click external symbol decompilation via `FindAssemblyPath` (AppDomain + runtime + NuGet) + `CSharpSkeletonEmitter` pipeline; **ECMA-335 token→offset** (`PeOffsetResolver` ✅); **hex sync** (`AssemblyHexSyncService` ✅); blank-panel dual-cache bug fixed ✅. Remaining: plugin panel improvements, PDB source-link matching. | ~60% |
| #107 | **Document Model** | `IDocumentBuffer` / `DocumentBuffer` (thread-safe, Dispatcher-marshalled); `IBufferAwareEditor` implemented by `CodeEditor`, `TextEditor`, `MarkdownEditorHost`, `CodeEditorSplitHost`, `XamlDesignerSplitHost`, **`HexEditor`** (`HexEditor.BufferAware.cs` partial — `ByteModified` debounce 300ms, `OpenStream(MemoryStream)` reload, 10MB cap); `DocumentManager` buffer lifecycle; `LspBufferBridge` (300 ms debounce → `DidChange`); `LspDocumentBridgeService`; **HexEditor block undo/redo** (ADR-UNDO-01 ✅ — paste/cut/delete as single undo step, `UndoGroup` composite, coalescence, VS-style history dropdown); **HexEditor ↔ CodeEditor shared `UndoEngine`** ✅ v0.6.4.10 (`IUndoAwareEditor`, `HexByteUndoEntry`, `DocumentBuffer` undo wiring). Remaining: multi-editor collaboration. | ~50% |
| #85–86 | **LSP Engine / SmartComplete** | Full JSON-RPC LSP client (`LspClientImpl`); `ServerCapabilities` parse; 10 LSP providers: completion (LSP-first + local fallback), hover, signature help, code actions (`Ctrl+.`), rename (`F2`), inlay hints, code lens, semantic tokens, breadcrumb bar, workspace symbols (`Ctrl+T`); `LspDocumentSync` (DidOpen/DidChange/DidClose); `LspStatusBarAdapter`; `LspServersOptionsPage`; F# (`fsautocomplete`) + VB.NET server entries ✅ (Roslyn in-process for C#/VB.NET — ADR-ROSLYN-01); 30 new tokens × 18 themes; **call hierarchy** (`LspCallHierarchyProvider` + `CallHierarchyPanel` ✅ Shift+Alt+H); **linked editing** (`LspLinkedEditingProvider` ✅), **incremental document sync** (`IIncrementalSyncClient`, `TextDocumentSyncKind=2` ✅ Phase 3), **pull-diagnostics interface** (`IDiagnosticsModeClient`, `UsesPullDiagnostics` ✅ Phase 3), **LSP inlay hints** (`LspInlayHintsLayer` ✅), **LSP declaration hints** (`LspDeclarationHintsLayer` ✅). Remaining: inline value hints. | ~70% |
| #172 | **Structure Editor** | Visual `.whfmt` binary template editor — block DataGrid with field types/offsets, drag-drop block reordering, `Ctrl+F` search, validation pipeline, undo/redo, `StructurePopToolbar`, `BlockTypeBadge`, `LiveWhfmtBuffer`, `VariablesTab` redesign, `TestTab` with live binary preview, variable cross-reference validation, expression `SmartComplete`, options page, `SimpleBlockInterpreter`, `ForensicPattern` tolerant converter. Remaining: live binary preview sync, complex type support (arrays, unions, conditionals, bitfields), template validation engine, import/export template packages. | ~30% |
| #156 | **Class Diagram Plugin** | 🔧 **~85% Done** — DSL v2 + syntax-highlighted pane (`classdiagram.whfmt` + `CodeEditorSplitHost`), 3 layout strategies (Force-Directed / Hierarchical / Swimlane), canvas minimap drag + corner-snap + hide, left-panel TreeView with colored selectable members, collapsible sections + dual metrics badge, hover tooltips (400 ms), context menu (ZoomToRect, clipboard export), scrollbars, session state save/restore; live arrow redraw during drag ✅ v0.6.5.95; round-trip Roslyn sync phases 1A–7 done (ADR-021–036); SVG exporter (`ClassDiagramSvgExporter`) ✅; **PNG export** (`ClassDiagramExportService.ExportPngAsync` ✅); **incremental Roslyn live-sync** (`AnalyzeFilesIncremental` + `_pendingChanges` bag — only changed files re-parsed, positions preserved ✅ v0.6.5.96). Remaining: VB.NET partial class merging in incremental path. | ~85% |

---

## 🔜 Planned — IDE Core & Build

| Feature # | Title | Description |
|-----------|-------|-------------|
| #36 | **Service Container / DI** | ✅ **Done** — `IServiceContainer` SDK contract + `ServiceContainerAdapter`; `AppServiceCollection` (MEF → `Microsoft.Extensions.DependencyInjection 8.0`); Singleton/Scoped/Transient lifecycle. |
| #37 | **Global CommandBus** | ✅ **Done** — `CommandBus` / `ICommandBus`; all IDE actions routed; `CommandRegistry` (~45 commands). |
| #38 | **Keyboard Shortcuts & Bindings** | ✅ **Done** — `KeyBindingService` / `IKeyBindingService`; configurable gestures; `KeyboardShortcutsPage`; conflict detection; plugin-extensible. |
| #39 | **User Preferences Persistence** | `ConfigurationManager`, per-section schemas, plugin config API, export/import, cross-session persistence. |
| #40 | **Centralized Logging & Diagnostics** | `LogService` (Info/Warning/Error/Debug), `DiagnosticService` (perf metrics), `LogSink` abstraction, Output + Error Panel integration. |
| #77 | **Workspace System & State Serialization** | Plugin lazy loading fully done (ADR-LAZY-01 ✅ v0.6.4.6 — manifest stubs, single-click activation, panel persistence across restarts). Multi-workspace management with project-specific settings — remaining. |
| #78 | **Command System (Internal IDE Commands)** | ✅ **Done** — `WpfHexEditor.Commands` project; central command registry; `KeyBindingService`; `KeyboardShortcutsPage`; TitleBar launcher. |
| #79 | **Scripting / Automation Engine** | ✅ **Done v0.6.5.95** — Dockable Roslyn scripting console; async execution; output history; `ScriptingModule`; `.hxscript` automation via terminal. |
| #82 | **Service Registry & Dependency Injection** | ✅ **Done** — `IServiceContainer` SDK + `ServiceContainerAdapter`; central DI container exposing all IDE services to plugins via `IServiceRegistry`. |
| #83 | **Options Document / IDE Settings** | Unified options panel for editor behavior, themes, plugins, and workspace config with live preview. |
| #87 | **Workspace Templates** | ✅ **Done v0.5.2** — `TemplateManager`, `ProjectScaffolder`, 3 built-in JSON templates, `NewProjectDialog`. |
| #100 | **IDE Localization Engine** | *(In Progress ~78% — see above)* |
| #101 | **`.sln` Parser** | *(In Progress — see above)* Remaining: nested solution folders, shared projects, full project graph. |
| #102 | **C# / VB.NET Project Support** | *(In Progress)* `.csproj` parsing via MSBuild Locator. Remaining: VB.NET write support, item group editing. |
| #103 | **MSBuild API Integration** | *(In Progress — see above)* Build/rebuild/clean done. Parallel project builds ✅. Incremental builds ✅. Remaining: error list navigation with file/line jump. |

---

## 🔜 Planned — Ultimate IDE Architecture (VS-Level)

| Feature # | Title | Description | Remarque |
|-----------|-------|-------------|----------|
| #153 | **Visual Scripting / Graph View** | Node-based visual representation for binary structures, memory flow, or data pipelines; plugin-extensible graphs for analysis and automation. | New concept, not yet in the roadmap |
| #154 | **Cross-Platform Ready Core** | WPF/.NET 8 foundation; potential interop with WinForms; future-proof design for possible Linux/macOS port with Avalonia/MAUI. `WpfHexEditor.Core.Contracts` + `whfmt.FileFormatCatalog` already target pure `net8.0` — first cross-platform packages. | New concept, partially started |

---

## 🔜 Planned — Editors & Code Intelligence

| Feature # | Title | Description |
|-----------|-------|-------------|
| #84 | **Code Editor — VS-Like Advanced** | *(In Progress ~90% — see above)* |
| #85 | **LSP Engine** | *(In Progress ~70% — see above)* Remaining: inline value hints, LSP 3.18 pull-diagnostics (`textDocument/diagnostic`). Note: Roslyn replaces OmniSharp for C#/VB.NET (ADR-ROSLYN-01). |
| #86 | **SmartComplete v4 (LSP)** | Advanced autocomplete, signature help, quick-info, multi-caret editing, virtual scroll for >1 GB files. |
| #88 | **Dynamic Snippets** | ✅ **Done v0.6.5.95** — `SnippetsManager`, context-aware snippets, dynamic variables; user/plugin/language-scoped; visual in-app snippet editor; default pack + user store. |
| #89 | **AI-Assisted Code Suggestions** | ✅ **Done v0.6.4.3** — AI Assistant plugin with 5 providers, 25 MCP tools, streaming chat, inline apply. Remaining: `AICompletionEngine` (inline ghost-text completions), `AIRefactoringAssistant`, plugin-extensible AI rules. |
| #94 | **Advanced Refactoring** | Rename symbol (workspace-wide), extract method/class, inline variable, move file between projects; AI-assisted suggestions. |
| #96 | **Code Analysis & Metrics** | Cyclomatic complexity, code duplication detection, dependency graphs; dedicated panel with filter/sort. |
| #106 | **.NET Decompilation via ILSpy** | *(In Progress — see #104–106 above)* C# skeleton view + full IL disassembly per method; "Go to Metadata Token" navigation. |
| #157 | **Go-to-References / Find All References** | ✅ **Done v0.6.3.7** — `Shift+F12`; `WorkspaceFileCache`; `InlineHintsService`; dockable `FindReferencesPanel`; `F8`/`Shift+F8` navigation. |
| #158 | **Peek Definition** | ✅ **Done** — `Alt+F12`; `InlinePeekHost`; resize handle; keyboard nav; Esc to close. |
| #159 | **Code Formatting** | ✅ **Done** — Format-on-save toggle; full LSP `textDocument/formatting`; `RoslynFormattingProvider`; `Ctrl+K Ctrl+D`. |
| #160 | **Sticky Scroll** | ✅ **Done v0.6.3.7** — Scope header pinned at top; line numbers in gutter; syntax-highlighted rows; 6 options; click to navigate. |
| #161 | **Code Minimap** | ✅ **Done** — `MinimapControl` (GlyphRun canvas, click-to-scroll, viewport slider, diagnostic overlay, context menu). |
| #162 | **Bracket Pair Colorization** | ✅ **Done v0.6.4.2** — `BracketPairColorizationEnabled` DP; `BracketDepthColorizer`; `CE_Bracket_1/2/3/4` tokens × 18 themes. |
| #163 | **Auto-close Brackets & Quotes** | ✅ **Done v0.6.3.7** — Auto-insert matching pairs; skip-over; wrap selection; 4 options. |
| #164 | **Smart Indentation** | ✅ **Done** — `SmartIndentService` (3 strategies); respects `.whfmt` indent rules. |
| #165 | **Column Ruler Guides** | ✅ **Done** — Configurable vertical ruler lines; `CE_RulerBrush` token; per-language defaults via `.whfmt`. |
| #166 | **Inline Gutter Change Markers** | ✅ **Done** — `ChangeMarkerGutterControl`; `GutterChangeTracker`; `CE_GutterAdded/Modified/Deleted` × 18 themes. |
| #167 | **Expand / Collapse All Folds** | ✅ **Done** — `FoldingEngine.CollapseAll/ExpandAll`; `Ctrl+M` shortcuts. |
| #168 | **Color Swatch Inline Preview** | ✅ **Done v0.6.4.2** — Detects CSS/XAML/C# color literals; renders 12×12 inline swatch. |
| #169 | **TextEditor Phase 2** | ✅ **Done** — Multi-caret (`Ctrl+Alt+Click`, `Ctrl+D`); column/block select (Shift+Alt+drag); LSP integration; advanced encoding auto-detection; text statistics panel. |
| #170 | **ImageViewer Phase 2** | ✅ **Done** — `ImageViewerFactory` + full viewer; inline color picker; histogram panel; batch export; pixel inspector overlay; animated GIF frame viewer. |
| #171 | **DiffViewer Phase 2** | ✅ **Done** — `DiffViewerFactory`; `DiffViewerViewModel`; side-by-side + unified diff view; chunk-level accept/reject; **word-level diff** (`MyersDiffAlgorithm.WordEdits`, inline `Run` highlights ✅ v0.6.5.96); **`.patch` export** (`DiffExportService.ExportUnifiedPatch`, `SaveFileDialog` ✅ v0.6.5.96); **3-way merge** (`ThreeWayMergeEngine`, `MergeConflict`, 3-column viewer, accept ours/theirs/both, save merged output ✅ v0.6.5.96). |
| #172 | **StructureEditor Phase 2** | *(In Progress ~30% — see above)* |
| #173 | **EntropyViewer Phase 2** | 🔧 **Partial** — `EntropyViewer` project exists with controls. Remaining: named section labels (overlay on chart), chart export (PNG/SVG), multi-file overlay comparison, zoom/pan, region selection → jump to hex offset. |
| #174 | **AudioViewer — Full Implementation** | ✅ **Done** — `WpfHexEditor.Editor.AudioViewer`; waveform rendering; playback controls; spectrogram view; audio metadata panel. |
| #175 | **TileEditor — Full Implementation** | ✅ **Done** — `TileEditorFactory` + `TileEditor`; tile grid picker; palette editor; sprite sheet layout; pixel-level editing; export PNG/BMP; zoom 1×–16×. |
| #176 | **JsonEditor — Full Implementation** | ✅ **Done** — `JsonEditorFactory`; dedicated JSON editor; collapsible tree view. Remaining: JSON Schema validation, JSON path navigator, format/minify/sort-keys, side-by-side diff. |
| #177 | **ScriptEditor Phase 2** | Step-debugger for `.hxscript` (breakpoints, watch, call stack); REPL panel (interactive HxScript console); snippet templates; syntax error underline + gutter marker; HxScript language server (LSP). |
| #178 | **DisassemblyViewer Phase 2** | x86/x64/ARM/WASM instruction decoding; jump arrows between branch targets; symbol table overlay; address navigation bar; cross-reference to hex view; export as `.asm`. |
| #155 | **Visual XAML Editor — Phase 2** | ✅ **Core + Phase 2 done** — `ConstraintAdorner`, gradient editor, binding path picker, perf overlay, responsive breakpoint bar shipped in v0.6.3. Remaining: trigger & animation timeline editor (beyond stub), data-binding wizard, "Go to Definition" for resource keys, multi-resolution DPI preview, export as standalone `.xaml`. |
| #179 | **Document Structure Panel** | ✅ **Done v0.6.4.6** — `WpfHexEditor.Plugins.DocumentStructure`; 8 providers; `IDocumentStructureProvider` SDK extension point; 18 `DS_*` theme tokens × 18 themes. |
| #180 | **Tab Groups** | ✅ **Done v0.6.4.6** — `ITabGroupService` SDK contract + `TabGroupService`; split horizontal/vertical; 16 `TG_*` theme tokens; keyboard shortcuts; settings page; 77 integration tests. |

---

## 🔜 Planned — Assembly & Binary

| Feature # | Title | Description |
|-----------|-------|-------------|
| #81 | **Plugin Sandbox (gRPC Migration + Hot-Reload)** | HWND embedding + IPC bridge done (v0.3.0). Remaining: gRPC transport migration, collectible `AssemblyLoadContext` hot-reload from sandbox, plugin restart-less upgrade flow. |
| #97 | **Large File Optimization** | `VirtualizationEngine`, `LazyParser`, multi-core SmartComplete adapter; virtualized display for >1 GB files, incremental parsing. |

---

## 🔜 Planned — DevOps & Collaboration

| Feature # | Title | Description |
|-----------|-------|-------------|
| #41 | **Plugin Marketplace** | `MarketplaceManager` — browse, install, update from online registry; signed `.whxplugin` packages. |
| #42 | **Plugin Security & Sandboxing** | Permission declarations at install time, integrity verification, AppDomain isolation. |
| #43 | **Plugin Auto-Update** | `UpdateService` / `UpdateChecker`, rollback support, scheduled checks for IDE + plugins. |
| #44 | **Integrated Debugger** | ✅ **Done v0.6.5.110** — `DebugModule` (merged from `WpfHexEditor.Plugins.Debugger`); `DebuggerServiceImpl`; `IDebugAdapterRegistry` / `IDebugVisualizerRegistry` SDK; nine debug panels; VS-style Call Stack toolbar. |
| #90 | **Debugger — Multi-Project** | Multi-project debug sessions via EventBus; supports scripts, plugins, and workspace projects. |
| #91b | **Output Panel — Debug Filters** | Context-menu filters in the Output panel (exceptions, module load/unload, thread exit). Requires Integrated Debugger (#44 ✅). |
| #91 | **Git Integration** | 🔧 **~40%** — `GitChangesPanel` (stage/unstage/commit/discard, diff preview), push/pull/fetch toolbar, branch picker popup, stash manager, status bar adapter, `GitHistoryPanel`, `BlameGutterControl`; 18 `GC_*` tokens. **Not yet integration-tested** — real-repo operations unverified. |
| #93 | **Plugin Installer / Marketplace UI** | Plugin search UI, download/update manager, sandbox enforcement at install time. |
| #95 | **Unit Testing Panel** | ✅ **Done** — `WpfHexEditor.Plugins.UnitTesting`; `DotnetTestRunner`; `TrxParser`; `UnitTestingPanel`; auto-run on `BuildSucceededEvent`; xunit/nunit/mstest auto-detection. |
| #98 | **Multi-User Collaboration** | Multi-cursor real-time editing, document sync, contextual chat/comments per line. |

---

## 🔜 Planned — UX & Infrastructure

| Feature # | Title | Description |
|-----------|-------|-------------|
| #99 | **Advanced UI/UX** | `NotificationManager`, `WorkspaceLayoutAdapter`; contextual inline notifications, layout persistence per workspace, full docking for all panels. |

---

## 🔜 Planned — Quality, Tooling & Operations

| Feature # | Title | Description |
|-----------|-------|-------------|
| #45 | **Plugin & Script Testing** | `TestRunnerService` — automated functional, compatibility, security and performance tests for plugins and scripts before execution or installation. |
| #46 | **Integrated Documentation & Contextual Help** | In-IDE documentation browser and context-sensitive help for all components (menus, commands, panels, plugins, APIs). |
| #47 | **Notification & Alert System** | Centralized `NotificationService` — inline toast alerts, priority levels, EventBus integration, plugin-extensible notification hooks. |
| #48 | **Monitoring & Analytics** | IDE health dashboard — plugin CPU/RAM usage, event frequency, command stats, latency tracking; exportable reports. |
| #49 | **Export / Import Projects & Configurations** | Full export/import of workspace configs, settings, plugin lists, templates and keybindings for portability and backup. |
| #54 | **Auto-Save & Restore** | ✅ **Done** — `AutoSaveService` in `WpfHexEditor.Editor.DocumentEditor`; automatic session backup at configurable intervals; full restore on crash. |
| #58 | **Reporting & Dashboards** | Dedicated report panel — code quality metrics, plugin health, build history, test results; filterable and exportable. |
| #63 | **Smart Session Backup & Auto-Resume** | Intelligent session snapshot (open files, cursors, layout, unsaved changes); seamless resume after restart or crash. |
| #65 | **Interactive Tutorials & Onboarding** | Step-by-step interactive onboarding for new users; contextual tutorial overlays for major features; plugin authors guide. |
| #67 | **Global Backup & Config Versioning** | Version-controlled snapshots of all IDE configurations, plugin manifests and workspace templates; rollback support. |
| #69 | **Dependency Monitoring & Plugin Compatibility** | Detect plugin dependency conflicts, SDK version mismatches, and incompatible combinations; alerts before install/update. |
| #71 | **CI Integration for Plugins & Scripts** | Trigger automated plugin test suites and script validation from CI pipelines; webhook support; result reporting via Output Panel. |

---

## 🔜 Planned — Distribution & Web Presence

| Feature # | Title | Description |
|-----------|-------|-------------|
| #108 | **Official Website** | Public project website — landing page, feature showcase, screenshots, documentation browser, changelog, download links and plugin registry. |
| #109 | **Installable Package** | Self-contained installer for the IDE — MSI / MSIX / WinGet package; auto-update channel; no .NET SDK required for end users; optional silent install for enterprise. |

---

## 🔜 Planned — Binary Analysis & Reverse Engineering

| Feature # | Title | Description |
|-----------|-------|-------------|
| #110 | **String Extraction Panel** | ✅ **Done v0.6.5.95** — `StringExtractionPanel` + `StringExtractorPanel`; ASCII/Unicode/custom-encoding scan; minimum-length filter; one-click jump to hex offset; CSV export. |
| #111 | **Cryptographic Hash Inspector** | ✅ **Done** — `HashInspectorPanel` + `HashInspectorViewModel`; MD5/SHA-1/SHA-256/SHA-512/CRC-32/Adler-32; real-time over selection or full file; copy-to-clipboard; reference-hash comparison. |
| #112 | **Embedded File Carver** | Detect and extract embedded PE files, ZIP archives, PNG images, and other magic-byte signatures from within a binary; carved blobs open as virtual child documents. |
| #113 | **YARA Rule Engine** | Author, compile, and run YARA rules directly in the IDE; matches highlighted in hex view and listed in a results panel with offset, length, and rule name. |
| #114 | **PE Import / Export Table Analyzer** | Parse PE32/PE64 IAT and Export Directory; display resolved DLL and function names side-by-side with raw offsets; flag suspicious ordinal-only imports. |
| #115 | **Binary Patch Recorder** | Record every byte modification as a named, annotated patch; export as binary diff, IDA script, or human-readable `.patch` file for reproducible patching. |
| #116 | **Obfuscation & Packing Detector** | Heuristic analysis of section entropy, suspicious imports, and known packer watermarks (UPX, Themida, MPRESS); produces a risk score and actionable report. |
| #117 | **Memory Snapshot Viewer** | Load Windows mini-dump (`.dmp`) or Linux core-dump files; display memory regions, thread stacks, loaded modules, and heap blocks in the hex view with structured overlays. |
| #119 | **Byte Frequency & Bigram Heatmap** | 🔧 **Partial** — `ByteFrequencyPanel` + `ByteFrequencyService` + `ByteFrequencyViewModel` done. Remaining: bigram (256×256 dot-plot), multi-file overlay comparison. |
| #124 | **Hex Bookmarks & Named Regions** | ✅ **Done** — `BookmarkService`, `EnhancedBookmark`, `BookmarkGroup`, `BookmarksPanel`, `BookmarkExportService`; persistent named bookmarks with groups, colors, comments; JSON import/export. |
| #139 | **Grammar File Support** | Add support for loading and parsing .syn grammar files from Synalysis; enable syntax highlighting, structured parsing, and integration with memory/code analysis workflows. |

---

## 🔜 Planned — Security & Forensics

| Feature # | Title | Description |
|-----------|-------|-------------|
| #118 | **File Signature Database** | ✅ **Partially done v0.6.4.75** — `EmbeddedFormatCatalog` ships 790+ format definitions with magic-byte signatures, MIME types, confidence scoring, and auto-detection on open; `whfmt.FileFormatCatalog` NuGet published. Remaining: user-extensible custom signatures (currently embedded-only). |
| #120 | **XOR / ROT Cipher Decoder** | Brute-force single-byte XOR keys and ROT offsets over a selection; score candidates by ASCII printability; display top results with one-click "apply to selection". |
| #121 | **Audit Log & Forensic Session Journal** | Record every user action (open, edit, navigate, export) with timestamps and byte-range context into a tamper-evident journal, exportable as signed HTML or JSON report. |
| #122 | **Certificate & ASN.1 Inspector** | Parse DER/PEM X.509 certificates, PKCS#7/12 containers, and raw ASN.1 structures embedded in any binary; tree-view with field names, OIDs, validity dates, and key parameters. |
| #123 | **Vulnerability Pattern Scanner** | Curated byte-pattern rules (stack cookies, heap metadata, safe SEH markers) that flag potential vulnerability indicators in native binaries directly in the hex view. |

---

## 🔜 Planned — Developer Productivity

| Feature # | Title | Description |
|-----------|-------|-------------|
| #125 | **Regex Search over Hex & ASCII** | Full regex search across hex byte patterns (e.g. `\xDE\xAD.{2}\xBE\xEF`) and decoded ASCII simultaneously, with match highlighting, results list, and replace support. |
| #126 | **Byte-Range Calculator** | Inline calculator panel for arithmetic, bitwise, and shift operations on raw byte values; accepts hex/decimal/binary input; shows two's-complement and IEEE-754 float interpretations. |
| #127 | **Column / Block Selection** | Rectangular block selection across hex and ASCII panes; paste, fill, or export selected columns as independent byte arrays — essential for fixed-width record formats. |
| #128 | **Binary Template Marketplace** | Community-driven repository of binary template definitions; browse, search, install, and auto-update format templates (PE, ELF, ZIP, PNG, MP4, etc.) from within the IDE. |
| #129 | **Multi-File Hex Session Tabs** | True independent hex editor sessions per tab with separate cursors, selections, undo stacks, and bookmarks — enabling parallel analysis of multiple binaries. |
| #130 | **Changeset Review Panel** | ✅ **Done** — `ChangesetEditorFactory` + `WpfHexEditor.Editor.ChangesetEditor`; diff-style view of all pending unsaved byte modifications; offset, original value, new value, age; per-change accept/reject controls before save. |

---

## 🔜 Planned — IDE Infrastructure

| Feature # | Title | Description |
|-----------|-------|-------------|
| #131 | **Plugin Dependency Graph Panel** | Interactive directed-graph visualization of all loaded plugins, their service dependencies, and SDK version constraints; detects cycles and orphaned dependencies. |
| #132 | **Theme Designer & Live Preview** | In-IDE XAML brush editor to create, fork, and export custom themes; changes applied live to all dockable panels; themes packaged as `.whtheme` files. |
| #133 | **Command Palette (Ctrl+Shift+P)** | ✅ **Done v0.6.3.6** — `CommandPaletteWindow` + `CommandPaletteService`; VS Code-style fuzzy-search palette; 9 modes (`>`/`@`/`:`/`#`/`%`/`?`/Tab); frequency boost; context boost; file search; content grep; `CommandPaletteOptionsPage`. |
| #134 | **Extension Point Debugger** | Developer panel listing every SDK extension point (menus, toolbar, statusbar, eventbus) registered by each plugin with live invocation counts and last-call stack. |
| #135 | **Workspace File Watcher** | Monitor all open project files for external changes (from other processes or Git operations); prompt to reload, diff, or merge changes without closing the editor tab. |
| #138 | **Welcome Screen Redesign** | ✅ **Done v0.6.5.95+** — VS Start Page-style welcome panel: template cards (8 cards wired to onNewFile/onOpenFile/onOpenProject), enriched recent file rows (icon + dir + date + pin/remove), search filter, categorised news feed with filter pills, `WelcomeNewsService` (JSON→changelog fallback, 30-min cache), `MruService` pin/unpin/remove, 7 `WS_*` theme tokens × 18 themes, 10 new AppResources keys × 28 satellites. |

---

## 🔜 Planned — Network & Protocol Analysis

| Feature # | Title | Description |
|-----------|-------|-------------|
| #136 | **PCAP / Network Capture Viewer** | Load `.pcap` and `.pcapng` files; display packet list, layer breakdown (Ethernet/IP/TCP/UDP/TLS), and raw payload bytes in the hex view. |
| #137 | **Protocol Dissector Plugin API** | Plugin contract allowing third parties to register custom protocol dissectors; dissected fields appear in the ParsedFields panel and hex view overlays. |

---

## 🔜 Planned — IDE Infrastructure Phase 2

| Feature # | Title | Description |
|-----------|-------|-------------|
| #179 | **Quick File Open (Ctrl+P)** | VS Code-style fuzzy file picker across entire solution; recent files first; preview on hover; open at line (`filename:42`); filter by extension; excludes `.gitignore` patterns. |
| #180 | **Terminal Phase 2** | Full ANSI / Xterm-compatible emulation (256-color, SGR codes, mouse events); terminal split panes (horizontal/vertical); SSH / WSL session integration; persistent command history across restarts; REPL hosting (Python/Node/Ruby); macro recording & playback. |
| #181 | **Build System Phase 2** | Parallel project builds (MSBuild `-m` flag); build cancel button (kills MSBuild process); per-project build duration profiling in Output panel; pre/post-build event steps UI; distributed build cache (optional); cross-platform build (dotnet CLI fallback). |
| #182 | **Docking Phase 2** | Persistent floating window positions (saved on close, restored on next session); keyboard panel switcher (`Ctrl+Tab`-like cycle across all panels); multi-item tabbed float windows (group panels in a single float). |
| #183 | **EventBus Phase 2** | Async event delivery (non-blocking handlers via `Task`); per-event filtering / routing rules; cross-process plugin events via IPC; plugin-publishable custom events via SDK; event delivery metrics (latency, queue depth) in Monitoring panel. |
| #184 | **Project System Phase 2** | Lazy project loading (defer heavy projects until first open); solution-wide find & replace (`Ctrl+Shift+H`); project dependency graph view; `.gitignore` support in folder loader; bulk rename/move items; nested project groups (>1 level). |
| #185 | **Settings Phase 2** | Per-workspace settings override (`.whsettings` in project root); named settings profiles (switch between Debug/Release/Presentation modes); settings import/export UI; settings keyword search; keyboard shortcut conflict detection + resolution dialog. |

---

## 🔜 Planned — Plugins Phase 2

| Feature # | Title | Description |
|-----------|-------|-------------|
| #186 | **AssemblyExplorer Phase 2** | Full ILSpy backend wiring (replaces Skeleton fallback) ✅ partial; **ECMA-335 token→offset** (`PeOffsetResolver` ✅); **hex sync** (`AssemblyHexSyncService` ✅). Remaining: PDB symbol + source-link matching UI, plugin panel improvements. |
| #187 | **ArchiveStructure Phase 2** | In-place archive editing (add/remove/rename entries without re-pack); multi-format packer (TAR/GZ/XZ/BR); compression ratio analysis per entry; drag-and-drop files into archive panel. |
| #188 | **FileComparison Phase 2** | Diff algorithm choice (LCS / Myers / Histogram — selectable in toolbar); binary patch export (`.bdiff` / `xdelta` format); unified diff view mode; **3-way merge ✅ v0.6.5.96** (base + ours + theirs — `ThreeWayMergeEngine`, conflict resolution UI, save merged output). Remaining: diff algorithm selector in toolbar, `.bdiff`/`xdelta` binary patch format. |
| #189 | **DiagnosticTools Phase 2** | Memory leak heuristic (detect growing gen2 objects across GC cycles); GC pressure analyzer (alert on LOH allocations); thread deadlock detector (wait graph); flame graph for CPU samples. |

---

## 🔜 Planned — LSP & Language Intelligence Phase 3

| Feature # | Title | Description |
|-----------|-------|-------------|
| #190 | **LSP Phase 3** | **Call hierarchy** (`LspCallHierarchyProvider` + `CallHierarchyPanel` ✅ Shift+Alt+H); **linked editing** (`LspLinkedEditingProvider` ✅). Remaining: inline value hints (debug scope expression eval), LSP 3.18 pull-diagnostics (`textDocument/diagnostic`), type hierarchy (`Ctrl+F12`). |
| #191 | **Core Source Analysis (Roslyn)** | C# incremental Roslyn parser replacing regex-based `SourceOutlineEngine`; VB.NET / F# parser support; cross-language symbol resolution; semantic model for LSP providers; `#if` / `#pragma` conditional compilation region handling. |
| #192 | **F# Full Language Support** | `FSharp.whfmt` syntax definition (keywords, computation expressions `async { }` / `seq { }` / `task { }`, active patterns, discriminated union arms, F# operators `\|>` / `>>` / `<-`); `LanguageRegistry` registration for `.fs` / `.fsx` / `.fsi`; `LspServerRegistry` auto-detect `fsautocomplete` (Ionide) with PATH check + install hint; Assembly Explorer F# decompilation language mapping; `DocumentManager` extension map update. See **DevPlan #9**. |
| #193 | **VB.NET Full Language Support** | `VBNet.whfmt` syntax definition (VB.NET keywords, `End Class` / `End Sub` / `End Function` block detection, `#Region` / `#End Region` named folding, `'` line comments, `'''` XML-doc comments, `&H` / `&B` / `&O` number literals, attribute `<…>` patterns); `LanguageRegistry` registration for `.vb`; OmniSharp `vbnet` language ID registration; Assembly Explorer VB.NET language mapping. See **DevPlan #9**. |

---

## 🔜 Planned — HexEditor Control (Internal — Low-Level)

Features internal to the `WPFHexaEditor` control itself — engine, ByteProvider, rendering, selection, in-buffer manipulation. Not panels or plugins.

| Feature # | Title | Description |
|-----------|-------|-------------|
| #194 | **Streaming ByteProvider for files >4 GB** | Switch large-file path to `MemoryMappedFile` with sliding 64 KB views; lift the current ~2 GB ceiling; lazy materialization of edit deltas. Enables forensic disk image analysis without saturating RAM. |
| #195 | **Copy-on-write virtual edits** | Replace whole-buffer mutation with `Dictionary<long, byte>` overlay merged on read. Allows multi-GB files to be edited paying only for actual changes; foundation for true streaming edits. |
| #196 | **Undo/Redo branching tree** | Replace linear undo stack with a DAG of edit states; preserves alternative branches when the user undoes then makes new edits. Exposed via `IUndoEngine` extension; visualized in existing history dropdown. |
| #197 | **Insert / Delete bytes (true)** | Real shift-the-rest insertion and deletion (currently overwrite-only). Requires `IByteProvider` to support gap-buffer / piece-table semantics so multi-GB files don't recopy. Foundation for ROM patching, exploit dev, container repair. |
| #198 | **Atomic save (`file.tmp` + rename)** | Audit + harden `ByteProvider.Save` to write to a sibling tempfile then `MoveFileEx(REPLACE_EXISTING)`; eliminates partial-write corruption on crash/poweroff. |
| #199 | **External file change detection** | `FileSystemWatcher` integration; detect when the open file is modified by another process; prompt user reload / merge / keep-mine; coalesce rapid events with 250 ms debounce. |
| #200 | **GlyphRun caching for hex cells** | Cache one frozen `GlyphRun` per byte value (256 entries) instead of formatting on every render pass; major scroll performance gain. Same approach for ASCII column. |
| #201 | **Dirty-rectangle rendering** | Replace global `InvalidateVisual()` with line-level invalidation; only redraw lines whose bytes changed. Cuts redraw cost dramatically on selection-only and caret-only updates. |
| #202 | **DataInspector throttle during inertia scroll** | Suppress inspector recompute during high-velocity scroll (>200 lines/s); resume on scroll-stop. Eliminates input-lag during fast navigation in big files. |
| #203 | **Pixel-perfect cell alignment** | Snap each hex cell origin to integer device pixels; eliminate sub-pixel glyph blur on non-HiDPI screens. Wire `UseLayoutRounding=true` and verify with `RenderOptions.SetEdgeMode`. |
| #204 | **Rectangular / column selection (Alt+drag)** | Block-rectangle selection across rows; copy/fill/XOR operates on the rect. Mirrors the `TextEditor` rect-select that already shipped (Editor.Core ADR). High-impact for structured-binary work. |
| #205 | **Multi-range selection (Ctrl+click)** | Maintain a list of disjoint selected ranges; copy/fill/replace/XOR fan out across all ranges. Compatible with existing `SelectionStart/Stop` via a `SelectionRanges` collection. |
| #206 | **Insert / Overwrite caret toggle (Insert key)** | Standard text-editor toggle missing in most hex editors; status-bar indicator; respects #197 insert mode when ON. |
| #207 | **Bit-level cursor (sub-nibble)** | Extend the existing nibble caret to single-bit positioning (Shift+Arrow toggles bits). Enables direct flag-byte editing without external calculator. |
| #208 | **Regex / hex-pattern search** | Pattern syntax `\x4D\x5A.{2}\x90` (yara-like); already partial via `SearchEngine` — promote to first-class `Find` dialog with capture groups and replace. |
| #209 | **Fill with repeated pattern** | Fill range with multi-byte pattern (`0xDEADBEEF`) instead of single byte; existing `FillBytes` extension. |
| #210 | **Bitwise ops on selection** | XOR / AND / OR / NOT against repeated key or rolling key; in-place; undo-aware (single `UndoGroup`). Useful for plain-XOR cipher peeling. |
| #211 | **Endianness swap on selection** | Swap 16/32/64-bit words in place over a selection; preserves caret; single undo entry. |
| #212 | **Hash / checksum live panel for selection** | Real-time CRC16/32, MD5, SHA-1/256/512, BLAKE3, Adler32 over the active selection; updates on selection change with debounce. Surfaces existing hash backends. |
| #213 | **Sparse-file aware editing** | Detect sparse / NTFS-compressed source files; preserve hole layout on save (`FSCTL_SET_SPARSE` / `FSCTL_SET_ZERO_DATA`). |
| #214 | **Raw volume / disk access** | `\\.\PhysicalDriveN` and `\\.\C:` open paths with elevated-permission gate; read-only by default; locked behind explicit `IPermissionService.RequireElevation()` consent. |

---

## 🔜 Planned — HexEditor Control (Internal — High-Level UX)

| Feature # | Title | Description |
|-----------|-------|-------------|
| #215 | **Configurable columns (show/hide/reorder)** | Toggle visibility of offset / hex / ASCII / custom-encoding columns; drag-to-reorder; persisted per editor instance. Add UTF-16 LE/BE, EBCDIC, custom codepage panes. |
| #216 | **Dynamic bytes-per-row** | Replace fixed `BytePerLine=16` with `auto` (snap to 8/16/24/32 based on viewport width) and `fluid` (fill viewport, no snap). Wired through the existing `BytePerLine` DP. |
| #217 | **Range highlighting (persistent)** | Color a `[start,end]` range with a custom brush that survives scroll; distinct from bookmarks (point markers) and search hits (transient). API: `AddHighlight(range, brush, label)`. |
| #218 | **Inline annotations** | Per-line gutter icon with hover tooltip ("PE header signature"); attached to a `[start,end]` range; persisted in workspace; collaborative-friendly. |
| #219 | **Vertical mini-map** | Compressed full-file overview pane on the right (à-la VS Code text minimap, but byte-density colored by entropy / change / search density); click to jump. Reuses the entropy LUT from the EntropyVisualizer plugin. |
| #220 | **Configurable ruler header** | Render offset row in hex / dec / oct; alignment 0/4/8/16; user-toggleable group separators. |
| #221 | **Smart `Ctrl+G` goto parser** | Parse `0x100`, `1024`, `+0x10` (relative), `-1` (from EOF), `RVA:0x4000`, `Section:.text+0x10`, `Symbol:main` (debugger join). Replaces today's int-only parser. |
| #222 | **Structural breadcrumb bar** | When the active file is parsed via `whfmt`, show "PE.OptionalHeader.DataDirectories[2]" at top of the editor; click each segment to jump. Reuses existing `BreadcrumbBar` widget. |
| #223 | **Back / Forward navigation history** | Browser-style `Alt+Left` / `Alt+Right`; stack of visited offsets bounded to 100 entries; survives scroll, not just goto. |
| #224 | **Smooth scroll animation** | 60 fps animated transition on PageDown / Home / End / programmatic `ScrollToOffset`; respects `Reduced motion` accessibility setting. |
| #225 | **Type-aware editing** | When the caret sits on a parsed `uint32` field, typing `42` writes `2A 00 00 00` and live-previews the diff in a popover; Esc cancels. Requires #222 structural awareness. |
| #226 | **Inline structural validation** | Edits that break a parsed checksum / CRC / structure are highlighted red with a tooltip explaining which constraint failed; non-blocking (the edit applies, but the warning persists). |
| #227 | **Modification templates** | Apply named templates ("PE relocation", "PNG IHDR width=N") to a selection; templates ship from `whfmt` and from a user folder; preview-then-apply. |
| #228 | **Live edit preview popover** | Mini-bubble over the cursor while typing a new byte: shows the proposed value + decoded interpretations (int/float/string/etc.) before commit. |
| #229 | **Split view** | ✅ **Done v0.6.5.16** — `HexEditorSplitHost` wraps two `HexEditor` instances sharing the same `ByteProvider`; independent scroll/selection/caret; inline toggle button at top of scrollbar; focus borders (split mode only); `ByteProvider.DataChanged` event propagates all mutation types (modify/insert/delete) to the peer pane; breadcrumb bar, format detection blocks, undo/redo and custom background overlays all synced to secondary pane; status bar hidden on secondary; standalone-safe; `IDocumentEditor` delegation (undo/redo always via primary where shared `UndoEngine` lives). |
| #230 | **Locked / pinned panes** | Freeze one half of a split at a fixed offset while the other scrolls; useful for keeping a header table visible while exploring later regions. |
| #231 | **Ghost overlay for visual diff** | Render a second buffer in transparent overlay on top of the current one; quick visual binary diff without leaving the editor. |
| #232 | **Smart paste auto-detection** | Detect clipboard format on paste: `0x41 0x42`, `4142`, `AB`, base64, hex-with-spaces, C array literal — convert and paste in one action. Status-bar hint shows detected source format. |
| #233 | **Screen reader (UIA) support** | Announce caret movements ("byte 0x4D 'M' at offset 0x100, in section .text"); selection range readout; expose hex cells as UIA `DataItem` controls. |
| #234 | **High-contrast WCAG AA palette** | Dedicated theme tokens with verified AA contrast ratios; activated automatically when Windows `HighContrast` is on; documented per-token contrast measurements. |
| #235 | **Complete keyboard-only navigation** | Audit & close every gap: all context-menu actions reachable via keyboard, focus visuals on all interactive cells, custom shortcut tab to jump caret to status bar / breadcrumb / inspector. |

---

## ✅ Recently Shipped

| Feature | Version / Release |
|---------|-------------------|
| **DiffViewer Phase 2 — word diff + .patch export + 3-way merge (#171 complete)** — Text tab with side-by-side line diff; word-level inline highlights (`MyersDiffAlgorithm.WordEdits`); Export `.patch` (unified diff format); 3-way merge tab (`ThreeWayMergeEngine` — base/ours/theirs, auto-accept single-side, conflict resolution buttons, save merged output); 28-lang localized (28 new keys × 28 satellites) | [0.6.5.96] — 2026-05-20 |
| **ClassDiagram — incremental Roslyn live-sync (#156-A)** — `AnalyzeFilesIncremental`: re-parses only changed files, preserves canvas positions of unchanged nodes; `_pendingChanges` ConcurrentBag drains per debounce cycle; O(1) path lookup via HashSet | [0.6.5.96] — 2026-05-20 |
| **Entropy Heatmap viewport rendering (#095)** — `AddCustomBackgroundBlockRange` SDK batch API; `EntropyHeatmapService` brush-cache + batch inject; `EntropyWindowSizeBytes` rename | [0.6.5.96] — 2026-05-20 |
| **Welcome Screen Redesign (#138)** — VS Start Page-style welcome panel: template cards, enriched recent file rows (icon + dir + date + pin/remove), search filter, categorised news feed with filter pills; `WelcomeNewsService` (JSON→changelog fallback, 30-min cache); `MruService` pin/unpin/remove; 7 `WS_*` theme tokens × 18 themes; 10 new AppResources keys × 28 satellites | [0.6.5.95+] — 2026-05-19 |
| **ScreenRecorder plugin** — full capture pipeline (BitBlt + DPI-aware region selector, F9/Shift+F9 global hotkey), timeline UI (thumbnails, drag-reorder, multi-select), GIF/PNG/FFmpeg export, playback scrubber, undo/redo, import/clipboard; themed + 28-lang localized; `.whscr` format | [0.6.5.95] — 2026-05-17 |
| **ClassDiagram live arrow redraw** — arrows follow nodes in real time during drag (≤60 relations); stale-arrow-after-drop fix | [0.6.5.95] — 2026-05-17 |
| **Scripting Console panel** — dockable Roslyn scripting console; history, output cap, CTS-safe shutdown; `ScriptingModule` | [0.6.5.95] — 2026-05-17 |
| **Snippets Phase 2** — visual in-app snippet editor: highlight box, conflict detector, import/export, variable picker, default pack + user store | [0.6.5.95] — 2026-05-17 |
| **Hex Diff / Patch panel** — side-by-side binary diff, concurrent reads, TOCTOU fix, patch export | [0.6.5.95] — 2026-05-17 |
| **Binary Analysis module** — 5 panels: navigate, EstimateSize, CSV export | [0.6.5.95] — 2026-05-17 |
| **ByteProvider v1.3.0** — DI/AOP/Slice/Metrics public API tier; 90 xUnit tests | [0.6.5.95] — 2026-05-17 |
| **whfmt.CodeGen v1.1.3** — JSONC header parsing in `.whfmt` files | [0.6.5.95] — 2026-05-17 |
| **WpfTerminal decoupled from IIDEHostContext** — `RoslynServiceFactory` injected; `Process` leak fixed | [0.6.5.95] — 2026-05-17 |
| **Build-number scheme reset** — `WpfHexEditor.App` realigned from `0.6.5.225` to `0.6.5.8` | [0.6.5.8] — 2026-05-12 |
| **Code Analysis scope UX + SplitButton re-run** — scope label (Solution/Project/File); SplitButton: left = re-run same scope, dropdown = Run Solution / Run Project… / Run File… | [0.6.5.225] — 2026-05-09 |
| **Code Analysis Treemap context menu** — right-click: Open File, Copy Path, Copy Metrics, Run Analysis on File, Filter to This Project, Highlight Top 10 Hotspots; 15 new keys × 28 satellites | [0.6.5.225] — 2026-05-09 |
| **DocumentEditor Glyph-Accurate Caret & Hit-Test** — `GetCharOffsetFromGlyphLines`/`GetCaretXYFromGlyphLines` via GlyphRun pipeline; synchronous `RebuildLayout` in all edit paths; opens-dirty fix; `GetCaretX` guard | [0.6.5.110] — 2026-05-05 |
| **DebugModule Core Integration (ADR-010)** — `WpfHexEditor.Plugins.Debugger` merged into `WpfHexEditor.App/Debug`; nine debug panels; VS-style Call Stack toolbar; `IDebugAdapterRegistry` / `IDebugVisualizerRegistry` SDK extension points preserved | [0.6.5.110] — 2026-05-05 |
| **HexEditor Split View (#229)** — `HexEditorSplitHost` with shared `ByteProvider`; inline split toggle; `ByteProvider.DataChanged` syncs all mutation types; breadcrumb, format blocks, undo/redo propagated to secondary; focus borders; options page toggle | [0.6.5.16] — 2026-05-04 |
| **DocEditor Page Rulers** — interactive rulers (horizontal + vertical) tracking zoom, caret position, and resize; `LayoutTransform`-based zoom; drag-to-move selection | [0.6.5.16] — 2026-05-04 |
| **Docking Layout Auto-Reset** — corrupt or oversized saved layouts auto-reset on startup instead of crashing | [0.6.5.16] — 2026-05-04 |
| **NuGet release wave** — 6 standalone packages: WPFHexaEditor 3.2.0, WpfCodeEditor 0.9.8.0, WpfDocking 0.9.7.0, WpfTerminal 0.9.7.0, WpfHexEditor.Core.ByteProvider 1.1.0, whfmt.FileFormatCatalog 1.1.0 | [0.6.5.15] — 2026-05-01 |
| **IDE Localization — 27 languages** — 77.9% DynamicResource coverage; all panels, menus, context menus, dialogs, toolbar buttons; per-assembly `LocalizedResourceDictionary` pattern; 28 satellite languages | [0.6.5.15] — 2026-05-01 |
| **WPFHexaEditor 3.2.0** — Go to offset dialog (`Ctrl+G`); unified `UndoEngine`; drag-selection auto-scroll fix; BreadcrumbBar freeze + phantom row fix | [0.6.5.15] — 2026-05-01 |
| **790+ .whfmt format definitions** — +100 new definitions; `FormatSchemaValidator` wired at load time; `EmbeddedFormatCatalog.GetAll()` returns `FrozenSet<T>` | [0.6.5.15] — 2026-05-01 |
| **whfmt.FileFormatCatalog v1.1.0** — `FormatMatcher`, `FormatFileAnalyzer`, `CatalogQuery`, `FormatMetadataExtensions`, `FormatSummaryBuilder` | [0.6.5.15] — 2026-05-01 |
| **Structure Editor** — visual `.whfmt` template editor with block DataGrid, drag-drop, validation pipeline, undo/redo, TestTab, expression SmartComplete, ForensicPattern tolerant converter | [0.6.4.75] — 2026-04-15 |
| **Window Menu + Win32 Fullscreen** — `_Window` top-level menu; `F11` fullscreen via Win32 covering full monitor | [0.6.4.8] — 2026-04-08 |
| **Git Integration G0–G7** — `GitChangesPanel`, push/pull/fetch, branch picker, stash manager, status bar adapter, `GitHistoryPanel`, `BlameGutterControl`; 18 `GC_*` tokens × 18 themes | [0.6.4.7] — 2026-04-07 |
| **Class Diagram Overkill Upgrade** — syntax-highlighted DSL pane, 3 layout strategies, canvas minimap, TreeView left panel, session state save/restore; round-trip Roslyn sync phases 1A–7 | [0.6.4.7] — 2026-04-07 |
| **AI Assistant Plugin** — 5 providers (Anthropic/OpenAI/Gemini/Ollama/Claude Code CLI), 25 MCP tools, streaming chat, inline apply, @mentions, command palette (`Ctrl+Shift+A`), 17 `CA_*` tokens × 18 themes | [0.6.4.3] — 2026-04-02 |
| **Roslyn Integration** — In-process `RoslynLanguageClient` replacing OmniSharp for C#/VB.NET | [0.6.4.3] — 2026-04-02 |
| **LSP Client Engine** — full JSON-RPC LSP client; 10 providers; `LspDocumentBridgeService`; `LspStatusBarAdapter`; 30 tokens × 18 themes | [0.6.3.6] — 2026-03-23 |
| **Command Palette** — `Ctrl+Shift+P`, 9 search modes, fuzzy scoring, frequency boost, `CommandPaletteOptionsPage` | [0.6.3.6] — 2026-03-23 |
| **Command System Central** — `WpfHexEditor.Commands`; `CommandRegistry` (~45 commands); `KeyBindingService`; `KeyboardShortcutsPage`; TitleBar launcher | [0.6.3.6] — 2026-03-23 |
| **Document Structure Panel (#179)** — `WpfHexEditor.Plugins.DocumentStructure`; 8 providers; `IDocumentStructureProvider` SDK; 18 `DS_*` tokens × 18 themes | [0.6.4.6] — 2026-04-06 |
| **Tab Groups (#180)** — `ITabGroupService` SDK + `TabGroupService`; split horizontal/vertical; 16 `TG_*` tokens; 77 integration tests | [0.6.4.6] — 2026-04-06 |
| **Visual XAML Designer** — live WPF canvas, bidirectional canvas↔code sync (~95%), move/resize/rotate handles, property inspector, multi-select, snap grid, 4 split layouts, zoom/pan, full Phase 2/3 (ConstraintAdorner, gradient editor, binding path picker, perf overlay, responsive breakpoint bar) | [0.6.0] — 2026-03-18 |
| **Shared `UndoEngine`** — coalescing (500 ms), transactions, save-point tracking, `Ctrl+Shift+Z` redo; replaces both editors' custom stacks | [0.6.0] — 2026-03-18 |
| **Unit Testing Panel (#95)** — `DotnetTestRunner`, `TrxParser`, `UnitTestingPanel`; auto-run on `BuildSucceededEvent`; xunit/nunit/mstest auto-detection | [0.6.0] — 2026-03-16 |
| **Plugin Sandbox — HWND embedding + IPC menu/toolbar/event bridges + auto-isolation + signing** (#81 Phase 9-12) | [0.3.0] — 2026-03-15 |
| VS-style docking engine (100% in-house) | [2.7.0] — 2026-02 |
| Project system (`.whsln` / `.whproj`) | [2.7.0] — 2026-02 |

> Full history → [CHANGELOG.md](CHANGELOG.md)
