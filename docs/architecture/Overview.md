# 🏗️ WPF HexEditor - Architecture Overview

**Complete system architecture for HexEditor with MVVM pattern and comprehensive API**

---

## 📋 Table of Contents

- [Introduction](#introduction)
- [Design Principles](#design-principles)
- [System Architecture](#system-architecture)
- [Layered Architecture](#layered-architecture)
- [Core Components](#core-components)
- [Data Flow](#data-flow)
- [Key Innovations](#key-innovations)
- [Performance Characteristics](#performance-characteristics)
- [See Also](#see-also)

---

## 📖 Introduction

HexEditor uses a **modern MVVM architecture** designed with clean software engineering principles:

- ✅ **Comprehensive ByteProvider API** (186+ methods)
- ✅ **Backward Compatible API** - Zero breaking changes from legacy architecture
- ⚡ **99% Performance Improvement** with custom rendering (vs legacy ItemsControl)
- 🏗️ **Clean MVVM Architecture** with separation of concerns
- 📦 **Modular Service Layer** (16 specialized services)
- 🔄 **Comprehensive Undo/Redo** with granular control
- 📊 **Rich Diagnostics** for profiling and monitoring

### 🆕 Advanced Features (Feb 2026)

- 🔍 **400+ File Format Auto-Detection** with binary templates and signature matching
- 🎨 **Parsed Fields Panel** with structure overlay and field type visualization
- 📊 **Data Inspector** with 15+ data type interpretations (int8/16/32/64, float, double, etc.)
- ⚖️ **File Diff** for side-by-side binary comparison with highlighting
- 📈 **BarChart Visualization** for byte distribution analysis
- ✅ **Automatic Checksum Validation** with visual error highlighting
- 📝 **Format Script Editor** with custom JsonEditor (61 Dependency Properties)
- 🎯 **Insert/Overwrite Modes** with visual editing feedback (caret blink, bold nibble)

---

## 🎯 Design Principles

### 1. **Separation of Concerns**
Each layer has a single, well-defined responsibility:
- **View** - UI rendering and user interaction
- **ViewModel** - Business logic and state management
- **Services** - Specialized functionality (search, clipboard, etc.)
- **ByteProvider** - Data access coordination
- **Core** - Low-level byte operations

### 2. **Virtual View Pattern**
Users see a **virtual representation** with all edits applied, while the original file remains unchanged until Save:
```
Original File: [41 42 43 44 45]
Insert FF at position 2
Virtual View:  [41 42 FF 43 44 45]  ← User sees this
Physical File: [41 42 43 44 45]     ← File unchanged
```

### 3. **Edit Tracking**
All changes are tracked in three separate collections:
- **Modifications** - Byte value changes (file length unchanged)
- **Insertions** - Added bytes (increases file length)
- **Deletions** - Removed bytes (decreases file length)

### 4. **Position Mapping**
Bidirectional conversion between virtual and physical positions:
- **Virtual Position** - What the user sees (includes insertions)
- **Physical Position** - Actual byte offset in file

### 5. **Performance First**
- Custom DrawingContext rendering (99% faster than ItemsControl)
- Lazy loading with caching
- Only render visible bytes
- Frozen brushes and cached FormattedText

---

## 🏗️ System Architecture

### High-Level Component Diagram

```mermaid
graph TB
    subgraph "🎨 Presentation Layer"
        HexEditor["HexEditor Control<br/>(Main API)"]
        HexEditor["HexEditor<br/>(UserControl)"]
        HexViewport["HexViewport<br/>(Custom Rendering)"]
    end

    subgraph "🧠 Business Logic Layer"
        ViewModel["HexEditorViewModel<br/>(MVVM Pattern)"]
    end

    subgraph "🔧 Service Layer"
        SearchService["SearchService<br/>(Find/Replace)"]
        ClipboardService["ClipboardService<br/>(Copy/Paste)"]
        BookmarkService["BookmarkService<br/>(Navigation)"]
        HighlightService["HighlightService<br/>(Visual Markers)"]
        SelectionService["SelectionService<br/>(Text Selection)"]
        FormatDetectionService["FormatDetectionService<br/>(400+ Formats)"]
        DataInspectorService["DataInspectorService<br/>(Type Interpretation)"]
        Others["+ 9 More Services"]
    end

    subgraph "💾 Data Access Layer"
        ByteProvider["ByteProvider<br/>(Coordinator & API)"]
    end

    subgraph "⚙️ Core Processing Layer"
        ByteReader["ByteReader<br/>(Virtual View)"]
        EditsManager["EditsManager<br/>(Track Changes)"]
        PositionMapper["PositionMapper<br/>(Virtual↔Physical)"]
        UndoRedoManager["UndoRedoManager<br/>(History)"]
        FileProvider["FileProvider<br/>(File I/O)"]
    end

    subgraph "💿 Storage Layer"
        FileSystem["File System<br/>(Disk/Memory/Stream)"]
    end

    HexEditor --> HexViewport
    HexEditor --> ViewModel

    ViewModel --> SearchService
    ViewModel --> ClipboardService
    ViewModel --> BookmarkService
    ViewModel --> HighlightService
    ViewModel --> SelectionService
    ViewModel --> FormatDetectionService
    ViewModel --> DataInspectorService
    ViewModel --> Others

    ViewModel --> ByteProvider
    SearchService --> ByteProvider
    ClipboardService --> ByteProvider
    FormatDetectionService --> ByteProvider
    DataInspectorService --> ByteProvider

    ByteProvider --> ByteReader
    ByteProvider --> EditsManager
    ByteProvider --> PositionMapper
    ByteProvider --> UndoRedoManager
    ByteProvider --> FileProvider

    ByteReader --> EditsManager
    ByteReader --> PositionMapper
    ByteReader --> FileProvider

    EditsManager --> PositionMapper

    FileProvider --> FileSystem

    style HexEditor fill:#fff9c4,stroke:#f57c00,stroke-width:3px
    style ViewModel fill:#e1f5ff,stroke:#0277bd,stroke-width:3px
    style ByteProvider fill:#ffccbc,stroke:#d84315,stroke-width:3px
    style ByteReader fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style FileSystem fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
```

---

## 🏛️ Layered Architecture

### Layer Responsibilities

```mermaid
graph LR
    subgraph "Layer 1: UI"
        UI["HexEditor<br/>HexViewport"]
    end

    subgraph "Layer 2: Presentation"
        VM["HexEditorViewModel<br/>State Management<br/>INotifyPropertyChanged"]
    end

    subgraph "Layer 3: Services"
        Services["16 Services<br/>Search, Clipboard,<br/>FormatDetection, DataInspector,<br/>Bookmark, etc."]
    end

    subgraph "Layer 4: Data Access"
        Provider["ByteProvider<br/>Coordinator<br/>Public API"]
    end

    subgraph "Layer 5: Core"
        Core["ByteReader<br/>EditsManager<br/>PositionMapper<br/>UndoRedoManager<br/>FileProvider"]
    end

    subgraph "Layer 6: Storage"
        Storage["File System<br/>Memory<br/>Stream"]
    end

    UI --> VM
    VM --> Services
    VM --> Provider
    Services --> Provider
    Provider --> Core
    Core --> Storage

    style UI fill:#fff9c4
    style VM fill:#e1f5ff
    style Services fill:#c8e6c9
    style Provider fill:#ffccbc
    style Core fill:#f8bbd0
    style Storage fill:#f3e5f5
```

### Layer Details

| Layer | Components | Responsibility | Thread Safety |
|-------|-----------|----------------|---------------|
| **1. UI** | HexEditor, HexViewport, ParsedFieldsPanel | User interaction, rendering | UI thread only |
| **2. Presentation** | HexEditorViewModel | Business logic, state management | UI thread only |
| **3. Services** | 16 specialized services | Feature implementation (search, format detection, data inspection, etc.) | UI thread only |
| **4. Data Access** | ByteProvider | API coordination, caching | UI thread only |
| **5. Core** | ByteReader, EditsManager, etc. | Low-level operations | Thread-safe |
| **6. Storage** | FileProvider, FileSystem | File I/O, persistence | Async-safe |

---

## ⚙️ Core Components

### 1. HexEditor (Main Control)

**Location**: [HexEditor.cs](../../Sources/WPFHexaEditor/HexEditor.cs)

**Purpose**: Public API surface with partial classes organized by functionality.

**Structure**:

```mermaid
graph TD
    Root["📦 HexEditor.cs<br/>(Main Control)"]

    Core["⚙️ Core/<br/>File, Stream, Byte, Edit<br/>Batch, Diagnostics, Async"]
    Features["✨ Features/<br/>Bookmarks, Highlights<br/>FileComparison, TBL"]
    Search["🔍 Search/<br/>Find, Replace, Count"]
    UI["🎨 UI/<br/>Events, Clipboard<br/>Zoom, UIHelpers"]
    Compat["🔄 Compatibility/<br/>Legacy V1 API wrappers"]

    Root --> Core
    Root --> Features
    Root --> Search
    Root --> UI
    Root --> Compat

    style Root fill:#e3f2fd,stroke:#1976d2,stroke-width:3px
    style Core fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    style Features fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    style Search fill:#e8f5e9,stroke:#388e3c,stroke-width:2px
    style UI fill:#fce4ec,stroke:#c2185b,stroke-width:2px
    style Compat fill:#e0f2f1,stroke:#00796b,stroke-width:2px
```

**Key Methods**:
```csharp
// File Operations
public void Open(string fileName)
public void Save()
public void Close()

// Byte Operations
public byte GetByte(long position)
public void ModifyByte(byte value, long position)
public void InsertByte(byte value, long position)
public void DeleteBytes(long position, long count)

// Search Operations
public long FindFirst(byte[] pattern, long startPosition = 0)
public List<long> FindAll(byte[] pattern)
public int CountOccurrences(byte[] pattern)

// Edit Operations
public void Undo()
public void Redo()
public void ClearModifications()
public void ClearInsertions()
public void ClearDeletions()
```

### 2. HexEditorViewModel

**Location**: [HexEditorViewModel.cs](../../Sources/WPFHexaEditor/Core/ViewModels/HexEditorViewModel.cs)

**Purpose**: MVVM pattern business logic layer, coordinates services and ByteProvider.

**Responsibilities**:
- Manages ByteProvider lifecycle
- Coordinates 15 specialized services
- Handles virtual position calculations for UI
- Implements INotifyPropertyChanged for data binding
- Manages selection and cursor state
- Generates HexLines for rendering

**Key Properties**:
```csharp
public ByteProvider Provider { get; }
public SelectionService SelectionService { get; }
public SearchService SearchService { get; }
public ClipboardService ClipboardService { get; }
// ... 12 more services

public long Position { get; set; }
public long SelectionStart { get; set; }
public long SelectionLength { get; set; }
public bool IsModified { get; }
```

### 3. ByteProvider (Data Access Coordinator)

**Location**: [ByteProvider.cs](../../Sources/WPFHexaEditor/Core/Bytes/ByteProvider.cs)

**Purpose**: Coordinates core components and provides 186-method public API.

**Architecture**:
```csharp
public class ByteProvider
{
    private readonly ByteReader _reader;       // Virtual view reader
    private readonly EditsManager _edits;      // Track all changes
    private readonly PositionMapper _mapper;   // Virtual↔Physical
    private readonly UndoRedoManager _undo;    // History stack
    private readonly FileProvider _file;       // File I/O

    // 186 public methods for complete API
    public byte ReadByte(long virtualPosition) => _reader.ReadByte(virtualPosition);
    public void ModifyByte(long position, byte value) { /* ... */ }
    public void InsertByte(long position, byte value) { /* ... */ }
    public void DeleteBytes(long position, long count) { /* ... */ }
    // ... 182 more methods
}
```

**Key Features**:
- ✅ **100% API Compatibility** with ByteProvider specification
- 🔄 **Edit Tracking** - Modifications, insertions, deletions
- 🎯 **Virtual View** - Users see final result before save
- 🔙 **Undo/Redo** - Unlimited history with granular control
- 💾 **Smart Save** - Fast path for modifications-only edits

### 4. ByteReader (Virtual View Engine)

**Location**: [ByteReader.cs](../../Sources/WPFHexaEditor/Core/Bytes/ByteReader.cs)

**Purpose**: Reads bytes from the **virtual view** (showing all edits applied).

**Algorithm**:
```csharp
public byte ReadByte(long virtualPosition)
{
    // 1. Check if position is an inserted byte
    if (_edits.IsInsertion(virtualPosition, out byte insertedValue))
        return insertedValue;

    // 2. Convert virtual → physical position
    long physicalPos = _mapper.VirtualToPhysical(virtualPosition);

    // 3. Check if deleted
    if (_edits.IsDeleted(physicalPos))
        throw new InvalidOperationException("Position deleted");

    // 4. Check if modified
    if (_edits.IsModified(physicalPos, out byte modifiedValue))
        return modifiedValue;

    // 5. Return original byte from file
    return _file.ReadByte(physicalPos);
}
```

### 5. EditsManager (Change Tracking)

**Location**: [EditsManager.cs](../../Sources/WPFHexaEditor/Core/Bytes/EditsManager.cs)

**Purpose**: Tracks all modifications, insertions, and deletions.

**Data Structures**:
```csharp
public class EditsManager
{
    // Modifications: physical position → new byte value
    private Dictionary<long, byte> _modifications;

    // Insertions: virtual position → list of inserted bytes (LIFO)
    private Dictionary<long, List<byte>> _insertions;

    // Deletions: physical position → deleted byte count
    private Dictionary<long, long> _deletions;

    // Statistics
    public int ModificationCount => _modifications.Count;
    public int InsertionCount => _insertions.Values.Sum(list => list.Count);
    public int DeletionCount => (int)_deletions.Values.Sum();
}
```

**LIFO Insertion Semantics**:
```
Insert 'A' at position 5:  [... A ...]
Insert 'B' at position 5:  [... B A ...]  // B pushed before A (LIFO)
Insert 'C' at position 5:  [... C B A ...]  // C pushed before B
```

### 6. PositionMapper (Coordinate Transformation)

**Location**: [PositionMapper.cs](../../Sources/WPFHexaEditor/Core/Bytes/PositionMapper.cs)

**Purpose**: Bidirectional mapping between virtual and physical positions.

**Mapping Logic**:
```csharp
public class PositionMapper
{
    // Segments track ranges where insertions/deletions occurred
    private List<MapSegment> _segments;

    public long VirtualToPhysical(long virtualPos)
    {
        // Find segment containing virtualPos
        // Subtract insertion offsets
        // Add deletion offsets
        // Return physical position
    }

    public long PhysicalToVirtual(long physicalPos)
    {
        // Find segment containing physicalPos
        // Add insertion offsets
        // Subtract deletion offsets
        // Return virtual position
    }
}
```

**Example**:
```
Original file: 10 bytes (positions 0-9)
Insert 3 bytes at position 5

Virtual positions:  0 1 2 3 4 5 6 7 | 8  9  10 11 12
                                    ↑ Insertions
Physical positions: 0 1 2 3 4       | 5  6  7  8  9

VirtualToPhysical(8) = 5   // Skip 3 inserted bytes
PhysicalToVirtual(5) = 8   // Account for 3 insertions
```

### 7. HexViewport (Custom Rendering)

**Location**: [HexViewport.cs](../../Sources/WPFHexaEditor/Core/Controls/HexViewport.cs)

**Purpose**: High-performance custom rendering using DrawingContext.

**Performance Optimization**:
```csharp
protected override void OnRender(DrawingContext dc)
{
    // Cache visible lines only
    var visibleLines = GetVisibleLines();

    foreach (var line in visibleLines)
    {
        // Draw offset column
        dc.DrawText(offsetText, offsetPosition);

        // Draw hex bytes with highlights
        foreach (var byteInfo in line.Bytes)
        {
            if (byteInfo.IsSelected)
                dc.DrawRectangle(selectionBrush, null, rect);

            dc.DrawText(hexText, hexPosition);
        }

        // Draw ASCII column
        dc.DrawText(asciiText, asciiPosition);
    }
}
```

**V1 vs V2 Rendering**:

| Approach | V1 (ItemsControl) | V2 (DrawingContext) | Speedup |
|----------|-------------------|---------------------|---------|
| Initial load (1000 lines) | ~450ms | ~5ms | **99% faster** |
| Scroll update | ~120ms | ~2ms | **98% faster** |
| Selection change | ~80ms | ~1ms | **99% faster** |
| Memory (10MB file) | ~950MB | ~85MB | **91% less** |

---

## 🆕 Advanced Services & Features (Feb 2026)

### 8. FormatDetectionService (File Format Intelligence)

**Location**: [FormatDetectionService.cs](../../Sources/WPFHexaEditor/Services/FormatDetectionService.cs)

**Purpose**: Automatic detection of 400+ file formats using binary signatures and templates.

**Key Features**:
- 🔍 **Signature Matching** - Identifies file type from magic bytes
- 📋 **Binary Templates** - Parses structured file formats (PNG, ZIP, ELF, PE, etc.)
- 🎯 **Field Parsing** - Extracts fields with proper types (uint8, uint16, string, etc.)
- ✅ **Checksum Validation** - Automatic validation with visual error display
- 📊 **Structure Overlay** - Visual representation of file structure in hex view

**Example Detection**:
```csharp
// PNG format detection
var formatInfo = formatDetectionService.DetectFormat(byteProvider);
// Returns: { FormatName: "PNG Image", Extension: "png", Confidence: 100% }

// Parse PNG header fields
var fields = formatDetectionService.ParseFields(byteProvider);
// Returns: [
//   { Name: "signature", Offset: 0, Length: 8, Type: "bytes", Value: [137, 80, 78, 71...] },
//   { Name: "width", Offset: 16, Length: 4, Type: "uint32", Value: 1920 },
//   { Name: "height", Offset: 20, Length: 4, Type: "uint32", Value: 1080 }
// ]
```

**Supported Format Categories**:
- 🖼️ **Images**: PNG, JPEG, GIF, BMP, TIFF, WebP, ICO
- 📦 **Archives**: ZIP, RAR, 7Z, TAR, GZ
- 🎵 **Audio**: MP3, WAV, FLAC, OGG, AAC
- 🎬 **Video**: MP4, AVI, MKV, WebM, MOV
- 📄 **Documents**: PDF, DOCX, XLSX, PPTX
- 💾 **Executables**: EXE, DLL, ELF, Mach-O
- 🎮 **Game ROMs**: NES, SNES, GB, GBA, N64
- And 350+ more formats...

### 9. DataInspectorService (Multi-Type Interpretation)

**Location**: [DataInspectorService.cs](../../Sources/WPFHexaEditor/Services/DataInspectorService.cs)

**Purpose**: Interprets selected bytes as multiple data types simultaneously.

**Supported Interpretations** (15+ types):
```csharp
// Select 8 bytes at position 100
var interpretations = dataInspectorService.Interpret(byteProvider, position: 100, length: 8);

// Returns:
// Int8:        127
// UInt8:       255
// Int16 (LE):  32767
// Int16 (BE):  -32768
// Int32 (LE):  2147483647
// Int32 (BE):  -2147483648
// Int64 (LE):  9223372036854775807
// Float (LE):  3.14159
// Double (LE): 2.718281828
// ASCII:       "Hello!\n"
// UTF-8:       "世界"
// UTF-16:      "🚀"
// Hex:         "48 65 6C 6C 6F 21 0A 00"
// Binary:      "01001000 01100101 01101100..."
// Timestamp:   "2026-02-22 14:30:00 UTC"
```

**UI Integration**:
- 📊 **Data Inspector Panel** - Real-time display as cursor moves
- 🎯 **Selection-based** - Updates automatically on selection change
- 🔄 **Endianness Toggle** - Switch between Little/Big Endian
- 📋 **Copy Values** - Right-click to copy specific interpretation

### 10. ParsedFieldsPanel (Structure Visualization)

**Location**: [ParsedFieldsPanel.xaml](../../Sources/WPFHexaEditor/Controls/ParsedFieldsPanel.xaml)

**Purpose**: Displays parsed file structure in a hierarchical tree view.

**Features**:
- 🌳 **Tree View** - Hierarchical field structure
- 🎨 **Color-Coded Types** - Visual distinction for different data types
- 📍 **Click to Navigate** - Jump to field location in hex view
- 🔍 **Field Details** - Name, offset, length, type, value
- 📊 **Structure Overlay** - Highlights field boundaries in hex view

**Example Display** (PNG file):
```
📄 PNG Image (1920x1080)
├─ 📦 PNG Signature (0x00, 8 bytes)
│  └─ Value: 89 50 4E 47 0D 0A 1A 0A
├─ 📦 IHDR Chunk (0x08, 25 bytes)
│  ├─ Length: 13 (uint32)
│  ├─ Type: "IHDR" (string)
│  ├─ Width: 1920 (uint32)
│  ├─ Height: 1080 (uint32)
│  ├─ Bit Depth: 8 (uint8)
│  ├─ Color Type: 2 (uint8)
│  └─ CRC: 0x1A2B3C4D (uint32) ✅
├─ 📦 IDAT Chunk (0x21, 65536 bytes)
│  └─ Compressed Data: [...]
└─ 📦 IEND Chunk (0x10021, 12 bytes)
   └─ CRC: 0xAE426082 (uint32) ✅
```

### 11. FileDiffService (Binary Comparison)

**Location**: [FileDiffService.cs](../../Sources/WPFHexaEditor/Services/FileDiffService.cs)

**Purpose**: Side-by-side comparison of two binary files with highlighting.

**Features**:
- ⚖️ **Dual View** - Two hex editors side-by-side
- 🎨 **Diff Highlighting** - Added (green), removed (red), modified (yellow)
- 🔄 **Synchronized Scrolling** - Both views scroll together
- 📊 **Statistics** - Total differences, % similarity
- 📍 **Navigation** - Jump to next/previous difference

**Use Cases**:
- 📝 Comparing file versions
- 🔧 Analyzing patch differences
- 🐛 Debugging binary protocol changes
- 🎮 ROM hacking comparison

### 12. FormatScriptEditor (Custom JsonEditor)

**Location**: [FormatScriptEditor/](../../Sources/WPFHexaEditor/Controls/FormatScriptEditor/)

**Purpose**: Professional JSON editor for creating binary format definitions.

**JsonEditor Features** (61 Dependency Properties):
- 🎨 **Syntax Highlighting** - 13+ token types (keys, values, keywords, etc.)
- 💡 **IntelliSense** - Context-aware autocomplete with 9 contexts
- ✅ **Real-time Validation** - 4-layer validation with squiggly lines
- 🔤 **Line Numbers** - Configurable gutter
- 🎯 **Bracket Matching** - Visual highlighting of matching {}, [], ()
- ↩️ **Auto-Indent** - Smart indentation on Enter
- 📋 **Clipboard** - Copy/Paste/Cut with formatting
- ⏮️ **Undo/Redo** - Unlimited history
- 🔍 **Find/Replace** - Regex support
- 📄 **Code Folding** - Collapse/expand blocks

**IntelliSense Contexts**:
1. Root level (formatName, signature, fields, etc.)
2. Field properties (name, type, offset, length, etc.)
3. Type suggestions (uint8, uint16, string, etc.)
4. Conditional expressions (calc:, var:)
5. Loop constructs (count, body)
6. Action definitions (onLoad, onParse)
7. Validation rules (checksum, bounds)
8. Display hints (color, format, label)
9. Custom properties (metadata, tags)

**Validation Layers**:
1. **JSON Syntax** - Parse errors, unclosed brackets
2. **Schema** - Missing required fields (formatName, signature)
3. **Format Rules** - Invalid field types, offset conflicts
4. **Semantic** - Undefined variable references, circular dependencies

---

## 🔄 Data Flow

### File Open Sequence

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant FP as FileProvider
    participant FS as FileSystem

    User->>HE: Open("data.bin")
    HE->>VM: Create ViewModel
    VM->>BP: new ByteProvider()
    BP->>FP: OpenFile("data.bin")
    FP->>FS: Open FileStream
    FS-->>FP: Stream handle
    FP-->>BP: File opened
    BP->>FP: Read first 64KB (cache)
    FP->>FS: Read bytes
    FS-->>FP: Byte data
    FP-->>BP: Cached bytes
    BP-->>VM: Provider ready
    VM-->>HE: ViewModel ready
    HE->>VM: GetVisibleLines()
    VM->>BP: ReadBytes(0, 1000)
    BP-->>VM: Byte data
    VM-->>HE: HexLine[]
    HE->>User: Display hex view
```

### Byte Modification Sequence (Insert Mode)

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant EM as EditsManager
    participant PM as PositionMapper
    participant UR as UndoRedoManager

    User->>HE: Type 'FF' at position 100
    HE->>VM: InsertByte(100, 0xFF)
    VM->>BP: InsertByte(100, 0xFF)
    BP->>EM: AddInsertion(100, 0xFF)
    EM->>PM: RecalculateSegments()
    PM-->>EM: Segments updated
    EM-->>BP: Insertion added
    BP->>UR: PushEdit(InsertEdit)
    UR-->>BP: Edit saved
    BP-->>VM: Length increased
    VM->>VM: RefreshVisibleLines()
    VM-->>HE: Lines updated
    HE->>User: Display updated view
```

---

## 🚀 Key Innovations

### 1. Virtual View Pattern

**Problem**: How to show edits without modifying the original file?

**Solution**: Maintain separate edit collections and compute virtual view on-demand.

```csharp
// User sees:    [41 42 FF 43 44 45]  (6 bytes)
// File has:     [41 42 43 44 45]     (5 bytes)
// Insertion:    Position 2 → 0xFF

// When user reads position 2:
ByteReader.ReadByte(2)
    → EditsManager.IsInsertion(2)  // Returns true, value=0xFF
    → Return 0xFF                   // No file access needed!
```

### 2. LIFO Insertion Semantics

**Problem**: Multiple insertions at same position - what order?

**Solution**: Last-In-First-Out (stack behavior).

```csharp
InsertByte(5, 'A');  // [... A ...]
InsertByte(5, 'B');  // [... B A ...]  ← B inserted before A
InsertByte(5, 'C');  // [... C B A ...]  ← C inserted before B

// Cursor stays after last insertion for natural typing flow
```

### 3. Smart Save Algorithm

**Problem**: Save performance for large files with many edits.

**Solution**: Two paths - fast path for modifications only, full rebuild for structural changes.

```csharp
public void Save()
{
    if (HasInsertionsOrDeletions)
    {
        // Full rebuild: iterate virtual positions
        RebuildFile();  // ~100ms for 10MB file
    }
    else
    {
        // Fast path: only write modified bytes
        ApplyModifications();  // ~1ms for 10MB file (100x faster!)
    }
}
```

### 4. Granular Undo Control (New in V2)

**Problem**: User wants to undo only certain types of edits.

**Solution**: Three granular clear methods.

```csharp
// Clear only value changes, keep structure
hexEditor.ClearModifications();

// Remove all inserted bytes
hexEditor.ClearInsertions();

// Restore all deleted bytes
hexEditor.ClearDeletions();

// Clear everything
hexEditor.ClearAllChanges();
```

### 5. Batch Operations

**Problem**: Thousands of small edits cause UI lag.

**Solution**: Batch mode defers UI updates until complete.

```csharp
hexEditor.BeginBatch();
try
{
    for (int i = 0; i < 10000; i++)
        hexEditor.ModifyByte((byte)(i % 256), i);
}
finally
{
    hexEditor.EndBatch();  // Update UI once
}
// Result: 3x faster than individual operations
```

---

## ⚡ Performance Characteristics

### Rendering Performance

| Operation | V1 | V2 | Improvement |
|-----------|----|----|-------------|
| Initial load (1000 lines) | 450ms | 5ms | **99% faster** |
| Scroll update | 120ms | 2ms | **98% faster** |
| Selection change | 80ms | 1ms | **99% faster** |
| Byte modification | 200ms | 3ms | **98% faster** |
| Insert byte | 250ms | 4ms | **98% faster** |

### Memory Usage

| File Size | V1 Memory | V2 Memory | Improvement |
|-----------|-----------|-----------|-------------|
| 1 MB | 180 MB | 25 MB | **86% less** |
| 10 MB | 950 MB | 85 MB | **91% less** |
| 100 MB | OOM | 320 MB | **Handles GB+ files** |

### Operation Complexity

| Operation | Time Complexity | Space Complexity |
|-----------|----------------|------------------|
| ReadByte | O(log n) | O(1) |
| ModifyByte | O(1) | O(1) |
| InsertByte | O(log n) | O(1) |
| DeleteBytes | O(log n) | O(1) |
| FindFirst | O(n) | O(1) |
| FindAll | O(n) | O(k) where k = matches |
| CountOccurrences | O(n) | O(1) |
| Undo/Redo | O(1) | O(h) where h = history |
| Save (modifications only) | O(m) | O(1) |
| Save (with insertions) | O(n) | O(n) |

*n = file size, m = modification count, k = match count, h = history depth*

---

## 🔗 See Also

### Core Systems Documentation
- [ByteProvider System](core-systems/byteprovider-system.md) - Data access coordination
- [Position Mapping](core-systems/position-mapping.md) - Virtual↔Physical conversion
- [Edit Tracking](core-systems/edit-tracking.md) - Modification management
- [Undo/Redo System](core-systems/undo-redo-system.md) - History management
- [Rendering System](core-systems/rendering-system.md) - Custom DrawingContext
- [Service Layer](core-systems/service-layer.md) - 16 specialized services

### Advanced Features Documentation (Feb 2026)
- [Format Detection System](advanced-features/format-detection.md) - 400+ file format auto-detection
- [Data Inspector](advanced-features/data-inspector.md) - Multi-type interpretation
- [Parsed Fields Panel](advanced-features/parsed-fields.md) - Structure visualization
- [File Diff System](advanced-features/file-diff.md) - Binary comparison
- [Format Script Editor](advanced-features/format-script-editor.md) - JsonEditor with IntelliSense

### Data Flow Documentation
- [File Operations](data-flow/file-operations.md) - Open, Close, Save sequences
- [Edit Operations](data-flow/edit-operations.md) - Modify, Insert, Delete sequences
- [Search Operations](data-flow/search-operations.md) - Find, Replace sequences
- [Save Operations](data-flow/save-operations.md) - Smart save algorithm
- [Format Detection Flow](data-flow/format-detection-flow.md) - Signature matching and field parsing

### Related Documentation
- [API Reference](../api-reference/) - Complete method documentation
- [PartialClasses README](../../Sources/WPFHexaEditor/PartialClasses/README.md) - Code organization
- [Services README](../../Sources/WPFHexaEditor/Services/README.md) - Service layer details
- [Performance Guide](../performance/) - Optimization tips
- [Main README](../../README.md) - Project overview

---

**Last Updated**: 2026-02-22
**Version**: 2.0+ (V2 architecture with Advanced Features)
**Status**: ✅ Production Ready
- ✅ 100% ByteProvider API (186 methods)
- ✅ 100% Legacy V1 API compatibility
- ✅ 400+ file format auto-detection
- ✅ 16 specialized services
- ✅ JsonEditor with 61 Dependency Properties
- ✅ Data Inspector, Parsed Fields, File Diff
