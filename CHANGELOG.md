# Changelog

All notable changes to **WpfHexEditor** are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) · Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

---

## What's Next

> Planned features — subject to change. Items marked ✅ are already shipped.

### Plugin System & SDK
- ✅ Public SDK (`WpfHexEditor.SDK`) — versioned plugin contracts (IWpfHexEditorPlugin, IIDEHostContext, IUIRegistry, …)
- ✅ `WpfHexEditor.PluginHost` — runtime host: discovery, loading, watchdog, permission service
- ✅ Process-level plugin sandboxing (`WpfHexEditor.PluginSandbox`) — named-pipe IPC, out-of-process stub
- ✅ `.whxplugin` package format + `WpfHexEditor.PluginInstaller` tool + `whxpack` CLI
- ✅ 7 first-party plugins (DataInspector, StructureOverlay, FileStatistics, PatternAnalysis, FileComparison, ArchiveStructure, CustomParserTemplate)
- ✅ `PluginManagerControl` — browse, enable/disable, uninstall loaded plugins
- ✅ `IPluginWithOptions` — optional contract; plugin settings auto-registered in IDE Options under "Plugins"
- Hot-load / Hot-unload plugins at runtime without IDE restart (AssemblyLoadContext collectible — UI not yet exposed)
- Real-time CPU / memory monitoring per plugin in Plugin Manager

### Integrated Terminal
- ✅ `WpfHexEditor.Core.Terminal` — command engine: 31 built-in commands (file, editor, project, panels, plugins, diagnostics)
- ✅ `WpfHexEditor.Terminal` — WPF dockable terminal panel (PowerShell-style prompt, history, colored output)
- Multi-tab terminal with separate shell sessions (PowerShell, Bash, CMD)
- Script file execution (`.hxscript`) with macro recording

### CodeEditor — VS-Like Overkill
- Multi-caret editing, virtual scroll for >1 GB files
- Incremental multi-language syntax highlighting + code folding / gutter with line numbers
- IntelliSense engine: autocomplete, snippets, quick-info, signature help
- Real-time diagnostics tightly integrated with Error Panel and EventBus

### IDE Core Infrastructure
- Service Container / Dependency Injection for all IDE components (#36)
- Global CommandBus — all IDE actions (menus, toolbar, terminal, plugins) routed centrally
- Configurable Keyboard Shortcuts with per-plugin extension support
- Persistent user preferences (global + per-workspace)

### Advanced IDE Modules — Roadmap (#88–#91)
- **#88 Dynamic Snippets** — user/plugin/language-scoped snippets with IntelliSense integration, `SnippetsManager`, `SnippetEditorDialog`
- **#89 AI-Assisted Code Suggestions** — contextual completions, auto-refactoring hints, `AICompletionEngine`, plugin-extensible
- **#90 Integrated Debugger** — breakpoints in gutter, watches, step over/into, `DebugManager`, EventBus integration
- **#91 Git Integration** — inline gutter diff, commit/push/pull/branch panel, `GitManager`, plugin hooks for linters
- Advanced Refactoring: rename symbol, extract method, move class — workspace-wide
- Unit Testing panel: auto-detect frameworks (NUnit/JUnit/PyTest), run by file/project
- Code Analysis & Metrics: dependency graphs, cyclomatic complexity, duplication reports
- Large File Performance: virtualization + lazy parsing + multi-thread IntelliSense
- Multi-User Collaboration / Pair Programming with real-time document sync
- Internationalization / Localization — EN/FR initial, plugin-extensible

---

## [Unreleased] — 2026-03 — Plugin System, Terminal & IDE Enhancements

### ✨ Added — Plugin System (5 new projects)

**`WpfHexEditor.SDK`** — public plugin contract layer
- `IWpfHexEditorPlugin` / `IWpfHexEditorPluginV2` — plugin lifecycle (Init / Activate / Deactivate / Dispose)
- `IIDEHostContext` — full IDE access gateway for plugins
- `IUIRegistry` — panel registration, Show / Hide / Toggle / Focus API
- `IDockingAdapter` — ShowDockablePanel / HideDockablePanel / ToggleDockablePanel / FocusDockablePanel
- Service contracts: `IHexEditorService`, `ICodeEditorService`, `IOutputService`, `IErrorPanelService`, `ISolutionExplorerService`, `IParsedFieldService`
- `IPluginWithOptions` — optional interface; plugins exposing it get an auto-registered Options page under "Plugins"
- `IFocusContextService`, `IPluginEventBus`, `IPluginState`, `IPluginDiagnostics`, `IPermissionService`, `IMarketplaceService`, `IThemeService`
- `PluginManifest` with `ResolvedDirectory` (runtime-only, `JsonIgnore`)

**`WpfHexEditor.PluginHost`** — runtime host infrastructure
- `WpfPluginHost` — discovery (`%AppData%\WpfHexEditor\Plugins\` + `bin\Plugins\`), load, unload, enable/disable
- `PluginEntry` — internal mutation API: `SetState`, `SetInstance`, `SetInitDuration`, `SetLoadedAt`, `SetFaultException`, `Unload()` (clears collectible `AssemblyLoadContext`)
- `PluginCrashHandler` — `HandleCrash` + async `HandleCrashAsync()` overload
- `PluginWatchdog` — `WrapAsync()` returns `Task<TimeSpan>` (elapsed time)
- `PluginOptionsRegistry` — thread-safe runtime registry of plugin options pages keyed by plugin ID
- `PluginLoadContext` — collectible `AssemblyLoadContext` for hot-unload
- `PluginManifestValidator`, `PluginDiagnosticsCollector`, `PluginScheduler`, `PluginEventBus`, `PluginStateSerializer`
- `PermissionService` — `PermissionChangedEventArgs` via object-initializer (PluginId, Permission, IsGranted)
- `UIRegistry` — delegates Show/Hide/Toggle/Focus to `IDockingAdapter`
- `PluginManagerControl.xaml` — enable/disable/uninstall actions, live status, `PluginListItemViewModel` (LoadedAt, AssemblyPath, OptionsPageFactory)
- `SandboxPluginProxy` — out-of-process proxy over named-pipe `IpcChannel`

**`WpfHexEditor.PluginSandbox`** — isolated host process (stub, `net8.0-windows`)
- Console host entry point; named-pipe IPC with main IDE process

**`WpfHexEditor.Core.Terminal`** — command engine (31 built-in commands)
- Category **Core**: `clear`, `echo`, `exit`, `help`, `history`, `version`
- Category **File System**: `copy-file`, `delete-file`, `list-files`, `open-file`, `open-folder`
- Category **Editor**: `close-file`, `read-hex`, `save-file`, `save-as`, `search`, `select-file`, `write-hex`
- Category **Project / Solution**: `close-project`, `close-solution`, `open-project`, `open-solution`, `reload-solution`
- Category **Panels**: `append-panel`, `clear-panel`, `close-panel`, `focus-panel`, `open-panel`, `toggle-panel`
- Category **Plugins**: `plugin-list`, `run-plugin`
- Category **Diagnostics**: `send-error`, `send-output`, `show-errors`, `show-logs`, `status`
- `HxScriptEngine` — executes `.hxscript` files; `CommandHistory` with persistence

**`WpfHexEditor.Terminal`** — WPF dockable terminal panel
- `TerminalPanel.xaml` — VS-style dockable panel, prompt input, colored output
- `TerminalPanelViewModel` — implements both `ITerminalContext` and `ITerminalOutput`; `TerminalOutputLine` with severity-to-brush converter

**7 First-party plugin packages** (`Sources/Plugins/`)
- `WpfHexEditor.Plugins.DataInspector` — wraps DataInspectorPanel; implements `IPluginWithOptions` (`DataInspectorOptionsPage`: display format, endianness, auto-refresh interval)
- `WpfHexEditor.Plugins.StructureOverlay` — wraps StructureOverlayPanel
- `WpfHexEditor.Plugins.FileStatistics` — wraps FileStatisticsPanel
- `WpfHexEditor.Plugins.PatternAnalysis` — wraps PatternAnalysisPanel
- `WpfHexEditor.Plugins.FileComparison` — wraps FileComparisonPanel
- `WpfHexEditor.Plugins.ArchiveStructure` — wraps ArchiveStructurePanel
- `WpfHexEditor.Plugins.CustomParserTemplate` — wraps CustomParserTemplatePanel

**Packaging tools** (`Sources/Tools/`)
- `WpfHexEditor.PackagingTool` — `whxpack` CLI, `ManifestFinalizer` (SHA-256), `PackageBuilder` → `.whxplugin` (ZIP)
- `WpfHexEditor.PluginInstaller` — WPF installer dialog, `PluginPackageExtractor`, `--silent` mode, `PluginInstallException`

### ✨ Added — App Integration (Plugin System)
- `MainWindow.PluginSystem.cs` — `InitializePluginSystemAsync`, `ShutdownPluginSystemAsync`, `UpdatePluginFocusContext`
- `OnOpenTerminal()` — creates `TerminalPanel` + `TerminalPanelViewModel`, docks as `"panel-terminal"`
- `OnOpenPluginManager()` — docks `PluginManagerControl` as `"plugin-manager"`
- **Tools menu** — Plugin Manager + Terminal entries; toolbar separators with `DataTrigger`-controlled visibility
- Dynamic Options registration — each plugin implementing `IPluginWithOptions` gets `OptionsPageRegistry.RegisterDynamic()` on load; `UnregisterDynamic()` on disable/unload

### ✨ Added — Options Module
- `HexEditorBehaviorPage.xaml(.cs)` — new Options page: data interpretation, scroll markers, advanced behavior
- `HexEditorStatusBarPage.xaml(.cs)` — new Options page: status bar element visibility toggles
- `PluginSystemOptionsPage.xaml(.cs)` — plugin loading settings, sandbox mode, auto-update policy
- `OptionsPageRegistry.RegisterDynamic(category, pageName, factory)` / `UnregisterDynamic(pageId)` — runtime plugin options page registration
- `AppSettings` — expanded with `SolutionExplorerSettings`, `CodeEditorDefaultSettings`, `TextEditorDefaultSettings`, plugin system settings (~30 new properties)

### ✨ Added — Service Layer
- `DockingAdapter` — `ShowDockablePanel`, `HideDockablePanel`, `ToggleDockablePanel`, `FocusDockablePanel` (uses `DockItemState.Hidden` check)
- `ErrorPanelServiceImpl` — `PostDiagnostic` now accepts `DiagnosticSeverity` enum (was string); maps SDK→Core severity; uses `AddSource`
- `SolutionExplorerServiceImpl` — `OpenFileHandler` delegate avoids circular App↔SDK reference
- `HexEditorServiceImpl` — `ReadBytes`, `GetSelectedBytes`, `SearchHex`, `SearchText`, `GoToOffset`

### ✨ Added — Docking Improvements
- `AutoHideBar` — `SnapshotReady` event (fires after open animation + Render priority) and `Dismissing` event (before close animation)
- `DockControl` — wires `SnapshotReady`/`Dismissing`; `MinCaptureSize` guard in `CaptureAutoHideSnapshot`
- `TabHoverPreview` — title-only fallback card for never-activated tabs; hides popup on selected tab; title from `DockItem.Tag`
- `AutoHideBarHoverPreview` — button-relative placement replaces `PlacementMode.Mouse` (Win32 layered window bug)
- `DockTabControl` — float threshold check moved before reorder-dispatch; vertical drag cancels reorder and floats instead
- `DockDragManager` — guard for `DocumentTabHost.Node == null`

### ✨ Added — Solution Explorer Improvements
- **Expand-state persistence** — captures/restores `IsExpanded` per node across `Rebuild()` in same session
- **Drag & drop from Windows Explorer** — `DataFormats.FileDrop` handled; files added to project without dialog
- **Delete from disk** — `ItemDeleteFromDiskRequested` event; moves to Recycle Bin with confirmation

### ✨ Added — HexEditor Enhancements
- `HexEditor.StatusBarContributor` — `RaiseHexStatusChanged()` fired on `ByteGrouping`, `OffSetStringVisual`, `DefaultCopyToClipboardMode` property changes
- net48 compatibility — `(CopyPasteMode[])Enum.GetValues(typeof(CopyPasteMode))` (generic `Enum.GetValues<T>()` not available in net48)

### ✨ Added — Format-Aware Editor Selection
- `EmbeddedFormatCatalog` — parses `preferredEditor` and `detection.isTextFormat` from `.whfmt` JSON
- `EmbeddedFormatEntry` record — `string? PreferredEditor` and `bool IsTextFormat`
- `EditorRegistry.FindFactory(string filePath, string? preferredId)` — preferred-first, fallback first-match
- 427 `.whfmt` format definitions annotated with `"preferredEditor"` key
- `MainWindow.GetPreferredEditorId()` — consults `EmbeddedFormatCatalog` (PreferredEditor → IsTextFormat → null)

### 🔧 Changed
- `PluginEntry` promoted from `internal` to `public`
- Section separators normalized from Unicode box-drawing (`──`) to ASCII (`--`) across 134 files

### 🐛 Fixed
- `PermissionService` — `PermissionChangedEventArgs` object-initializer form; missing `System.IO` using
- `PluginManifestValidator` — constructor signature and `result.IsValid` (was `!result.HasErrors`)
- `SolutionExplorerPanel` — CS0136 variable scope collision in `OnTreeDragOver`

---

## [Unreleased] — 2026-03 — IDE & Project System

### ✨ Added — Project System
- **Solution & Project management** (`.whsln` / `.whproj` formats) with `SolutionManager` singleton
- **Virtual & physical folders** in Solution Explorer — `PhysicalFolderNodeVm`, `PhysicalFileNodeVm`
- **Show All Files** mode — scans disk recursively and shows untracked files at 45% opacity
- **Format versioning pipeline** — `IFormatMigrator` + `MigrationPipeline` (current version: 2)
- **V1→V2 format migration** — in-memory only, file never modified automatically
- **Atomic upgrade** — `UpgradeFormatAsync` creates `.v{N}.bak` backups before writing
- **Read-only format mode** — `ISolution.IsReadOnlyFormat` blocks saves on unupgraded files
- **File templates** — Binary, TBL, JSON, Text with `FileTemplateRegistry`
- **`IEditorPersistable`** interface — bookmarks, scroll, caret, encoding restored per file
- **`IItemLink`** — typed links between project items (e.g. `.bin` ↔ `.tbl`)

### ✨ Added — IDE Panels
- **Error Panel** (`IErrorPanel`) — VS-style diagnostics panel with severity filtering and navigation
- **`IDiagnosticSource`** interface — any editor can push diagnostics to the Error Panel
- **JsonEditor diagnostics** — real-time JSON validation errors forwarded to Error Panel
- **TblEditor diagnostics** — validation errors forwarded to Error Panel
- **`ERR_*` theme keys** — Error Panel colors in all 8 themes

### ✨ Added — Search
- **QuickSearchBar** — inline Ctrl+F overlay (VSCode-style), no dialog popup
- **Ctrl+Shift+F** → opens full AdvancedSearchDialog (5 search modes)
- **"⋯" button** in QuickSearchBar — closes bar and opens AdvancedSearchDialog

### ✨ Added — IDE
- **WelcomePanel** — VS Start Page-style welcome document shown on IDE launch (`doc-welcome` ContentId)
  - Quick Actions: New File, Open File, Open Project / Solution, Options
  - Recent Files & Recent Projects filtered to physically existing paths only (deleted entries hidden)
  - Live changelog fetched from GitHub (`raw.githubusercontent.com`) — loading state + offline fallback
  - Resources section: GitHub Repository + Report an Issue buttons (opens default browser)
  - App `Logo.ico` displayed in header
  - Full theme compliance via existing `DockBackgroundBrush` / `DockTabActiveBrush` / `DockMenuForegroundBrush` keys
  - Fix: `Loaded -= OnLoaded` prevents dialog-loop caused by Click-handler accumulation on docking re-attach

### ✨ Added — Themes & UI
- **`PanelCommon.xaml`** — shared panel toolbar styles (30px VS-style toolbar, Segoe MDL2 icon buttons)
- **`Panel_*` theme keys** — `ToolbarBrush`, `ToolbarBorderBrush`, `ToolbarButtonHoverBrush`, etc. in all 8 themes
- **ParsedFieldsPanel** refactored to VS-style toolbar (draggable title bar removed)
- **`PFP_*` theme keys** — ParsedFieldsPanel colors in all 8 themes

### ✨ Added — Docking
- **Tab colorization** — per-tab custom color with `TabSettingsDialog`
- **Left / right tab strip placement** — configurable per dock group
- **`TabSettingsDialog`** — color picker + placement selector

### ✨ Added — HexEditor API
- **`LoadTBL(string)`** public API — load a TBL file programmatically
- **Auto-apply project TBL** — when a `.bin` is opened and its project has a linked `.tbl`, it is applied automatically
- **`TblEditorRequested`** event — opens TBL Editor without circular reference (Core→TblEditor)

### 🔧 Changed
- `SolutionExplorer` moved to `WpfHexEditor.Panels.IDE` (was `WpfHexEditor.WindowPanels`)
- `ParsedFieldsPanel` — `TitleBarDragStarted` event removed; docking system handles floating
- App status bar — HexEditor internal status bar hidden (`ShowStatusBar = false`); App owns the status bar

---

## [2.7.0] — 2026-02 — IDE Application & Editor Plugin System

### ✨ Added — WpfHexEditor.App
- **Full IDE application** with VS-style docking (`WpfHexEditor.Docking.Wpf`)
- **`IDocumentEditor`** plugin contract — Undo, Redo, Copy, Cut, Paste, Save, IsDirty, IsReadOnly
- **`EditorRegistry`** — plugin registration at startup (`EditorRegistry.Instance.Register(...)`)
- **Content factory with cache** — `Dictionary<string, UIElement>` keyed by `ContentId`
- **`ActiveDocumentEditor`** INPC property — drives Edit menu bindings
- **`ActiveHexEditor`** INPC property — drives status bar DataContext
- **VS2022-style status bar** — left: editor status messages · center: EditMode + BytePerLine · right: panel count
- **Auto-sync** via `DockHost.ActiveItemChanged` → connects/disconnects ParsedFieldsPanel per active tab

### ✨ Added — Editors
- **TBL Editor** (`WpfHexEditor.Editor.TblEditor`) — standalone `TblEditorControl`, implements `IDocumentEditor`
  - Virtualized DataGrid, inline Ctrl+F search overlay, status bar
  - `TblExportService.ExportToTblFile()` for save/export
- **JSON Editor** (`WpfHexEditor.Editor.JsonEditor`) — implements `IDocumentEditor` + `IDiagnosticSource`
  - Real-time validation, `PerformValidation()`, `EnableValidation` toggle
- **Text Editor** (`WpfHexEditor.Editor.TextEditor`) — implements `IDocumentEditor` + `IEditorPersistable`
  - Caret/scroll persistence (1-based in DTO, 0-based in VM)

### ✨ Added — Panels
- **ParsedFieldsPanel** singleton — auto-connects to active HexEditor via `IParsedFieldsPanel` interface
- **DataInspectorPanel** — 40+ byte type interpretations at caret
- **SolutionExplorerPanel** — hierarchical VM tree, `ISolutionExplorerPanel`
- **PropertiesPanel** — context-aware F4 panel via `IPropertyProvider` / `IPropertiesPanel`

### ✨ Added — HexEditor IDocumentEditor
- `HexEditor` implements `IDocumentEditor` via `HexEditor.DocumentEditor.cs`
- `RaiseHexStatusChanged()` — fires `StatusMessage` after load, edit mode change, bytes-per-line change
- `IDiagnosticSource` implementation — exposes `HexEditor.DiagnosticSource.cs`
- `IEditorPersistable` implementation — bookmarks, scroll, caret, encoding, edit mode

### 🔧 Changed — Docking Engine
- `CreateTabControl` — all `DockGroupNode` panels always get title bar + `TabStripPlacement = Dock.Bottom`
- `FloatGroup()` — `GroupFloated` event fires **before** `LayoutChanged` (prevents duplicate windows)
- `FindWindowForItem` — checks both active item and non-active group items
- `RestoreFloatingWindows()` — called by `RebuildVisualTree()` for persisted float positions
- `FloatLeft / FloatTop / LastDockSide` serialized in `DockItemDto`

---

## [2.6.0] — 2026-02-22 — V1 Legacy Removal & Multi-Byte Fixes

### 🚨 Breaking — V1 Legacy Removed
Complete removal of Legacy V1 code after 12-month deprecation period:
- `HexEditorLegacy.xaml/.cs` (6,521 LOC)
- `ByteProviderLegacy.cs` (1,890 LOC)
- 6 legacy sample projects (5,079 LOC)
- V1 rendering classes: `BaseByte`, `HexByte`, `StringByte`, `FastTextLine` (2,051 LOC)
- V1 search dialogs: `FindWindow`, `FindReplaceWindow`

**Total removed: 17,093 lines of code (-30% codebase)**

`CompatibilityLayer` (725 LOC) is **kept** for API backward compatibility — zero migration required.

### 🐛 Fixed — Multi-Byte Mode (ByteSize 16/32)
- Click positioning in Bit16/32 modes for both hex and ASCII panels
- Keyboard navigation left/right in multi-byte modes
- Unified hit testing via single `HitTestByteWithArea` method (click, mouseover, drag, auto-scroll)
- ASCII mouseover alignment in multi-byte modes
- `GetDisplayCharacter` — returns all bytes in multi-byte groups (e.g. `"MZ"` instead of `"M"`)
- `GetCharacterDisplayWidth` — FormattedText measurement for pixel-perfect alignment
- ByteOrder setting respected in ASCII panel (HiLo/LoHi)
- TBL hex key building in multi-byte mode uses `Values[]` array

### ✨ Added
- **`RestoreOriginalByte(long)`** — restore a single modified byte to its original value
- **`RestoreOriginalBytes(long[])`** / **`RestoreOriginalBytes(IEnumerable<long>)`** — batch restore
- **`RestoreOriginalBytesInRange(long, long)`** — restore a contiguous range
- **`RestoreAllModifications()`** — clear all modifications at once
- Full Undo/Redo integration for restore operations
- Category localization **`CategoryKeyboardMouse`** added to all 19 language files

---

## [2.5.0] — 2026-02-14 — Critical Bug Fixes & V2 Architecture

### 🐛 Fixed — Critical
**Issue #145 — Insert Mode Hex Input** ✅
- Typing consecutive hex chars in Insert Mode now works: `FFFFFFFF` → `FF FF FF FF` (was `F0 F0 F0 F0`)
- Root cause: `PositionMapper.PhysicalToVirtual()` returned wrong position for inserted bytes
- Files: `PositionMapper.cs`, `ByteReader.cs`, `ByteProvider.cs`

**Save Data Loss Bug** ✅
- Catastrophic data loss (MB → KB file corruption) fully resolved
- Same PositionMapper bug caused `ByteReader` to read wrong bytes during save
- Fast save path added for modification-only edits (10-100x faster)

### 🚀 Performance
- **10-100x faster save** — debug logging overhead removed from all production paths
- **Fast save path** — modification-only edits bypass full virtual read

### 🏗️ Architecture
- **MVVM + 16 specialized services**
- Extracted 2,500+ lines of business logic into services
- 80+ unit tests (xUnit, .NET 8.0-windows)

---

## [2.2.0] — 2026-01 — Search Performance & Service Architecture

### 🚀 Performance
- **LRU Search Cache** — 10-100x faster repeated searches, O(1) lookup, auto-invalidation at all 11 modification points
- **Parallel Multi-Core Search** — 2-4x faster for files > 100MB, automatic threshold
- **SIMD Vectorization** (net5.0+) — AVX2/SSE2, 4-8x faster single-byte search, processes 32 bytes/cycle
- **Span\<T\> + ArrayPool** — 2-5x faster, 90% less memory allocation
- **Profile-Guided Optimization** (.NET 8.0+) — 10-30% CPU boost, 30-50% faster startup

### 🏗️ Architecture — Service Layer (10 Services)
`ClipboardService` · `FindReplaceService` · `UndoRedoService` · `SelectionService` · `HighlightService` · `ByteModificationService` · `BookmarkService` · `TblService` · `PositionService` · `CustomBackgroundService`

### 🧪 Testing
- 80+ unit tests: `SelectionServiceTests` (35), `FindReplaceServiceTests` (35), `HighlightServiceTests` (10+)

---

## [2.1.0] — 2025 — V2 Rendering Engine

### ✨ Added
- `HexEditorV2` control with custom `DrawingContext` rendering
- MVVM architecture with `HexEditorViewModel`
- True Insert Mode with virtual position mapping
- Custom background blocks for byte range highlighting
- BarChart visualization mode

### 🚀 Performance
- Rendering: **99% faster** vs V1 (DrawingContext vs ItemsControl)
- Memory: **80-90% reduction**

---

## [2.0.0] — 2024 — Multi-Targeting & Async

### ✨ Added
- .NET 8.0-windows support
- Multi-targeting: .NET Framework 4.8 + .NET 8.0-windows
- Async file operations with progress and cancellation
- C# 12 / 13 language features

---

## [1.x] — 2023 and earlier

Legacy V1 monolithic architecture. See [GitHub Releases](https://github.com/abbaye/WpfHexEditorIDE/releases) for historical notes.

V1 NuGet package (`WPFHexaEditor`) remains available for existing users but is no longer maintained. See [Migration Guide](docs/migration/MIGRATION.md).

---

## Legend

| Icon | Meaning |
|------|---------|
| 🚀 | Performance improvement |
| 🐛 | Bug fix |
| ✨ | New feature |
| 🔧 | Internal change / refactor |
| 🏗️ | Architecture change |
| 🧪 | Testing |
| 🚨 | Breaking change |
