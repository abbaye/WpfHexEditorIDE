# Changelog

All notable changes to WPF HexEditor will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.2.1] - 2026-02-14

### 🐛 Fixed - Critical Bug Fixes

#### Issue #145: Insert Mode Hex Input Bug ✅ RESOLVED
- **Fixed**: Typing consecutive hex characters in Insert Mode now works correctly
  - Before: "FFFFFFFF" produced "F0 F0 F0 F0" (incorrect)
  - After: "FFFFFFFF" produces "FF FF FF FF" (correct)
- **Root Cause**: `PositionMapper.PhysicalToVirtual()` returned position of first inserted byte instead of physical byte position
- **Impact**: Insert mode now works perfectly in both hex and ASCII panels
- **Commits**: 405b164 (root cause), 35b19b5 (cursor sync + LIFO offset)
- **Files Changed**:
  - `PositionMapper.cs` lines 278-290 - Fixed PhysicalToVirtual calculation
  - `ByteReader.cs` lines 76-156 - Corrected LIFO offset calculation
  - `ByteProvider.cs` lines 264-300 - Fixed ModifyInsertedByte LIFO offset
  - `HexEditorV2.xaml.cs` - Added cursor position synchronization with drift tolerance
- **Documentation**: [ISSUE_145_CLOSURE.md](ISSUE_145_CLOSURE.md), [ISSUE_HexInput_Insert_Mode.md](ISSUE_HexInput_Insert_Mode.md)

#### Save Data Loss Bug ✅ COMPLETELY RESOLVED
- **Fixed**: Root cause of catastrophic data loss during Save operations (MB → KB file corruption)
- **Root Cause**: Same PositionMapper bug caused ByteReader to read wrong bytes during Save
- **Validation**: ✅ ALL comprehensive tests passed (2026-02-14)
  - ✅ Insertions: file size = original + inserted bytes
  - ✅ Deletions: file size = original - deleted bytes
  - ✅ Modifications: file size unchanged
  - ✅ Mixed edits (insertions + deletions + modifications): all verified correct
  - ✅ After save, reopen and verify: content matches perfectly
- **Performance**: Added fast save path for modification-only edits (10-100x faster)
- **Commits**: 405b164 (root cause), 35b19b5 (LIFO offset fixes)
- **Documentation**: [ISSUE_Save_DataLoss.md](ISSUE_Save_DataLoss.md), [RESOLVED_ISSUES.md](docs/RESOLVED_ISSUES.md)

### 📚 Documentation

#### ARCHITECTURE_V2.md - Complete V2 Architecture Documentation
- **Added**: Comprehensive architecture documentation with Mermaid diagrams
  - Component overview and dependencies
  - ByteProvider V2 internal architecture
  - LIFO insertion semantics with visual examples
  - Virtual/physical position mapping algorithms
  - Save operation flow with error scenarios
  - Performance characteristics and optimization strategies
- **Fixed**: All Mermaid diagram rendering errors for GitHub compatibility
  - Replaced 97 `<br/>` tags with " -" separators (commit 6af1bee)
  - Fixed Component Overview diagram (commit d572de1)
  - Fixed sequenceDiagram loop syntax (commit 9a31b3f)
- **Commits**: 3800bd8 (content update), 6af1bee, d572de1, 9a31b3f (diagram fixes)
- **File**: [ARCHITECTURE_V2.md](ARCHITECTURE_V2.md)

### 🔧 Internal Improvements

#### PositionMapper Fix (Commit 405b164)
```csharp
// BEFORE (WRONG):
if (physicalPosition == segment.PhysicalPos) {
    virtualPos = segment.VirtualOffset;  // Returns first inserted byte
    return virtualPos;
}

// AFTER (CORRECT):
if (physicalPosition == segment.PhysicalPos) {
    virtualPos = segment.VirtualOffset + segment.InsertedCount;  // Returns physical byte
    return virtualPos;
}
```
**Impact**: This single fix resolved BOTH the Insert Mode display bug AND the Save data loss bug.

#### ByteReader LIFO Offset Fix (Commits 405b164, 35b19b5)
- Corrected virtual space layout understanding to match PositionMapper semantics
- Fixed LIFO offset calculation: `targetOffset = totalInsertions - 1 - relativePosition`
- **Result**: ByteReader now correctly reads inserted bytes with proper LIFO ordering

#### Cursor Position Synchronization (Commit 35b19b5)
- Added `Dispatcher.Invoke(DispatcherPriority.Send)` for synchronous cursor updates
- Added drift tolerance (±1 position) in Insert mode to handle async position updates
- **Result**: Cursor stays locked on editing position during nibble entry

#### Performance Optimizations - Debug Log Removal
- **Removed**: All `Debug.WriteLine` diagnostic logging from production code paths
- **Files**: ByteReader.cs, ByteProvider.cs, EditsManager.cs, HexEditorV2.xaml.cs, HexViewport.cs, StateService.cs, HexEditorViewModel.cs
- **Impact**: Significant performance improvement for insert/modify/save operations
- **Result**: Faster save operations, reduced I/O overhead, cleaner production code

## [2.2.0] - 2026-01-XX

### 🚀 Added - Performance Optimizations

#### Search Performance
- **LRU Cache for Search Results** - 10-100x faster repeated searches
  - O(1) lookup performance with intelligent cache eviction
  - Thread-safe with configurable capacity (default: 20 cached searches)
  - Automatic invalidation on data modifications (11 invalidation points)
- **Parallel Multi-Core Search** - 2-4x faster for large files (> 100MB)
  - Automatic threshold detection uses all CPU cores for large files
  - Zero overhead for small files (automatic fallback)
  - Thread-safe with overlap handling for patterns spanning chunks
- **SIMD Vectorization** (net5.0+) - 4-8x faster single-byte searches
  - AVX2/SSE2 hardware acceleration
  - Processes 32 bytes at once (AVX2) or 16 bytes (SSE2)
  - Automatic hardware detection and fallback

#### Memory Optimizations
- **Span<byte> + ArrayPool** - 2-5x faster with 90% less memory allocation
  - Zero-allocation memory operations
  - Buffer pooling for efficient resource usage
- **Profile-Guided Optimization (PGO)** (.NET 8.0+) - 10-30% CPU performance boost
  - Dynamic runtime optimization with tiered compilation
  - 30-50% faster startup with ReadyToRun (AOT compilation)

#### UI Rendering Optimizations
- **Cached Typeface & FormattedText** (BaseByte.cs) - 2-3x faster rendering
  - Reuses expensive WPF objects
  - Intelligent cache invalidation on text/font changes
- **Cached Width Calculations** (HexByte.cs) - 10-100x faster width lookups
  - Static Dictionary cache with O(1) lookups
  - Thread-safe with lock protection
- **Batch Visual Updates** (BaseByte.cs) - 2-5x faster multi-property changes
  - BeginUpdate/EndUpdate pattern prevents redundant updates

#### Data Structure Optimizations
- **HighlightService HashSet Migration** - 2-3x faster highlight operations
  - 50% less memory (single long vs key-value pair)
  - Single lookup operations (no redundant ContainsKey checks)
- **Batching Support** - 10-100x faster bulk highlighting
  - BeginBatch/EndBatch pattern prevents UI updates during operations
  - Bulk APIs: AddHighLightRanges() (14x faster), AddHighLightPositions() (27x faster)

### 🏗️ Added - Architecture

#### Service-Based Architecture (10 Services)
- **Core Services (6)**:
  - ClipboardService - Copy/paste/cut operations
  - FindReplaceService - Search with LRU cache
  - UndoRedoService - History management
  - SelectionService - Selection validation
  - HighlightService - Visual byte marking (stateful)
  - ByteModificationService - Insert/delete/modify

- **Additional Services (4)**:
  - BookmarkService - Bookmark management (stateful)
  - TblService - Character table handling (stateful)
  - PositionService - Position calculations
  - CustomBackgroundService - Background colors (stateful)

**Benefits**:
- ✅ Separation of concerns
- ✅ Unit testable components (80+ tests)
- ✅ Reusable across projects
- ✅ 0 breaking changes in public API

### 🧪 Added - Testing
- **80+ Unit Tests** with xUnit (.NET 8.0-windows)
  - SelectionServiceTests - 35 tests
  - FindReplaceServiceTests - 35 tests
  - HighlightServiceTests - 10+ tests
- **Comprehensive coverage** for service layer
- **CI/CD integration** ready

### 📚 Added - Documentation
- **19 comprehensive README files** covering all components
- **Service documentation** with API references and examples
- **Performance guide** with benchmarking results
- **Architecture diagrams** (Mermaid format)

### 🐛 Fixed
- **Search cache invalidation** - Cache now properly cleared at all 11 modification points
- **Memory leaks** - Fixed service lifecycle management
- **UI freezing** - Async/await support for long operations

### 🔧 Changed
- **Extracted 2500+ lines** of business logic into services
- **Refactored HexEditor** from 6115 lines (god class) to maintainable architecture
- **Improved performance** - Combined optimizations yield 10-100x speedup

## [2.1.0] - 2025-XX-XX

### Added
- HexEditorV2 control with custom DrawingContext rendering
- MVVM architecture with HexEditorViewModel
- True Insert Mode support with virtual position mapping
- Custom background blocks for byte range highlighting
- BarChart visualization mode
- AvalonDock integration support

### Changed
- Rendering performance improved by 99% vs V1
- Memory usage reduced by 80-90%

### Deprecated
- Some V1 API methods (compatibility maintained via overloads)

## [2.0.0] - 2024-XX-XX

### Added
- .NET 8.0-windows support
- Multi-targeting (.NET Framework 4.8 + .NET 8.0)
- Async file operations
- Memory-mapped file support (experimental)

### Changed
- Upgraded to C# 12 preview features
- Improved TBL Unicode support

## [1.x] - 2023 and earlier

Legacy V1 architecture (monolithic design).

See GitHub releases for historical changelog.

---

## Legend

- 🚀 Performance improvement
- 🐛 Bug fix
- ✨ New feature
- 🔧 Internal change
- 📚 Documentation
- ⚠️ Breaking change
- 🏗️ Architecture change
- 🧪 Testing improvement

---

**Format**: [Major.Minor.Patch]
- **Major**: Breaking changes
- **Minor**: New features (backward compatible)
- **Patch**: Bug fixes (backward compatible)
