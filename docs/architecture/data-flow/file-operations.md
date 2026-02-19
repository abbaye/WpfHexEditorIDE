# File Operations Data Flow

**Complete sequence diagrams for file open, close, and save operations**

---

## 📋 Table of Contents

- [Overview](#overview)
- [Open File Sequence](#open-file-sequence)
- [Open Stream Sequence](#open-stream-sequence)
- [Save File Sequence](#save-file-sequence)
- [Close File Sequence](#close-file-sequence)
- [Async Operations](#async-operations)

---

## 📖 Overview

This document details the complete data flow for file operations, showing how components interact during open, save, and close operations.

---

## 📂 Open File Sequence

### Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant FP as FileProvider
    participant EM as EditsManager
    participant PM as PositionMapper
    participant FS as FileSystem

    User->>HE: Open("data.bin")
    HE->>HE: Validate file path
    HE->>VM: Create ViewModel
    activate VM

    VM->>BP: new ByteProvider()
    activate BP

    BP->>EM: new EditsManager()
    activate EM
    EM-->>BP: Manager created
    deactivate EM

    BP->>PM: new PositionMapper(edits)
    activate PM
    PM-->>BP: Mapper created
    deactivate PM

    BP->>FP: OpenFile("data.bin")
    activate FP

    FP->>FS: Open FileStream
    activate FS
    alt File exists
        FS-->>FP: Stream handle
    else File not found
        FS-->>FP: FileNotFoundException
        FP-->>BP: Error
        BP-->>VM: Error
        VM-->>HE: Error
        HE->>User: Show error message
    end
    deactivate FS

    FP->>FP: Get file size
    FP->>FS: Read first 64KB (cache)
    activate FS
    FS-->>FP: Byte data
    deactivate FS
    FP-->>BP: File opened

    BP->>BP: Calculate total lines
    BP-->>VM: Provider ready
    deactivate FP
    deactivate BP

    VM->>VM: Initialize services
    VM-->>HE: ViewModel ready

    HE->>VM: GetVisibleLines(0, 50)
    activate VM
    VM->>BP: ReadBytes(0, 800)
    activate BP
    BP->>FP: ReadBytes(0, 800)
    activate FP
    FP->>FP: Check cache
    alt Cache hit
        FP-->>BP: Cached bytes
    else Cache miss
        FP->>FS: Read from disk
        activate FS
        FS-->>FP: Byte data
        deactivate FS
        FP->>FP: Update cache
        FP-->>BP: Bytes
    end
    deactivate FP
    BP-->>VM: Byte data
    deactivate BP

    VM->>VM: Generate HexLine objects
    VM-->>HE: HexLine[]
    deactivate VM

    HE->>HE: Update HexViewport
    HE->>User: Display hex view
```

### Step-by-Step Breakdown

#### Step 1: User Initiates Open

```csharp
// User code
hexEditor.FileName = "data.bin";

// Or programmatically
hexEditor.Open("data.bin");
```

**Actions**:
1. Validate file path
2. Check file exists
3. Check read permissions

#### Step 2: Create ViewModel

```csharp
// HexEditor creates ViewModel
_viewModel = new HexEditorViewModel();
```

**Actions**:
1. Allocate ViewModel instance
2. Initialize properties
3. Prepare for ByteProvider

#### Step 3: Create ByteProvider

```csharp
// ViewModel creates ByteProvider
_provider = new ByteProvider();
_provider.Open(fileName);
```

**Actions**:
1. Create EditsManager (empty)
2. Create PositionMapper
3. Create FileProvider
4. Open file stream

#### Step 4: Open FileStream

```csharp
// FileProvider opens stream
_fileStream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
_fileSize = _fileStream.Length;
```

**Actions**:
1. Open file with read/write access
2. Get file size
3. Initialize cache

#### Step 5: Cache First Chunk

```csharp
// FileProvider caches first 64KB
byte[] cache = new byte[65536];
_fileStream.Read(cache, 0, cache.Length);
_cache.Add(0, cache);
```

**Benefit**: Instant access to file header and initial data.

#### Step 6: Generate Visible Lines

```csharp
// ViewModel generates lines for viewport
var lines = new List<HexLine>();
for (long line = 0; line < 50; line++)
{
    long position = line * 16;
    byte[] lineBytes = _provider.ReadBytes(position, 16);
    lines.Add(CreateHexLine(line, lineBytes));
}
```

**Actions**:
1. Calculate visible range
2. Read bytes for each line
3. Format hex and ASCII strings
4. Return HexLine objects

#### Step 7: Display in UI

```csharp
// HexEditor updates viewport
_hexViewport.UpdateVisibleLines(lines);
_hexViewport.InvalidateVisual();  // Trigger render
```

**Result**: User sees hex view of file.

---

## 🌊 Open Stream Sequence

### Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant SP as StreamProvider
    participant Stream

    User->>HE: OpenStream(stream)
    HE->>HE: Validate stream
    alt Stream is null
        HE->>User: ArgumentNullException
    else Stream not readable
        HE->>User: ArgumentException
    else Valid stream
        HE->>VM: Create ViewModel
        VM->>BP: new ByteProvider()
        BP->>SP: OpenStream(stream)
        activate SP

        SP->>Stream: CanRead?
        Stream-->>SP: true

        SP->>Stream: CanSeek?
        Stream-->>SP: true

        SP->>Stream: Length
        Stream-->>SP: stream length

        SP->>Stream: Position = 0
        Stream-->>SP: OK

        SP->>Stream: Read first 64KB
        activate Stream
        Stream-->>SP: Byte data
        deactivate Stream

        SP-->>BP: Stream opened
        deactivate SP

        BP-->>VM: Provider ready
        VM-->>HE: ViewModel ready
        HE->>User: Display hex view
    end
```

### Stream Requirements

```csharp
public void OpenStream(Stream stream)
{
    // Validate stream
    if (stream == null)
        throw new ArgumentNullException(nameof(stream));

    if (!stream.CanRead)
        throw new ArgumentException("Stream must be readable");

    if (!stream.CanSeek)
        throw new ArgumentException("Stream must be seekable");

    // Use stream
    _stream = stream;
    _length = stream.Length;

    // Cache first chunk
    CacheStreamData(0, 65536);
}
```

---

## 💾 Save File Sequence

### Sequence Diagram (Smart Save)

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant EM as EditsManager
    participant FP as FileProvider
    participant FS as FileSystem

    User->>HE: Save()
    HE->>VM: Save()
    VM->>BP: Save()

    BP->>EM: HasInsertions || HasDeletions?
    EM-->>BP: Check result

    alt Only modifications (Fast path)
        BP->>EM: GetModifications()
        EM-->>BP: Dictionary<long, byte>

        loop For each modification
            BP->>FP: WriteByte(position, value)
            FP->>FS: Seek(position)
            FS-->>FP: OK
            FP->>FS: Write(value)
            FS-->>FP: OK
        end

        FP->>FS: Flush()
        FS-->>FP: OK

        BP->>EM: ClearModifications()
        EM-->>BP: Cleared

        BP-->>VM: Save complete (fast path)

    else Has insertions/deletions (Full rebuild)
        BP->>FP: CreateTempFile()
        activate FP
        FP->>FS: Create temp file
        FS-->>FP: Temp stream

        BP->>BP: Calculate virtual length

        loop For each virtual position
            BP->>EM: IsInsertion(pos)?
            alt Is insertion
                EM-->>BP: Inserted byte value
                BP->>FP: WriteByte(temp, value)
            else Not insertion
                BP->>PM: VirtualToPhysical(pos)
                PM-->>BP: Physical position
                BP->>FP: ReadByte(original, physPos)
                FP-->>BP: Original byte
                BP->>EM: IsModified(physPos)?
                alt Is modified
                    EM-->>BP: Modified value
                    BP->>FP: WriteByte(temp, modValue)
                else Not modified
                    BP->>FP: WriteByte(temp, origValue)
                end
            end
        end

        BP->>FP: CloseOriginalFile()
        FP-->>BP: Closed

        BP->>FP: ReplaceTempWithOriginal()
        FP->>FS: Delete original
        FS-->>FP: OK
        FP->>FS: Move temp to original
        FS-->>FP: OK

        BP->>FP: ReopenFile()
        FP->>FS: Open file
        FS-->>FP: Stream handle

        BP->>EM: ClearAll()
        EM-->>BP: All edits cleared

        BP->>PM: Reset()
        PM-->>BP: Mapper reset

        BP-->>VM: Save complete (full rebuild)
        deactivate FP
    end

    VM-->>HE: Save complete
    HE->>User: File saved
```

### Fast Path vs Full Rebuild

**Fast Path** (modifications only):
- ✅ Seek to each modified position
- ✅ Write new byte value
- ✅ No file length change
- ⚡ **100x faster** for small edit counts

**Full Rebuild** (insertions/deletions):
- 📝 Create temporary file
- 🔄 Iterate virtual positions
- 💾 Write bytes in virtual order
- 🔄 Replace original with temp
- ⏱️ **Takes longer** but handles all edits

---

## 🚪 Close File Sequence

### Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant EM as EditsManager
    participant FP as FileProvider
    participant FS as FileSystem

    User->>HE: Close()
    HE->>HE: Check if modified

    alt Has unsaved changes
        HE->>User: Show save prompt
        User->>HE: User choice

        alt Save changes
            HE->>VM: Save()
            VM->>BP: Save()
            Note over BP,FS: Save sequence (see above)
            BP-->>VM: Saved
            VM-->>HE: Saved
        else Discard changes
            Note over HE: Continue to close
        else Cancel
            HE->>User: Close cancelled
        end
    end

    HE->>VM: Dispose()
    activate VM

    VM->>BP: Dispose()
    activate BP

    BP->>EM: Dispose()
    activate EM
    EM->>EM: Clear modifications
    EM->>EM: Clear insertions
    EM->>EM: Clear deletions
    EM-->>BP: Disposed
    deactivate EM

    BP->>FP: Dispose()
    activate FP
    FP->>FP: Clear cache
    FP->>FS: Close stream
    activate FS
    FS->>FS: Flush buffers
    FS->>FS: Release file handle
    FS-->>FP: Closed
    deactivate FS
    FP-->>BP: Disposed
    deactivate FP

    BP-->>VM: Disposed
    deactivate BP

    VM->>VM: Dispose services
    VM-->>HE: Disposed
    deactivate VM

    HE->>HE: Clear viewport
    HE->>User: File closed
```

---

## ⚡ Async Operations

### Async Open Sequence

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant FP as FileProvider
    participant BG as Background Thread
    participant UI as UI Thread

    User->>HE: OpenAsync("large.bin")
    HE->>VM: OpenAsync()
    VM->>BP: OpenAsync()

    BP->>FP: OpenFileAsync()
    activate FP

    FP->>BG: Task.Run(() => Open())
    activate BG

    BG->>FS: Open FileStream
    FS-->>BG: Stream handle

    BG->>FS: Get file size
    FS-->>BG: Size (5 GB)

    loop Read chunks
        BG->>FS: Read 64KB
        FS-->>BG: Chunk data

        BG->>FP: Update cache
        FP->>UI: Report progress
        activate UI
        UI->>User: Update progress bar
        deactivate UI
    end

    BG-->>FP: File opened
    deactivate BG
    FP-->>BP: Complete
    deactivate FP

    BP-->>VM: Provider ready
    VM-->>HE: ViewModel ready
    HE->>User: Display hex view
```

### Code Example

```csharp
// Async open with progress
var progress = new Progress<double>(percent =>
{
    progressBar.Value = percent;
    statusLabel.Text = $"Opening: {percent:F1}%";
});

await hexEditor.OpenAsync("large.bin", progress);
Console.WriteLine("File opened");
```

---

## 🔗 See Also

- [Edit Operations](edit-operations.md) - Modify, insert, delete sequences
- [Save Operations](save-operations.md) - Detailed save algorithm
- [Search Operations](search-operations.md) - Find and replace sequences

---

**Last Updated**: 2026-02-19
**Version**: V2.0
