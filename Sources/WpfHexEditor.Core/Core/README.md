# Core Components

This directory contains the core components and infrastructure that power the WPF HexEditor control.

## 📁 Directory Structure

```
Core/
├── Bytes/              # Byte manipulation and provider classes
├── Cache/              # ⚡ LRU cache for search results (NEW v2.2+)
├── CharacterTable/     # TBL file support for custom character mappings
├── Converters/         # WPF value converters
├── EventArguments/     # Custom event argument classes
├── Interfaces/         # Core interfaces
├── MethodExtention/    # Extension methods (includes SIMD vectorization)
├── Native/             # P/Invoke and native Windows API calls
├── BookMark.cs         # Bookmark functionality
├── Caret.cs            # Text caret implementation
├── ConstantReadOnly.cs # Constants and read-only values
├── CustomBackgroundBlock.cs  # Custom background coloring
├── Enumeration.cs      # Enumerations used throughout
├── KeyValidator.cs     # Keyboard input validation
└── RandomBrushes.cs    # Random color generation for highlights
```

## 🔧 Core Components

### 📦 Bytes/ - Byte Manipulation

The heart of the hex editor's data handling.

#### **ByteProvider.cs**
Central class for managing binary data from files or streams.

**Key features:**
- Stream and file-based data access
- Undo/redo stack management
- Modification tracking
- Copy/paste operations
- Find and replace operations
- Insert and delete operations

**Properties:**
```csharp
public long Length { get; }
public bool IsOpen { get; }
public Stack<ByteModified> UndoStack { get; }
public Stack<ByteModified> RedoStack { get; }
public long UndoCount { get; }
```

**Key methods:**
```csharp
public (byte? singleByte, bool succes) GetByte(long position)
public void AddByteModified(byte originalByte, byte newByte, long position)
public void Undo()
public void Redo()
public IEnumerable<long> FindIndexOf(byte[] data, long startPosition)
public void Paste(long startPosition, byte[] data, bool isInsert)
```

#### **ByteModified.cs**
Represents a single byte modification for undo/redo functionality.

```csharp
public class ByteModified
{
    public byte Byte { get; set; }
    public long BytePositionInStream { get; set; }
    public ByteAction Action { get; set; }
}
```

#### **Byte_8bit.cs, Byte_16bit.cs, Byte_32bit.cs**
Different byte width implementations for editing in various modes.

- **8-bit:** Standard single-byte editing
- **16-bit:** Word editing (LoHi/HiLo support)
- **32-bit:** Double-word editing

#### **ByteConverters.cs**
Utility class for converting between different data representations.

```csharp
public static string ByteToHex(byte b)
public static string ByteToDecimal(byte b)
public static string ByteToBinary(byte b)
public static byte[] StringToByte(string hex)
```

#### **ByteDifference.cs**
Used for file comparison features.

```csharp
public class ByteDifference
{
    public long Position { get; set; }
    public byte? OriginalByte { get; set; }
    public byte? NewByte { get; set; }
}
```

---

### ⚡ Cache/ - Performance Caching (NEW v2.2+)

High-performance caching infrastructure for search operations.

#### **LRUCache.cs**
Generic LRU (Least Recently Used) cache implementation with O(1) operations.

**Key features:**
- Thread-safe with proper locking
- O(1) lookups, inserts, and evictions
- Automatic eviction of least recently used items
- Configurable capacity (default: 20 entries)
- Generic implementation: `LRUCache<TKey, TValue>`

**Properties:**
```csharp
public int Capacity { get; }
public int Count { get; }
```

**Key methods:**
```csharp
public bool TryGet(TKey key, out TValue value)  // O(1) - moves to front on access
public void Put(TKey key, TValue value)         // O(1) - evicts LRU if at capacity
public bool Remove(TKey key)                     // O(1) - removes specific item
public void Clear()                              // Clears all items
public string GetStatistics()                    // Returns cache usage stats
```

**Performance:**
- 10-100x faster for repeated searches (cache hit)
- Minimal memory overhead (configurable capacity)
- Used internally by FindReplaceService

#### **SearchCacheKey.cs**
Efficient cache key for search operations using polynomial rolling hash.

**Structure:**
```csharp
public struct SearchCacheKey : IEquatable<SearchCacheKey>
{
    public int PatternHash { get; }      // Polynomial rolling hash of pattern
    public long StartPosition { get; }   // Search start position
    public long FileLength { get; }      // File length (detects modifications)
}
```

**How it works:**
- **Pattern Hash:** Polynomial rolling hash (hash = hash * 31 + pattern[i])
- **Fast Comparison:** Hash-based equality check (O(1))
- **Modification Detection:** File length changes invalidate cache

**Usage:**
```csharp
var cache = new LRUCache<SearchCacheKey, List<long>>(capacity: 20);
var key = new SearchCacheKey(pattern, startPosition, fileLength);

if (cache.TryGet(key, out var cachedResults))
{
    // Cache hit - 10-100x faster!
    return cachedResults;
}

// Cache miss - perform search and cache result
var results = PerformSearch(pattern, startPosition);
cache.Put(key, results);
```

---

### 🎨 CharacterTable/ - TBL Support

Manages custom character tables for game modding and proprietary formats.

**Features:**
- Load custom .tbl files
- Unicode support
- Multi-byte character mappings
- Real-time character translation

**Usage:**
```csharp
var tblStream = new TblStream(@"C:\path\to\game.tbl");
string character = tblStream.GetString(new byte[] { 0x42 });
```

---

### 📊 Converters/ - WPF Value Converters

Collection of XAML value converters for data binding.

**Common converters:**
- BoolToVisibility
- ByteToHexString
- LongToHexString
- ColorToBrush
- InverseBool

**Usage in XAML:**
```xaml
<TextBlock Visibility="{Binding IsVisible, Converter={StaticResource BoolToVisibilityConverter}}" />
```

---

### 📢 EventArguments/ - Custom Events

Custom event argument classes for HexEditor events.

**Examples:**
- `ByteModifiedEventArgs` - Fired when bytes are modified
- `SelectionChangedEventArgs` - Fired when selection changes
- `BookmarkChangedEventArgs` - Fired when bookmarks change

---

### 🔌 Interfaces/ - Core Interfaces

Defines contracts for extensibility.

#### **IByte.cs**
Interface for byte display controls.

```csharp
public interface IByte
{
    byte? Byte { get; set; }
    long BytePositionInStream { get; set; }
    ByteAction Action { get; set; }
    bool IsSelected { get; set; }
    bool IsHighLight { get; set; }
}
```

#### **IByteControl.cs**
Interface for byte editing controls.

```csharp
public interface IByteControl
{
    void UpdateVisual();
    void Clear();
    void UpdateDataContext(long position);
}
```

#### **IByteModified.cs**
Interface for modification tracking.

```csharp
public interface IByteModified
{
    long BytePositionInStream { get; set; }
    byte Byte { get; set; }
    ByteAction Action { get; set; }
}
```

---

### 🛠️ MethodExtention/ - Extension Methods

Useful extension methods for common operations.

**Examples:**
```csharp
public static string ToHex(this byte value)
public static byte[] ToByteArray(this string hexString)
public static bool IsBetween(this long value, long min, long max)
```

---

### 💻 Native/ - Windows API

P/Invoke declarations for native Windows functionality.

**Features:**
- Clipboard operations
- File I/O optimization
- Memory-mapped files
- Performance-critical operations

---

### 🔖 BookMark.cs

Manages user bookmarks in the hex view.

```csharp
public class BookMark
{
    public long BytePositionInStream { get; set; }
    public string Description { get; set; }
    public Brush Marker { get; set; }
}
```

**Features:**
- Add/remove bookmarks at any position
- Custom descriptions
- Color-coded markers
- Navigation between bookmarks

---

### ✏️ Caret.cs

Text caret implementation for byte editing.

**Features:**
- Blinking caret animation
- Position tracking
- Focus management
- Insert/overwrite mode support

---

### 📐 ConstantReadOnly.cs

Application-wide constants.

**Examples:**
```csharp
public static readonly int BytePerLine = 16;
public static readonly int DefaultFontSize = 12;
public static readonly string DefaultEncoding = "ASCII";
```

---

### 🎨 CustomBackgroundBlock.cs

Allows custom background colors for byte ranges.

```csharp
public class CustomBackgroundBlock
{
    public long StartOffset { get; set; }
    public long Length { get; set; }
    public Color Color { get; set; }
    public string Description { get; set; }
}
```

**Use cases:**
- Highlighting file sections (header, data, footer)
- Visualizing differences in file comparison
- Marking important data structures
- Color-coding by data type

**Example:**
```csharp
hexEditor.CustomBackgroundBlockItems = new List<CustomBackgroundBlock>
{
    new CustomBackgroundBlock
    {
        StartOffset = 0x00,
        Length = 0x100,
        Color = Colors.LightBlue,
        Description = "File Header"
    }
};
```

---

### 📝 Enumeration.cs

Core enumerations used throughout the application.

**Key enumerations:**

```csharp
public enum ByteAction
{
    Nothing,
    Added,
    Deleted,
    Modified
}

public enum CopyPasteMode
{
    HexaString,
    ASCIIString,
    TBLString,
    CSharpCode,
    VBNetCode,
    CCode,
    JavaCode,
    FSharpCode
}

public enum DataVisualType
{
    Hexadecimal,
    Decimal,
    Binary
}

public enum ByteSpacerGroup
{
    TwoByte,
    FourByte,
    EightByte
}

public enum ByteDataMode
{
    Byte8Bit,
    Byte16Bit,
    Byte32Bit
}

public enum ByteOrder
{
    LoHi,  // Little-endian
    HiLo   // Big-endian
}
```

---

### ⌨️ KeyValidator.cs

Validates keyboard input for different data entry modes.

```csharp
public static bool IsHexKey(Key key)
public static bool IsDecimalKey(Key key)
public static bool IsBinaryKey(Key key)
public static bool IsNumericKey(Key key)
```

**Features:**
- Hexadecimal input validation (0-9, A-F)
- Decimal input validation (0-9)
- Binary input validation (0-1)
- Navigation key handling

---

### 🎨 RandomBrushes.cs

Generates random colors for highlights and bookmarks.

```csharp
public static Brush PickBrush()
public static Brush PickBrush(int seed)
public static List<Brush> GetAllBrushes()
```

**Use cases:**
- Bookmark color assignment
- Search result highlighting
- Multi-file visual differentiation

---

## 🏗️ Architecture Patterns

### Provider Pattern
`ByteProvider` acts as the data source abstraction, allowing different backends (file, stream, memory).

### Command Pattern
Undo/redo implemented via command pattern with `ByteModified` as commands.

### Observer Pattern
Events throughout for reactive UI updates.

### Strategy Pattern
Different byte modes (8/16/32 bit) use strategy pattern for rendering.

---

## 🔄 Data Flow

```
User Action
    ↓
HexEditor Control
    ↓
Service Layer (ClipboardService, FindReplaceService, etc.)
    ↓
ByteProvider
    ↓
Stream/File
```

---

## 🚀 Performance Optimizations (v2.2+)

### Legacy Optimizations
1. **Memory-mapped files** - Used for large file handling
2. **Lazy loading** - Only load visible bytes
3. **Virtualization** - UI virtualization for thousands of bytes
4. **Batch operations** - Group multiple modifications for undo/redo

### NEW Performance Features (v2.2+)

#### Tier 1: Span<byte> + ArrayPool
- **Location:** `Bytes/ByteProviderSpanExtensions.cs`
- **Performance:** 2-5x faster, 90% less memory allocation
- **Methods:** `GetBytesPooled()`, `FindIndexOfOptimized()`, `CountOccurrencesOptimized()`

#### Tier 2: Async/Await
- **Location:** `Bytes/ByteProviderAsyncExtensions.cs`
- **Performance:** ∞ (UI stays responsive during long operations)
- **Methods:** `FindAllAsync()`, `GetByteAsync()`, `GetBytesAsync()`
- **Features:** IProgress<int> progress reporting, CancellationToken support

#### Tier 3: SIMD Vectorization (net5.0+)
- **Location:** `MethodExtention/SpanSearchSIMDExtensions.cs`
- **Performance:** 4-8x faster for single-byte searches
- **Hardware:** AVX2 (32 bytes at once), SSE2 (16 bytes at once)
- **Methods:** `FindFirstSIMD()`, `FindAllSIMD()`, `CountOccurrencesSIMD()`

#### Tier 4: LRU Cache
- **Location:** `Cache/LRUCache.cs`, `Cache/SearchCacheKey.cs`
- **Performance:** 10-100x faster for repeated searches
- **Features:** O(1) operations, thread-safe, automatic eviction
- **Capacity:** Configurable (default: 20 cached searches)

#### Tier 5: Parallel Search
- **Location:** `Bytes/ByteProviderParallelExtensions.cs`
- **Performance:** 2-4x faster for files > 100MB
- **Features:** Multi-core CPU utilization with Parallel.For
- **Automatic:** Threshold detection (100MB), zero overhead for small files

#### Tier 6: Profile-Guided Optimization (PGO)
- **Location:** `WpfHexEditorCore.csproj` configuration
- **Performance:** 10-30% boost for CPU-intensive operations
- **Features:** Dynamic PGO, ReadyToRun (AOT), TieredCompilation
- **Platform:** .NET 8.0+ Release builds only

**Combined Results:**
- **10-100x faster** operations (depending on optimization tier and use case)
- **95% less memory** allocation
- **100% backward compatible** - no breaking changes
- **Automatic selection** - optimizations activate based on file size/hardware

See [PERFORMANCE_GUIDE.md](../../PERFORMANCE_GUIDE.md) for comprehensive documentation.

---

## 🔗 Integration with Services

The Core components are used by the [Services layer](../Services/README.md):

- **ClipboardService** → Uses `ByteProvider.CopyToClipboard()`
- **FindReplaceService** → Uses `ByteProvider.FindIndexOf()`
- **UndoRedoService** → Uses `ByteProvider.UndoStack` and `RedoStack`
- **SelectionService** → Uses `ByteProvider.GetByte()` and length validation

---

## 📚 Additional Resources

- [Services Documentation](../Services/README.md) - Business logic layer
- [Samples](../../Samples/README.md) - Usage examples
- [Main README](../../../README.md) - Project overview

---

✨ Core architecture by Derek Tremblay (derektremblay666@gmail.com)
