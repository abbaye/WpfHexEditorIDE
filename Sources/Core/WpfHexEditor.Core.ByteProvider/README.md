# WpfHexEditor.Core.ByteProvider

> Cross-platform byte provider for gigabyte-scale binary files — zero WPF dependency.

[![.NET](https://img.shields.io/badge/.NET-net8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/WpfHexEditor.Core.ByteProvider?logo=nuget)](https://www.nuget.org/packages/WpfHexEditor.Core.ByteProvider)  
[![Version](https://img.shields.io/badge/version-1.3.1-blue)](https://www.nuget.org/packages/WpfHexEditor.Core.ByteProvider)
[![License](https://img.shields.io/badge/License-AGPL--3.0-blue.svg)](https://github.com/abbaye/WpfHexEditorControl/blob/master/LICENSE)


> **Full documentation**: [WpfHexEditor-Core-ByteProvider-guide.md](https://github.com/abbaye/WpfHexEditorIDE/blob/master/Sources/Core/WpfHexEditor.Core.ByteProvider/WpfHexEditor-Core-ByteProvider-guide.md) — API reference, architecture, integration guides, and usage examples.

---

## What's New in 1.3.1

- **Fix**: Null-coalescing simplifications across `ByteProvider` internals — minor allocation reduction on high-frequency read paths.
- **Fix**: Static analysis warnings resolved (nullable annotations, unused variable cleanup).
- **No public API changes** — drop-in upgrade from 1.3.0.

## What's New in 1.3.0

- **New**: `IByteProviderFactory` + `ByteProviderFactory` — create-and-open in one call, DI-registrable via `services.AddByteProviderFactory()`.
- **New**: `ByteProviderDecorator` abstract base — forward-all AOP pattern; subclass to intercept reads, writes, or searches without touching `ByteProvider` directly.
- **New**: `ByteSlice` — zero-allocation span-like view over a virtual range (`index`, `SubSlice`, `SequenceEqual`, `CopyTo`).
- **New**: `IByteProviderMetrics` — telemetry hooks wired into Open/Close/Modify/Insert/Delete/Save/Search; `NullByteProviderMetrics` singleton for zero-overhead default.
- **New**: `ByteProviderOptions.WithMetrics()` — fluent injection of custom metrics implementation.

## What's New in 1.2.0

- **New**: `IByteProvider` public interface — mock or decorate in tests without referencing the concrete class.
- **New**: `ByteProviderOptions` fluent configuration (MMF threshold, undo depth, cache size).
- **New**: `MemoryMappedFileProvider` — automatic MMF switch for files &gt; 512 MB.
- **New**: Rich events — `ByteModified`, `BytesInserted`, `BytesDeleted`, `SaveCompleted`.
- **New**: `ByteProviderDiff` — binary diff engine (`Compare` + `ApplyDiff`).
- **New**: `KnownPatterns` — pre-built magic-byte arrays (PE, ELF, ZIP, PNG, PDF, SQLite…).
- **New**: Async streaming — `FindAllAsync`, `ReadLinesAsync`, `CopyToAsync`, `SearchRegexAsync`.

## What's New in 1.1.1

- **`IUndoAwareEditor` contract finalized** — any editor or view-model can plug into the `ByteProvider` undo stack with a uniform interface; multiple subscribers can now share a single `UndoEngine` instance. Drop-in upgrade from 1.1.0.
- **No public API changes.**

## What's New in 1.1.0

- **`IUndoAwareEditor` interface** + `HexByteUndoEntry` typed entry — public extension point for any consumer that wants to participate in the `ByteProvider` undo stack.
- **`UndoEngine` shareable across consumers** — multiple `IUndoAwareEditor` subscribers can be wired to a single `ByteProvider` instance and observe the same undo/redo events.
- **Regression test suite added** — `ByteProvider`, `UndoRedoManager`, `SearchEngine`, `EditsManager`, `ChangesetSnapshot`.

## Features

| Component | Description |
|---|---|
| `ByteProvider` | Ultra-fast file I/O with virtual/physical position mapping, gigabyte-scale support |
| `EditsManager` | In-memory edit tracking (modify, insert, delete) without touching the original file |
| `UndoRedoManager` | Full undo/redo with batch transactions, coalescence, and description stack |
| `SearchEngine` | Boyer-Moore-Horspool pattern search (text, hex, wildcard), parallel for large files |
| `ChangesetSnapshot` | Immutable O(e) snapshot of pending edits for serialization and persistence |
| `FileProvider` | Stream abstraction (file, memory stream, read-only) with caching |
| `PositionMapper` | Virtual/physical offset mapping accounting for inserts and deletes |
| `ByteReader` | Intelligent reads with multi-layer caching |

---

## Quick Start

### Open a file and read bytes

```csharp
using WpfHexEditor.Core.Bytes;

var provider = new ByteProvider();
provider.OpenFile("firmware.bin");

// Read 16 bytes at offset 0x100
byte[] data = provider.GetBytes(0x100, 16);

Console.WriteLine($"File size: {provider.VirtualLength} bytes");
Console.WriteLine($"Is open:   {provider.IsOpen}");
```

### Open from a stream or memory buffer

```csharp
// From any stream
provider.OpenStream(myStream, readOnly: true);

// From a byte array
provider.OpenMemory(new byte[] { 0x4D, 0x5A, 0x90, 0x00 });
```

### Modify bytes with undo/redo

```csharp
// Modify a single byte
provider.ModifyByte(0x200, 0xFF);

// Modify multiple bytes at once
provider.ModifyBytes(0x200, new byte[] { 0xFF, 0xFE, 0xFD });

// Insert bytes
provider.InsertBytes(0x300, new byte[] { 0x00, 0x01, 0x02 });

// Delete bytes
provider.DeleteBytes(0x400, count: 4);

// Undo / Redo
provider.Undo();
provider.Redo();

Console.WriteLine($"Can undo: {provider.CanUndo}  ({provider.UndoCount} steps)");
Console.WriteLine($"Can redo: {provider.CanRedo}  ({provider.RedoCount} steps)");
```

### Batch undo transactions

```csharp
// Group multiple edits into a single undo step
provider.BeginUndoTransaction("Fill with zeros");
for (int i = 0; i < 256; i++)
    provider.ModifyByte(0x1000 + i, 0x00);
provider.CommitUndoTransaction();

// Undo reverses all 256 edits in one step
provider.Undo();
```

### Search

```csharp
using WpfHexEditor.Core.Search.Models;

// Search by byte pattern (MZ header)
var options = new SearchOptions
{
    Pattern = new byte[] { 0x4D, 0x5A },
    StartPosition = 0
};

SearchResult result = provider.Search(options);

foreach (var match in result.Matches)
    Console.WriteLine($"Found at 0x{match.Position:X8}");

// Search by text
SearchResult textResult = provider.SearchText("Copyright", caseSensitive: false);

// Search by hex pattern with wildcard
SearchResult hexResult = provider.SearchHex("4D 5A ?? 00");
```

### Changeset snapshot

```csharp
using WpfHexEditor.Core.Changesets;

// Capture all pending edits — O(e), never reads the file
ChangesetSnapshot snapshot = provider.GetChangesetSnapshot();

Console.WriteLine($"Has edits       : {snapshot.HasEdits}");
Console.WriteLine($"Modified ranges : {snapshot.Modified.Count}");
Console.WriteLine($"Inserted blocks : {snapshot.Inserted.Count}");
Console.WriteLine($"Deleted ranges  : {snapshot.Deleted.Count}");

foreach (var range in snapshot.Modified)
    Console.WriteLine($"  Modified {range.Values.Length} bytes at 0x{range.Offset:X8}");
```

---

## Project Structure

```
WpfHexEditor.Core.ByteProvider/
├── Bytes/
│   ├── ByteProvider.cs              ← Main entry point
│   ├── ByteProvider.Search.cs       ← Search integration
│   ├── ByteProvider.Changeset.cs    ← Changeset snapshot
│   ├── EditsManager.cs              ← Edit tracking
│   ├── UndoRedoManager.cs           ← Undo/redo stacks
│   ├── FileProvider.cs              ← Stream abstraction
│   ├── PositionMapper.cs            ← Virtual/physical mapping
│   └── ByteReader.cs                ← Cached reads
├── Search/
│   ├── Services/SearchEngine.cs     ← Boyer-Moore-Horspool
│   └── Models/                      ← SearchOptions, SearchResult, SearchMode
├── Services/
│   └── UndoRedoService.cs
└── Changesets/
    └── ChangesetSnapshot.cs         ← ModifiedRange, InsertedBlock, DeletedRange
```

---

## Dependencies

None. Zero external NuGet dependencies. Pure .NET 8.0.

---

## License

GNU Affero General Public License v3.0 — Copyright 2026 Derek Tremblay.
See [LICENSE](https://github.com/abbaye/WpfHexEditorControl/blob/master/LICENSE).
