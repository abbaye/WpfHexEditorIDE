# WpfHexEditor.Core.ByteProvider — Documentation (v1.1.1)

> **What you get** — a cross-platform (`net8.0`, **zero WPF**) byte-provider
> engine for editing gigabyte-scale binary files. `ByteProvider` is the V2
> ultra-optimized rewrite that separates file I/O, edit tracking,
> virtual/physical position mapping, and multi-layer caching into composable
> services. Includes a Boyer-Moore-Horspool `SearchEngine` (text/hex/wildcard),
> a 1000-deep `UndoRedoManager` with coalescence and batching, an immutable
> `ChangesetSnapshot` for serialising pending edits without copying the file,
> and the `IBinaryDataSource` adapter contract that lets format-aware tooling
> work with any editor.

## Table of Contents

1. [Installation](#installation)
2. [Architecture](#architecture)
3. [Public API Reference](#public-api-reference)
4. [Usage Examples](#usage-examples)
5. [Search Engine Reference](#search-engine-reference)
6. [Undo / Redo Reference](#undo--redo-reference)
7. [Changeset Snapshot Reference](#changeset-snapshot-reference)
8. [Threading and Performance Notes](#threading-and-performance-notes)
9. [License](#license)

---

## Installation

```bash
dotnet add package WpfHexEditor.Core.ByteProvider --version 1.1.1
```

Requirements:

| Item | Value |
|---|---|
| Target framework | `net8.0` (cross-platform — Windows, Linux, macOS) |
| WPF / WinForms | **none** (`<UseWPF>false</UseWPF>`) |
| External dependencies | none (only `Microsoft.SourceLink.GitHub` private build asset) |
| Source Link | enabled |

The assembly ships as `WpfHexEditor.Core.ByteProvider.dll` with namespaces:

- `WpfHexEditor.Core.Bytes` — `ByteProvider`, `EditsManager`, `UndoRedoManager`, `FileProvider`, `PositionMapper`, `ByteReader`, `ByteModified`, `ByteConverters`, `HexLookup`
- `WpfHexEditor.Core.Changesets` — `ChangesetSnapshot`, `ModifiedRange`, `InsertedBlock`, `DeletedRange`
- `WpfHexEditor.Core.Interfaces` — `IBinaryDataSource`, `IByteModified`
- `WpfHexEditor.Core.Search.Models` — `SearchOptions`, `SearchResult`, `SearchMatch`, `SearchMode`, `RelativeSearchOptions`, `RelativeSearchResult`, `SearchHistoryEntry`, `EncodingProposal`
- `WpfHexEditor.Core.Search.Services` — `SearchEngine`
- `WpfHexEditor.Core.Services` — `UndoRedoService`
- `WpfHexEditor.Core.CharacterTable` — `DteType`, `DefaultCharacterTableType`
- `WpfHexEditor.Core` — enums (`ByteAction`, `CopyPasteMode`, etc.) and `ConstantReadOnly`

---

## Architecture

### Component Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│  ByteProvider (sealed partial)                                   │
│  ┌────────────┐  ┌──────────────┐  ┌──────────────┐  ┌────────┐ │
│  │FileProvider│  │EditsManager  │  │PositionMapper│  │ByteRead│ │
│  │  – Stream  │  │  – Modified  │  │  – V↔P map   │  │  – LRU │ │
│  │  – 64 KB $ │  │  – Inserted  │  │  – cache     │  │  – $   │ │
│  │            │  │  – Deleted   │  │              │  │        │ │
│  └─────┬──────┘  └──────┬───────┘  └──────┬───────┘  └───┬────┘ │
│        │                │                  │              │      │
│        └────────────────┴──────────────────┴──────────────┘      │
│                                │                                  │
│                       UndoRedoManager (1000-deep)                 │
│                       (coalescence + batch transactions)          │
│                                                                   │
│  Partials: ByteProvider.Search.cs    → SearchEngine integration   │
│            ByteProvider.Changeset.cs → ChangesetSnapshot capture  │
└──────────────────────────────────────────────────────────────────┘
```

### Design Principles

| Principle | How it is enforced |
|---|---|
| **Cross-platform** | `net8.0`, no Windows-specific APIs (no `WindowsBase`, no `PresentationCore`) |
| **Zero WPF** | `<UseWPF>false</UseWPF>` — usable from console, ASP.NET, MAUI, Avalonia, etc. |
| **Composition over inheritance** | `ByteProvider` is a façade over five private services; each service is independently testable |
| **Virtual ↔ Physical separation** | the user sees a *virtual* file (with inserts/deletes applied); `PositionMapper` translates to physical file offsets |
| **Immutable snapshots** | `ChangesetSnapshot` and its three records (`ModifiedRange`, `InsertedBlock`, `DeletedRange`) are `sealed record` and immutable |
| **O(e) snapshot capture** | `GetChangesetSnapshot()` iterates only the edit dictionaries, never the file |

### Virtual vs Physical positions

`ByteProvider` distinguishes between:

| Concept | Definition |
|---|---|
| `PhysicalLength` | raw on-disk byte count from `FileProvider` |
| `VirtualLength` | `PhysicalLength + Σ inserted − Σ deleted` |
| Virtual position | the offset the user sees in the editor (continuous, no gaps) |
| Physical position | the actual offset in the backing stream |

All public `ByteProvider` methods take *virtual* positions. The
`PositionMapper` handles translation transparently.

---

## Public API Reference

### `sealed partial class WpfHexEditor.Core.Bytes.ByteProvider : IDisposable`

#### Properties

| Property | Type | Description |
|---|---|---|
| `FilePath` | `string` | Backing file path, or null for in-memory buffer. |
| `IsOpen` | `bool` | True when a file / stream / memory buffer is loaded. |
| `IsReadOnly` | `bool` | True when the source disallows writes. |
| `Stream` | `Stream` | The underlying stream (for advanced scenarios). |
| `PhysicalLength` | `long` | Raw size of the backing storage. |
| `VirtualLength` | `long` | Size as the user sees it (with edits applied). |
| `HasChanges` | `bool` | True when at least one edit is pending. |
| `ModificationStats` | `(int modified, int inserted, int deleted)` | Per-type edit counts. |
| `UndoRedoManager` | `UndoRedoManager` | Read-only access to the undo engine. |
| `CanUndo` / `CanRedo` | `bool` | Stack-status flags. |
| `UndoCount` / `RedoCount` | `int` | Stack depth. |

#### Events

| Event | Signature | Fired when |
|---|---|---|
| `ChangesCleared` | `EventHandler` | All edits are cleared (save or explicit clear). |

#### Open / Close / Save

| Method | Notes |
|---|---|
| `OpenFile(string filePath, bool readOnly = false)` | Open from disk. |
| `OpenStream(Stream stream, bool readOnly = false)` | Open from any seekable stream. |
| `OpenMemory(byte[] data, bool readOnly = false)` | Open from an in-memory buffer. |
| `Close()` | Closes the source and clears edits. |
| `Save()` | Flush pending edits in-place. |
| `SaveAs(string newFilePath, bool overwrite = false)` | Write to a new file. |
| `SubmitChanges()` / `SubmitChanges(string newFilename, bool overwrite)` | V1-compatible save aliases. |
| `Reload()` | Discard edits and re-read from the source. |
| `Dispose()` | Release the underlying file/stream. |

#### Edit operations

| Method | Notes |
|---|---|
| `ModifyByte(long virtualPosition, byte value)` | Single-byte modify (records undo). |
| `ModifyBytes(long startVirtualPosition, byte[] values)` | Bulk modify. |
| `InsertByte(long virtualPosition, byte value)` | Single-byte insert. |
| `InsertBytes(long virtualPosition, byte[] bytes)` | Bulk insert. |
| `DeleteByte(long virtualPosition)` | Single-byte delete. |
| `DeleteBytes(long startVirtualPosition, long count)` | Bulk delete. |
| `Paste(long virtualPosition, byte[] bytes, bool allowExtend)` | Paste = modify-then-extend semantics. |
| `FillWithByte(long virtualPosition, long length, byte value)` | Repeated-fill helper. |
| `RestoreOriginalByte(long virtualPosition)` | Revert a single modification. |
| `RestoreOriginalBytes(long[] virtualPositions)` / `RestoreOriginalBytes(IEnumerable<long>)` | Bulk revert; returns the count restored. |
| `RestoreOriginalBytesInRange(long startVirtualPosition, long stopVirtualPosition)` | Range revert. |
| `RestoreAllModifications()` | Revert every modification. |
| `ResetByte` / `ResetBytes` / `ResetBytesInRange` / `ResetAllBytes` | V1-compatible aliases. |
| `ClearAllEdits()` | Drop every pending edit (modified + inserted + deleted). |
| `ClearModifications()` / `ClearInsertions()` / `ClearDeletions()` | Per-type clear. |

#### Read operations

| Method | Notes |
|---|---|
| `ReadByte()` | Read one byte at the current stream position. |
| `Stream.Position = …` then `ReadByte()` | Random-access read — uses the multi-layer cache. |

#### Undo / Redo

| Method | Notes |
|---|---|
| `Undo()` | Pop one entry (single op or batch group) and revert. |
| `Redo()` | Re-apply the last undone entry. |
| `ClearUndoRedoHistory()` | Empty both stacks. |
| `BeginUndoTransaction(string description)` | Start a named batch — every subsequent edit is grouped. |
| `CommitUndoTransaction()` | Close the batch and push a single `UndoGroup` onto the stack. |
| `RollbackUndoTransaction()` | Discard the in-progress batch. |
| `BeginBatch()` / `EndBatch()` | Lower-level batching (no description). |
| `GetUndoDescriptions(int maxCount = 20)` | Human-readable list, newest first. |
| `GetRedoDescriptions(int maxCount = 20)` | Same for redo. |

#### Bulk introspection

| Method | Notes |
|---|---|
| `GetByteModifieds(ByteAction action)` | Returns `IDictionary<long, ByteModified>` for `Modified` / `Added` / `Deleted`. |
| `GetAllModifiedVirtualPositions()` | Enumerate every modified position (virtual). |
| `GetCacheStatistics()` | Multi-layer cache hit/miss diagnostics (debug-only string). |
| `GetChangesetSnapshot()` | Immutable snapshot of all pending edits (see §7). |

#### Quick search shortcuts (legacy)

| Method | Notes |
|---|---|
| `FindFirst(byte[] pattern, long startPosition = 0)` | Returns the first virtual offset or `-1`. |
| `FindNext(byte[] pattern, long currentPosition)` | Returns the next match or `-1`. |
| `FindLast(byte[] pattern, long startPosition = 0)` | Returns the last match or `-1`. |
| `FindAll(byte[] pattern, long startPosition = 0)` | Enumerates every match. |
| `CountOccurrences(byte[] pattern, long startPosition = 0)` | Returns the match count. |

> For new code prefer `Search(SearchOptions, …)` — see §5.

### `sealed class WpfHexEditor.Core.Bytes.EditsManager`

| Member | Description |
|---|---|
| `ModifiedCount`, `InsertedPositionsCount`, `TotalInsertedBytesCount`, `DeletedCount` | Statistics. |
| `HasChanges` | True when any edit is pending. |
| `ModifyByte(long physicalPosition, byte value)` | Mark a byte modified. |
| `IsModified(long physicalPosition)` | Lookup. |
| `GetModifiedByte(long physicalPosition)` | Returns `(byte value, bool exists)`. |
| `RemoveModification(long physicalPosition)` | Revert one. |
| `InsertByte(long physicalPosition, byte value)` / `InsertBytes(long, byte[])` | Insertion. |
| `GetInsertedBytesAt(long physicalPosition)` | Returns the ordered `List<InsertedByte>`. |
| `HasInsertionsAt`, `GetInsertionCountAt` | Insertion introspection. |
| `GetAllModifiedPositions()` | Enumerates every modified physical position. |
| `GetAllModifiedBytes()` | Enumerates `(position, value)` pairs. |
| `GetInsertionPositionsWithCounts()` | Returns `Dictionary<long, int>`. |
| `GetAllDeletedPositions()` | Enumerates deleted physical positions. |

`InsertedByte` is a public `struct` holding `Value` and `VirtualOffset`.

### `interface WpfHexEditor.Core.Interfaces.IBinaryDataSource`

| Member | Type |
|---|---|
| `FilePath` | `string?` (null for in-memory) |
| `Length` | `long` |
| `IsReadOnly` | `bool` |
| `ReadBytes(long offset, int length)` | `byte[]` (may be short if at EOF) |
| `WriteBytes(long offset, byte[] data)` | throws `InvalidOperationException` when read-only |
| `DataChanged` | `event EventHandler?` — fires on edit, undo, redo |

The recommended pattern is a thin adapter wrapping `ByteProvider`. See
example 6 below.

### `class WpfHexEditor.Core.Bytes.ByteModified : IByteModified`

Lightweight DTO returned from `GetByteModifieds`:

| Property | Type | Description |
|---|---|---|
| `Byte` | `byte?` | New byte value (nullable for deletions). |
| `BytePositionInStream` | `long` | Virtual position. |
| `Action` | `ByteAction` (`Nothing`, `Modified`, `Added`, `Deleted`) | Edit kind. |
| `UndoLength` | `long` | Number of bytes this entry covers (legacy compatibility). |
| `GetCopy()` | `ByteModified` | Deep clone. |

---

## Usage Examples

### Example 1 — Open, modify, save

```csharp
using WpfHexEditor.Core.Bytes;

using var provider = new ByteProvider();
provider.OpenFile(@"C:\firmware.bin");

// Patch byte at virtual offset 0x100 with 0x90 (NOP on x86).
provider.ModifyByte(0x100, 0x90);

// Insert four bytes after offset 0x200.
provider.InsertBytes(0x200, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

// Delete a range.
provider.DeleteBytes(0x300, 16);

// Flush.
provider.Save();
```

### Example 2 — In-memory editing

```csharp
var data = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
using var provider = new ByteProvider();
provider.OpenMemory(data);

provider.ModifyByte(0, 0x59); // 'Y'
Console.WriteLine(provider.HasChanges);            // true
Console.WriteLine(provider.VirtualLength);         // 5
Console.WriteLine(provider.ModificationStats);     // (1, 0, 0)
```

### Example 3 — Batched undo (paste as one step)

```csharp
provider.BeginUndoTransaction("Paste 256 bytes");
try
{
    provider.ModifyBytes(0x500, payload);
    provider.CommitUndoTransaction();
}
catch
{
    provider.RollbackUndoTransaction();
    throw;
}

// One Ctrl+Z reverts all 256 byte writes in a single step.
provider.Undo();
```

### Example 4 — Coalesced single-byte typing

```csharp
// Simulate a user typing four hex digits within 500 ms.
provider.ModifyByte(0x10, 0xAB);
provider.ModifyByte(0x11, 0xCD);
provider.ModifyByte(0x12, 0xEF);
provider.ModifyByte(0x13, 0x42);

Console.WriteLine(provider.UndoCount); // 1 — the four single-byte ops were coalesced
provider.Undo();                        // single step reverts all four bytes
```

### Example 5 — Stream as source

```csharp
using var ms = new MemoryStream();
ms.Write(File.ReadAllBytes("input.bin"));
ms.Position = 0;

using var provider = new ByteProvider();
provider.OpenStream(ms);

// Edit freely…
provider.InsertBytes(0, new byte[] { 0x7F, 0x45, 0x4C, 0x46 }); // ELF magic
provider.SaveAs("out.bin", overwrite: true);
```

### Example 6 — IBinaryDataSource adapter

```csharp
public sealed class ByteProviderDataSource : IBinaryDataSource
{
    private readonly ByteProvider _bp;
    public ByteProviderDataSource(ByteProvider bp) { _bp = bp; }

    public string? FilePath => _bp.FilePath;
    public long    Length   => _bp.VirtualLength;
    public bool    IsReadOnly => _bp.IsReadOnly;

    public byte[] ReadBytes(long offset, int length)
    {
        var stream = _bp.Stream;
        stream.Position = offset;
        var buf = new byte[length];
        int read = stream.Read(buf, 0, length);
        return read == length ? buf : buf.AsSpan(0, read).ToArray();
    }

    public void WriteBytes(long offset, byte[] data) => _bp.ModifyBytes(offset, data);

    public event EventHandler? DataChanged;
    // Wire ByteProvider's ChangesCleared / Undo / Redo to DataChanged…
}
```

---

## Search Engine Reference

### `class WpfHexEditor.Core.Search.Services.SearchEngine`

Boyer-Moore-Horspool algorithm with optional parallelism for files > 10 MB.

| Member | Notes |
|---|---|
| `SearchEngine(ByteProvider byteProvider)` | Constructor (provider injected). |
| `SearchResult Search(SearchOptions options, CancellationToken = default)` | Full configurable search. |
| `SearchMatch FindNext(byte[] pattern, long startPosition, bool forward, bool useWildcard, byte wildcardByte, CancellationToken)` | Single-match shortcut. |
| `List<SearchMatch> FindAll(byte[] pattern, bool useWildcard, byte wildcardByte, CancellationToken)` | All matches. |

### `class SearchOptions`

| Property | Default | Description |
|---|---|---|
| `Pattern` | — | `byte[]` to search for. |
| `CaseSensitive` | `true` | For text-converted patterns. |
| `SearchBackward` | `false` | Direction. |
| `StartPosition` | `0` | Virtual offset. |
| `EndPosition` | `-1` | `-1` = end of file. |
| `UseWildcard` | `false` | Enable `0xFF` (or custom) wildcard byte. |
| `WildcardByte` | `0xFF` | Wildcard sentinel. |
| `UseParallelSearch` | `true` | Auto-enable parallelism above 10 MB. |
| `ParallelChunkSize` | `1 MB` | Chunk size in bytes. |
| `MaxResults` | `0` (unlimited) | Cap result count. |
| `WrapAround` | `false` | Restart from beginning when reaching EOF. |
| `ContextRadius` | `8` | Capture N bytes around each match. |

### Shortcut methods on `ByteProvider` (partial class)

| Method | Description |
|---|---|
| `Search(SearchOptions, CancellationToken)` | Full search. |
| `FindNextAdvanced(byte[] pattern, long startPosition, bool forward, bool useWildcard, byte wildcardByte, CancellationToken)` | Single match. |
| `FindAllAdvanced(byte[] pattern, bool useWildcard, byte wildcardByte, CancellationToken)` | All matches. |
| `SearchText(string text, Encoding = null, bool caseSensitive = true, long startPosition = 0, CancellationToken)` | Text search (encoding-aware). |
| `SearchHex(string hexPattern, long startPosition = 0, CancellationToken)` | Hex string search with `??` / `**` wildcards. |

### Example — Text + hex + wildcard

```csharp
// Text search (UTF-8 by default).
var result = provider.SearchText("MZ");
foreach (var match in result.Matches)
    Console.WriteLine($"PE header candidate at 0x{match.Position:X}");

// Hex search with wildcards.
var hex = provider.SearchHex("48 ?? 6C 6C 6F"); // H?llo

// Cancellable advanced search.
var opts = new SearchOptions {
    Pattern       = new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, // ELF
    MaxResults    = 100,
    ContextRadius = 16
};
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var elf = provider.Search(opts, cts.Token);
```

---

## Undo / Redo Reference

### `sealed class WpfHexEditor.Core.Bytes.UndoRedoManager`

| Property | Description |
|---|---|
| `CanUndo` / `CanRedo` | Stack-status. |
| `UndoStackCount` / `RedoStackCount` | Depth. |
| `IsInBatchMode` | True between `BeginBatch` and `EndBatch`. |
| `MaxUndoStackSize` | Default `1000`. Trims oldest entries beyond. |

| Method | Description |
|---|---|
| `RecordModify(long, byte[] oldValues, byte[] newValues)` | Record a modify. |
| `RecordInsert(long, byte[] insertedValues)` | Record an insert. |
| `RecordDelete(long, byte[] deletedValues, long[]? physicalPositions = null)` | Record a delete (physical positions enable green-border-free undelete). |
| `PopUndo()` / `PopRedo()` | Return the next entry (boxed `UndoOperation` or `UndoGroup`). |
| `PeekUndo()` / `PeekRedo()` | Non-destructive peek. |
| `PeekUndoDescription()` / `PeekRedoDescription()` | Human-readable label. |
| `GetUndoDescriptions(int maxCount = 20)` | List of descriptions (newest first). |
| `GetRedoDescriptions(int maxCount = 20)` | Redo equivalent. |
| `BeginBatch(string description = "")` / `EndBatch()` / `RollbackBatch()` | Transactional grouping. |
| `ClearAll()` / `ClearRedo()` | Stack clear. |

### `enum UndoOperationType`

`Modify`, `Insert`, `Delete`.

### `struct UndoOperation`

Public fields: `Type`, `VirtualPosition`, `OldValues`, `NewValues`, `Count`.
Coalescence merges consecutive single-byte adjacent `Modify` ops within
500 ms — invisible to the caller, only the merged step appears in the stack.

### `class WpfHexEditor.Core.Services.UndoRedoService`

Convenience façade taking a `ByteProvider` instance:

```csharp
public bool Undo(ByteProvider provider);
public bool Redo(ByteProvider provider);
public void ClearAll(ByteProvider provider);
public bool CanUndo(ByteProvider provider);
public bool CanRedo(ByteProvider provider);
```

### Shared-undo contract — `IUndoAwareEditor`

Consumers that want multiple views (or multiple controls) to share the same
undo stack implement `IUndoAwareEditor`. `ByteProvider.UndoRedoManager` is
exposed as a property so any host can push `HexByteUndoEntry` instances into
a shared `UndoEngine`. Typical scenarios: two viewports of the same buffer,
or a text-side adapter wanting its edits to participate in the same undo
history as byte-level edits.

---

## Changeset Snapshot Reference

### `sealed record ChangesetSnapshot`

| Member | Type | Description |
|---|---|---|
| `Modified` | `IReadOnlyList<ModifiedRange>` | Contiguous runs of modified bytes. |
| `Inserted` | `IReadOnlyList<InsertedBlock>` | Blocks inserted before a physical offset. |
| `Deleted` | `IReadOnlyList<DeletedRange>` | Contiguous deleted ranges. |
| `HasEdits` | `bool` | Convenience accessor. |
| `Empty` | `static readonly` | Reusable empty instance. |

### `sealed record ModifiedRange(long Offset, byte[] Values)`

### `sealed record InsertedBlock(long Offset, byte[] Bytes)`

### `sealed record DeletedRange(long Start, long Count)`

### Example — serialise a `.whchg` patch

```csharp
var snapshot = provider.GetChangesetSnapshot();
if (!snapshot.HasEdits) return;

var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText("patch.whchg", json);
```

Capture is **O(e)** in the number of edits — never reads the underlying file.
The resulting snapshot is immutable and safe to enumerate from any thread.

---

## Threading and Performance Notes

### Thread safety

| Component | Read | Write |
|---|---|---|
| `ByteProvider` | not thread-safe; serialize via lock | not thread-safe |
| `EditsManager` | safe for reads, external locking required for writes | requires lock |
| `UndoRedoManager` | reads safe, writes require external locking | requires lock |
| `ChangesetSnapshot` | fully immutable, fully thread-safe | n/a |
| `SearchEngine` | safe to run multiple searches in parallel **as long as the underlying ByteProvider is not edited concurrently** | n/a |

For multi-threaded scenarios, wrap `ByteProvider` calls in a `lock` block or
expose it via a thread-affine actor.

### Caching layers

| Layer | Size | Notes |
|---|---|---|
| `FileProvider` cache | 64 KB sliding window | minimises stream reads |
| `PositionMapper` cache | unbounded LRU on virtual↔physical mapping | enabled by `EnableCache()` in the constructor |
| `ByteReader` cache | per-line + per-page | invalidated on every edit (or coalesced in batch mode) |

Call `GetCacheStatistics()` for hit/miss telemetry while profiling.

### Search performance

- **Boyer-Moore-Horspool** — ~99% faster than naive byte-by-byte comparison
  on long patterns.
- **Auto-parallel** when `SearchOptions.UseParallelSearch=true` and the search
  range exceeds 10 MB (`PARALLEL_THRESHOLD`).
- Chunks overlap by `pattern.Length - 1` bytes to catch matches that span
  chunk boundaries.
- Backward search disables parallelism (sequential only).

### Undo coalescence

Single-byte `Modify` ops at adjacent virtual positions within a 500 ms window
are merged into a single `UndoOperation` carrying the concatenated old/new
byte arrays. This keeps the undo stack compact during keyboard editing without
caller cooperation.

### Batch mode

Wrap large bulk edits in `BeginBatch()` / `EndBatch()` (or
`BeginUndoTransaction(name)` / `CommitUndoTransaction()`):

- defers cache invalidation until the batch ends — order-of-magnitude faster
  for paste / fill operations,
- groups all recorded ops into a single `UndoGroup` so the user undoes the
  entire operation atomically.

### Allocation tips

- `ChangesetSnapshot` capture allocates one array per contiguous run — for
  files with sparse edits this is sub-KB.
- `SearchResult.Matches` is a `List<SearchMatch>`; cap with
  `SearchOptions.MaxResults` to avoid unbounded growth on pathological inputs.

---

## License

GNU Affero General Public License v3.0 — `AGPL-3.0-only`.

- Copyright © 2026 Derek Tremblay (derektremblay666@gmail.com)
- Authors / contributors: Derek Tremblay, Claude Sonnet 4.6
- Repository: https://github.com/abbaye/WpfHexEditorControl

If your application is distributed under a license incompatible with AGPL-3.0,
contact the author for an alternative licensing arrangement.
