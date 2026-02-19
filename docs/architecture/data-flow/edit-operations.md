# Edit Operations Data Flow

**Complete sequence diagrams for modify, insert, and delete operations**

---

## 📋 Table of Contents

- [Overview](#overview)
- [Modify Byte Sequence](#modify-byte-sequence)
- [Insert Byte Sequence](#insert-byte-sequence)
- [Delete Bytes Sequence](#delete-bytes-sequence)
- [Batch Operations](#batch-operations)
- [Undo/Redo Flow](#undoredo-flow)

---

## 📖 Overview

This document details the complete data flow for byte editing operations, showing how modifications, insertions, and deletions are tracked and applied.

---

## ✏️ Modify Byte Sequence

### Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant EM as EditsManager
    participant UR as UndoRedoManager
    participant FP as FileProvider

    User->>HE: Type 'FF' at position 0x100
    HE->>VM: ModifyByte(0x100, 0xFF)

    VM->>BP: ModifyByte(0x100, 0xFF)
    activate BP

    BP->>BP: Validate position
    alt Position out of range
        BP->>VM: ArgumentOutOfRangeException
        VM->>HE: Error
        HE->>User: Show error
    else Position valid
        BP->>FP: GetOriginalByte(0x100)
        activate FP
        FP->>FP: Check cache
        alt Cache hit
            FP-->>BP: Cached byte (0x42)
        else Cache miss
            FP->>FS: Read from disk
            FS-->>FP: Byte (0x42)
            FP-->>BP: Byte (0x42)
        end
        deactivate FP

        BP->>EM: AddModification(0x100, 0xFF)
        activate EM
        EM->>EM: Check if already modified
        alt First modification
            EM->>EM: Store: 0x100 → 0xFF
        else Already modified
            EM->>EM: Update: 0x100 → 0xFF
        end
        EM->>EM: Increment modification count
        EM-->>BP: Modification added
        deactivate EM

        BP->>UR: PushEdit(ModifyCommand)
        activate UR
        UR->>UR: Create command (0x100, oldValue=0x42, newValue=0xFF)
        UR->>UR: Push to undo stack
        UR->>UR: Clear redo stack
        UR-->>BP: Edit recorded
        deactivate UR

        BP->>BP: RaiseDataChanged()
        BP-->>VM: Modification complete
        deactivate BP

        VM->>VM: RefreshVisibleLines()
        VM-->>HE: Lines updated
        HE->>HE: InvalidateVisual()
        HE->>User: Display updated view
    end
```

### Step-by-Step Breakdown

#### Step 1: User Input

```csharp
// User types in hex viewport
// HexViewport captures KeyDown event
protected override void OnKeyDown(KeyEventArgs e)
{
    if (IsHexChar(e.Key))
    {
        char hexChar = GetHexChar(e.Key);
        ProcessHexInput(hexChar);
    }
}

private void ProcessHexInput(char hexChar)
{
    // Accumulate hex nibbles
    if (_hexBuffer.Length == 0)
    {
        _hexBuffer.Append(hexChar);  // First nibble
    }
    else
    {
        _hexBuffer.Append(hexChar);  // Second nibble
        byte value = Convert.ToByte(_hexBuffer.ToString(), 16);

        // Send to ViewModel
        _viewModel.ModifyByte(_currentPosition, value);

        _hexBuffer.Clear();
        _currentPosition++;
    }
}
```

#### Step 2: Validate and Get Original

```csharp
public void ModifyByte(long position, byte value)
{
    // Validate
    if (position < 0 || position >= Length)
        throw new ArgumentOutOfRangeException(nameof(position));

    // Get original value (for undo)
    byte originalValue = _fileProvider.ReadByte(position);

    // Apply modification
    _editsManager.AddModification(position, value);
}
```

#### Step 3: Track in EditsManager

```csharp
public void AddModification(long position, byte value)
{
    // Store modification
    _modifications[position] = value;

    // Update stats
    _modificationCount = _modifications.Count;

    // Raise event
    OnModificationAdded(position, value);
}
```

#### Step 4: Record for Undo

```csharp
// Create undo command
var command = new ModifyCommand
{
    Position = position,
    OldValue = originalValue,
    NewValue = value
};

// Push to undo stack
_undoManager.PushEdit(command);
```

#### Step 5: Update UI

```csharp
// ViewModel refreshes visible lines
private void RefreshVisibleLines()
{
    var lines = GenerateVisibleLines(_firstVisibleLine, _visibleLineCount);
    _hexViewport.UpdateVisibleLines(lines);
}
```

**Result**: Byte appears modified (red color) in hex view.

---

## ➕ Insert Byte Sequence (Insert Mode)

### Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant EM as EditsManager
    participant PM as PositionMapper
    participant UR as UndoRedoManager

    User->>HE: Type 'FF' at position 0x100 (Insert mode)
    HE->>VM: InsertByte(0x100, 0xFF)

    VM->>BP: InsertByte(0x100, 0xFF)
    activate BP

    BP->>BP: Validate position
    alt Position > Length
        BP->>VM: ArgumentOutOfRangeException
        VM->>HE: Error
        HE->>User: Show error
    else Position valid
        BP->>EM: AddInsertion(0x100, 0xFF)
        activate EM

        EM->>EM: Get or create stack at 0x100
        alt First insertion at position
            EM->>EM: Create new Stack<byte>()
            EM->>EM: _insertions[0x100] = stack
        end

        EM->>EM: Push value to stack (LIFO)
        EM->>EM: _insertions[0x100].Push(0xFF)
        EM->>EM: Increment insertion count

        EM->>PM: OnInsertionAdded(0x100, 1)
        activate PM
        PM->>PM: Find or create segment
        PM->>PM: Increment inserted count
        PM->>PM: Recalculate segments after 0x100

        loop For each segment after 0x100
            PM->>PM: Adjust virtual offsets
            PM->>PM: Adjust physical mappings
        end

        PM-->>EM: Segments updated
        deactivate PM

        EM-->>BP: Insertion added
        deactivate EM

        BP->>BP: Update virtual length
        Note over BP: VirtualLength++

        BP->>UR: PushEdit(InsertCommand)
        activate UR
        UR->>UR: Create command (0x100, value=0xFF)
        UR->>UR: Push to undo stack
        UR->>UR: Clear redo stack
        UR-->>BP: Edit recorded
        deactivate UR

        BP->>BP: RaiseLengthChanged()
        BP->>BP: RaiseDataChanged()

        BP-->>VM: Insertion complete
        deactivate BP

        VM->>VM: Update cursor position
        Note over VM: Cursor moves to next position

        VM->>VM: RefreshVisibleLines()
        VM-->>HE: Lines updated
        HE->>HE: InvalidateVisual()
        HE->>User: Display updated view
    end
```

### LIFO Insertion Example

```
Original:  [41 42 43 44 45] at 0x100-0x104

User types 'FF' at position 0x100:
Insertions[0x100] = Stack [FF]
Virtual:   [FF 41 42 43 44 45]
Positions:  ↑100  ↑101 ↑102

User types 'AA' at position 0x100 again:
Insertions[0x100] = Stack [AA, FF]  (LIFO)
Virtual:   [AA FF 41 42 43 44 45]
Positions:  ↑100 ↑101 ↑102 ↑103

User types 'BB' at position 0x100 again:
Insertions[0x100] = Stack [BB, AA, FF]  (LIFO)
Virtual:   [BB AA FF 41 42 43 44 45]
Positions:  ↑100 ↑101 ↑102 ↑103 ↑104
```

### Code Example

```csharp
public void InsertByte(long virtualPosition, byte value)
{
    // Validate
    if (virtualPosition < 0 || virtualPosition > Length)
        throw new ArgumentOutOfRangeException(nameof(virtualPosition));

    // Add insertion
    _editsManager.AddInsertion(virtualPosition, value);

    // Update mapper
    _positionMapper.OnInsertionAdded(virtualPosition, 1);

    // Update length
    _virtualLength++;

    // Record undo
    var command = new InsertCommand(virtualPosition, value);
    _undoManager.PushEdit(command);

    // Notify
    RaiseLengthChanged();
    RaiseDataChanged();
}
```

---

## ➖ Delete Bytes Sequence

### Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant EM as EditsManager
    participant PM as PositionMapper
    participant UR as UndoRedoManager
    participant FP as FileProvider

    User->>HE: Select bytes 0x100-0x104 and press Delete
    HE->>VM: DeleteBytes(0x100, 5)

    VM->>BP: DeleteBytes(0x100, 5)
    activate BP

    BP->>BP: Validate range
    alt Invalid range
        BP->>VM: ArgumentException
        VM->>HE: Error
        HE->>User: Show error
    else Valid range
        BP->>BP: Convert virtual to physical
        Note over BP: Check if deleting insertions

        BP->>EM: Check insertion range
        EM-->>BP: No insertions in range

        BP->>PM: VirtualToPhysical(0x100)
        PM-->>BP: Physical position 0x95

        BP->>FP: ReadBytes(0x95, 5)
        activate FP
        FP-->>BP: Deleted bytes [41 42 43 44 45]
        deactivate FP

        BP->>EM: AddDeletion(0x95, 5)
        activate EM

        EM->>EM: Check for overlapping deletions
        alt Overlaps existing deletion
            EM->>BP: InvalidOperationException
            BP->>VM: Error
            VM->>HE: Error
            HE->>User: Show error
        else No overlap
            EM->>EM: Store deletion: 0x95 → 5
            EM->>EM: Increment deletion count

            EM->>PM: OnDeletionAdded(0x95, 5)
            activate PM
            PM->>PM: Find or create segment
            PM->>PM: Add deletion range
            PM->>PM: Recalculate segments

            loop For each segment after 0x95
                PM->>PM: Adjust virtual offsets
                PM->>PM: Collapse deleted range
            end

            PM-->>EM: Segments updated
            deactivate PM

            EM-->>BP: Deletion added
        end
        deactivate EM

        BP->>BP: Update virtual length
        Note over BP: VirtualLength -= 5

        BP->>UR: PushEdit(DeleteCommand)
        activate UR
        UR->>UR: Create command (0x95, deletedBytes=[41 42 43 44 45])
        UR->>UR: Push to undo stack
        UR->>UR: Clear redo stack
        UR-->>BP: Edit recorded
        deactivate UR

        BP->>BP: RaiseLengthChanged()
        BP->>BP: RaiseDataChanged()

        BP-->>VM: Deletion complete
        deactivate BP

        VM->>VM: Update selection
        Note over VM: Clear selection

        VM->>VM: RefreshVisibleLines()
        VM-->>HE: Lines updated
        HE->>HE: InvalidateVisual()
        HE->>User: Display updated view
    end
```

### Deletion with Insertions

```
Original file: [41 42 43 44 45 46 47 48]  (positions 0-7)

Insert 3 bytes at position 2:
Virtual: [41 42 AA BB CC 43 44 45 46 47 48]
          ↑0  ↑1  ↑2  ↑3  ↑4  ↑5  ↑6  ↑7  ↑8  ↑9  ↑10

Delete positions 3-5 (virtual):
- Position 3: BB (insertion) → Remove from insertion stack
- Position 4: CC (insertion) → Remove from insertion stack
- Position 5: 43 (physical position 2) → Mark as deleted

Result:
Virtual: [41 42 AA 44 45 46 47 48]
          ↑0  ↑1  ↑2  ↑3  ↑4  ↑5  ↑6  ↑7

Physical deletions: Position 2 (byte 0x43)
Insertion updates: Stack[AA] only (BB and CC removed)
```

---

## 📦 Batch Operations

### Batch Sequence Diagram

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant EM as EditsManager
    participant UR as UndoRedoManager

    User->>HE: Fill range with 0xFF
    HE->>VM: FillBytes(0x100, 1000, 0xFF)

    VM->>BP: BeginBatch()
    activate BP
    BP->>UR: BeginBatch()
    activate UR
    UR->>UR: Create BatchCommand
    UR-->>BP: Batch started
    deactivate UR
    BP->>BP: Suspend events
    BP-->>VM: Batch mode active
    deactivate BP

    loop For each position (1000 iterations)
        VM->>BP: ModifyByte(pos, 0xFF)
        activate BP
        BP->>EM: AddModification(pos, 0xFF)
        EM-->>BP: Added
        BP->>UR: PushEdit(ModifyCommand)
        activate UR
        UR->>UR: Add to BatchCommand
        Note over UR: No individual undo entries
        UR-->>BP: Recorded
        deactivate UR
        BP-->>VM: Modified (no event)
        deactivate BP
    end

    VM->>BP: EndBatch()
    activate BP
    BP->>UR: EndBatch()
    activate UR
    UR->>UR: Push BatchCommand to undo stack
    UR-->>BP: Batch finalized
    deactivate UR
    BP->>BP: Resume events
    BP->>BP: RaiseDataChanged() (once)
    BP-->>VM: Batch complete
    deactivate BP

    VM->>VM: RefreshVisibleLines() (once)
    VM-->>HE: Update complete
    HE->>User: Display updated view
```

### Batch Performance

**Without Batch**:
- 1000 edits = 1000 events = 1000 UI updates = **slow**

**With Batch**:
- 1000 edits = 1 event = 1 UI update = **3x faster**

### Code Example

```csharp
// Without batch: slow
for (int i = 0; i < 10000; i++)
{
    hexEditor.ModifyByte(i, 0xFF);  // Triggers event each time
}
// Total time: ~3000ms

// With batch: fast
hexEditor.BeginBatch();
try
{
    for (int i = 0; i < 10000; i++)
    {
        hexEditor.ModifyByte(i, 0xFF);  // No event
    }
}
finally
{
    hexEditor.EndBatch();  // Single event
}
// Total time: ~1000ms (3x faster)
```

---

## ↩️ Undo/Redo Flow

### Undo Sequence

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant UR as UndoRedoManager
    participant EM as EditsManager

    User->>HE: Undo (Ctrl+Z)
    HE->>VM: Undo()

    VM->>BP: Undo()
    BP->>UR: Undo()
    activate UR

    UR->>UR: Check CanUndo
    alt No undo history
        UR->>BP: InvalidOperationException
        BP->>VM: Error
        VM->>HE: Error
        HE->>User: Beep
    else Has undo history
        UR->>UR: Pop from undo stack
        UR->>UR: Get command (ModifyCommand)

        UR->>EM: command.Undo(editsManager)
        activate EM

        alt Restore to original
            EM->>EM: RemoveModification(position)
        else Restore to previous modification
            EM->>EM: AddModification(position, oldValue)
        end

        EM-->>UR: Undo executed
        deactivate EM

        UR->>UR: Push command to redo stack
        UR-->>BP: Undo complete
        deactivate UR

        BP->>BP: RaiseDataChanged()
        BP-->>VM: Data restored

        VM->>VM: RefreshVisibleLines()
        VM-->>HE: Update complete
        HE->>User: Display previous state
    end
```

### Redo Sequence

```mermaid
sequenceDiagram
    actor User
    participant HE as HexEditor
    participant VM as ViewModel
    participant BP as ByteProvider
    participant UR as UndoRedoManager
    participant EM as EditsManager

    User->>HE: Redo (Ctrl+Y)
    HE->>VM: Redo()

    VM->>BP: Redo()
    BP->>UR: Redo()
    activate UR

    UR->>UR: Check CanRedo
    alt No redo history
        UR->>BP: InvalidOperationException
        BP->>VM: Error
        VM->>HE: Error
        HE->>User: Beep
    else Has redo history
        UR->>UR: Pop from redo stack
        UR->>UR: Get command (ModifyCommand)

        UR->>EM: command.Execute(editsManager)
        activate EM
        EM->>EM: AddModification(position, newValue)
        EM-->>UR: Execute complete
        deactivate EM

        UR->>UR: Push command back to undo stack
        UR-->>BP: Redo complete
        deactivate UR

        BP->>BP: RaiseDataChanged()
        BP-->>VM: Data restored

        VM->>VM: RefreshVisibleLines()
        VM-->>HE: Update complete
        HE->>User: Display restored state
    end
```

---

## 🔗 See Also

- [File Operations](file-operations.md) - Open, close, save sequences
- [Save Operations](save-operations.md) - Smart save algorithm
- [Undo/Redo System](../core-systems/undo-redo-system.md) - History management details

---

**Last Updated**: 2026-02-19
**Version**: V2.0
