# Redo()

Re-apply a previously undone operation.

---

## 📋 Description

The `Redo()` method re-applies an operation that was previously undone with `Undo()`. This allows users to step forward through their edit history after stepping backward.

**Key characteristics**:
- ✅ **Re-applies undone operations** in order
- 🔄 **Works with all edit types** - modifications, insertions, deletions
- 📦 **Batch-aware** - single redo for batched operations
- ⚡ **Instant execution** - O(1) complexity
- 🎯 **Moves back to undo stack** - can be undone again
- ❌ **Cleared on new edit** - making new edit clears redo stack

**Implementation**: Command pattern with redo stack.

---

## 📝 Signatures

```csharp
// Redo last undone operation
public void Redo()

// Check if redo available
public bool CanRedo { get; }

// Get redo stack depth
public int RedoDepth { get; }
```

**Since:** V1.0

---

## 🔄 Returns

| Method/Property | Return Type | Description |
|-----------------|-------------|-------------|
| `Redo()` | `void` | No return value |
| `CanRedo` | `bool` | True if redo is available |
| `RedoDepth` | `int` | Number of operations that can be redone |

---

## 🎯 Examples

### Example 1: Basic Redo

```csharp
using WpfHexaEditor;

// Make an edit
hexEditor.ModifyByte(0xFF, 0x100);

// Undo it
hexEditor.Undo();

// Redo it
if (hexEditor.CanRedo)
{
    hexEditor.Redo();
    Console.WriteLine("Edit re-applied");
}
```

---

### Example 2: Undo/Redo Button Implementation

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Update button states
        hexEditor.DataChanged += (s, e) => UpdateUndoRedoButtons();

        // Undo button (Ctrl+Z)
        undoButton.Click += (s, e) => PerformUndo();

        // Redo button (Ctrl+Y)
        redoButton.Click += (s, e) => PerformRedo();

        // Keyboard shortcuts
        this.KeyDown += Window_KeyDown;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Z: Undo
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PerformUndo();
            e.Handled = true;
        }
        // Ctrl+Y: Redo
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PerformRedo();
            e.Handled = true;
        }
        // Ctrl+Shift+Z: Redo (alternative)
        else if (e.Key == Key.Z &&
                 Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            PerformRedo();
            e.Handled = true;
        }
    }

    private void PerformUndo()
    {
        if (hexEditor.CanUndo)
        {
            hexEditor.Undo();
            statusLabel.Text = "Undo performed";
        }
        else
        {
            statusLabel.Text = "Nothing to undo";
        }
    }

    private void PerformRedo()
    {
        if (hexEditor.CanRedo)
        {
            hexEditor.Redo();
            statusLabel.Text = "Redo performed";
        }
        else
        {
            statusLabel.Text = "Nothing to redo";
        }
    }

    private void UpdateUndoRedoButtons()
    {
        undoButton.IsEnabled = hexEditor.CanUndo;
        redoButton.IsEnabled = hexEditor.CanRedo;

        undoButton.ToolTip = $"Undo ({hexEditor.UndoDepth} available)";
        redoButton.ToolTip = $"Redo ({hexEditor.RedoDepth} available)";
    }
}
```

---

### Example 3: Redo Multiple Operations

```csharp
// Redo last N operations
private void RedoMultiple(int count)
{
    int redone = 0;

    for (int i = 0; i < count && hexEditor.CanRedo; i++)
    {
        hexEditor.Redo();
        redone++;
    }

    MessageBox.Show(
        $"Redone {redone} operations",
        "Redo Complete");
}

// Usage: Redo last 5 operations
RedoMultiple(5);
```

---

### Example 4: Redo All Operations

```csharp
// Redo all available operations
private void RedoAll()
{
    if (!hexEditor.CanRedo)
    {
        MessageBox.Show("Nothing to redo", "Info");
        return;
    }

    int redoneCount = 0;

    while (hexEditor.CanRedo)
    {
        hexEditor.Redo();
        redoneCount++;
    }

    MessageBox.Show(
        $"Redone all {redoneCount} operations",
        "Redo Complete");
}
```

---

### Example 5: Undo/Redo Navigator

```csharp
// Navigate through edit history
public class EditHistoryNavigator
{
    private HexEditor _hexEditor;

    public EditHistoryNavigator(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void StepBackward()
    {
        if (_hexEditor.CanUndo)
        {
            _hexEditor.Undo();
            Console.WriteLine($"Stepped backward (Undo: {_hexEditor.UndoDepth}, Redo: {_hexEditor.RedoDepth})");
        }
        else
        {
            Console.WriteLine("Already at beginning of history");
        }
    }

    public void StepForward()
    {
        if (_hexEditor.CanRedo)
        {
            _hexEditor.Redo();
            Console.WriteLine($"Stepped forward (Undo: {_hexEditor.UndoDepth}, Redo: {_hexEditor.RedoDepth})");
        }
        else
        {
            Console.WriteLine("Already at end of history");
        }
    }

    public void GoToBeginning()
    {
        int steps = 0;
        while (_hexEditor.CanUndo)
        {
            _hexEditor.Undo();
            steps++;
        }
        Console.WriteLine($"Went to beginning ({steps} steps backward)");
    }

    public void GoToEnd()
    {
        int steps = 0;
        while (_hexEditor.CanRedo)
        {
            _hexEditor.Redo();
            steps++;
        }
        Console.WriteLine($"Went to end ({steps} steps forward)");
    }

    public void ShowPosition()
    {
        Console.WriteLine($"History position: " +
                         $"{_hexEditor.UndoDepth} ← [current] → {_hexEditor.RedoDepth}");
    }
}

// Usage
var navigator = new EditHistoryNavigator(hexEditor);

// Make edits
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);
hexEditor.ModifyByte(0xBB, 0x300);

navigator.ShowPosition();  // 3 ← [current] → 0

// Navigate backward
navigator.StepBackward();
navigator.StepBackward();
navigator.ShowPosition();  // 1 ← [current] → 2

// Navigate forward
navigator.StepForward();
navigator.ShowPosition();  // 2 ← [current] → 1
```

---

### Example 6: Redo with Visual Feedback

```csharp
private void RedoWithFeedback()
{
    if (!hexEditor.CanRedo)
    {
        statusLabel.Text = "Nothing to redo";
        statusLabel.Foreground = Brushes.Gray;
        return;
    }

    // Get state before redo
    int undoDepthBefore = hexEditor.UndoDepth;
    int redoDepthBefore = hexEditor.RedoDepth;
    long lengthBefore = hexEditor.Length;

    // Perform redo
    hexEditor.Redo();

    // Get state after redo
    int undoDepthAfter = hexEditor.UndoDepth;
    int redoDepthAfter = hexEditor.RedoDepth;
    long lengthAfter = hexEditor.Length;

    // Visual feedback
    statusLabel.Text = $"Operation redone";
    statusLabel.Foreground = Brushes.Green;

    // Flash animation
    var animation = new ColorAnimation
    {
        From = Colors.LightGreen,
        To = Colors.Transparent,
        Duration = TimeSpan.FromSeconds(1)
    };

    var brush = new SolidColorBrush(Colors.LightGreen);
    hexEditor.Background = brush;
    brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);

    // Log details
    Console.WriteLine("Redo Details:");
    Console.WriteLine($"  Undo depth: {undoDepthBefore} → {undoDepthAfter}");
    Console.WriteLine($"  Redo depth: {redoDepthBefore} → {redoDepthAfter}");
    Console.WriteLine($"  File length: {lengthBefore} → {lengthAfter}");
}
```

---

### Example 7: Redo with Verification

```csharp
// Redo with automatic verification
public class VerifiedRedo
{
    private HexEditor _hexEditor;
    private Dictionary<int, byte[]> _checksums = new();

    public VerifiedRedo(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public bool RedoWithVerification()
    {
        if (!_hexEditor.CanRedo)
        {
            Console.WriteLine("Nothing to redo");
            return false;
        }

        // Calculate checksum before redo
        byte[] checksumBefore = CalculateChecksum();

        // Get expected checksum (if we have it)
        int redoDepthBefore = _hexEditor.RedoDepth;

        // Perform redo
        _hexEditor.Redo();

        // Verify file integrity
        if (!VerifyIntegrity())
        {
            Console.WriteLine("⚠️ Warning: File integrity check failed after redo");

            // Undo the redo (go back)
            if (_hexEditor.CanUndo)
            {
                _hexEditor.Undo();
                Console.WriteLine("Redo reversed due to integrity failure");
            }

            return false;
        }

        Console.WriteLine("✓ Redo verified successfully");
        return true;
    }

    private byte[] CalculateChecksum()
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] data = _hexEditor.GetBytes(0, (int)Math.Min(_hexEditor.Length, 1000));
        return md5.ComputeHash(data);
    }

    private bool VerifyIntegrity()
    {
        // Check file length is reasonable
        if (_hexEditor.Length < 0)
            return false;

        // Check we can read first byte
        try
        {
            if (_hexEditor.Length > 0)
            {
                byte b = _hexEditor.GetByte(0);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

---

### Example 8: Redo History Timeline

```csharp
// Visual timeline of undo/redo history
public class HistoryTimeline
{
    private HexEditor _hexEditor;
    private List<HistoryState> _timeline = new();
    private int _currentIndex = 0;

    public HistoryTimeline(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
        CaptureState();  // Initial state

        hexEditor.DataChanged += (s, e) => UpdateTimeline();
    }

    private void CaptureState()
    {
        _timeline.Add(new HistoryState
        {
            Timestamp = DateTime.Now,
            Length = _hexEditor.Length,
            HasChanges = _hexEditor.HasChanges,
            Description = $"State {_timeline.Count}"
        });
    }

    private void UpdateTimeline()
    {
        // If we're in middle of timeline and make new edit,
        // truncate future states
        if (_currentIndex < _timeline.Count - 1)
        {
            _timeline.RemoveRange(_currentIndex + 1,
                                 _timeline.Count - _currentIndex - 1);
        }

        CaptureState();
        _currentIndex = _timeline.Count - 1;
    }

    public void ShowTimeline()
    {
        Console.WriteLine($"History Timeline ({_timeline.Count} states):");

        for (int i = 0; i < _timeline.Count; i++)
        {
            var state = _timeline[i];
            string marker = (i == _currentIndex) ? "→" : " ";
            string canUndo = (i < _currentIndex) ? "↓" : " ";
            string canRedo = (i > _currentIndex) ? "↑" : " ";

            Console.WriteLine(
                $"{marker} {canUndo}{canRedo} " +
                $"[{state.Timestamp:HH:mm:ss}] {state.Description} " +
                $"(Length: {state.Length}, Modified: {state.HasChanges})");
        }

        Console.WriteLine($"\nCurrent: {_currentIndex}, " +
                         $"Can Undo: {_hexEditor.CanUndo}, " +
                         $"Can Redo: {_hexEditor.CanRedo}");
    }

    public void JumpToState(int index)
    {
        if (index < 0 || index >= _timeline.Count)
        {
            Console.WriteLine("Invalid state index");
            return;
        }

        if (index < _currentIndex)
        {
            // Jump backward (undo)
            int undos = _currentIndex - index;
            for (int i = 0; i < undos && _hexEditor.CanUndo; i++)
            {
                _hexEditor.Undo();
            }
        }
        else if (index > _currentIndex)
        {
            // Jump forward (redo)
            int redos = index - _currentIndex;
            for (int i = 0; i < redos && _hexEditor.CanRedo; i++)
            {
                _hexEditor.Redo();
            }
        }

        _currentIndex = index;
        Console.WriteLine($"Jumped to state {index}");
    }
}

public class HistoryState
{
    public DateTime Timestamp { get; set; }
    public long Length { get; set; }
    public bool HasChanges { get; set; }
    public string Description { get; set; }
}

// Usage
var timeline = new HistoryTimeline(hexEditor);

// Make edits
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);

// Show timeline
timeline.ShowTimeline();

// Undo some
hexEditor.Undo();

timeline.ShowTimeline();

// Jump to specific state
timeline.JumpToState(1);
```

---

## 💡 Use Cases

### 1. Exploratory Editing

```csharp
// Allow users to explore different edit paths
public class ExploratoryEditor
{
    private HexEditor _hexEditor;

    public ExploratoryEditor(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void ExploreEditPaths()
    {
        Console.WriteLine("=== Exploring Edit Path A ===");

        // Path A: Try modification
        _hexEditor.ModifyByte(0xFF, 0x100);
        Console.WriteLine("Applied Path A modification");

        // Test result
        if (!TestModification())
        {
            Console.WriteLine("Path A failed, trying Path B...");

            // Undo Path A
            _hexEditor.Undo();

            // Path B: Try different modification
            _hexEditor.ModifyByte(0xAA, 0x100);
            Console.WriteLine("Applied Path B modification");

            if (TestModification())
            {
                Console.WriteLine("Path B succeeded!");
            }
        }
        else
        {
            Console.WriteLine("Path A succeeded!");
        }
    }

    public void CompareAlternatives()
    {
        // Try option 1
        _hexEditor.ModifyByte(0xFF, 0x100);
        var result1 = EvaluateResult();
        Console.WriteLine($"Option 1 score: {result1}");

        // Undo and try option 2
        _hexEditor.Undo();
        _hexEditor.ModifyByte(0xAA, 0x100);
        var result2 = EvaluateResult();
        Console.WriteLine($"Option 2 score: {result2}");

        // Choose best option
        if (result1 > result2)
        {
            // Undo option 2, redo option 1
            _hexEditor.Undo();
            _hexEditor.Redo();
            Console.WriteLine("Selected Option 1 (better score)");
        }
        else
        {
            Console.WriteLine("Selected Option 2 (better score)");
        }
    }

    private bool TestModification()
    {
        // Test if modification works
        return true;  // Placeholder
    }

    private double EvaluateResult()
    {
        // Evaluate quality of current state
        return Random.Shared.NextDouble();  // Placeholder
    }
}
```

---

### 2. Undo/Redo with Preview

```csharp
// Show preview of undo/redo operation before applying
public class PreviewUndoRedo
{
    private HexEditor _hexEditor;

    public PreviewUndoRedo(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void PreviewAndUndo()
    {
        if (!_hexEditor.CanUndo)
        {
            Console.WriteLine("Nothing to undo");
            return;
        }

        // Capture current state
        long lengthBefore = _hexEditor.Length;
        byte[] sampleBefore = _hexEditor.GetBytes(0, 100);

        // Show what will happen
        Console.WriteLine("Preview of undo:");
        Console.WriteLine($"  Current length: {lengthBefore}");

        var result = MessageBox.Show(
            "Undo last operation?",
            "Preview Undo",
            MessageBoxButton.YesNo);

        if (result == MessageBoxResult.Yes)
        {
            _hexEditor.Undo();

            // Show what changed
            long lengthAfter = _hexEditor.Length;
            Console.WriteLine($"  After undo length: {lengthAfter}");
            Console.WriteLine($"  Length change: {lengthAfter - lengthBefore}");
        }
    }

    public void PreviewAndRedo()
    {
        if (!_hexEditor.CanRedo)
        {
            Console.WriteLine("Nothing to redo");
            return;
        }

        var result = MessageBox.Show(
            "Redo previously undone operation?",
            "Preview Redo",
            MessageBoxButton.YesNo);

        if (result == MessageBoxResult.Yes)
        {
            _hexEditor.Redo();
            Console.WriteLine("Operation redone");
        }
    }
}
```

---

### 3. Teaching/Training Mode

```csharp
// Demonstrate edit operations with undo/redo
public class TeachingMode
{
    private HexEditor _hexEditor;

    public TeachingMode(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public async Task DemonstrateEditSequence()
    {
        Console.WriteLine("=== Demonstrating Edit Sequence ===");

        // Step 1: Modify
        Console.WriteLine("Step 1: Modifying byte at 0x100");
        _hexEditor.ModifyByte(0xFF, 0x100);
        await Task.Delay(1000);

        // Step 2: Insert
        Console.WriteLine("Step 2: Inserting byte at 0x200");
        _hexEditor.InsertByte(0xAA, 0x200);
        await Task.Delay(1000);

        // Step 3: Show result
        Console.WriteLine("Step 3: Current state (2 operations applied)");
        await Task.Delay(1000);

        // Rewind demonstration
        Console.WriteLine("\n=== Rewinding Demonstration ===");

        while (_hexEditor.CanUndo)
        {
            Console.WriteLine("Undoing last operation...");
            _hexEditor.Undo();
            await Task.Delay(1000);
        }

        Console.WriteLine("Back to initial state");

        // Replay demonstration
        Console.WriteLine("\n=== Replaying Demonstration ===");

        while (_hexEditor.CanRedo)
        {
            Console.WriteLine("Redoing operation...");
            _hexEditor.Redo();
            await Task.Delay(1000);
        }

        Console.WriteLine("Demonstration complete!");
    }
}

// Usage
var teaching = new TeachingMode(hexEditor);
await teaching.DemonstrateEditSequence();
```

---

### 4. Edit History Branching Simulation

```csharp
// Simulate branching edit history (V2 doesn't support true branching)
public class BranchingSimulator
{
    private HexEditor _hexEditor;
    private Stack<byte[]> _branchSnapshots = new();

    public BranchingSimulator(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public void SaveBranch()
    {
        // Save current state before branching
        byte[] snapshot = _hexEditor.GetAllBytes();
        _branchSnapshots.Push(snapshot);

        Console.WriteLine("Branch point saved");
    }

    public void RestoreBranch()
    {
        if (_branchSnapshots.Count == 0)
        {
            Console.WriteLine("No branch to restore");
            return;
        }

        // Restore previous branch
        byte[] snapshot = _branchSnapshots.Pop();

        // Undo all to clear current branch
        while (_hexEditor.CanUndo)
        {
            _hexEditor.Undo();
        }

        // Restore snapshot
        _hexEditor.Close();
        _hexEditor.OpenMemory(snapshot);

        Console.WriteLine("Branch restored");
    }
}
```

---

## ⚠️ Important Notes

### Redo is Cleared on New Edit

**Critical Behavior**: Making any edit clears the redo stack.

```csharp
// Make edit
hexEditor.ModifyByte(0xFF, 0x100);

// Undo
hexEditor.Undo();
Console.WriteLine($"Can redo: {hexEditor.CanRedo}");  // True

// Make NEW edit (clears redo!)
hexEditor.ModifyByte(0xAA, 0x200);
Console.WriteLine($"Can redo: {hexEditor.CanRedo}");  // False!
```

**Why**: Prevents timeline branching (keeps history linear).

---

### Redo Stack Depth

- Redo stack is unlimited (same as undo)
- Each undo moves operation to redo stack
- Maximum depth = number of operations undone

---

### Save Does Not Clear Redo

```csharp
// Make edit
hexEditor.ModifyByte(0xFF, 0x100);

// Undo
hexEditor.Undo();

// Save (redo still available!)
hexEditor.Save();
Console.WriteLine($"Can redo: {hexEditor.CanRedo}");  // Still true!

// Redo works even after save
hexEditor.Redo();
```

---

### Batch Operations

```csharp
// Batch operation
hexEditor.BeginBatch();
hexEditor.ModifyByte(0xFF, 0x100);
hexEditor.ModifyByte(0xAA, 0x200);
hexEditor.EndBatch();

// Undo (removes entire batch)
hexEditor.Undo();

// Redo (restores entire batch)
hexEditor.Redo();
```

---

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread

---

## 🔗 See Also

- **[Undo()](undo.md)** - Reverse last edit operation
- **[ClearUndoHistory()](clearundohistory.md)** - Clear undo/redo stacks
- **[BeginBatch() / EndBatch()](beginbatch.md)** - Group operations
- **[ModifyByte()](../byte-operations/modifybyte.md)** - Modify bytes (creates undo entry)

---

**Last Updated**: 2026-02-19
**Version**: V2.0 (Command Pattern with Unlimited Redo)
