# Undo()

Reverse the last edit operation.

---

## 📋 Description

The `Undo()` method reverses the last edit operation, restoring the file to its previous state. WPF HexEditor V2 implements **unlimited undo** using the Command pattern, allowing you to undo any number of operations.

**Key characteristics**:
- ✅ **Unlimited undo** - no depth limit (configurable)
- 🔄 **Reverses all edit types** - modifications, insertions, deletions
- 📦 **Batch-aware** - single undo for BeginBatch/EndBatch block
- ⚡ **Instant execution** - O(1) complexity
- 🎯 **Moves to redo stack** - can be redone with Redo()

**Implementation**: Command pattern with two stacks (undo + redo).

---

## 📝 Signatures

```csharp
// Undo last operation
public void Undo()

// Check if undo available
public bool CanUndo { get; }

// Get undo stack depth
public int UndoDepth { get; }
```

**Since:** V1.0

---

## 🔄 Returns

| Method/Property | Return Type | Description |
|-----------------|-------------|-------------|
| `Undo()` | `void` | No return value |
| `CanUndo` | `bool` | True if undo is available |
| `UndoDepth` | `int` | Number of operations that can be undone |

---

## 🎯 Examples

### Example 1: Basic Undo

```csharp
using WpfHexaEditor;

// Make an edit
hexEditor.ModifyByte(0xFF, 0x100);

// Undo it
if (hexEditor.CanUndo)
{
    hexEditor.Undo();
    Console.WriteLine("Edit undone");
}
```

---

### Example 2: Undo Button Implementation

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Update undo button state
        hexEditor.DataChanged += (s, e) => UpdateUndoRedoButtons();

        // Undo button click
        undoButton.Click += (s, e) =>
        {
            if (hexEditor.CanUndo)
            {
                hexEditor.Undo();
            }
        };

        // Keyboard shortcut (Ctrl+Z)
        this.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (hexEditor.CanUndo)
                {
                    hexEditor.Undo();
                }
                e.Handled = true;
            }
        };
    }

    private void UpdateUndoRedoButtons()
    {
        undoButton.IsEnabled = hexEditor.CanUndo;
        redoButton.IsEnabled = hexEditor.CanRedo;

        // Update tooltips with stack depth
        undoButton.ToolTip = $"Undo ({hexEditor.UndoDepth} operations)";
        redoButton.ToolTip = $"Redo ({hexEditor.RedoDepth} operations)";
    }
}
```

---

### Example 3: Undo with Confirmation

```csharp
private void UndoWithConfirmation()
{
    if (!hexEditor.CanUndo)
    {
        MessageBox.Show("Nothing to undo", "Info");
        return;
    }

    // Show what will be undone
    string undoDescription = GetLastOperationDescription();

    var result = MessageBox.Show(
        $"Undo operation: {undoDescription}?",
        "Confirm Undo",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (result == MessageBoxResult.Yes)
    {
        hexEditor.Undo();
        statusLabel.Text = "Operation undone";
    }
}

private string GetLastOperationDescription()
{
    // Get description of last operation
    // (Implementation depends on custom tracking)
    return "Last modification";
}
```

---

### Example 4: Undo Multiple Operations

```csharp
// Undo last N operations
private void UndoMultiple(int count)
{
    int undone = 0;

    for (int i = 0; i < count && hexEditor.CanUndo; i++)
    {
        hexEditor.Undo();
        undone++;
    }

    MessageBox.Show(
        $"Undone {undone} operations",
        "Undo Complete");
}

// Usage: Undo last 5 operations
UndoMultiple(5);
```

---

### Example 5: Undo Until Specific State

```csharp
// Undo until file has no changes
private void UndoAllChanges()
{
    if (!hexEditor.HasChanges)
    {
        MessageBox.Show("No changes to undo", "Info");
        return;
    }

    int undoneCount = 0;

    while (hexEditor.HasChanges && hexEditor.CanUndo)
    {
        hexEditor.Undo();
        undoneCount++;
    }

    MessageBox.Show(
        $"Undone all {undoneCount} changes\n" +
        $"File restored to original state",
        "Undo Complete");
}
```

---

### Example 6: Undo with Status Display

```csharp
private void UndoWithStatus()
{
    if (!hexEditor.CanUndo)
    {
        statusLabel.Text = "Nothing to undo";
        return;
    }

    // Get state before undo
    int undoDepthBefore = hexEditor.UndoDepth;
    int redoDepthBefore = hexEditor.RedoDepth;

    // Perform undo
    hexEditor.Undo();

    // Get state after undo
    int undoDepthAfter = hexEditor.UndoDepth;
    int redoDepthAfter = hexEditor.RedoDepth;

    // Display status
    statusLabel.Text = $"Operation undone (undo: {undoDepthAfter}, redo: {redoDepthAfter})";

    Console.WriteLine("Undo Status:");
    Console.WriteLine($"  Undo depth: {undoDepthBefore} → {undoDepthAfter}");
    Console.WriteLine($"  Redo depth: {redoDepthBefore} → {redoDepthAfter}");
}
```

---

### Example 7: Undo with Transaction Log

```csharp
// Track undo operations for audit trail
public class UndoLogger
{
    private List<UndoLogEntry> _log = new();
    private HexEditor _hexEditor;

    public UndoLogger(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void LoggedUndo()
    {
        if (!_hexEditor.CanUndo)
        {
            Console.WriteLine("Nothing to undo");
            return;
        }

        // Log before undo
        var entry = new UndoLogEntry
        {
            Timestamp = DateTime.Now,
            UndoDepthBefore = _hexEditor.UndoDepth,
            RedoDepthBefore = _hexEditor.RedoDepth
        };

        // Perform undo
        _hexEditor.Undo();

        // Log after undo
        entry.UndoDepthAfter = _hexEditor.UndoDepth;
        entry.RedoDepthAfter = _hexEditor.RedoDepth;

        _log.Add(entry);

        Console.WriteLine($"[{entry.Timestamp:HH:mm:ss}] Undo performed");
    }

    public void PrintLog()
    {
        Console.WriteLine($"Undo Log ({_log.Count} entries):");
        foreach (var entry in _log.TakeLast(10))
        {
            Console.WriteLine($"  [{entry.Timestamp:HH:mm:ss}] " +
                            $"Undo: {entry.UndoDepthBefore} → {entry.UndoDepthAfter}, " +
                            $"Redo: {entry.RedoDepthBefore} → {entry.RedoDepthAfter}");
        }
    }
}

public class UndoLogEntry
{
    public DateTime Timestamp { get; set; }
    public int UndoDepthBefore { get; set; }
    public int UndoDepthAfter { get; set; }
    public int RedoDepthBefore { get; set; }
    public int RedoDepthAfter { get; set; }
}

// Usage
var logger = new UndoLogger(hexEditor);

// Perform edits
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);

// Undo with logging
logger.LoggedUndo();
logger.LoggedUndo();

// Print log
logger.PrintLog();
```

---

### Example 8: Conditional Undo (Undo Until Condition)

```csharp
// Undo until specific condition met
public class ConditionalUndo
{
    private HexEditor _hexEditor;

    public ConditionalUndo(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Undo until file size reaches target
    public int UndoUntilSize(long targetSize)
    {
        int undoneCount = 0;

        while (_hexEditor.CanUndo && _hexEditor.Length != targetSize)
        {
            _hexEditor.Undo();
            undoneCount++;

            // Safety limit
            if (undoneCount > 10000)
            {
                Console.WriteLine("Safety limit reached (10,000 undos)");
                break;
            }
        }

        return undoneCount;
    }

    // Undo until no insertions remain
    public int UndoUntilNoInsertions()
    {
        int undoneCount = 0;

        while (_hexEditor.CanUndo && _hexEditor.InsertionCount > 0)
        {
            _hexEditor.Undo();
            undoneCount++;
        }

        return undoneCount;
    }

    // Undo until checkpoint
    public int UndoToCheckpoint(long checkpointLength)
    {
        int undoneCount = 0;

        while (_hexEditor.CanUndo && _hexEditor.Length != checkpointLength)
        {
            _hexEditor.Undo();
            undoneCount++;
        }

        return undoneCount;
    }
}

// Usage
var conditionalUndo = new ConditionalUndo(hexEditor);

// Save current size as checkpoint
long checkpoint = hexEditor.Length;

// Make edits...
hexEditor.InsertBytes(0, new byte[] { 0x01, 0x02, 0x03 });
hexEditor.ModifyByte(0xFF, 0x100);

// Undo to checkpoint
int undone = conditionalUndo.UndoToCheckpoint(checkpoint);
Console.WriteLine($"Undone {undone} operations to restore checkpoint size");
```

---

## 💡 Use Cases

### 1. Safe Experimentation

```csharp
// Try modifications without committing
public class ExperimentalEditor
{
    private HexEditor _hexEditor;
    private int _experimentStartDepth;

    public ExperimentalEditor(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void BeginExperiment()
    {
        _experimentStartDepth = _hexEditor.UndoDepth;
        Console.WriteLine("Experiment started - changes can be rolled back");
    }

    public void CommitExperiment()
    {
        Console.WriteLine("Experiment committed");
        // Changes kept in undo history
    }

    public void RollbackExperiment()
    {
        int currentDepth = _hexEditor.UndoDepth;
        int undosNeeded = currentDepth - _experimentStartDepth;

        for (int i = 0; i < undosNeeded && _hexEditor.CanUndo; i++)
        {
            _hexEditor.Undo();
        }

        Console.WriteLine($"Experiment rolled back ({undosNeeded} operations undone)");
    }
}

// Usage: Try patch, rollback if it doesn't work
var experiment = new ExperimentalEditor(hexEditor);

experiment.BeginExperiment();

// Try modifications
hexEditor.ModifyByte(0x90, 0x1000);
hexEditor.ModifyByte(0x90, 0x1001);

// Test if patch works
if (!TestPatch())
{
    experiment.RollbackExperiment();  // Undo all experimental changes
}
else
{
    experiment.CommitExperiment();  // Keep changes
}
```

---

### 2. Undo After Accidental Bulk Edit

```csharp
// Recover from accidental mass modification
private void RecoverFromBulkEdit()
{
    // User accidentally filled entire file with 0xFF
    MessageBox.Show(
        "Detected large bulk edit. Undo?",
        "Accidental Edit?",
        MessageBoxButton.YesNo);

    // If yes, undo the bulk operation
    if (hexEditor.CanUndo)
    {
        hexEditor.Undo();  // Single undo reverses entire batch!
        MessageBox.Show("Bulk edit undone", "Recovered");
    }
}
```

---

### 3. Step-by-Step Patch Application with Rollback

```csharp
// Apply patch with ability to rollback each step
public class PatchApplier
{
    private HexEditor _hexEditor;
    private List<PatchStep> _steps;

    public PatchApplier(HexEditor hexEditor, List<PatchStep> steps)
    {
        _hexEditor = hexEditor;
        _steps = steps;
    }

    public bool ApplyPatchWithRollback()
    {
        int stepsApplied = 0;

        try
        {
            foreach (var step in _steps)
            {
                Console.WriteLine($"Applying step {stepsApplied + 1}/{_steps.Count}");

                // Apply patch step
                _hexEditor.ModifyBytes(step.Position, step.NewData);
                stepsApplied++;

                // Verify patch
                if (!VerifyPatchStep(step))
                {
                    throw new Exception($"Step {stepsApplied} verification failed");
                }
            }

            Console.WriteLine("All patch steps applied successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Patch failed at step {stepsApplied}: {ex.Message}");

            // Rollback applied steps
            for (int i = 0; i < stepsApplied && _hexEditor.CanUndo; i++)
            {
                _hexEditor.Undo();
            }

            MessageBox.Show(
                $"Patch failed and rolled back\n" +
                $"Steps applied: {stepsApplied}/{_steps.Count}",
                "Patch Failed");

            return false;
        }
    }

    private bool VerifyPatchStep(PatchStep step)
    {
        byte[] actual = _hexEditor.GetBytes(step.Position, step.NewData.Length);
        return actual.SequenceEqual(step.NewData);
    }
}

public class PatchStep
{
    public long Position { get; set; }
    public byte[] NewData { get; set; }
}
```

---

### 4. Undo History Browser

```csharp
// Browse undo history with preview
public class UndoHistoryBrowser
{
    private HexEditor _hexEditor;
    private List<HistorySnapshot> _snapshots = new();

    public UndoHistoryBrowser(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
        CaptureSnapshot();  // Initial state

        // Capture after each edit
        hexEditor.DataChanged += (s, e) => CaptureSnapshot();
    }

    private void CaptureSnapshot()
    {
        _snapshots.Add(new HistorySnapshot
        {
            Timestamp = DateTime.Now,
            Length = _hexEditor.Length,
            UndoDepth = _hexEditor.UndoDepth,
            HasChanges = _hexEditor.HasChanges
        });
    }

    public void ShowHistory()
    {
        Console.WriteLine($"History ({_snapshots.Count} snapshots):");

        for (int i = _snapshots.Count - 1; i >= 0; i--)
        {
            var snapshot = _snapshots[i];
            string marker = (i == _snapshots.Count - 1) ? "→" : " ";

            Console.WriteLine($"{marker} [{snapshot.Timestamp:HH:mm:ss}] " +
                            $"Length: {snapshot.Length}, " +
                            $"Undo: {snapshot.UndoDepth}, " +
                            $"Modified: {snapshot.HasChanges}");
        }
    }

    public void UndoToSnapshot(int snapshotIndex)
    {
        int currentIndex = _snapshots.Count - 1;
        int undosNeeded = currentIndex - snapshotIndex;

        for (int i = 0; i < undosNeeded && _hexEditor.CanUndo; i++)
        {
            _hexEditor.Undo();
        }

        Console.WriteLine($"Undone to snapshot {snapshotIndex}");
    }
}

public class HistorySnapshot
{
    public DateTime Timestamp { get; set; }
    public long Length { get; set; }
    public int UndoDepth { get; set; }
    public bool HasChanges { get; set; }
}
```

---

## ⚠️ Important Notes

### Undo Stack is Unlimited

- **Default**: No depth limit (unlimited undo)
- Each operation adds to undo stack
- Clear with `ClearUndoHistory()` to free memory

### Batch Operations Count as One Undo

```csharp
hexEditor.BeginBatch();
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);
hexEditor.ModifyByte(0xBB, 0x300);
hexEditor.EndBatch();

// Single undo reverses ALL 3 modifications
hexEditor.Undo();
```

### Undo After Save

- Undo works **after saving** (operations remain in history)
- To clear history after save: `ClearUndoHistory()`

```csharp
hexEditor.Save();
// Can still undo modifications (even though file saved)

hexEditor.Undo();  // Works!

// To clear history:
hexEditor.Save();
hexEditor.ClearUndoHistory();  // Now undo stack empty
```

### Undo Clears Redo Stack

- Making new edit after Undo clears Redo stack
- This prevents branching timelines

```csharp
// Make edit
hexEditor.ModifyByte(0xFF, 0x100);

// Undo
hexEditor.Undo();

// Redo available
Console.WriteLine($"Can redo: {hexEditor.CanRedo}");  // True

// Make new edit (clears redo)
hexEditor.ModifyByte(0xAA, 0x200);

Console.WriteLine($"Can redo: {hexEditor.CanRedo}");  // False (redo cleared)
```

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread

---

## 🔗 See Also

- **[Redo()](redo.md)** - Reverse an undo operation
- **[ClearUndoHistory()](clearundohistory.md)** - Clear all undo/redo history
- **[BeginBatch() / EndBatch()](beginbatch.md)** - Group operations into single undo
- **[ModifyByte()](../byte-operations/modifybyte.md)** - Modify bytes (undoable)
- **[InsertByte()](../byte-operations/insertbyte.md)** - Insert bytes (undoable)

---

**Last Updated**: 2026-02-19
**Version**: V2.0 (Command Pattern with Unlimited Undo)
