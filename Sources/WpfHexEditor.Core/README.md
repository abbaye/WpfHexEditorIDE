# WpfHexEditor.Core

> Data access layer and service library powering the WpfHexEditor ecosystem — ByteProvider, 16+ services, search engine, rendering models, and format detection.

[![.NET](https://img.shields.io/badge/.NET-net48%20%7C%20net8.0--windows-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](../../LICENSE)

---

## Architecture

```mermaid
graph TB
    subgraph Core["WpfHexEditor.Core"]

        subgraph DATA["Core / Data Layer"]
            BP["ByteProvider\n(Central Coordinator — 186+ APIs)"]
            BR["ByteReader\n(Virtual View computation)"]
            EM["EditsManager\n(Modifications / Insertions / Deletions)"]
            PM["PositionMapper\n(Virtual ↔ Physical — O(log n))"]
            URM["UndoRedoManager\n(Unlimited depth, batching)"]
            FP["FileProvider\n(File / Stream / MemoryMapped I/O)"]
        end

        subgraph SVC["Services (16+)"]
            S_SEARCH["FindReplaceService\n(LRU cache, parallel, SIMD)"]
            S_UNDO["UndoRedoService"]
            S_CLIP["ClipboardService"]
            S_BK["BookmarkService\n(+Export +Search)"]
            S_HL["HighlightService\n(stateful)"]
            S_SEL["SelectionService"]
            S_MOD["ByteModificationService"]
            S_TBL["TblService\n(character tables)"]
            S_POS["PositionService"]
            S_BG["CustomBackgroundService"]
            S_FMT["FormatDetectionService\n(400+ formats)"]
            S_CMP["ComparisonService\n(+Parallel +SIMD)"]
            S_PAT["PatternRecognitionService"]
            S_OVL["StructureOverlayService"]
            S_LRO["LongRunningOperationService"]
            S_ST["StateService"]
        end

        subgraph SM["SearchModule"]
            HSE["HexSearchEngine\n(Parallel, SIMD, all modes)"]
        end

        subgraph OTHER["Other"]
            REND["Rendering/\n(Brush, Highlight, Marker models)"]
            FMT["Formatters/\n(Hex, Decimal, Text formatters)"]
            EVT["Events/\n(ByteEventArgs, etc.)"]
            TOOLS["Tools/\n(BinaryTools, ByteConverters)"]
            IFACE["Interfaces/\n(Core interfaces)"]
            MDL["Models/\n(Enums, DTOs)"]
        end
    end

    BP --> BR & EM & PM & URM & FP
    BR --> EM & PM & FP
    EM --> PM

    style BP fill:#ffccbc,stroke:#d84315,stroke-width:3px
    style SVC fill:#e8f5e9,stroke:#388e3c,stroke-width:2px
    style HSE fill:#e3f2fd,stroke:#1976d2,stroke-width:2px
```

---

## Project Structure

```
WpfHexEditor.Core/
├── Core/
│   ├── Bytes/
│   │   ├── ByteProvider.cs          ← Central coordinator (186+ APIs)
│   │   ├── ByteReader.cs            ← Virtual view with edits applied
│   │   ├── EditsManager.cs          ← Track mods / inserts / deletes
│   │   ├── PositionMapper.cs        ← Virtual ↔ physical position (O(log n))
│   │   ├── UndoRedoManager.cs       ← Unlimited undo/redo + batching
│   │   └── FileProvider.cs          ← File / stream / memory-mapped I/O
│   ├── CharacterTable/              ← TBL file format parser
│   ├── Converters/                  ← WPF value converters
│   ├── EventArguments/              ← Custom EventArgs
│   ├── Interfaces/                  ← Core contracts
│   ├── MethodExtention/             ← Extension methods
│   └── Native/                      ← Windows API P/Invoke
│
├── Services/                        ← 16+ specialized services
│   ├── FindReplaceService.cs
│   ├── UndoRedoService.cs
│   ├── ClipboardService.cs
│   ├── BookmarkService.cs
│   ├── BookmarkExportService.cs
│   ├── BookmarkSearchService.cs
│   ├── HighlightService.cs
│   ├── SelectionService.cs
│   ├── ByteModificationService.cs
│   ├── TblService.cs
│   ├── PositionService.cs
│   ├── CustomBackgroundService.cs
│   ├── FormatDetectionService.cs    ← 400+ format signatures
│   ├── ComparisonService.cs
│   ├── ComparisonServiceParallel.cs
│   ├── ComparisonServiceSIMD.cs
│   ├── PatternRecognitionService.cs
│   ├── StructureOverlayService.cs
│   ├── FileDiffService.cs
│   ├── LongRunningOperationService.cs
│   ├── StateService.cs
│   └── VirtualizationService.cs
│
├── SearchModule/                    ← HexSearchEngine (parallel SIMD)
├── Rendering/                       ← Brush / highlight / marker models
├── Formatters/                      ← Hex / decimal / text output
├── Events/                          ← Event argument classes
├── Models/                          ← Enums, DTOs
├── Tools/                           ← BinaryTools, ByteConverters
├── Controls/                        ← Shared WPF controls
├── ViewModels/                      ← Shared view models
└── PartialClasses/                  ← Shared partial class infrastructure
```

---

## ByteProvider — Central API

`ByteProvider` is the single coordinator between all data-layer components. It exposes 186+ methods across categories:

```mermaid
graph LR
    Consumer["HexEditorViewModel\nor any Consumer"] --> BP["ByteProvider"]

    BP --> BR["ByteReader\n(get bytes at virtual pos)"]
    BP --> EM["EditsManager\n(record changes)"]
    BP --> PM["PositionMapper\n(convert positions)"]
    BP --> URM["UndoRedoManager\n(push/pop actions)"]
    BP --> FP["FileProvider\n(open / save / close)"]
```

### Key API groups

| Category | Examples |
|----------|---------|
| **File** | `Open(path)`, `OpenStream(stream)`, `Close()`, `SubmitChanges()` |
| **Read** | `GetByte(position)`, `GetBytes(position, count)`, `Length` |
| **Write** | `ModifyByte(position, value)`, `InsertByte(position, value)`, `DeleteByte(position)` |
| **Selection** | `SelectionStart`, `SelectionStop`, `SelectAll()` |
| **Search** | `FindFirst(pattern)`, `FindAll(pattern)`, `CountOccurrences(pattern)` |
| **Undo** | `Undo()`, `Redo()`, `ClearUndoHistory()`, `BeginBatch()`, `EndBatch()` |
| **Bookmarks** | `AddBookmark(position)`, `RemoveBookmark(position)`, `Bookmarks` |
| **State** | `IsModified`, `HasChanges`, `CanUndo`, `CanRedo`, `IsReadOnly` |

---

## Virtual View Pattern

Users always see a **virtual representation** with all pending edits applied. The original file is never modified until `SubmitChanges()`:

```mermaid
sequenceDiagram
    participant User
    participant ByteReader
    participant EditsManager
    participant FileProvider

    User->>ByteReader: GetByte(virtualPos=5)
    ByteReader->>EditsManager: IsInserted(5)?
    EditsManager-->>ByteReader: Yes → inserted byte
    ByteReader-->>User: FF (inserted value)

    User->>ByteReader: GetByte(virtualPos=6)
    ByteReader->>EditsManager: IsDeleted / modified?
    EditsManager-->>ByteReader: No change
    ByteReader->>FileProvider: GetByte(physicalPos=5)
    FileProvider-->>ByteReader: 43 (original)
    ByteReader-->>User: 43 (original value)
```

---

## Services

### FindReplaceService
- LRU cache for repeated patterns
- Parallel search with thread-pool partitioning
- SIMD vectorization on .NET 8 (AVX2 / SSE2)
- Modes: Hex bytes, Text (UTF-8/16/etc.), Regex, TBL, Wildcard

### FormatDetectionService
- 400+ binary format signatures (magic bytes, header patterns)
- Returns `FormatInfo` with name, confidence, sub-type
- Used by `ParsedFieldsPanel` and IDE toolbar

### ComparisonService
- Three implementations: sequential, parallel, SIMD
- Auto-selects based on file size and runtime capabilities
- Returns `DiffBlock` list for diff navigation

### BookmarkService + BookmarkExportService
- In-memory bookmark collection with position + label + color
- Export to JSON, CSV, or markdown

---

## Search Engine

```mermaid
flowchart LR
    Input["Search query\n+ mode"] --> HSE["HexSearchEngine"]
    HSE --> P1["Thread 1\nPartition 0..N/4"]
    HSE --> P2["Thread 2\nPartition N/4..N/2"]
    HSE --> P3["Thread 3\nPartition N/2..3N/4"]
    HSE --> P4["Thread 4\nPartition 3N/4..N"]
    P1 & P2 & P3 & P4 --> MERGE["Merge & sort results"]
    MERGE --> OUT["IEnumerable&lt;long&gt; positions"]
```

---

## Performance Characteristics

| File Size | Load Time | Memory | Search |
|-----------|-----------|--------|--------|
| 1 KB | < 1 ms | ~1 MB | < 0.1 ms |
| 10 MB | ~5 ms | ~85 MB | ~10 ms |
| 100 MB | ~12 ms | ~90 MB | ~80 ms |
| 1 GB | ~20 ms | ~95 MB | ~300 ms |

Memory stays near-constant because `ByteProvider` uses memory-mapped I/O — only modified bytes are held in RAM.

---

## Dependencies

`WpfHexEditor.Core` has **zero third-party dependencies**. It only references:
- `WpfHexEditor.HexBox` (hex input controls)
- `WpfHexEditor.ColorPicker` (color selection)
- Standard .NET / WPF assemblies

---

## License

Apache 2.0 — Copyright 2016–2026 Derek Tremblay. See [LICENSE](../../LICENSE).
