# ClearUndoHistory()

Clear all undo and redo history, freeing memory.

---

## 📋 Description

The `ClearUndoHistory()` method removes all undo and redo operations from memory. This is useful for **freeing memory** after saving or when you want to prevent users from undoing past a certain point.

**Key characteristics**:
- 🗑️ **Clears both stacks** - undo and redo
- 💾 **Frees memory** - releases command objects
- ❌ **Irreversible** - cannot recover cleared history
- ⚡ **Instant** - O(1) operation
- ✅ **File unchanged** - only affects history tracking

**Warning**: After calling this method, users **cannot undo or redo** any previous operations!

---

## 📝 Signatures

```csharp
// Clear all undo/redo history
public void ClearUndoHistory()
```

**Since:** V1.0

---

## 🔄 Returns

| Return Type | Description |
|-------------|-------------|
| `void` | No return value |

---

## 🎯 Examples

### Example 1: Clear After Save

```csharp
using WpfHexaEditor;

// Make edits
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);

// Save file
hexEditor.Save();

// Clear history (free memory, prevent undo past save point)
hexEditor.ClearUndoHistory();

Console.WriteLine($"Can undo: {hexEditor.CanUndo}");  // False
Console.WriteLine($"Can redo: {hexEditor.CanRedo}");  // False
```

---

### Example 2: Clear with Confirmation

```csharp
private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
{
    if (hexEditor.UndoDepth == 0 && hexEditor.RedoDepth == 0)
    {
        MessageBox.Show("History is already empty", "Info");
        return;
    }

    var result = MessageBox.Show(
        $"Clear undo/redo history?\n\n" +
        $"This will remove:\n" +
        $"  • {hexEditor.UndoDepth} undo operations\n" +
        $"  • {hexEditor.RedoDepth} redo operations\n\n" +
        $"This action cannot be undone!",
        "Clear History",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning);

    if (result == MessageBoxResult.Yes)
    {
        hexEditor.ClearUndoHistory();
        MessageBox.Show("History cleared", "Success");
        UpdateUndoRedoButtons();
    }
}
```

---

### Example 3: Auto-Clear After Save

```csharp
// Automatically clear history after successful save
private async Task SaveAndClearHistoryAsync()
{
    if (!hexEditor.HasChanges)
    {
        MessageBox.Show("No changes to save", "Info");
        return;
    }

    try
    {
        // Save file
        var progress = new Progress<double>(p => progressBar.Value = p);
        await hexEditor.SaveAsync(progress);

        // Clear history to free memory
        hexEditor.ClearUndoHistory();

        MessageBox.Show(
            "File saved successfully\n" +
            "Undo history cleared",
            "Success");

        UpdateStatus("Saved and history cleared");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error saving: {ex.Message}", "Error");
    }
}
```

---

### Example 4: Clear at Checkpoints

```csharp
// Clear history at strategic checkpoints
public class CheckpointManager
{
    private HexEditor _hexEditor;
    private List<Checkpoint> _checkpoints = new();

    public CheckpointManager(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void CreateCheckpoint(string name)
    {
        // Save current state
        var checkpoint = new Checkpoint
        {
            Name = name,
            Timestamp = DateTime.Now,
            FileLength = _hexEditor.Length,
            HasChanges = _hexEditor.HasChanges
        };

        _checkpoints.Add(checkpoint);

        // Save file
        if (_hexEditor.HasChanges)
        {
            _hexEditor.Save();
        }

        // Clear history at checkpoint
        _hexEditor.ClearUndoHistory();

        Console.WriteLine($"Checkpoint '{name}' created (history cleared)");
    }

    public void ListCheckpoints()
    {
        Console.WriteLine($"Checkpoints ({_checkpoints.Count}):");
        foreach (var cp in _checkpoints)
        {
            Console.WriteLine($"  [{cp.Timestamp:HH:mm:ss}] {cp.Name} " +
                            $"(Length: {cp.FileLength})");
        }
    }
}

// Usage: Clear history at milestones
var checkpoints = new CheckpointManager(hexEditor);

// Work on file...
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.Save();

// Create checkpoint (clears history)
checkpoints.CreateCheckpoint("After header modification");

// Continue working...
hexEditor.ModifyByte(0xAA, 0x200);

// Another checkpoint
checkpoints.CreateCheckpoint("After data modification");
```

---

### Example 5: Memory Management Strategy

```csharp
// Manage memory by clearing history periodically
public class HistoryMemoryManager
{
    private HexEditor _hexEditor;
    private const int MAX_UNDO_OPERATIONS = 1000;
    private int _operationsSinceLastClear = 0;

    public HistoryMemoryManager(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;

        // Monitor operations
        hexEditor.DataChanged += (s, e) => OnOperationPerformed();
    }

    private void OnOperationPerformed()
    {
        _operationsSinceLastClear++;

        // Clear if threshold reached
        if (_operationsSinceLastClear >= MAX_UNDO_OPERATIONS)
        {
            Console.WriteLine($"⚠️ Undo limit reached ({MAX_UNDO_OPERATIONS} operations)");

            // Auto-save
            if (_hexEditor.HasChanges)
            {
                _hexEditor.Save();
                Console.WriteLine("Auto-saved file");
            }

            // Clear history
            _hexEditor.ClearUndoHistory();
            _operationsSinceLastClear = 0;

            Console.WriteLine("History cleared to free memory");
        }
    }

    public void ForceCleanup()
    {
        if (_hexEditor.UndoDepth > 0 || _hexEditor.RedoDepth > 0)
        {
            _hexEditor.ClearUndoHistory();
            _operationsSinceLastClear = 0;
            Console.WriteLine("Manual history cleanup performed");
        }
    }
}

// Usage
var memoryManager = new HistoryMemoryManager(hexEditor);

// Make many edits (automatically clears at 1000 operations)
for (int i = 0; i < 5000; i++)
{
    hexEditor.ModifyByte(0xFF, i);
}

// Manual cleanup
memoryManager.ForceCleanup();
```

---

### Example 6: Clear with Undo Prevention

```csharp
// Prevent undo past critical operations
public class CriticalOperationProtector
{
    private HexEditor _hexEditor;

    public CriticalOperationProtector(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void PerformCriticalOperation(Action operation, string description)
    {
        Console.WriteLine($"Performing critical operation: {description}");

        // Warn user
        var result = MessageBox.Show(
            $"About to perform critical operation:\n{description}\n\n" +
            $"Undo history will be cleared after this operation.\n" +
            $"Continue?",
            "Critical Operation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            Console.WriteLine("Critical operation cancelled");
            return;
        }

        try
        {
            // Perform operation
            operation();

            // Save
            _hexEditor.Save();

            // Clear history (prevent undo of critical operation)
            _hexEditor.ClearUndoHistory();

            Console.WriteLine($"Critical operation complete (history cleared)");

            MessageBox.Show(
                $"Critical operation completed:\n{description}\n\n" +
                "Undo history has been cleared.",
                "Success");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Critical operation failed: {ex.Message}",
                "Error");
        }
    }
}

// Usage: Encrypt file (prevent undo)
var protector = new CriticalOperationProtector(hexEditor);

protector.PerformCriticalOperation(() =>
{
    // Encrypt entire file
    for (long i = 0; i < hexEditor.Length; i++)
    {
        byte b = hexEditor.GetByte(i);
        byte encrypted = (byte)(b ^ 0xFF);  // Simple XOR encryption
        hexEditor.ModifyByte(encrypted, i);
    }
}, "File Encryption");
```

---

### Example 7: Selective History Clear

```csharp
// Clear only specific types of operations (V2 extension)
public class SelectiveHistoryClearer
{
    private HexEditor _hexEditor;

    public SelectiveHistoryClearer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void ClearOldHistory(TimeSpan age)
    {
        // Note: Not natively supported
        // This is a conceptual example

        Console.WriteLine($"Clearing history older than {age.TotalMinutes} minutes");

        // In practice, you would need custom tracking
        // For now, just clear all if any operation is old enough

        _hexEditor.ClearUndoHistory();
    }

    public void ClearRedoOnly()
    {
        // Clear only redo stack by making dummy edit and undoing
        // (Workaround since there's no separate clear method)

        if (_hexEditor.RedoDepth > 0)
        {
            Console.WriteLine("Clearing redo stack only...");

            // Save redo depth
            int redoCount = _hexEditor.RedoDepth;

            // Make dummy edit (clears redo)
            long pos = 0;
            byte oldValue = _hexEditor.GetByte(pos);
            hexEditor.ModifyByte(oldValue, pos);  // No-op modification

            // Undo dummy edit
            _hexEditor.Undo();

            Console.WriteLine($"Cleared {redoCount} redo operations");
        }
    }
}
```

---

### Example 8: History Clear with Statistics

```csharp
// Track and report history statistics before clearing
public class HistoryStatistics
{
    private HexEditor _hexEditor;

    public HistoryStatistics(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void ClearWithStatistics()
    {
        // Gather statistics before clearing
        var stats = GatherStatistics();

        // Display statistics
        Console.WriteLine("History Statistics:");
        Console.WriteLine($"  Undo operations: {stats.UndoDepth}");
        Console.WriteLine($"  Redo operations: {stats.RedoDepth}");
        Console.WriteLine($"  Total operations: {stats.TotalOperations}");
        Console.WriteLine($"  Estimated memory: ~{stats.EstimatedMemoryKB} KB");

        // Clear
        _hexEditor.ClearUndoHistory();

        Console.WriteLine("History cleared");
    }

    private HistoryStats GatherStatistics()
    {
        int undoDepth = _hexEditor.UndoDepth;
        int redoDepth = _hexEditor.RedoDepth;
        int total = undoDepth + redoDepth;

        // Estimate memory (rough approximation)
        // Each command ~100 bytes (varies by operation type)
        int estimatedMemoryKB = (total * 100) / 1024;

        return new HistoryStats
        {
            UndoDepth = undoDepth,
            RedoDepth = redoDepth,
            TotalOperations = total,
            EstimatedMemoryKB = estimatedMemoryKB
        };
    }
}

public class HistoryStats
{
    public int UndoDepth { get; set; }
    public int RedoDepth { get; set; }
    public int TotalOperations { get; set; }
    public int EstimatedMemoryKB { get; set; }
}

// Usage
var stats = new HistoryStatistics(hexEditor);

// Make many edits
for (int i = 0; i < 10000; i++)
{
    hexEditor.ModifyByte(0xFF, i % 1000);
}

// Clear with statistics
stats.ClearWithStatistics();
```

---

## 💡 Use Cases

### 1. Post-Save Cleanup

```csharp
// Clear history after saving to free memory
private void SaveWithCleanup()
{
    if (!hexEditor.HasChanges)
    {
        MessageBox.Show("No changes to save");
        return;
    }

    try
    {
        // Save
        hexEditor.Save();

        // Clear history (user can't undo saved changes anyway)
        hexEditor.ClearUndoHistory();

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();

        MessageBox.Show(
            "File saved and memory freed",
            "Success");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}", "Error");
    }
}
```

---

### 2. Session Management

```csharp
// Clear history when starting new editing session
public class SessionManager
{
    private HexEditor _hexEditor;
    private DateTime _sessionStart;

    public SessionManager(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void StartNewSession()
    {
        // Warn if unsaved changes
        if (_hexEditor.HasChanges)
        {
            var result = MessageBox.Show(
                "Save current changes before starting new session?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel);

            if (result == MessageBoxResult.Cancel)
                return;

            if (result == MessageBoxResult.Yes)
            {
                _hexEditor.Save();
            }
        }

        // Clear history
        _hexEditor.ClearUndoHistory();

        // Reset session
        _sessionStart = DateTime.Now;

        Console.WriteLine($"New session started at {_sessionStart:HH:mm:ss}");
        Console.WriteLine("History cleared");
    }

    public void EndSession()
    {
        TimeSpan duration = DateTime.Now - _sessionStart;

        Console.WriteLine($"Session ended (duration: {duration.TotalMinutes:F1} minutes)");

        // Save and clear
        if (_hexEditor.HasChanges)
        {
            _hexEditor.Save();
        }

        _hexEditor.ClearUndoHistory();
    }
}
```

---

### 3. Batch Processing with Memory Management

```csharp
// Process multiple files with history cleanup between files
public class BatchProcessor
{
    private HexEditor _hexEditor;

    public BatchProcessor(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public async Task ProcessFilesAsync(List<string> files)
    {
        int processed = 0;

        foreach (var file in files)
        {
            Console.WriteLine($"Processing {file}...");

            // Open file
            _hexEditor.FileName = file;

            // Process file
            await ProcessFileAsync();

            // Save
            _hexEditor.Save();

            // Clear history (free memory for next file)
            _hexEditor.ClearUndoHistory();

            // Close file
            _hexEditor.Close();

            // Force GC every 10 files
            if (++processed % 10 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Console.WriteLine($"Processed {processed}/{files.Count} files (GC performed)");
            }
        }

        Console.WriteLine($"Batch complete: {processed} files processed");
    }

    private async Task ProcessFileAsync()
    {
        // Batch processing logic
        _hexEditor.BeginBatch();

        for (long i = 0; i < _hexEditor.Length; i++)
        {
            byte b = _hexEditor.GetByte(i);
            // Process byte...
        }

        _hexEditor.EndBatch();

        await Task.CompletedTask;
    }
}
```

---

### 4. Point of No Return

```csharp
// Create point of no return (prevent undo past this point)
public class PointOfNoReturn
{
    private HexEditor _hexEditor;

    public PointOfNoReturn(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void SetPointOfNoReturn(string reason)
    {
        Console.WriteLine($"=== POINT OF NO RETURN ===");
        Console.WriteLine($"Reason: {reason}");

        // Warn user
        var result = MessageBox.Show(
            $"This creates a point of no return.\n\n" +
            $"Reason: {reason}\n\n" +
            $"You will NOT be able to undo operations after this point.\n\n" +
            $"Continue?",
            "Point of No Return",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            Console.WriteLine("Point of no return cancelled");
            return;
        }

        // Save current state
        _hexEditor.Save();

        // Clear history (no undo past this point!)
        _hexEditor.ClearUndoHistory();

        Console.WriteLine("Point of no return set");
        Console.WriteLine("All previous operations can no longer be undone");
    }
}

// Usage: File encryption creates point of no return
var pointOfNoReturn = new PointOfNoReturn(hexEditor);

pointOfNoReturn.SetPointOfNoReturn("File Encryption - cannot undo encryption");
```

---

## ⚠️ Important Notes

### Irreversible Operation

**Warning**: ClearUndoHistory() is **irreversible**!

```csharp
// Make edits
hexEditor.ModifyByte(0xFF, 0x100);

// Clear history
hexEditor.ClearUndoHistory();

// Cannot undo anymore!
Console.WriteLine($"Can undo: {hexEditor.CanUndo}");  // False
```

---

### Clears Both Undo and Redo

```csharp
// Make edits
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);

// Undo one
hexEditor.Undo();

Console.WriteLine($"Undo: {hexEditor.UndoDepth}, Redo: {hexEditor.RedoDepth}");
// Output: "Undo: 1, Redo: 1"

// Clear (removes both!)
hexEditor.ClearUndoHistory();

Console.WriteLine($"Undo: {hexEditor.UndoDepth}, Redo: {hexEditor.RedoDepth}");
// Output: "Undo: 0, Redo: 0"
```

---

### File State Unchanged

- Clearing history does **NOT change file content**
- Only removes ability to undo/redo

```csharp
// Modify byte
hexEditor.ModifyByte(0xFF, 0x100);

// Clear history
hexEditor.ClearUndoHistory();

// Modification still exists!
byte value = hexEditor.GetByte(0x100);
Console.WriteLine($"Value: 0x{value:X2}");  // Still 0xFF

// Just can't undo it anymore
Console.WriteLine($"Can undo: {hexEditor.CanUndo}");  // False
```

---

### Memory Benefits

- Frees memory used by command objects
- Each undo entry ~50-200 bytes (varies by operation type)
- 10,000 operations ≈ 0.5-2 MB memory saved

---

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread

---

## 🔗 See Also

- **[Undo()](undo.md)** - Reverse last operation
- **[Redo()](redo.md)** - Re-apply undone operation
- **[Save()](../file-operations/save.md)** - Save file (often done before clearing)
- **[BeginBatch() / EndBatch()](beginbatch.md)** - Group operations

---

**Last Updated**: 2026-02-19
**Version**: 2.0
