# 📂 PartialClasses

This folder contains the **partial class implementation** of the `HexEditor` control, organized by functional category. The `HexEditor` class is split across multiple files for better maintainability and clear separation of concerns.

---

## 📖 Overview

The `HexEditor` control is implemented using **C# partial classes**, where each file handles a specific aspect of functionality:

```
HexEditor.cs (main)
├── Core/              → Essential operations
├── Features/          → Advanced features
├── Search/            → Search and replace
├── UI/                → User interface
└── Compatibility/     → Legacy V1 support
```

---

## 📂 Folder Structure

### 🎯 Core Operations (`Core/`)

Core functionality essential for the editor to work.

| File | Description | Key Methods |
|------|-------------|-------------|
| **HexEditor.FileOperations.cs** | File I/O operations | `Open()`, `Save()`, `SaveAs()`, `Close()` |
| **HexEditor.StreamOperations.cs** | Stream/memory operations | `OpenStream()`, `OpenMemory()` |
| **HexEditor.ByteOperations.cs** | Byte manipulation | `GetByte()`, `ModifyByte()`, `InsertByte()`, `DeleteBytes()` |
| **HexEditor.EditOperations.cs** | Edit operations | `Undo()`, `Redo()`, `Copy()`, `Cut()`, `Paste()`, `Clear*()` |
| **HexEditor.BatchOperations.cs** | Batch mode for performance | `BeginBatch()`, `EndBatch()` |
| **HexEditor.Diagnostics.cs** | Diagnostics and profiling | `GetDiagnostics()`, `GetCacheStatistics()` |
| **HexEditor.AsyncOperations.cs** | Async operations | Async save/load with progress |

### ✨ Advanced Features (`Features/`)

Optional features that enhance functionality.

| File | Description | Key Methods |
|------|-------------|-------------|
| **HexEditor.Bookmarks.cs** | Bookmark management | `AddBookmark()`, `RemoveBookmark()`, `ClearBookmarks()` |
| **HexEditor.CustomBackgroundBlocks.cs** | Custom highlighting | `SetHighlight()`, `ClearHighlight()` |
| **HexEditor.FileComparison.cs** | Binary file comparison | `CompareFiles()` |
| **HexEditor.StatePersistence.cs** | Save/load editor state | `SaveState()`, `LoadState()` |
| **HexEditor.TBL.cs** | TBL character tables | `LoadTBL()`, `UnloadTBL()` |

### 🔍 Search and Replace (`Search/`)

Search functionality and pattern matching.

| File | Description | Key Methods |
|------|-------------|-------------|
| **HexEditor.Search.cs** | Search operations | `FindFirst()`, `FindNext()`, `FindAll()`, `CountOccurrences()` |
| **HexEditor.FindReplace.cs** | Find and replace | `ReplaceFirst()`, `ReplaceNext()`, `ReplaceAll()` |

### 🎨 User Interface (`UI/`)

UI-related functionality and visual elements.

| File | Description | Key Methods |
|------|-------------|-------------|
| **HexEditor.Events.cs** | Event handlers | All event raising methods |
| **HexEditor.Clipboard.cs** | Clipboard operations | `CopyToClipboard()`, `PasteFromClipboard()` |
| **HexEditor.Highlights.cs** | Visual highlights | Highlight management |
| **HexEditor.Zoom.cs** | Zoom functionality | `ZoomIn()`, `ZoomOut()`, `ResetZoom()` |
| **HexEditor.UIHelpers.cs** | UI helper methods | Scroll, focus, visibility |

### 🔄 Compatibility Layer (`Compatibility/`)

V1 API compatibility for seamless migration.

| File | Description | Purpose |
|------|-------------|---------|
| **HexEditor.CompatibilityLayer.Properties.cs** | V1 properties | Legacy property wrappers |
| **HexEditor.CompatibilityLayer.Methods.cs** | V1 methods | Legacy method wrappers |

---

## 💡 Code Examples

### Example 1: Basic File Operations

```csharp
using WpfHexaEditor;

// Create editor
var hexEditor = new HexEditor();

// Open file (HexEditor.FileOperations.cs)
hexEditor.FileName = "data.bin";

// Modify byte (HexEditor.ByteOperations.cs)
hexEditor.ModifyByte(0x42, 0x100);

// Save changes (HexEditor.FileOperations.cs)
hexEditor.Save();
```

### Example 2: Search and Replace

```csharp
// Find pattern (HexEditor.Search.cs)
var pattern = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
long position = hexEditor.FindFirst(pattern);

if (position >= 0)
{
    Console.WriteLine($"Found at position: 0x{position:X}");

    // Replace pattern (HexEditor.FindReplace.cs)
    var replacement = new byte[] { 0xCA, 0xFE, 0xBA, 0xBE };
    hexEditor.ReplaceFirst(pattern, replacement);
}
```

### Example 3: Batch Operations for Performance

```csharp
// Begin batch mode (HexEditor.BatchOperations.cs)
hexEditor.BeginBatch();

try
{
    // Multiple operations without UI updates
    for (long i = 0; i < 1000; i++)
    {
        hexEditor.ModifyByte((byte)(i % 256), i);
    }
}
finally
{
    // End batch and update UI once (HexEditor.BatchOperations.cs)
    hexEditor.EndBatch();
}

// Result: 3x faster than individual operations
```

### Example 4: Using Bookmarks

```csharp
// Add bookmarks (HexEditor.Bookmarks.cs)
hexEditor.AddBookmark(0x100, "Header start");
hexEditor.AddBookmark(0x500, "Data section");
hexEditor.AddBookmark(0x1000, "Footer");

// Navigate to bookmark
var bookmarks = hexEditor.GetBookmarks();
hexEditor.SetPosition(bookmarks[0].Position);
```

### Example 5: Async Operations with Progress

```csharp
// Async save with progress (HexEditor.AsyncOperations.cs)
var progress = new Progress<double>(percent =>
{
    Console.WriteLine($"Saving: {percent:F1}%");
});

await hexEditor.SaveAsync("output.bin", progress);
```

### Example 6: Diagnostics and Profiling

```csharp
// Get diagnostics (HexEditor.Diagnostics.cs)
var diagnostics = hexEditor.GetDiagnostics();
Console.WriteLine($"File size: {diagnostics.FileSize} bytes");
Console.WriteLine($"Modifications: {diagnostics.ModificationCount}");
Console.WriteLine($"Insertions: {diagnostics.InsertionCount}");
Console.WriteLine($"Deletions: {diagnostics.DeletionCount}");

// Cache statistics (HexEditor.Diagnostics.cs)
var cacheStats = hexEditor.GetCacheStatistics();
Console.WriteLine($"Cache hit rate: {cacheStats.HitRate:F2}%");
```

### Example 7: Granular Clear Operations (New in V2)

```csharp
// Scenario: Keep structure but reset values
hexEditor.InsertBytes(0x1000, new byte[256]); // Add section
hexEditor.ModifyByte(0x10, 0x100);            // Change value
hexEditor.ModifyByte(0x20, 0x200);            // Change value

// Clear only modifications, keep insertion (HexEditor.EditOperations.cs)
hexEditor.ClearModifications();

// Result:
// - Insertion at 0x1000 preserved
// - Modifications at 0x10, 0x20 cleared
```

---

## 🏗️ Architecture Pattern

### Partial Class Benefits

1. **📁 Separation of Concerns** - Each file handles one aspect
2. **🔍 Easy Navigation** - Find functionality by category
3. **👥 Team Collaboration** - Multiple developers, less merge conflicts
4. **📖 Maintainability** - Smaller files, easier to understand
5. **🧪 Testability** - Clear boundaries for unit testing

### File Organization Logic

```
Core/       → What the editor MUST have
Features/   → What makes it powerful
Search/     → Specialized search features
UI/         → How users interact
Compatibility/ → Legacy support
```

---

## 🔗 Related Folders

- **[Core/Bytes/](../Core/Bytes/)** - ByteProvider system (data layer)
- **[Services/](../Services/)** - Service layer (15 specialized services)
- **[Dialog/](../Dialog/)** - Dialog windows (Find, Replace, GoTo)

---

## 📚 Documentation

### API Reference
- **[File Operations](../../docs/api-reference/file-operations/)** - Open, Save, Close APIs
- **[Byte Operations](../../docs/api-reference/byte-operations/)** - Get, Modify, Insert, Delete APIs
- **[Search Operations](../../docs/api-reference/search/)** - Find, Replace, Count APIs
- **[Editing Operations](../../docs/api-reference/editing/)** - Undo, Redo, Clear APIs

### Guides
- **[Getting Started](../../GETTING_STARTED.md)** - Tutorial
- **[Architecture](../../docs/architecture/)** - System design
- **[Performance Guide](../../docs/performance/)** - Optimization tips

---

## 🆕 What's New in V2

### New APIs Added
- ✅ **OpenStream()** / **OpenMemory()** - Stream operations
- ✅ **BeginBatch()** / **EndBatch()** - Batch mode
- ✅ **GetDiagnostics()** - Full diagnostics
- ✅ **ModifyBytes()** - Batch byte modification
- ✅ **CountOccurrences()** - Memory-efficient counting
- ✅ **ClearModifications()** / **ClearInsertions()** / **ClearDeletions()** - Granular clear

### Improvements
- ⚡ **3x faster** batch operations
- 🧹 **Granular undo** - Clear specific edit types
- 📊 **Rich diagnostics** - Cache stats, memory usage
- 🔄 **Async everything** - All I/O operations async

---

## 🎯 Quick Reference

| Task | Method | File |
|------|--------|------|
| Open file | `FileName = "file.bin"` | FileOperations.cs |
| Open stream | `OpenStream(stream)` | StreamOperations.cs |
| Get byte | `GetByte(position)` | ByteOperations.cs |
| Modify byte | `ModifyByte(value, pos)` | ByteOperations.cs |
| Find pattern | `FindFirst(pattern)` | Search.cs |
| Replace all | `ReplaceAll(find, replace)` | FindReplace.cs |
| Add bookmark | `AddBookmark(pos, desc)` | Bookmarks.cs |
| Undo/Redo | `Undo()` / `Redo()` | EditOperations.cs |
| Save | `Save()` | FileOperations.cs |

---

**Last Updated:** 2026-02-19
**Version:** V2.0
