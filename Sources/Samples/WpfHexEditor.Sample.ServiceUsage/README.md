# WpfHexEditor.Sample.ServiceUsage

## 📋 Description

This console application demonstrates how to use all 10 services from WPFHexaEditor directly without the HexEditor UI control. This is useful for:

- Understanding the service architecture
- Using services in headless/automated scenarios
- Learning the service APIs
- Testing service functionality
- Building custom tools with WPFHexaEditor services

## 🎯 Purpose

Shows practical usage of:

1. **SelectionService** - Selection validation and byte retrieval
2. **FindReplaceService** - Searching and replacing patterns
3. **ClipboardService** - Copy/paste operations
4. **HighlightService** - Managing highlighted positions
5. **ByteModificationService** - Inserting, deleting, and modifying bytes
6. **UndoRedoService** - Undo/redo history management
7. **BookmarkService** - Bookmark management and navigation
8. **CustomBackgroundService** - Background color blocks
9. **PositionService** - Position calculations and conversions
10. **TblService** - Character table operations

## 🚀 Running the Sample

```bash
cd Sources/Samples/WpfHexEditor.Sample.ServiceUsage
dotnet run
```

## 📖 What It Does

The sample:

1. Creates a temporary test file with recognizable patterns
2. Opens it with a `ByteProvider`
3. Demonstrates each service with practical examples
4. Shows console output explaining each operation
5. Cleans up the temporary file

## 💡 Key Concepts Demonstrated

### Service Independence
- Services work without the HexEditor UI
- Each service is instantiated and used independently
- Services only depend on `ByteProvider` (for data services)

### Stateless vs Stateful Services
- **Stateless** (6): SelectionService, FindReplaceService, ClipboardService, ByteModificationService, UndoRedoService, PositionService
- **Stateful** (4): HighlightService, BookmarkService, CustomBackgroundService, TblService

### Common Patterns
```csharp
// Stateless service - no state to maintain
var selectionService = new SelectionService();
var bytes = selectionService.GetSelectionBytes(provider, start, stop);

// Stateful service - maintains internal state
var highlightService = new HighlightService();
highlightService.AddHighLight(100, 4);
bool isHighlighted = highlightService.IsHighlighted(102); // true
```

## 📚 Code Highlights

### Demo 1: SelectionService
- Validates selections
- Fixes inverted ranges
- Retrieves selected bytes
- Adjusts out-of-bounds selections

### Demo 2: FindReplaceService
- Searches for byte patterns
- Finds first, last, and all occurrences
- Uses search cache for performance
- Demonstrates cache management

### Demo 3: ClipboardService
- Checks copy/delete permissions
- Prepares copy data
- Supports multiple copy modes

### Demo 4: HighlightService
- Adds/removes highlights
- Checks highlight status
- Groups consecutive ranges
- Manages highlight state

### Demo 5: ByteModificationService
- Checks modification permissions
- Modifies, inserts, deletes bytes
- Validates operations

### Demo 6: UndoRedoService
- Manages undo/redo stack
- Performs undo/redo operations
- Clears history

### Demo 7: BookmarkService
- Creates bookmarks with descriptions
- Navigates between bookmarks
- Filters by marker type
- Manages bookmark state

### Demo 8: CustomBackgroundService
- Defines colored regions
- Checks for overlaps
- Queries blocks by position/range
- Manages block state

### Demo 9: PositionService
- Calculates line/column numbers
- Converts hex strings
- Validates and clamps positions
- Handles position arithmetic

### Demo 10: TblService
- Loads character tables
- Converts bytes to strings
- Manages TBL bookmarks
- Handles custom encodings

## 🔧 Requirements

- .NET 8.0 or later
- Windows (for WPF dependencies like `SolidColorBrush`)

## 📝 Notes

- All services are in the `WpfHexaEditor.Services` namespace
- Services use dependency injection via method parameters
- No UI thread required - services are thread-safe for read operations
- The sample creates a 1KB test file with recognizable patterns

## 🎓 Learning Path

1. Run the sample to see console output
2. Read the code to understand service APIs
3. Modify demos to experiment with different operations
4. Use services in your own applications

## 🔗 Related Documentation

- [Services/README.md](../../WPFHexaEditor/Services/README.md) - Complete service documentation
- [ARCHITECTURE.md](../../../ARCHITECTURE.md) - Architecture overview
- [ServiceUsageExample.md](../../WPFHexaEditor/Services/ServiceUsageExample.md) - Code examples

---

✨ Sample by Derek Tremblay and contributors
