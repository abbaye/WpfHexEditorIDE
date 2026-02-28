# Services Architecture

This folder contains services that encapsulate the business logic of the HexEditor.
The goal is to reduce the complexity of the `HexEditor` class by extracting responsibilities into dedicated services.

## 📋 Available Services (10 Total)

### 1. 📋 ClipboardService
**Responsibility:** Manage copy/paste/cut operations

**Main methods:**
- `CopyToClipboard()` - Copy to clipboard
- `CopyToStream()` - Copy to stream
- `GetCopyData()` - Retrieve copied data
- `FillWithByte()` - Fill selection with a byte
- `CanCopy()` - Check if copy is possible
- `CanDelete()` - Check if deletion is possible

**Usage:**
```csharp
var clipboardService = new ClipboardService
{
    DefaultCopyMode = CopyPasteMode.HexaString
};

// Copy selection
clipboardService.CopyToClipboard(_provider, SelectionStart, SelectionStop, _tblCharacterTable);

// Check if can copy
if (clipboardService.CanCopy(SelectionLength, _provider))
{
    // ...
}
```

---

### 2. 🔍 FindReplaceService ⚡ (ULTRA-OPTIMIZED v2.2+)
**Responsibility:** Search and replace data with **LRU cache** + **parallel multi-core search**

**Performance Enhancements (v2.2+):**
- **LRU Cache:** 10-100x faster for repeated searches (O(1) lookups)
- **Parallel Search:** 2-4x faster for large files (> 100MB, uses all CPU cores)
- **Async Support:** UI stays responsive during long searches
- **SIMD Integration:** 4-8x faster single-byte searches (net5.0+)

**Main methods:**
- `FindFirst()` / `FindFirstAsync()` - Find first occurrence
- `FindNext()` - Find next occurrence
- `FindLast()` - Find last occurrence
- `FindAll()` / `FindAllAsync()` - Find all occurrences
- `FindAllOptimized()` - Span-based optimized search (2-5x faster)
- `FindAllCachedOptimized()` - **LRU cached** + **parallel** search (10-100x faster on cache hit)
- `CountOccurrences()` - Count occurrences (optimized, zero allocation)
- `CountOccurrencesAsync()` - Async counting with progress
- `ReplaceFirst()` - Replace first occurrence
- `ReplaceAll()` / `ReplaceAllAsync()` - Replace all occurrences
- `ClearCache()` - Clear LRU search cache
- `GetCacheStatistics()` - Get cache usage stats

**Constructor:**
```csharp
public FindReplaceService(int cacheCapacity = 20)
```

**Usage:**
```csharp
// Create service with custom cache capacity (default: 20)
var findReplaceService = new FindReplaceService(cacheCapacity: 50);

// Standard search
byte[] searchData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
long position = findReplaceService.FindFirst(_provider, searchData);

// OPTIMIZED: Cached + Parallel + Span (FASTEST!)
// First call: 18ms (full search with parallel multi-core)
var results1 = findReplaceService.FindAllCachedOptimized(_provider, searchData, 0);

// Repeated call: 0.2ms (cache hit - 90x faster!)
var results2 = findReplaceService.FindAllCachedOptimized(_provider, searchData, 0);

// Async with progress reporting (UI stays responsive)
var progress = new Progress<int>(percent => ProgressBar.Value = percent);
var cts = new CancellationTokenSource();
var resultsAsync = await findReplaceService.FindAllAsync(_provider, searchData, 0, progress, cts.Token);

// Count occurrences (zero allocation, optimized)
int count = findReplaceService.CountOccurrences(_provider, searchData, 0);

// Replace with progress
var replaceData = new byte[] { 0x57, 0x6F, 0x72, 0x6C, 0x64 };
var replacedCount = await findReplaceService.ReplaceAllAsync(_provider, searchData, replaceData, false, false, progress, cts.Token);

// Check cache statistics
string cacheStats = findReplaceService.GetCacheStatistics();
// Output: "LRU Cache: 5/20 items, Usage: 25.0%"
```

**Advanced Features:**

1. **Automatic Optimization Selection:**
   - Files < 100MB: Standard optimized search
   - Files > 100MB: Parallel multi-core search (2-4x faster)
   - Repeated searches: LRU cache hit (10-100x faster)

2. **LRU Cache Details:**
   - Thread-safe with O(1) operations
   - Automatic eviction of least recently used results
   - Configurable capacity (default: 20 cached searches)
   - Cache key: Pattern hash + start position + file length
   - Automatically cleared on file modifications

3. **Parallel Search (> 100MB files):**
   - Uses all available CPU cores with Parallel.For
   - 1MB chunks with overlap handling for patterns spanning boundaries
   - Thread-safe result collection with ConcurrentBag
   - Zero overhead for small files (automatic threshold detection)

**Performance Comparison:**
```
Operation: Find all occurrences of 4-byte pattern in 150MB file

Traditional:        2,400ms
Optimized:            850ms  (2.8x faster)
Parallel:             350ms  (6.9x faster on 8-core CPU)
Cached (repeat):        2ms  (1,200x faster - cache hit!)
```

**Note:** Cache is automatically cleared when file data is modified (insert, delete, modify operations).

---

### 3. ↩️ UndoRedoService
**Responsibility:** Manage undo/redo history

**Main methods:**
- `Undo()` - Undo last action (returns byte position)
- `Redo()` - Redo an undone action (returns byte position)
- `ClearAll()` - Clear all history
- `CanUndo()` - Check if undo is possible
- `CanRedo()` - Check if redo is possible
- `GetUndoCount()` - Get number of actions in history
- `GetUndoStack()` - Get undo stack

**Usage:**
```csharp
var undoRedoService = new UndoRedoService();

// Undo
if (undoRedoService.CanUndo(_provider))
{
    long position = undoRedoService.Undo(_provider);
    // Update position in UI
}

// Redo
if (undoRedoService.CanRedo(_provider))
{
    long position = undoRedoService.Redo(_provider, repeat: 3);
    // Redo the last 3 undone actions
}
```

---

### 4. 🎯 SelectionService
**Responsibility:** Manage selection and validation

**Main methods:**
- `IsValidSelection()` - Check if selection is valid
- `GetSelectionLength()` - Calculate selection length
- `FixSelectionRange()` - Fix start/stop order
- `ValidateSelection()` - Validate and adjust selection to bounds
- `GetSelectionBytes()` - Retrieve selected bytes
- `GetAllBytes()` - Retrieve all bytes from provider
- `GetSelectAllStart()` / `GetSelectAllStop()` - Calculate positions for "Select All"
- `IsAllSelected()` - Check if everything is selected
- `HasSelection()` - Check if a selection exists
- `ExtendSelection()` - Extend selection with an offset
- `GetSelectionByte()` - Retrieve byte at position

**Usage:**
```csharp
var selectionService = new SelectionService();

// Check selection
if (selectionService.IsValidSelection(SelectionStart, SelectionStop))
{
    long length = selectionService.GetSelectionLength(SelectionStart, SelectionStop);
    byte[] data = selectionService.GetSelectionBytes(_provider, SelectionStart, SelectionStop);
}

// Fix selection if inverted
var (start, stop) = selectionService.FixSelectionRange(SelectionStart, SelectionStop);

// Validate and adjust to bounds
var (validStart, validStop) = selectionService.ValidateSelection(_provider, SelectionStart, SelectionStop);
```

---

### 5. ✨ HighlightService ⚡ (OPTIMIZED v2.2+)
**Responsibility:** Manage byte highlighting (search results, marked bytes)

**Performance Enhancements (v2.2+):**
- **HashSet Migration:** 2-3x faster, 50% less memory (was Dictionary<long, long>)
- **Single Lookup Operations:** Add/Remove use HashSet's return value directly
- **Batching Support:** 10-100x faster for bulk operations (BeginBatch/EndBatch)
- **Bulk Operations:** AddHighLightRanges, AddHighLightPositions (5-10x faster)

**Main methods:**
- `AddHighLight(long startPosition, long length)` - Add highlight to bytes (OPTIMIZED)
- `RemoveHighLight(long startPosition, long length)` - Remove highlight from bytes (OPTIMIZED)
- `UnHighLightAll()` - Remove all highlights
- `IsHighlighted(long position)` - Check if position is highlighted (O(1))
- `GetHighlightCount()` - Get number of highlighted positions
- `HasHighlights()` - Check if any highlights exist
- `GetHighlightedPositions()` - Get all highlighted positions
- `GetHighlightedRanges()` - Get grouped consecutive ranges
- `BeginBatch()` / `EndBatch()` - Batch operations **NEW v2.2+**
- `AddHighLightRanges()` - Bulk add ranges **NEW v2.2+**
- `AddHighLightPositions()` - Bulk add positions **NEW v2.2+**

**Usage:**
```csharp
var highlightService = new HighlightService();

// Single highlight
highlightService.AddHighLight(position, dataLength);

// Batch operations (10-100x faster for bulk highlights)
highlightService.BeginBatch();
foreach (var result in searchResults)
    highlightService.AddHighLight(result.Position, result.Length);
var (added, removed) = highlightService.EndBatch();

// Bulk operations (5-10x faster than loops)
var ranges = new List<(long, long)> { (100, 10), (200, 5), (500, 20) };
highlightService.AddHighLightRanges(ranges);

// Check if position is highlighted
if (highlightService.IsHighlighted(position))
{
    // Display with highlight color
}

// Get all highlighted ranges (optimized)
foreach (var (start, length) in highlightService.GetHighlightedRanges())
{
    Console.WriteLine($"Highlighted: 0x{start:X} - {length} bytes");
}

// Clear all highlights
highlightService.UnHighLightAll();
```

**Note:** This service is **stateful** and maintains a HashSet of highlighted positions internally.

---

### 6. 🔧 ByteModificationService
**Responsibility:** Manage byte modifications (insert, delete, modify, restore) ✨ **Updated for Issue #127**

**Main methods:**
- `ModifyByte()` - Modify byte at position
- `InsertByte()` - Insert single byte
- `InsertByte(with length)` - Insert byte N times
- `InsertBytes()` - Insert array of bytes
- `DeleteBytes()` - Delete bytes at position
- `DeleteRange()` - Delete range (auto-fixes inverted start/stop)
- `CanModify()` - Check if modification allowed
- `CanInsert()` - Check if insertion allowed
- `CanDelete()` - Check if deletion allowed

**✨ NEW - Restore Operations (Issue #127):**
- `RestoreOriginalByte()` - Restore single byte to original value
- `RemoveModification()` - V2-compatible alias
- `ResetByte()` - Concise alias
- `RestoreOriginalBytes(long[])` - Restore multiple bytes (array)
- `RestoreOriginalBytes(IEnumerable<long>)` - Restore multiple bytes (LINQ support)
- `RestoreOriginalBytesInRange()` - Restore range of bytes
- `RestoreAllModifications()` - Restore ALL modifications
- `CanRestore()` - Check if restore is allowed

**Usage:**
```csharp
var modService = new ByteModificationService();

// Modify a byte
if (modService.ModifyByte(_provider, 0xFF, position, 1, readOnlyMode))
{
    Console.WriteLine("Byte modified successfully");
}

// Insert bytes
int inserted = modService.InsertBytes(_provider, new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F },
    position, canInsertAnywhere);

// Delete range (handles inverted positions automatically)
long lastPos = modService.DeleteRange(_provider, start, stop, readOnlyMode, allowDelete);

// ✨ NEW - Restore modified byte to original value (Issue #127)
if (modService.RestoreOriginalByte(_provider, 0x100))
{
    Console.WriteLine("Byte restored to original value");
}

// ✨ NEW - Restore multiple bytes
long[] positions = { 0x100, 0x200, 0x300 };
int count = modService.RestoreOriginalBytes(_provider, positions);
Console.WriteLine($"Restored {count} bytes");

// ✨ NEW - Restore with LINQ
var modifiedPositions = _provider.GetByteModifieds(ByteAction.Modified)
    .Keys
    .Where(p => p >= 0x1000 && p <= 0x2000);
count = modService.RestoreOriginalBytes(_provider, modifiedPositions);

// ✨ NEW - Restore range
count = modService.RestoreOriginalBytesInRange(_provider, 0x100, 0x200);

// ✨ NEW - Restore ALL modifications
count = modService.RestoreAllModifications(_provider);

// Check permissions
if (modService.CanModify(_provider, readOnlyMode))
{
    // Perform modification
}

if (modService.CanRestore(_provider))
{
    // Perform restore
}
```

**Note:** All methods include validation and return success/failure indicators.

**Three naming variants available:**
- `RestoreOriginalByte()` - Descriptive ✅ Recommended
- `RemoveModification()` - V2-compatible
- `ResetByte()` - Concise

---

### 7. 🔖 BookmarkService
**Responsibility:** Manage bookmark operations

**Main methods:**
- `AddBookmark(long position, string description, ScrollMarker marker)` - Add bookmark
- `RemoveBookmark(long position, ScrollMarker? marker)` - Remove bookmark at position
- `RemoveAllBookmarks(ScrollMarker marker)` - Remove all bookmarks of type
- `ClearAll()` - Clear all bookmarks
- `GetAllBookmarks()` - Get all bookmarks
- `GetBookmarksByMarker(ScrollMarker marker)` - Get bookmarks by type
- `GetBookmarkAt(long position)` - Get bookmark at position
- `HasBookmarkAt(long position)` - Check if bookmark exists
- `GetNextBookmark(long position)` - Get next bookmark after position
- `GetPreviousBookmark(long position)` - Get previous bookmark before position
- `UpdateBookmarkDescription(long position, string description)` - Update description

**Usage:**
```csharp
var bookmarkService = new BookmarkService();

// Add bookmark
bookmarkService.AddBookmark(0x1000, "Important data", ScrollMarker.Bookmark);

// Navigate bookmarks
var nextBookmark = bookmarkService.GetNextBookmark(currentPosition);
if (nextBookmark != null)
{
    SetPosition(nextBookmark.BytePositionInStream);
}

// Check if position has bookmark
if (bookmarkService.HasBookmarkAt(position))
{
    // Display bookmark indicator
}
```

---

### 8. 📚 TblService
**Responsibility:** Manage TBL (character table) operations

**Main methods:**
- `LoadFromFile(string fileName)` - Load TBL from file
- `LoadDefault(DefaultCharacterTableType type)` - Load default table (ASCII, EBCDIC)
- `Clear()` - Clear current table
- `GetTblBookmarks()` - Get bookmarks from TBL
- `HasBookmarks()` - Check if table has bookmarks
- `GetBookmarkCount()` - Get count of TBL bookmarks
- `FindMatch(string hex, bool showSpecialValue)` - Find character match for hex
- `BytesToString(byte[] bytes)` - Convert bytes to TBL string
- `IsDefaultTable()` - Check if using default table
- `IsFileTable()` - Check if loaded from file
- `GetTableInfo()` - Get table description

**Usage:**
```csharp
var tblService = new TblService();

// Load TBL file
if (tblService.LoadFromFile("game.tbl"))
{
    // Use TBL for text display
    string text = tblService.BytesToString(bytes);

    // Get TBL bookmarks
    foreach (var bookmark in tblService.GetTblBookmarks())
    {
        Console.WriteLine($"TBL Bookmark: {bookmark.Description}");
    }
}

// Load default ASCII table
tblService.LoadDefault(DefaultCharacterTableType.Ascii);
```

**Note:** Used for ROM reverse engineering and game hacking to decode custom character encodings.

---

### 9. 📐 PositionService
**Responsibility:** Position calculations and conversions

**Main methods:**
- `GetLineNumber(long position, ...)` - Calculate line number for position
- `GetColumnNumber(long position, ...)` - Calculate column number
- `GetCountOfByteDeletedBeforePosition(long position, ByteProvider)` - Count deleted bytes
- `GetValidPositionFrom(long position, long correction, ByteProvider)` - Get valid position with deleted byte correction
- `HexLiteralToLong(string hexLiteral)` - Convert hex string to long
- `LongToHex(long position)` - Convert long to hex string
- `GetFirstVisibleBytePosition(...)` - Calculate first visible byte based on scroll
- `IsBytePositionVisible(long bytePosition, ...)` - Check if position is visible
- `IsPositionValid(long position, long maxLength)` - Validate position in range
- `ClampPosition(long position, long min, long max)` - Clamp position to range

**Usage:**
```csharp
var positionService = new PositionService();

// Get line/column
long line = positionService.GetLineNumber(position, byteShiftLeft, hideByteDeleted,
    bytePerLine, byteSizeRatio, provider);
long column = positionService.GetColumnNumber(position, hideByteDeleted, allowVisualByteAddress,
    visualStart, byteShiftLeft, bytePerLine, provider);

// Hex conversion
var (success, position) = positionService.HexLiteralToLong("0xFF00");
string hex = positionService.LongToHex(65535); // "FF00"

// Validate position
if (positionService.IsPositionValid(position, provider.Length))
{
    // Position is valid
}
```

---

### 10. 🎨 CustomBackgroundService
**Responsibility:** Manage custom background color blocks

**Main methods:**
- `AddBlock(CustomBackgroundBlock block)` - Add background block
- `AddBlock(long start, long length, SolidColorBrush color, string description)` - Add block with parameters
- `AddBlocks(IEnumerable<CustomBackgroundBlock> blocks)` - Add multiple blocks
- `RemoveBlock(CustomBackgroundBlock block)` - Remove specific block
- `RemoveBlocksAt(long position)` - Remove blocks at position
- `RemoveBlocksInRange(long start, long end)` - Remove blocks in range
- `ClearAll()` - Clear all blocks
- `GetAllBlocks()` - Get all blocks
- `GetBlockAt(long position)` - Get first block at position
- `GetBlocksAt(long position)` - Get all blocks at position (overlapping)
- `GetBlocksInRange(long start, long end)` - Get blocks in range
- `HasBlockAt(long position)` - Check if block exists at position
- `WouldOverlap(long start, long length)` - Check if new block would overlap
- `GetOverlappingBlocks(long start, long length)` - Get overlapping blocks

**Usage:**
```csharp
var backgroundService = new CustomBackgroundService();

// Add colored block
backgroundService.AddBlock(0x1000, 256, Brushes.LightBlue, "Header section");
backgroundService.AddBlock(0x2000, 512, Brushes.LightGreen, "Data section");

// Get block at position
var block = backgroundService.GetBlockAt(position);
if (block != null)
{
    // Apply block color to display
    BackgroundColor = block.Color;
}

// Check for overlaps before adding
if (!backgroundService.WouldOverlap(newStart, newLength))
{
    backgroundService.AddBlock(newStart, newLength, Brushes.Yellow);
}

// Get all blocks in viewport
foreach (var block in backgroundService.GetBlocksInRange(firstVisible, lastVisible))
{
    // Render colored background
}
```

**Note:** Useful for visually marking file structure sections (headers, data, padding, etc.)

---

## 🏗️ Architecture

```
HexEditor (Main WPF Control)
    ├── ClipboardService          (Copy/Paste/Cut)
    ├── FindReplaceService        (Search with cache)
    ├── UndoRedoService           (History management)
    ├── SelectionService          (Selection validation)
    ├── HighlightService          (Search result highlighting) [STATEFUL]
    ├── ByteModificationService   (Insert/Delete/Modify)
    ├── BookmarkService           (Bookmark management) [STATEFUL]
    ├── TblService                (Character tables) [STATEFUL]
    ├── PositionService           (Position calculations)
    └── CustomBackgroundService   (Background coloring) [STATEFUL]
```

## 📦 Benefits of this Architecture

1. **Separation of Concerns** - Each service has a single responsibility
2. **Testability** - Services can be unit tested in isolation
3. **Reusability** - Services can be used in other contexts
4. **Maintainability** - Code is easier to understand and modify
5. **Extensibility** - Easy to add new services
6. **Reduced Complexity** - HexEditor.xaml.cs reduced from managing everything to coordinating services
7. **Clear API** - Each service has well-defined public methods
8. **No Breaking Changes** - Public API of HexEditor preserved

## 🔧 Implementation Details

### Service Types

**Stateless Services** (6):
- ClipboardService
- FindReplaceService
- UndoRedoService
- SelectionService
- ByteModificationService
- PositionService

**Stateful Services** (4):
- HighlightService (maintains highlight dictionary)
- BookmarkService (maintains bookmark list)
- TblService (maintains current TBL table)
- CustomBackgroundService (maintains background block list)

### Design Principles

- **Dependency Injection**: All dependencies passed as method parameters
- **Return Values**: Methods return success/failure indicators instead of throwing exceptions
- **Validation**: All inputs validated at service boundary
- **Immutability**: Services don't modify passed parameters
- **Isolation**: Services don't depend on HexEditor or each other
- **UI Separation**: Services handle business logic, HexEditor handles UI updates

## 📝 Development Notes

- All services are in the `WpfHexaEditor.Services` namespace
- Services are stateless when possible (6/10 are stateless)
- Dependencies passed as parameters (no constructor injection)
- Services do NOT depend on HexEditor (strong decoupling)
- Services handle business logic, HexEditor handles UI updates (RefreshView, SetScrollMarker, UpdateStatusBar, etc.)
- Single-line methods use `=>` expression syntax
- All methods documented with XML comments

## 🐛 Bug Fixes

**Critical Bug Fixed - Search Cache Invalidation:**
- **Issue:** Search cache was never invalidated after data modifications
- **Impact:** Users received stale/incorrect search results after editing
- **Solution:** Added `ClearCache()` calls at all 11 data modification points in HexEditor
- **Locations:** ModifyByte, Paste, FillWithByte, ReplaceByte, ReplaceFirst, ReplaceAll, Undo handler, InsertByte, InsertBytes, DeleteSelection

## 🎯 Coverage

**What's covered by services:**
- ✅ Clipboard operations (Copy/Paste/Cut)
- ✅ Find and replace with caching
- ✅ Undo/redo history
- ✅ Selection validation and management
- ✅ Search result highlighting
- ✅ Byte modifications (insert/delete/modify)
- ✅ Bookmark management
- ✅ TBL character table operations
- ✅ Position calculations and conversions
- ✅ Custom background blocks

**What remains in HexEditor:**
- 🎨 WPF UI logic (dependency properties, visual tree, rendering)
- 🔄 Event handling and coordination
- 📊 Status bar updates
- 🖼️ ScrollBar and viewport management
- 🎯 Caret and focus management
- 🖱️ Mouse and keyboard interaction

## 📊 Statistics

- **Total Services**: 10
- **Lines Extracted**: ~2500+ lines of business logic
- **Stateless Services**: 6
- **Stateful Services**: 4
- **Public Methods**: ~150+
- **Zero Breaking Changes**: ✅
- **Compilation Errors**: 0
- **Warnings**: 0

## 🚀 Future Enhancements

While the current 10 services cover all significant business logic, potential enhancements could include:

- **Unit Tests**: Create comprehensive test suite for each service
- **Async Operations**: Add async variants for file I/O heavy operations
- **Event System**: Add event notifications for service state changes
- **Service Composition**: Create higher-level services that compose existing services
- **Performance Monitoring**: Add instrumentation to measure service call performance

---

## 📚 Related Documentation

- **[Performance Guide](../../../PERFORMANCE_GUIDE.md)** - ⚡ Service performance optimization and benchmarking
- [Architecture Documentation](../../../ARCHITECTURE.md) - Overall system architecture
- [Unit Tests](../../WPFHexaEditor.Tests/Services/) - Service test suite (275 tests)
- [Main README](../../../README.md) - Project overview
