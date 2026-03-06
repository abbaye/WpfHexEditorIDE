# Changelog

All notable changes to **WpfHexEditor** are documented here.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) · Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html)

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
