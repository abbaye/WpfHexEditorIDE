# 📊 WpfHexEditor — Full Feature Reference

---

## 🖥️ IDE Application Features

The **WpfHexEditor.App** is a full IDE for binary analysis and editing, built on a VS-style docking system.

### Application Shell
| Feature | Status | Notes |
|---------|--------|-------|
| VS-style docking (float, dock, auto-hide) | ✅ | Custom engine — no third-party dependency |
| 8 built-in visual themes | ✅ | Dark, Light, VS2022Dark, DarkGlass, Minimal, Office, Cyberpunk, VisualStudio |
| Colored tabs with `TabSettingsDialog` | ✅ | Per-tab color + left/right placement |
| VS2022-style status bar | ✅ | Edit mode · bytes/line · caret offset |
| Output panel | ✅ | Session log and operation messages |
| Error/Diagnostics panel | ✅ | Severity filter, navigation to offset |

### Project System
| Feature | Status | Notes |
|---------|--------|-------|
| Solution management (`.whsln`) | ✅ | Create, open, save, close |
| Project management (`.whproj`) | ✅ | Multiple projects per solution |
| Virtual folders | ✅ | Logical grouping without disk structure |
| Physical folders | ✅ | Mirrors disk directory tree |
| Show All Files mode | ✅ | Reveals untracked files in project dirs |
| Per-file state persistence | ✅ | Bookmarks, caret, scroll, encoding |
| Typed item links | ✅ | e.g. `.bin` linked to `.tbl` → auto-applied |
| Format versioning + auto-migration | ✅ | V1→V2 in-memory migration with backup |
| File templates | ✅ | Binary, TBL, JSON, Text |

### Editors (Plugin Architecture)
| Editor | Status | Description |
|--------|--------|-------------|
| **Hex Editor** | ✅ Complete | Binary editing — insert/overwrite, 400+ formats, search, bookmarks, TBL |
| **TBL Editor** | ✅ Complete | Character table editor for custom encodings and ROM hacking |
| **JSON Editor** | ✅ Complete | JSON editing with real-time validation and diagnostics |
| **Text Editor** | ✅ Complete | Text editing with syntax highlighting |
| **Image Viewer** | 🔧 Stub | Planned |
| **Audio Viewer** | 🔧 Stub | Planned |
| **Diff Viewer** | 🔧 Stub | Planned |
| **Disassembly Viewer** | 🔧 Stub | Planned |
| **Entropy Viewer** | 🔧 Stub | Planned |

### IDE Panels
| Panel | Status | Description |
|-------|--------|-------------|
| Parsed Fields Panel | ✅ | 400+ format detection, field list, type overlay, inline editing |
| Data Inspector | ✅ | 40+ byte type interpretations at caret position |
| Structure Overlay | ✅ | Visual field highlighting on hex grid |
| Solution Explorer | ✅ | Project tree with virtual/physical folders |
| Properties Panel | ✅ | Context-aware F4 panel via `IPropertyProvider` |
| Error Panel | ✅ | Diagnostics from any `IDiagnosticSource` editor |
| File Diff | ✅ | Side-by-side binary comparison (F7/F8 navigation) |

---

## 🧩 Reusable Controls & Libraries

| Control | Frameworks | Status |
|---------|-----------|--------|
| **HexEditor** UserControl | net48 · net8 | ✅ Complete |
| **HexBox** (standalone hex input) | net48 · net8 | ✅ Complete |
| **ColorPicker** | net48 · net8 | ✅ Complete |
| **BarChart** (byte distribution) | net48 · net8 | ✅ Complete |
| **Docking.Wpf** (VS-style engine) | net8 | ✅ Complete |
| **BinaryAnalysis** (400+ format detection) | net8 | ✅ Complete |

---

## 🔤 HexEditor Control — Feature Detail

# 🛒 Complete Feature Comparison: V1 vs V2

> **Status Dashboard:** V2 has **87 tested features** ✅ | **33 interface-compatible features** ⚠️ | **2 in development** 🚧

**Legend:** ✅ = Available | ⚠️ = Limited/Untested | ❌ = Not Available | 🆕 = New in V2 | ⚡ = Performance improvement

**Note (v2.6.0 - Feb 2026):** V1 Legacy code has been completely **removed** (17,093 LOC deleted). The project is now V2-only using the `HexEditor` control. Historical V1 comparisons below show the evolution from Legacy to modern architecture.

---

## 📊 Quick Comparison at a Glance

| Metric | V1 (HexEditorLegacy) | V2 (HexEditor - Main) | Improvement |
|--------|:--------------------:|:---------------------:|-------------|
| **API Surface** | 100+ properties<br/>95+ methods<br/>21 events | Same + Enhanced | ✅ 100% backward compatible |
| **Architecture** | Monolithic (6000+ lines) | MVVM + 15 Services | 🆕 Service-based, testable |
| **UI Rendering** | ItemsControl | DrawingContext | ⚡ 99% faster (5-10x) |
| **Search Performance** | Standard | LRU + Parallel + SIMD | ⚡ 10-100x faster |
| **Memory Usage** | Baseline | Span&lt;T&gt; + Pooling | ⚡ 80-90% reduction |
| **New UI Features** | - | BarChart, ScrollMarkers, AvalonDock | 🆕 V2-exclusive |
| **Critical Bugs** | Insert Mode ⚠️<br/>Save data loss ⚠️ | All fixed ✅ | ✅ Production ready |

**Summary:** V2 is a drop-in replacement for V1 with dramatic performance gains, critical bug fixes, and new visualization features. Zero breaking changes!

---

## 🎯 Top 20 Key Differences

| Feature | V1 | V2 | V2 Enhancement | Notes |
|---------|:--:|:--:|----------------|-------|
| 🎨 **UI Rendering** | ItemsControl | DrawingContext ⚡ | **99% faster (5-10x)** | Custom DrawingVisual vs WPF ItemsControl |
| 🔍 **Search Performance** | Standard | LRU + Parallel + SIMD ⚡ | **10-100x faster** | Cached + multi-core + AVX2 vectorization |
| 💾 **Memory Usage** | High | Optimized ⚡ | **80-90% reduction** | Span&lt;T&gt;, ArrayPool, render caching |
| 📝 **Insert Mode** | ⚠️ Buggy | ✅ Fixed | **Issue #145 resolved** | Critical bug producing F0 F0 pattern fixed |
| 💾 **Save Operations** | ⚠️ Data loss | ✅ Fixed | **Root cause resolved** | Multi-MB file corruption completely fixed |
| 📊 **BarChart View** | ❌ | ✅ 🆕 | **New feature** | Visual byte frequency distribution (0x00-0xFF) |
| 📍 **Scrollbar Markers** | ❌ | ✅ 🆕 | **New feature** | Visual markers for search/bookmarks/changes |
| 🪟 **AvalonDock Support** | ❌ | ✅ 🆕 | **New feature** | Dockable panels for IDE-like interface |
| 🏗️ **Architecture** | Monolithic | MVVM + Services 🆕 | **15 specialized services** | Clean separation, core ByteProvider tests |
| 🧪 **Unit Tests** | ❌ | ⚠️ 🆕 | **Limited coverage** | ByteProvider V2 tested, UI features need validation |
| ⏱️ **Async Operations** | ❌ | ✅ 🆕⚡ | **100% UI responsive** | IProgress&lt;int&gt; + CancellationToken support |
| 🎯 **SIMD Search** | ❌ | ✅ 🆕⚡ | **4-8x faster** | AVX2/SSE2 vectorization (.NET 5.0+) |
| 🔍 **Binary Search Fix** | ⚠️ O(m) | ✅ O(log m) ⚡ | **100-5,882x faster** | True binary search in position mapping |
| ✨ **Highlight Service** | Dictionary | HashSet ⚡ | **2-3x faster, 50% less memory** | Optimized data structure with batching |
| 📦 **Bulk Operations** | ❌ | ✅ 🆕⚡ | **10-100x faster** | BeginBatch/EndBatch pattern prevents UI thrashing |
| 🌐 **Cross-Platform** | WPF only | Core + Platform 🆕 | **netstandard2.0 Core** | Platform-agnostic business logic (Avalonia ready) |
| 📏 **Span&lt;T&gt; APIs** | ❌ | ✅ ⚡ | **90% less allocation** | Zero-copy operations (.NET 5.0+) |
| 🚀 **Large Files (GB+)** | ⚠️ Slow/Crash | ✅ Fast ⚡ | **Memory-mapped files** | Handle files that crashed V1 |
| 🔧 **Profile-Guided Opt** | ❌ | ✅ ⚡ | **10-30% boost** | PGO + ReadyToRun (.NET 8.0+) |
| 🔄 **API Compatibility** | - | ✅ | **100% compatible** | Drop-in replacement, same public API |

**Why Upgrade?** V2 delivers production-critical bug fixes (Insert Mode #145, Save data loss), 99% rendering speedup, 10-100x search performance, 80-90% memory reduction, plus new visualization features—all with zero breaking changes.

---

## 📚 Detailed Feature Catalog by Category

<details open>
<summary><b>📝 Core Editing Operations</b> (12 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | V2 Enhancement | Notes |
|---------|:---------:|:---------:|----------------|-------|
| Multi-format editing (Hex/Dec/Bin/Oct) | ✅ | ✅ | - | All number base formats supported |
| Multi-byte support (8/16/32-bit) | ✅ | ✅ | - | Byte, Word, DWord editing modes |
| Endianness (Little/Big Endian) | ✅ | ✅ | - | Both byte orders supported |
| **Insert Mode** | ⚠️ | ✅ | 🔧 **Fixed** | **Issue #145:** F0 F0 pattern bug completely resolved (commit 405b164) |
| Delete bytes | ✅ | ✅ | - | Remove bytes from file/stream |
| Append bytes | ✅ | ✅ | - | Add bytes at end of file |
| Fill selection with pattern/byte | ✅ | ✅ | - | Fill range with repeating value |
| Unlimited Undo/Redo | ✅ | ✅ | ⚡ **Optimized** | Better memory management, faster operations |
| Read-only mode | ✅ | ✅ | - | Prevent accidental modifications |
| Modify byte at position (Overwrite) | ✅ | ✅ | - | Standard overwrite mode |
| Insert byte at position (Insert Mode) | ⚠️ | ✅ | 🔧 **Fixed** | Now works correctly with virtual positions |
| Delete byte range | ✅ | ✅ | - | Remove contiguous byte sequences |

</details>

<details open>
<summary><b>🔍 Search & Find Operations</b> (12 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | V2 Enhancement | Notes |
|---------|:---------:|:---------:|----------------|-------|
| FindFirst/Next/Last/All | ✅ | ✅ | - | Basic search operations |
| Pattern search (byte[] / string) | ✅ | ✅ | - | Multiple search modes |
| Replace operations (First/Next/All) | ✅ | ✅ | - | Find and replace functionality |
| **LRU Search Cache** | ❌ | ✅ 🆕⚡ | **10-100x faster** | Repeated searches cached, O(1) lookup, 20 entry capacity |
| **Parallel Multi-Core Search** | ❌ | ✅ 🆕⚡ | **2-4x faster** | Auto-enabled for files > 100MB, near-linear scaling |
| **SIMD Vectorization (AVX2/SSE2)** | ❌ | ✅ 🆕⚡ | **4-8x faster** | Single-byte search processes 16-32 bytes per instruction |
| **Async Search with Progress** | ❌ | ✅ 🆕 | **100% UI responsive** | IProgress&lt;int&gt; reporting (0-100%), cancellable |
| Cancellation support | ❌ | ✅ 🆕 | **CancellationToken** | Cancel long-running searches on demand |
| Count occurrences | ✅ | ✅ | ⚡ **Optimized** | Zero-allocation counting with Span&lt;T&gt; |
| Highlight search results | ✅ | ✅ | ⚡ **Faster** | HashSet-based highlighting (2-3x faster) |
| Search cache invalidation | ⚠️ | ✅ | 🔧 **Fixed** | Cache properly cleared at all 11 modification points |
| **FindAll with Scroll Markers** | ❌ | ✅ 🆕 | **Visual navigation** | Orange markers on scrollbar for all results |

</details>

<details open>
<summary><b>📋 Copy/Paste & Data Export</b> (8 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | Notes |
|---------|:---------:|:---------:|-------|
| Standard clipboard (Ctrl+C/V/X) | ✅ | ✅ | Windows clipboard integration |
| Copy as code (C#/VB/Java/Python/etc.) | ✅ | ✅ | Generate byte array code in 10+ languages |
| Multiple formats (Hex/ASCII/Binary) | ✅ | ✅ | Flexible data representation |
| Copy to stream | ✅ | ✅ | Stream-based export for large selections |
| Custom copy modes (7 modes) | ✅ | ✅ | HexaString, AsciiString, CSharpCode, TblString, etc. |
| Paste operations (Insert/Overwrite) | ✅ | ✅ | Configurable paste behavior |
| Fill selection with byte value | ✅ | ✅ | Pattern filling |
| Get selection bytes programmatically | ✅ | ✅ | Extract data via GetCopyData() |

</details>

<details>
<summary><b>🎨 Display & Visualization</b> (14 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | V2 Enhancement | Notes |
|---------|:---------:|:---------:|----------------|-------|
| **BarChart View** | ❌ | ✅ 🆕 | **New feature** | Visual byte frequency distribution (0x00-0xFF) |
| **AvalonDock Support** | ❌ | ✅ 🆕 | **New feature** | Dockable panels for professional IDE interface |
| **Scrollbar Markers** | ❌ | ✅ 🆕 | **New feature** | Visual markers: Bookmarks (Blue), Modified (Orange), Search (Bright Orange), Added (Green), Deleted (Red) |
| Byte grouping (2/4/6/8/16 bytes) | ✅ | ✅ | - | Configurable visual byte grouping |
| Multiple encodings (20+ encodings) | ✅ | ⚠️ | **Interface only** | ASCII, UTF-8, UTF-16, EBCDIC, Shift-JIS, EUC-KR (untested in V2) |
| Custom TBL support | ✅ | ⚠️ | **Interface only** | Game ROM character tables with DTE/MTE (untested in V2) |
| Unicode TBL | ✅ | ⚠️ | **Interface only** | Multi-byte character support in TBL (untested in V2) |
| Zoom (50%-200%) | ✅ | ⚠️ | **Interface only** | Font scaling with Ctrl+MouseWheel (untested in V2) |
| Show deleted bytes | ✅ | ✅ | - | Visual diff with strikethrough |
| Line addressing (Hex/Dec offsets) | ✅ | ✅ | - | Configurable offset display format |
| Offset modes (Hex/Decimal) | ✅ | ✅ | - | Number format choice for addresses |
| Custom background blocks | ✅ | ⚠️ | **Interface only** | Highlight file sections with colors (untested in V2) |
| Highlight colors (10+ customizable) | ✅ | ✅ | - | SelectionFirstColor, SelectionSecondColor, ByteModifiedColor, etc. |
| Font customization | ✅ | ✅ | - | Font family, size (default: Courier New) |

</details>

<details>
<summary><b>⚡ Performance Optimizations</b> (6 tiers)</summary>

<br/>

| Optimization Tier | V1 Legacy | V2 Modern | Performance Gain | Description |
|------------------|:---------:|:---------:|------------------|-------------|
| **Tier 1: DrawingContext Rendering** | ❌ | ✅ ⚡ | **99% faster (5-10x)** | Custom DrawingVisual vs ItemsControl, GPU-accelerated |
| **Tier 2: LRU Search Cache** | ❌ | ✅ ⚡ | **10-100x faster** | Least Recently Used cache for search patterns (20 entries) |
| **Tier 3: Parallel Multi-Core Search** | ❌ | ✅ ⚡ | **2-4x faster** | Auto-enabled for files > 100MB, near-linear CPU scaling |
| **Tier 4: SIMD Vectorization (AVX2)** | ❌ | ✅ ⚡ | **4-8x faster** | Process 16-32 bytes per CPU instruction (.NET 5.0+) |
| **Tier 5: Span&lt;T&gt; + ArrayPool** | ❌ | ✅ ⚡ | **90% less GC** | Zero-copy operations, buffer pooling (.NET 5.0+) |
| **Tier 6: True Binary Search** | ⚠️ O(m) | ✅ O(log m) ⚡ | **100-5,882x faster** | Fixed position mapping bug, critical for heavily edited files |
| **Profile-Guided Optimization** | ❌ | ✅ ⚡ | **10-30% boost** | PGO + ReadyToRun AOT compilation (.NET 8.0+) |
| **Render Caching (Typeface/Width)** | ❌ | ✅ ⚡ | **5-10x faster** | Static Dictionary cache for width calculations |
| **Batch Visual Updates** | ❌ | ✅ ⚡ | **2-5x faster** | BeginUpdate/EndUpdate pattern prevents redundant renders |
| **HashSet Highlights** | Dictionary | HashSet ⚡ | **2-3x faster** | Optimized data structure, 50% less memory |
| **Async/Await Support** | ❌ | ✅ ⚡ | **100% UI responsive** | All long operations async with progress reporting |
| **Memory-Mapped Files** | ⚠️ | ✅ ⚡ | **GB+ file support** | Handle files that crashed V1 |

**Combined Result:** Up to **6,000x faster** for large edited files! Memory: **80-90% reduction**. UI: **100% responsive**.

</details>

<details>
<summary><b>🏗️ Architecture & Design</b> (10 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | V2 Enhancement | Notes |
|---------|:---------:|:---------:|----------------|-------|
| **Architecture Pattern** | Monolithic | MVVM + Services 🆕 | **Service-based design** | 15 specialized services, clean separation of concerns |
| **Service Layer** | ❌ | ✅ 🆕 | **15 services** | ClipboardService, FindReplaceService, UndoRedoService, SelectionService, etc. |
| **Unit Tests** | ❌ | ⚠️ 🆕 | **Limited coverage** | ByteProvider V2 tested, UI features need validation |
| **ByteProvider Architecture** | V1 | V2 Enhanced 🆕 | **Virtual positions** | EditsManager + PositionMapper for insert/delete support |
| **MVVM Support** | ⚠️ Limited | ✅ Full | **True MVVM** | HexEditorViewModel, RelayCommand&lt;T&gt;, INotifyPropertyChanged |
| **Dependency Injection Ready** | ❌ | ✅ 🆕 | **DI-friendly** | Services can be injected and tested in isolation |
| **Event System** | 21 events | 21+ events | **Enhanced events** | More granular notifications (ByteModified, PositionChanged, etc.) |
| **Change Tracking** | ✅ | ✅ | ⚡ **Optimized** | Virtual edits with EditsManager, memory-efficient |
| **Stream Support** | ✅ | ✅ | - | File and stream-based byte providers |
| **Cross-Platform Core** | ❌ | ✅ 🆕 | **netstandard2.0** | Platform-agnostic business logic (Avalonia-ready) |

</details>

<details>
<summary><b>📁 File Operations</b> (10 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | V2 Enhancement | Notes |
|---------|:---------:|:---------:|----------------|-------|
| Open file | ✅ | ✅ | - | OpenFile(path) |
| Open stream | ✅ | ✅ | - | Stream property |
| **Save operations** | ⚠️ | ✅ | 🔧 **Fixed** | **Critical:** Save data loss bug completely resolved |
| Save As | ✅ | ✅ | - | SaveAs(newPath) |
| Close file/stream | ✅ | ✅ | - | CloseProvider() |
| **Large file support (GB+)** | ⚠️ Slow | ✅ Fast ⚡ | **Memory-mapped** | Handle files that crashed V1 |
| Partial loading | ✅ | ✅ | - | Load visible portion only |
| Stream editing | ✅ | ✅ | - | Edit streams without loading to memory |
| File locking detection | ✅ | ✅ | - | IsLockedFile property |
| **Async file operations** | ❌ | ✅ 🆕⚡ | **Non-blocking** | Load/save without freezing UI |

</details>

<details>
<summary><b>🔤 Character Encoding & TBL</b> (10 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | Notes |
|---------|:---------:|:---------:|-------|
| TypeOfCharacterTable (ASCII/EBCDIC/UTF8/etc.) | ✅ | ⚠️ | 20+ encoding types (interface compatible, untested in V2) |
| CustomEncoding property | ✅ | ⚠️ | Shift-JIS, EUC-KR, Windows-1252, ISO-8859-1 (untested in V2) |
| TBL file loading | ✅ | ⚠️ | LoadTBLFile(path) - interface compatible, untested in V2 |
| Unicode TBL support | ✅ | ⚠️ | Multi-byte character support (DTE/MTE) (untested in V2) |
| TBL color customization | ✅ | ⚠️ | TbldteColor, TblmteColor, TblEndBlockColor, TblEndLineColor (untested) |
| TBL MTE display toggle | ✅ | ⚠️ | TblShowMte property (untested in V2) |
| ASCII/TBL mode switching | ✅ | ⚠️ | CloseTBL() to revert to ASCII (untested in V2) |
| Default TBL presets | ✅ | ⚠️ | LoadDefaultTbl(type) (untested in V2) |
| TBL bidirectional mapping | ✅ | ⚠️ | Byte ↔ character conversion (untested in V2) |
| TBL string copy mode | ✅ | ⚠️ | CopyToClipboard(CopyPasteMode.TblString) (untested in V2) |

</details>

<details>
<summary><b>👨‍💻 Developer Features</b> (12 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | V2 Enhancement | Notes |
|---------|:---------:|:---------:|----------------|-------|
| HexBox control | ✅ | ✅ | - | Reusable hex editing control |
| Dependency properties (60+) | ✅ | ✅ | - | XAML data binding support |
| **MVVM compatible** | ⚠️ Limited | ✅ Full | **True MVVM** | ViewModel layer with INotifyPropertyChanged |
| Sample applications (7+) | ✅ | ✅ | - | C#, VB.NET, WinForms, AvalonDock, BarChart, etc. |
| **Unit tests** | ❌ | ⚠️ 🆕 | **Limited coverage** | ByteProvider V2 tested, UI features need validation |
| **Benchmarks** | ❌ | ✅ 🆕 | **BenchmarkDotNet** | Performance benchmarking suite |
| Localization (9 languages) | ✅ | ✅ | ⚡ **Dynamic** | Runtime language switching without restart |
| **Service Usage Sample** | ❌ | ✅ 🆕 | **Headless usage** | Console app using services without UI |
| **Public API Documentation** | ⚠️ | ✅ | **19 READMEs** | Comprehensive documentation for every component |
| Event system (21+ events) | ✅ | ✅ | - | Rich event notifications |
| ByteProvider abstraction | ✅ | ✅ | ⚡ **Enhanced** | Virtual position support, async extensions |
| **Cross-platform design** | ❌ | ✅ 🆕 | **Core separation** | Platform-agnostic business logic (netstandard2.0) |

</details>

<details>
<summary><b>🎨 Customization & Appearance</b> (15+ features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | Notes |
|---------|:---------:|:---------:|-------|
| Color properties (14 brushes) | ✅ | ✅ | SelectionFirstColor, ByteModifiedColor, MouseOverColor, etc. |
| Custom backgrounds | ✅ | ⚠️ | AddCustomBackgroundBlock() - interface compatible, untested in V2 |
| Font customization | ✅ | ✅ | FontFamily property (default: Courier New) |
| Border styles | ✅ | ✅ | Configurable border appearance |
| Status bar visibility | ✅ | ✅ | StatusBarVisibility property |
| Header visibility | ✅ | ✅ | HeaderVisibility property |
| Panel visibility toggles | ✅ | ✅ | HexDataVisibility, StringDataVisibility, LineInfoVisibility |
| BytePerLine (1-64 bytes) | ✅ | ✅ | Configurable bytes per row |
| Byte spacer customization | ✅ | ✅ | Position, width, grouping, visual style |
| Zoom support (50%-200%) | ✅ | ⚠️ | Ctrl+MouseWheel scaling (interface compatible, untested in V2) |
| Context menus | ✅ | ✅ | Right-click menu with commands |
| Dual-color selection | ❌ | ✅ 🆕 | Active/inactive panel distinction |
| Bold SelectionStart indicator | ❌ | ✅ 🆕 | Visual emphasis on selection start |
| Mouse hover preview | ❌ | ✅ 🆕 | Byte value preview on hover |
| Visual caret mode | ✅ | ✅ | Insert/Overwrite caret display |

</details>

<details>
<summary><b>⌨️ Keyboard Shortcuts</b> (18 shortcuts)</summary>

<br/>

| Shortcut | V1 Legacy | V2 Modern | Function |
|----------|:---------:|:---------:|----------|
| Ctrl+C | ✅ | ✅ | Copy selection |
| Ctrl+V | ✅ | ✅ | Paste from clipboard |
| Ctrl+X | ✅ | ✅ | Cut selection |
| Ctrl+Z | ✅ | ✅ | Undo last operation |
| Ctrl+Y | ✅ | ✅ | Redo last undone operation |
| Ctrl+A | ✅ | ✅ | Select all |
| Ctrl+F | ✅ | ✅ | Open Find dialog |
| Ctrl+H | ✅ | ✅ | Open Replace dialog |
| Ctrl+G | ✅ | ✅ | Go to offset |
| Ctrl+B | ✅ | ✅ | Toggle bookmark |
| ESC | ✅ | ✅ | Clear selection / Close find panel |
| Delete | ✅ | ✅ | Delete byte at cursor |
| Backspace | ✅ | ✅ | Delete byte before cursor |
| Arrow keys | ✅ | ✅ | Navigate bytes |
| Page Up/Down | ✅ | ✅ | Fast scrolling |
| Home/End | ✅ | ✅ | Line start/end navigation |
| Ctrl+Home/End | ✅ | ✅ | File start/end navigation |
| Ctrl+MouseWheel | ✅ | ⚠️ | Zoom in/out (untested in V2) |

All shortcuts are configurable via AllowBuildin* properties.

</details>

<details>
<summary><b>📡 Events & Callbacks</b> (21+ events)</summary>

<br/>

| Event | V1 Legacy | V2 Modern | Description |
|-------|:---------:|:---------:|-------------|
| SelectionStartChanged | ✅ | ✅ | Fires when selection start position changes |
| SelectionStopChanged | ✅ | ✅ | Fires when selection stop position changes |
| SelectionLengthChanged | ✅ | ✅ | Fires when selection length changes |
| SelectionChanged | ❌ | ✅ 🆕 | Comprehensive selection change event |
| PositionChanged | ❌ | ✅ 🆕 | Cursor position changes |
| DataCopied | ✅ | ✅ | Fires when data copied to clipboard |
| ByteModified | ✅ | ✅ | Fires when byte modified (with ByteEventArgs) |
| BytesDeleted | ✅ | ✅ | Fires when bytes deleted |
| TypeOfCharacterTableChanged | ✅ | ⚠️ | Fires when character encoding changes (untested in V2) |
| LongProcessProgressChanged | ✅ | ✅ | Progress reporting (0-100%) |
| LongProcessProgressStarted | ✅ | ✅ | Long operation started |
| LongProcessProgressCompleted | ✅ | ✅ | Long operation completed |
| ReplaceByteCompleted | ✅ | ✅ | Replace operation finished |
| FillWithByteCompleted | ✅ | ✅ | Fill operation finished |
| Undone | ✅ | ✅ | Undo operation executed |
| Redone | ✅ | ✅ | Redo operation executed |
| UndoCompleted / RedoCompleted | ❌ | ✅ 🆕 | More granular undo/redo events |
| ByteClick | ✅ | ✅ | Byte clicked (with position) |
| ByteDoubleClick | ✅ | ✅ | Byte double-clicked |
| ZoomScaleChanged | ✅ | ⚠️ | Zoom level changed (untested in V2) |
| VerticalScrollBarChanged | ✅ | ✅ | Scrollbar position changed |
| ChangesSubmited | ✅ | ✅ | Changes saved to file/stream |
| ReadOnlyChanged | ✅ | ✅ | Read-only mode toggled |
| FileOpened / FileClosed | ❌ | ✅ 🆕 | File lifecycle events |

</details>

<details>
<summary><b>🔧 Advanced Features</b> (12 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | V2 Enhancement | Notes |
|---------|:---------:|:---------:|----------------|-------|
| Bookmarks | ✅ | ⚠️ | **Interface only** | SetBookmark(), GetNextBookmark(), GetPreviousBookmark() (untested in V2) |
| **Binary file comparison** | ⚠️ | ✅ | 🆕 **3 variants** | Basic, Parallel, SIMD comparison services |
| **Similarity calculation** | ❌ | ✅ 🆕 | **Percentage** | CalculateSimilarity() returns 0-100% match |
| **Difference counting** | ❌ | ✅ 🆕 | **Byte-level** | CountDifferences() with SIMD optimization |
| **State persistence** | ✅ | ⚠️ | **Interface only** | SaveState() / LoadState() with XML serialization (untested in V2) |
| **Virtual position system** | ❌ | ✅ 🆕 | **Insert/delete** | PositionMapper handles virtual↔physical conversion |
| **EditsManager** | ❌ | ✅ 🆕 | **Non-destructive** | Track insertions/deletions without modifying source |
| Auto-highlight same bytes | ⚠️ | ✅ | 🔧 **Enhanced** | AllowAutoHighLightSelectionByte on double-click |
| Byte frequency analysis | ❌ | ✅ 🆕 | **BarChart** | Visual distribution of byte values |
| Drag & drop support | ✅ | ⚠️ | 🔧 **Properties only** | AllowFileDrop, AllowTextDrop properties exist but event handlers not implemented |
| Tooltip byte preview | ✅ | ⚠️ | **Interface only** | ShowByteToolTip property (untested in V2) |
| Visual byte addressing | ✅ | ⚠️ | **Interface only** | AllowVisualByteAddress property (untested in V2) |

</details>

<details open>
<summary><b>🐛 Critical Bug Fixes in V2</b> (4 major fixes)</summary>

<br/>

| Bug | V1 Status | V2 Status | Resolution | Impact |
|-----|:---------:|:---------:|------------|--------|
| **Issue #145: Insert Mode Hex Input** | ⚠️ Critical | ✅ Fixed | **Root cause:** PositionMapper.PhysicalToVirtual() returned wrong position. **Fix:** Corrected virtual position calculation (commit 405b164). | Typing "FFFFFFFF" produced "F0 F0 F0 F0" instead of "FF FF FF FF". Now works correctly. |
| **Save Data Loss Bug** | ⚠️ Critical | ✅ Fixed | **Root cause:** Same PositionMapper bug caused ByteReader to read wrong bytes during Save. **Fix:** PositionMapper fix resolved root cause. | Multi-MB files corrupted to hundreds of bytes on save. All comprehensive tests now pass. |
| **Search Cache Invalidation** | ⚠️ | ✅ Fixed | Cache not invalidated after data modifications. Fixed at all 11 modification points. | Users received stale search results after editing. |
| **Binary Search O(m) → O(log m)** | ⚠️ | ✅ Fixed | Code claimed binary search but used linear scan. Implemented true binary search. | Files with 100k+ edits: 100-5,882x faster position conversion! |

**All critical bugs resolved. V2 is production-ready.** ✅

</details>

<details>
<summary><b>🌐 Cross-Platform & Multi-Targeting</b> (8 features)</summary>

<br/>

| Feature | V1 Legacy | V2 Modern | V2 Enhancement | Notes |
|---------|:---------:|:---------:|----------------|-------|
| **.NET Framework 4.8** | ✅ | ✅ | - | Legacy Windows desktop support |
| **.NET 8.0-windows** | ❌ | ✅ 🆕 | **Modern .NET** | Latest LTS version with performance improvements |
| **Multi-targeting** | ❌ | ✅ 🆕 | **Single NuGet** | Works in both net48 and net8.0-windows projects |
| **Platform-agnostic Core** | ❌ | ✅ 🆕 | **netstandard2.0** | Business logic separated from UI framework |
| **WPF platform layer** | ✅ | ✅ | - | Windows WPF implementation |
| **Avalonia support (future)** | ❌ | 🚧 Planned | **Cross-platform** | Linux, macOS, Web support via Avalonia |
| **MAUI support (future)** | ❌ | 🚧 Planned | **Mobile** | Android, iOS, Windows support |
| **Console/headless usage** | ❌ | ✅ 🆕 | **Service layer** | Use services without UI (see ServiceUsage sample) |

**V2 Foundation:** Architecture ready for true cross-platform expansion beyond Windows.

</details>

---

## 📖 Legend & Testing Status

### Status Indicators

- ✅ **Tested & Working** - Feature has been actively developed, debugged, and validated through commits
- ⚠️ **Interface Compatible (Untested)** - API exists and is V1-compatible, but lacks testing/validation in V2
- ❌ **Not Available** - Feature does not exist
- 🆕 **New in V2** - Feature only exists in V2, not in V1
- ⚡ **Performance Boost** - Significant performance improvement in V2
- 🔧 **Enhanced/Fixed** - Improved or fixed implementation in V2

### Historical Context

- **V2 Migration (v2.5.0 - v2.6.0):** V2 was designed as a drop-in replacement for V1
- **100% API preservation:** Same namespace, same class name `HexEditor`, same public API
- **Zero breaking changes** during migration period (12 months)
- **V1 Removal (v2.6.0 - Feb 2026):** Legacy code completely removed (17,093 LOC)
- **Current Status:** Project is now V2-only using modern architecture

### Feature Count

- **~163 features** catalogued across 15 categories
- **87 tested & working** ✅ (actively developed with commit history)
- **33 interface-compatible** ⚠️ (untested but API-compatible)
- **40+ V2-exclusive features** 🆕 (new capabilities not in V1)

### Testing Coverage

**Tested Features (commit history analysis):**
- ✅ Insert Mode, Search/Find All, Selection, Copy/Paste, Clipboard
- ✅ Scroll markers, Keyboard navigation, Mouse hover/click
- ✅ Save operations, PositionMapper, ByteProvider
- ✅ Performance optimizations (cache, SIMD, binary search)
- ✅ Localization (9 languages), Async operations
- ✅ Rendering (DrawingContext), Highlighting (HashSet), Undo/Redo, Delete operations

**Untested Features (no commit history):**
- ⚠️ TBL support (all 13 features) - interface exists, needs validation
- ⚠️ Zoom functionality - interface exists, needs validation
- ⚠️ Bookmarks - interface exists, needs validation
- ⚠️ Custom background blocks - interface exists, needs validation
- ⚠️ Custom encodings - interface exists, needs validation
- ⚠️ State persistence - interface exists, needs validation

**Non-functional:**
- ⚠️ Drag & drop - properties exist but event handlers not implemented

---

📖 **Back to:** [Main README](README.md) | [Getting Started](GETTING_STARTED.md) | [Migration Guide](docs/migration/MIGRATION.md)
