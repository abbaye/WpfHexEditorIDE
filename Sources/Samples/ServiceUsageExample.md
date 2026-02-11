# Service Usage Examples

This document demonstrates how to use WPF HexEditor's service layer directly in your applications.

## 🎯 Why Use Services Directly?

The services provide reusable business logic that can be used:
- In automated testing scenarios
- In console applications
- In custom UI implementations
- For batch processing operations

---

## 📋 ClipboardService Example

```csharp
using WpfHexaEditor.Services;
using WpfHexaEditor.Core.Bytes;

// Initialize service
var clipboardService = new ClipboardService
{
    DefaultCopyMode = CopyPasteMode.HexaString
};

// Open a file
var provider = new ByteProvider("myfile.bin");

// Copy 100 bytes starting at position 0
bool success = clipboardService.CopyToClipboard(provider, 0, 99, null);

if (success)
{
    Console.WriteLine("Data copied to clipboard!");
}

// Get copy data as byte array
byte[] copiedData = clipboardService.GetCopyData(provider, 0, 99, null, CopyPasteMode.ByteArray);
Console.WriteLine($"Copied {copiedData.Length} bytes");

// Check if copy is possible
if (clipboardService.CanCopy(100, provider))
{
    // Copy operation
}
```

---

## 🔍 FindReplaceService Example

```csharp
using WpfHexaEditor.Services;
using System.Diagnostics;

// Initialize service
var findService = new FindReplaceService();

// Open a file
var provider = new ByteProvider("data.bin");

// Search for a byte pattern
byte[] searchPattern = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

// Find first occurrence
long position = findService.FindFirst(provider, searchPattern);
Console.WriteLine($"First occurrence at position: {position:X}");

// Find all occurrences with caching
var allPositions = findService.FindAllCached(provider, searchPattern);
Console.WriteLine($"Found {allPositions.Count()} occurrences");

foreach (var pos in allPositions.Take(10))
{
    Console.WriteLine($"  - Position: 0x{pos:X}");
}

// Replace all occurrences
byte[] replacePattern = new byte[] { 0x57, 0x6F, 0x72, 0x6C, 0x64 }; // "World"
var replacedPositions = findService.ReplaceAll(provider, searchPattern, replacePattern, false, false);
Console.WriteLine($"Replaced {replacedPositions.Count()} occurrences");

// Clear cache after modifications
findService.ClearCache();
```

### Debug Output Example

When running in DEBUG mode, you'll see output like:
```
[14:32:15.123] [FindReplaceService] FindFirst: data.Length=5, startPosition=0
[14:32:15.145] [FindReplaceService] FindFirst: Completed in 22ms, position=100
[14:32:15.150] [FindReplaceService] FindAllCached: data.Length=5, startPosition=0
[14:32:15.890] [FindReplaceService] FindAllCached: Returned 42 results (cache hit: False)
[14:32:16.001] [FindReplaceService] ClearCache: Invalidating search cache
```

---

## ↩️ UndoRedoService Example

```csharp
using WpfHexaEditor.Services;

var undoService = new UndoRedoService();
var provider = new ByteProvider("document.bin");

// Make some modifications
provider.AddByteModified(0x00, 0xFF, 100);
provider.AddByteModified(0x11, 0xAA, 101);

// Check if undo is possible
if (undoService.CanUndo(provider))
{
    // Undo last action
    long position = undoService.Undo(provider);
    Console.WriteLine($"Undone modification at position {position}");
}

// Get undo count
int undoCount = undoService.GetUndoCount(provider);
Console.WriteLine($"Undo stack has {undoCount} actions");

// Redo multiple times
if (undoService.CanRedo(provider))
{
    long position = undoService.Redo(provider, repeat: 2);
    Console.WriteLine($"Redone 2 actions, last at position {position}");
}

// Clear all history
undoService.ClearAll(provider);
```

---

## 🎯 SelectionService Example

```csharp
using WpfHexaEditor.Services;

var selectionService = new SelectionService();
var provider = new ByteProvider("file.bin");

// Validate selection
long start = 100;
long stop = 200;

if (selectionService.IsValidSelection(start, stop))
{
    // Get selection length
    long length = selectionService.GetSelectionLength(start, stop);
    Console.WriteLine($"Selection: {length} bytes");

    // Get selected bytes
    byte[] selectedData = selectionService.GetSelectionBytes(provider, start, stop);
    Console.WriteLine($"First byte: 0x{selectedData[0]:X2}");
}

// Fix inverted selection
var (fixedStart, fixedStop) = selectionService.FixSelectionRange(stop, start);
Console.WriteLine($"Fixed range: {fixedStart} - {fixedStop}");

// Validate and adjust to bounds
var (validStart, validStop) = selectionService.ValidateSelection(provider, -10, 999999);
Console.WriteLine($"Validated range: {validStart} - {validStop}");

// Select all
long selectAllStart = selectionService.GetSelectAllStart(provider);
long selectAllStop = selectionService.GetSelectAllStop(provider);
Console.WriteLine($"Select all: {selectAllStart} - {selectAllStop}");
```

---

## ✨ HighlightService Example

```csharp
using WpfHexaEditor.Services;

var highlightService = new HighlightService();

// Add highlight for search results
highlightService.AddHighLight(100, 5); // Highlight 5 bytes at position 100
highlightService.AddHighLight(200, 3); // Highlight 3 bytes at position 200

// Check if position is highlighted
if (highlightService.IsHighlighted(102))
{
    Console.WriteLine("Position 102 is highlighted");
}

// Get all highlighted positions
var positions = highlightService.GetHighlightedPositions();
Console.WriteLine($"Total highlighted positions: {positions.Count()}");

// Get optimized ranges (consecutive bytes grouped)
var ranges = highlightService.GetHighlightedRanges();
foreach (var (start, length) in ranges)
{
    Console.WriteLine($"Highlighted range: 0x{start:X} ({length} bytes)");
}

// Clear specific highlight
highlightService.RemoveHighLight(100, 5);

// Clear all highlights
highlightService.UnHighLightAll();
```

---

## 🔧 ByteModificationService Example

```csharp
using WpfHexaEditor.Services;

var modService = new ByteModificationService();
var provider = new ByteProvider("data.bin");
bool readOnlyMode = false;
bool allowInsert = true;
bool allowDelete = true;

// Modify a byte
if (modService.ModifyByte(provider, 0xFF, 100, 1, readOnlyMode))
{
    Console.WriteLine("Byte modified successfully");
}

// Insert bytes
byte[] newData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
int inserted = modService.InsertBytes(provider, newData, 50, allowInsert);
Console.WriteLine($"Inserted {inserted} bytes");

// Delete a range
long lastPosition = modService.DeleteRange(provider, 100, 200, readOnlyMode, allowDelete);
Console.WriteLine($"Deleted range, last position: {lastPosition}");

// Check permissions before operations
if (modService.CanModify(provider, readOnlyMode))
{
    // Safe to modify
}

if (modService.CanInsert(provider, allowInsert))
{
    // Safe to insert
}

if (modService.CanDelete(provider, readOnlyMode, allowDelete))
{
    // Safe to delete
}
```

---

## 🔖 BookmarkService Example

```csharp
using WpfHexaEditor.Services;
using WpfHexaEditor.Core;

var bookmarkService = new BookmarkService();

// Add bookmarks
bookmarkService.AddBookmark(0x1000, "File header", ScrollMarker.Bookmark);
bookmarkService.AddBookmark(0x2000, "Data section", ScrollMarker.Bookmark);
bookmarkService.AddBookmark(0x3000, "Important offset", ScrollMarker.Bookmark);

// Navigate bookmarks
long currentPos = 0x1500;

var nextBookmark = bookmarkService.GetNextBookmark(currentPos);
if (nextBookmark != null)
{
    Console.WriteLine($"Next bookmark: {nextBookmark.Description} at 0x{nextBookmark.BytePositionInStream:X}");
}

var prevBookmark = bookmarkService.GetPreviousBookmark(currentPos);
if (prevBookmark != null)
{
    Console.WriteLine($"Previous bookmark: {prevBookmark.Description} at 0x{prevBookmark.BytePositionInStream:X}");
}

// Check if position has bookmark
if (bookmarkService.HasBookmarkAt(0x1000))
{
    var bookmark = bookmarkService.GetBookmarkAt(0x1000);
    Console.WriteLine($"Bookmark found: {bookmark.Description}");
}

// Update bookmark description
bookmarkService.UpdateBookmarkDescription(0x1000, "Updated header info");

// Get all bookmarks
foreach (var bookmark in bookmarkService.GetAllBookmarks())
{
    Console.WriteLine($"0x{bookmark.BytePositionInStream:X}: {bookmark.Description}");
}

// Remove bookmark
bookmarkService.RemoveBookmark(0x2000);

// Clear all
bookmarkService.ClearAll();
```

---

## 📚 TblService Example

```csharp
using WpfHexaEditor.Services;
using WpfHexaEditor.Core.CharacterTable;

var tblService = new TblService();

// Load TBL file for ROM hacking
if (tblService.LoadFromFile("game.tbl"))
{
    Console.WriteLine("TBL file loaded successfully");

    // Convert bytes to text using TBL
    byte[] romData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
    string decodedText = tblService.BytesToString(romData);
    Console.WriteLine($"Decoded text: {decodedText}");

    // Get TBL bookmarks
    if (tblService.HasBookmarks())
    {
        foreach (var bookmark in tblService.GetTblBookmarks())
        {
            Console.WriteLine($"TBL Bookmark: {bookmark.Description}");
        }
    }
}

// Load default ASCII table
tblService.LoadDefault(DefaultCharacterTableType.Ascii);

// Get table info
string info = tblService.GetTableInfo();
Console.WriteLine($"Current table: {info}");

// Check table type
if (tblService.IsDefaultTable())
{
    Console.WriteLine("Using default table");
}

if (tblService.IsFileTable())
{
    Console.WriteLine("Using custom TBL file");
}

// Clear table
tblService.Clear();
```

---

## 📐 PositionService Example

```csharp
using WpfHexaEditor.Services;

var positionService = new PositionService();
var provider = new ByteProvider("file.bin");

// Calculate line and column
long position = 0x1234;
int bytePerLine = 16;
long byteShiftLeft = 0;
bool hideByteDeleted = false;
int byteSizeRatio = 1;

long line = positionService.GetLineNumber(position, byteShiftLeft, hideByteDeleted,
    bytePerLine, byteSizeRatio, provider);

long column = positionService.GetColumnNumber(position, hideByteDeleted, false,
    0, byteShiftLeft, bytePerLine, provider);

Console.WriteLine($"Position 0x{position:X} is at line {line}, column {column}");

// Hex conversion
var (success, parsedPosition) = positionService.HexLiteralToLong("0xFF00");
if (success)
{
    Console.WriteLine($"Parsed hex: {parsedPosition} (decimal)");
}

string hexString = positionService.LongToHex(65535);
Console.WriteLine($"65535 in hex: {hexString}");

// Position validation
if (positionService.IsPositionValid(position, provider.Length))
{
    Console.WriteLine("Position is valid");
}

// Clamp position to valid range
long clampedPos = positionService.ClampPosition(position, 0, provider.Length - 1);
Console.WriteLine($"Clamped position: 0x{clampedPos:X}");
```

---

## 🎨 CustomBackgroundService Example

```csharp
using WpfHexaEditor.Services;
using System.Windows.Media;

var bgService = new CustomBackgroundService();

// Add colored regions
bgService.AddBlock(0x0000, 256, Brushes.LightBlue, "Header");
bgService.AddBlock(0x0100, 512, Brushes.LightGreen, "Data section");
bgService.AddBlock(0x0300, 128, Brushes.Yellow, "Metadata");

// Check for overlaps before adding
if (!bgService.WouldOverlap(0x0500, 64))
{
    bgService.AddBlock(0x0500, 64, Brushes.LightCoral, "Special data");
}

// Get block at position
var block = bgService.GetBlockAt(0x0150);
if (block != null)
{
    Console.WriteLine($"Block at 0x150: {block.Description} (Color: {block.Color})");
}

// Get all blocks in a range (viewport)
long firstVisible = 0x0100;
long lastVisible = 0x0200;

foreach (var b in bgService.GetBlocksInRange(firstVisible, lastVisible))
{
    Console.WriteLine($"Visible block: {b.Description} at 0x{b.StartOffset:X}");
}

// Get overlapping blocks for a new region
var overlaps = bgService.GetOverlappingBlocks(0x0180, 64);
Console.WriteLine($"Would overlap with {overlaps.Count()} existing blocks");

// Remove blocks at position
int removed = bgService.RemoveBlocksAt(0x0150);
Console.WriteLine($"Removed {removed} blocks");

// Get all blocks sorted
foreach (var b in bgService.GetBlocksSorted())
{
    Console.WriteLine($"0x{b.StartOffset:X}-0x{b.StopOffset:X}: {b.Description}");
}
```

---

## 🔗 Combining Multiple Services

```csharp
// Real-world example: Search, highlight results, and add bookmarks

var findService = new FindReplaceService();
var highlightService = new HighlightService();
var bookmarkService = new BookmarkService();
var provider = new ByteProvider("data.bin");

// Search for pattern
byte[] pattern = new byte[] { 0xFF, 0xD8, 0xFF }; // JPEG header
var results = findService.FindAllCached(provider, pattern);

Console.WriteLine($"Found {results.Count()} JPEG headers");

// Highlight and bookmark each result
int count = 1;
foreach (var position in results)
{
    // Highlight the match
    highlightService.AddHighLight(position, pattern.Length);

    // Add bookmark
    bookmarkService.AddBookmark(position, $"JPEG #{count}", ScrollMarker.SearchMatch);

    count++;
}

Console.WriteLine($"Highlighted {highlightService.GetHighlightCount()} positions");
Console.WriteLine($"Added {bookmarkService.GetBookmarkCount()} bookmarks");

// Navigate through results using bookmarks
long currentPos = 0;
while (true)
{
    var nextMatch = bookmarkService.GetNextBookmark(currentPos, ScrollMarker.SearchMatch);
    if (nextMatch == null)
        break;

    Console.WriteLine($"Next match at: 0x{nextMatch.BytePositionInStream:X}");
    currentPos = nextMatch.BytePositionInStream;
}
```

---

## 🚀 Performance Tips

1. **Use caching wisely**
   - `FindAllCached()` is faster for repeated searches
   - Clear cache after data modifications

2. **Batch operations**
   - Use `AddBlocks()` instead of multiple `AddBlock()` calls
   - Use `GetBlocksInRange()` to get only visible blocks

3. **Check before acting**
   - Use `Can*()` methods before operations
   - Validate selections with `ValidateSelection()`

4. **Debug mode logging**
   - Logs only appear in DEBUG builds
   - Zero performance impact in RELEASE

---

## 📚 Next Steps

- **Unit testing:** Use services in your unit tests for isolated testing
- **Custom UI:** Build alternative UIs using the same business logic
- **Automation:** Script batch operations using services
- **Extensions:** Create custom services following the same pattern

See [Services Documentation](../WPFHexaEditor/Services/README.md) for complete API reference.
