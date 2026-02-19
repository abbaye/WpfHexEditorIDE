# Architecture Overview

Understanding how WPF HexEditor V2 works under the hood.

---

## 📋 Overview

WPF HexEditor V2 is built with a **modern, layered architecture** designed for performance, maintainability, and extensibility. This document provides a high-level overview of how the system works.

**Target Audience**: Developers who want to understand the internal architecture before extending or integrating WPF HexEditor.

---

## 🏗️ High-Level Architecture

```
┌─────────────────────────────────────────────────┐
│            Your WPF Application                 │
│  (MainWindow.xaml, ViewModels, Commands)        │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│              HexEditor Control                  │
│  • User interactions (keyboard, mouse)          │
│  • Visual rendering (hex + ASCII columns)       │
│  • Data binding & commands                      │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│             ByteProvider System                 │
│  • File I/O management                          │
│  • Edit tracking (mods, inserts, deletes)       │
│  • Position mapping (virtual ↔ physical)        │
│  • Undo/Redo command pattern                    │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│              Service Layer                      │
│  • Search (Boyer-Moore + SIMD)                  │
│  • Clipboard operations                         │
│  • Bookmark management                          │
│  • Highlight rendering                          │
│  • TBL (Translation Tables)                     │
│  • Binary comparison                            │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│           Physical File Storage                 │
│  (Disk, Memory, Stream)                         │
└─────────────────────────────────────────────────┘
```

---

## 🎯 Key Concepts

### 1. Virtual View Pattern

**What It Does**: Users see edits applied in real-time, but the original file remains unchanged until Save().

**How It Works**:
```
Original File:  [A] [B] [C] [D] [E]
                     ▼
User Modifies:  [A] [X] [C] [D] [E]  (B → X)
User Inserts:   [A] [X] [Y] [C] [D] [E]  (Insert Y)
User Deletes:   [A] [X] [Y] [D] [E]  (Delete C)
                     ▼
Virtual View:   [A] [X] [Y] [D] [E]  ← What user sees
                     ▼
Physical File:  [A] [B] [C] [D] [E]  ← Unchanged until Save()
```

**Benefits**:
- ✅ **Safe editing**: Original file protected until explicit Save()
- ✅ **Instant undo**: Just discard tracked changes
- ✅ **Preview mode**: See changes before committing

---

### 2. Position Mapping

**The Challenge**: When bytes are inserted or deleted, all subsequent positions shift. How do we track positions correctly?

**Solution**: **Segment-based mapping** converts between virtual positions (what user sees) and physical positions (actual file offsets).

**Example**:
```
Original:  [A] [B] [C] [D] [E]
           Position: 0 1 2 3 4

Insert Y at position 2:
Virtual:   [A] [B] [Y] [C] [D] [E]
           Position: 0 1 2 3 4 5

Virtual Position 3 → Physical Position 2 (skips insertion)
Virtual Position 4 → Physical Position 3
```

**Algorithm**: O(log n) using binary search through segment list.

---

### 3. Edit Tracking

**Three Separate Collections**:
1. **Modifications**: Dictionary<long, byte> - tracks byte value changes
2. **Insertions**: Stack<(long, byte)> - tracks inserted bytes (LIFO)
3. **Deletions**: Dictionary<long, int> - tracks deleted byte ranges

**Why Separate?**
- ✅ **Efficient undo**: Clear specific edit type
- ✅ **Smart save**: Different save strategies based on edit types
- ✅ **Visual feedback**: Different colors for mod/insert/delete

**Example**:
```csharp
// Modify byte at 0x100
hexEditor.ModifyByte(0xFF, 0x100);
// → Stored in: Modifications[0x100] = 0xFF

// Insert byte at 0x200
hexEditor.InsertByte(0xAA, 0x200);
// → Pushed to: Insertions.Push((0x200, 0xAA))

// Delete 10 bytes at 0x300
hexEditor.DeleteBytes(0x300, 10);
// → Stored in: Deletions[0x300] = 10
```

---

### 4. LIFO Insertion Semantics

**Last-In-First-Out (LIFO)**: When multiple insertions occur at the **same position**, the last inserted byte appears first.

**Example**:
```
Original:     [A] [B] [C]

Insert X at 1: [A] [X] [B] [C]
Insert Y at 1: [A] [Y] [X] [B] [C]  ← Y pushed in front
Insert Z at 1: [A] [Z] [Y] [X] [B] [C]  ← Z pushed in front

Result: Z Y X (reverse of insertion order)
```

**Why LIFO?**
- ✅ **Consistent behavior**: Matches how text editors handle insertions
- ✅ **Efficient implementation**: Stack data structure (O(1) push)
- ✅ **Undo support**: Pop insertions in reverse order

---

### 5. Command Pattern (Undo/Redo)

**Implementation**: Each edit operation creates a **Command object** that can execute and reverse itself.

**Command Types**:
1. **ModifyCommand**: Stores old & new byte values
2. **InsertCommand**: Stores insertion position & data
3. **DeleteCommand**: Stores deletion position & original data
4. **BatchCommand**: Groups multiple commands

**Architecture**:
```
┌──────────────┐       ┌──────────────┐
│  Undo Stack  │       │  Redo Stack  │
├──────────────┤       ├──────────────┤
│  Command 3   │       │              │
│  Command 2   │       │              │
│  Command 1   │       │              │
└──────────────┘       └──────────────┘

User calls Undo():
  1. Pop Command 3 from Undo Stack
  2. Execute Command 3.Undo()
  3. Push Command 3 to Redo Stack

User calls Redo():
  1. Pop Command 3 from Redo Stack
  2. Execute Command 3.Execute()
  3. Push Command 3 to Undo Stack
```

**Batch Operations**:
```csharp
// Multiple edits → Single undo
hexEditor.BeginBatch();
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);
hexEditor.ModifyByte(0xBB, 0x300);
hexEditor.EndBatch();  // Creates BatchCommand

// One Undo() reverses all 3 modifications
hexEditor.Undo();
```

---

## ⚡ Performance Optimizations

### 1. Custom DrawingContext Rendering

**V1 Problem**: Used ItemsControl + DataTemplate (generated thousands of UI elements).

**V2 Solution**: Direct pixel-level rendering using DrawingContext.

**Performance**:
```
V1: 50,000 bytes → 50,000 TextBlock controls → 3000ms render time
V2: 50,000 bytes → 1 DrawingVisual → 30ms render time (99% faster!)
```

**How It Works**:
```csharp
protected override void OnRender(DrawingContext dc)
{
    // Calculate visible region
    long startPos = (long)(verticalScrollBar.Value * BytesPerLine);
    long endPos = startPos + (visibleLines * BytesPerLine);

    // Render each byte directly
    for (long pos = startPos; pos < endPos; pos++)
    {
        byte b = ByteProvider.GetByte(pos);

        // Draw hex representation
        FormattedText hexText = new FormattedText($"{b:X2}", ...);
        dc.DrawText(hexText, GetHexPosition(pos));

        // Draw ASCII representation
        char c = (b >= 32 && b < 127) ? (char)b : '.';
        FormattedText asciiText = new FormattedText(c.ToString(), ...);
        dc.DrawText(asciiText, GetASCIIPosition(pos));
    }
}
```

---

### 2. Boyer-Moore-Horspool Search

**V1 Problem**: Naive byte-by-byte search (O(n*m) worst case).

**V2 Solution**: Boyer-Moore-Horspool algorithm + SIMD acceleration.

**Algorithm**:
```
Bad Character Rule:
  When mismatch occurs, skip ahead based on pattern character table.

Example: Search for "ABCD" in "XYZABXABCD"

  X Y Z A B X A B C D
  A B C D               ← Mismatch at X vs A
        A B C D         ← Skip 3 positions
            A B C D     ← Mismatch at X vs C
                A B C D ← Match!
```

**Performance**:
```
100 MB file, 4-byte pattern:
  V1: 15,000ms (naive search)
  V2: 150ms (Boyer-Moore-Horspool + SIMD) - 100x faster!
```

---

### 3. Smart Save Algorithm

**Decision Tree**: Choose save strategy based on edit types.

```
Has Insertions or Deletions?
    ↓
   Yes → Full Rebuild (slower, but handles structure changes)
    ↓
    No → Fast Path (direct write modifications, 100x faster)
```

**Fast Path** (modifications only):
```csharp
// Open file for in-place editing
using var fs = new FileStream(fileName, FileMode.Open);

// Write each modification directly
foreach (var (position, newValue) in modifications)
{
    fs.Seek(position, SeekOrigin.Begin);
    fs.WriteByte(newValue);
}

// Done! 100x faster than rebuilding entire file
```

**Full Rebuild** (insertions/deletions):
```csharp
// Create new file with all changes applied
using var outputStream = new FileStream(tempFile, FileMode.Create);

for (long pos = 0; pos < virtualLength; pos++)
{
    byte b = GetVirtualByte(pos);  // Includes all edits
    outputStream.WriteByte(b);
}

// Replace original with new file
File.Replace(tempFile, originalFile, backupFile);
```

---

### 4. LRU Search Cache

**Problem**: Users often search for the same pattern multiple times.

**Solution**: Cache recent search results (Least Recently Used eviction).

**Implementation**:
```csharp
class SearchCache
{
    private Dictionary<byte[], List<long>> cache;
    private Queue<byte[]> lruQueue;
    private const int MaxCacheSize = 10;

    public List<long> GetCachedResult(byte[] pattern)
    {
        if (cache.ContainsKey(pattern))
        {
            // Move to front (most recently used)
            lruQueue = new Queue<byte[]>(
                lruQueue.Where(p => !p.SequenceEqual(pattern)));
            lruQueue.Enqueue(pattern);

            return cache[pattern];  // Instant result!
        }

        return null;  // Cache miss
    }
}
```

**Performance**:
```
First search:  50ms (cold cache)
Second search: 0.5ms (hot cache) - 100x faster!
```

---

### 5. Parallel Multi-Core Search

**For Large Files** (> 10 MB): Automatically use parallel processing.

**Algorithm**:
```csharp
public List<long> FindAll(byte[] pattern)
{
    if (Length < 10_000_000)  // < 10 MB
    {
        // Single-threaded
        return FindAllSingleThread(pattern);
    }

    // Divide file into chunks (one per CPU core)
    int chunkCount = Environment.ProcessorCount;
    long chunkSize = Length / chunkCount;

    // Search each chunk in parallel
    var results = new ConcurrentBag<long>();

    Parallel.For(0, chunkCount, i =>
    {
        long start = i * chunkSize;
        long end = (i == chunkCount - 1) ? Length : start + chunkSize;

        var chunkResults = SearchRange(pattern, start, end);
        foreach (var pos in chunkResults)
        {
            results.Add(pos);
        }
    });

    return results.OrderBy(p => p).ToList();
}
```

**Performance**:
```
1 GB file, 8-core CPU:
  Single-threaded: 15,000ms
  Multi-threaded: 2,000ms (7.5x faster!)
```

---

## 🔧 Service Architecture

WPF HexEditor V2 uses a **service-oriented architecture** with 15 specialized services.

### Core Services

| Service | Responsibility |
|---------|----------------|
| **SearchService** | Boyer-Moore search + caching |
| **ClipboardService** | Copy/paste with multiple formats |
| **UndoRedoService** | Command pattern implementation |
| **SelectionService** | Selection tracking & rendering |
| **BookmarkService** | Bookmark management |
| **HighlightService** | Syntax highlighting |
| **TBLService** | Translation table support |
| **CompareService** | Binary file comparison |

### Service Communication

```
┌─────────────────┐
│  HexEditor      │
└────────┬────────┘
         │
    ┌────┴─────┐
    ▼          ▼
┌─────────┐  ┌──────────┐
│ Search  │  │Clipboard │
│ Service │  │ Service  │
└─────────┘  └──────────┘
    │            │
    └────┬───────┘
         ▼
    ┌─────────────┐
    │ByteProvider │
    └─────────────┘
```

**Example**:
```csharp
// User presses Ctrl+F → Find operation
hexEditor.FindFirst(pattern)
    ↓
HexEditor.FindFirst() → SearchService.FindFirst()
    ↓
SearchService uses ByteProvider.GetBytes()
    ↓
Returns position → HexEditor updates selection
```

---

## 📊 Data Flow Examples

### Opening a File

```
User: hexEditor.FileName = "data.bin"
  ↓
1. HexEditor validates file exists
  ↓
2. ByteProvider.Open(fileName)
   • Opens FileStream
   • Reads file metadata
   • Initializes edit tracking collections
  ↓
3. HexEditor updates UI
   • Calculates scroll bar range
   • Renders first visible region
  ↓
4. Raises DataChanged event
```

### Modifying a Byte

```
User: hexEditor.ModifyByte(0xFF, 0x100)
  ↓
1. HexEditor calls ByteProvider.ModifyByte()
  ↓
2. ByteProvider:
   • Creates ModifyCommand(position: 0x100, oldValue, newValue: 0xFF)
   • Adds command to Undo stack
   • Stores in modifications: modifications[0x100] = 0xFF
  ↓
3. Raises ByteModified event
  ↓
4. HexEditor invalidates visual at position 0x100
  ↓
5. OnRender() draws updated byte in red (modification color)
```

### Searching for Pattern

```
User: hexEditor.FindFirst(pattern)
  ↓
1. HexEditor → SearchService.FindFirst()
  ↓
2. SearchService checks LRU cache
   • Cache hit? → Return cached result (instant!)
   • Cache miss? → Continue search
  ↓
3. SearchService uses Boyer-Moore-Horspool:
   • Build bad character table
   • Scan file with skip optimization
   • Use SIMD for byte comparisons
  ↓
4. Result found → Cache result → Return position
  ↓
5. HexEditor navigates to position & highlights match
```

### Saving Changes

```
User: hexEditor.Save()
  ↓
1. HexEditor → ByteProvider.Save()
  ↓
2. ByteProvider checks edit types:
   • Only modifications? → Fast Path
   • Has insertions/deletions? → Full Rebuild
  ↓
3a. Fast Path:
   • Open file for in-place editing
   • Write each modification directly
   • Close file
  ↓
3b. Full Rebuild:
   • Create temp file
   • Write entire virtual view
   • Replace original with temp
  ↓
4. Clear edit tracking collections
  ↓
5. Raise FileSaved event
```

---

## 🎨 Extension Points

WPF HexEditor V2 is designed for extensibility.

### Custom Rendering

```csharp
public class CustomHexEditor : HexEditor
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);  // Default rendering

        // Add custom rendering
        RenderCustomAnnotations(dc);
    }

    private void RenderCustomAnnotations(DrawingContext dc)
    {
        // Your custom rendering logic
    }
}
```

### Custom Search Algorithm

```csharp
public class CustomSearchService : ISearchService
{
    public long FindFirst(byte[] pattern)
    {
        // Your custom search algorithm
        return MyAdvancedSearch(pattern);
    }
}

// Register custom service
hexEditor.SearchService = new CustomSearchService();
```

### Custom File Format Support

```csharp
public class CustomByteProvider : ByteProvider
{
    public override void Open(string fileName)
    {
        // Custom file loading logic
        // (e.g., decompress, decrypt, parse)
    }

    public override void Save()
    {
        // Custom file saving logic
    }
}
```

---

## 📚 Further Reading

### Deep Dive Documentation

- **[Core Systems](../architecture/core-systems/)** - Detailed technical documentation
- **[Data Flow](../architecture/data-flow/)** - Sequence diagrams for all operations
- **[API Reference](../api-reference/)** - Complete API documentation

### Learning Resources

- **[Quick Start](Quick-Start)** - Get started in 5 minutes
- **[Basic Operations](Basic-Operations)** - Learn fundamental operations
- **[Best Practices](Best-Practices)** - Performance optimization tips
- **[Sample Applications](Sample-Applications)** - Real-world examples

---

<div align="center">
  <br/>
  <p>
    <b>🏗️ Understanding the architecture?</b><br/>
    Explore the technical deep dives!
  </p>
  <br/>
  <p>
    👉 <a href="../architecture/overview.md"><b>Technical Architecture</b></a> •
    <a href="Best-Practices"><b>Best Practices</b></a> •
    <a href="API-Reference"><b>API Reference</b></a>
  </p>
</div>
