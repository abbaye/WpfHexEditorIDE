# Basic Operations Guide

Learn the fundamental operations for working with WPF HexEditor.

---

## 📋 Overview

This guide covers the essential operations you'll use every day:
- ✅ Opening files
- ✅ Navigating data
- ✅ Editing bytes
- ✅ Searching patterns
- ✅ Saving changes
- ✅ Using undo/redo

**Time to complete**: ~15 minutes

---

## 📂 Opening Files

### Method 1: Set FileName Property

```csharp
hexEditor.FileName = @"C:\Data\file.bin";
```

### Method 2: Use File Dialog

```csharp
private void OpenFile_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog
    {
        Filter = "All Files (*.*)|*.*|Binary Files (*.bin)|*.bin",
        Title = "Select a file"
    };

    if (dialog.ShowDialog() == true)
    {
        hexEditor.FileName = dialog.FileName;
    }
}
```

### Method 3: Open from Stream

```csharp
using var stream = File.OpenRead("data.bin");
hexEditor.OpenStream(stream);
```

### Method 4: Open from Memory

```csharp
byte[] data = { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
hexEditor.OpenMemory(data);
```

---

## 🧭 Navigating Data

### Get Current Position

```csharp
long position = hexEditor.Position;
Console.WriteLine($"Current position: 0x{position:X}");
```

### Set Position (Go To)

```csharp
// Go to specific position
hexEditor.SetPosition(0x1000);

// Scroll to position
hexEditor.ScrollToPosition(0x2000);
```

### Navigate with Keyboard

| Key | Action |
|-----|--------|
| **Arrow Keys** | Move cursor |
| **Page Up/Down** | Scroll page |
| **Home** | Go to line start |
| **End** | Go to line end |
| **Ctrl+Home** | Go to file start |
| **Ctrl+End** | Go to file end |
| **Ctrl+G** | Go to position dialog |

### Get File Information

```csharp
// File properties
long fileSize = hexEditor.Length;
string fileName = hexEditor.FileName;
bool isModified = hexEditor.HasChanges;
bool canWrite = hexEditor.CanWrite;

// Display info
Console.WriteLine($"File: {fileName}");
Console.WriteLine($"Size: {fileSize:N0} bytes");
Console.WriteLine($"Modified: {isModified}");
```

---

## ✏️ Editing Bytes

### Read a Byte

```csharp
long position = 0x100;
byte value = hexEditor.GetByte(position);
Console.WriteLine($"Byte at 0x{position:X}: 0x{value:X2}");
```

### Modify a Byte

```csharp
// Change byte value
hexEditor.ModifyByte(0xFF, 0x100);

// Verify change
byte newValue = hexEditor.GetByte(0x100);
Console.WriteLine($"New value: 0x{newValue:X2}");
```

### Insert Bytes (Insert Mode)

```csharp
// Enable insert mode
hexEditor.AllowInsertMode = true;

// Insert byte
hexEditor.InsertByte(0xFF, 0x100);

// Insert multiple bytes
byte[] data = { 0xDE, 0xAD, 0xBE, 0xEF };
hexEditor.InsertBytes(0x200, data);
```

### Delete Bytes

```csharp
// Delete single byte
hexEditor.DeleteByte(0x100);

// Delete range
hexEditor.DeleteBytes(0x200, 10);  // Delete 10 bytes at 0x200
```

### Fill Range with Value

```csharp
// Fill 256 bytes with 0x00
hexEditor.BeginBatch();
for (int i = 0; i < 256; i++)
{
    hexEditor.ModifyByte(0x00, 0x1000 + i);
}
hexEditor.EndBatch();
```

---

## 🔍 Searching

### Search for Byte Pattern

```csharp
// Define pattern
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };

// Find first occurrence
long position = hexEditor.FindFirst(pattern);

if (position >= 0)
{
    Console.WriteLine($"Found at 0x{position:X}");
    hexEditor.SetPosition(position);  // Navigate to match
}
else
{
    MessageBox.Show("Pattern not found");
}
```

### Find All Occurrences

```csharp
byte[] pattern = { 0xFF, 0xFF };
List<long> positions = hexEditor.FindAll(pattern);

Console.WriteLine($"Found {positions.Count} matches:");
foreach (var pos in positions.Take(10))
{
    Console.WriteLine($"  - 0x{pos:X}");
}
```

### Count Occurrences (Memory Efficient)

```csharp
// Count without storing positions
byte[] pattern = { 0x00 };  // Count null bytes
int count = hexEditor.CountOccurrences(pattern);

Console.WriteLine($"File contains {count} null bytes");
```

### Search for Text String

```csharp
// Search for ASCII text
string searchText = "Hello";
byte[] pattern = Encoding.ASCII.GetBytes(searchText);
long position = hexEditor.FindFirst(pattern);

if (position >= 0)
{
    Console.WriteLine($"Found '{searchText}' at 0x{position:X}");
}
```

### Search with Wildcards

```csharp
// Custom wildcard search
private long FindWithWildcard(byte[] pattern, byte wildcardValue = 0x100)
{
    for (long pos = 0; pos <= hexEditor.Length - pattern.Length; pos++)
    {
        bool match = true;
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != wildcardValue)  // Skip wildcard
            {
                if (hexEditor.GetByte(pos + i) != pattern[i])
                {
                    match = false;
                    break;
                }
            }
        }

        if (match)
            return pos;
    }

    return -1;
}

// Usage: Find pattern with wildcard (0x100 = any byte)
byte[] pattern = { 0x4D, 0x5A, 0x100, 0x100 };  // MZ ?? ??
long pos = FindWithWildcard(pattern);
```

---

## 🔁 Replace Operations

### Replace First Occurrence

```csharp
byte[] findPattern = { 0xDE, 0xAD };
byte[] replacePattern = { 0xCA, 0xFE };

int replaced = hexEditor.ReplaceFirst(findPattern, replacePattern);

if (replaced > 0)
{
    Console.WriteLine("Pattern replaced");
}
```

### Replace All Occurrences

```csharp
byte[] findPattern = { 0xFF, 0xFF };
byte[] replacePattern = { 0x00, 0x00 };

int count = hexEditor.ReplaceAll(findPattern, replacePattern);

Console.WriteLine($"Replaced {count} occurrences");
```

---

## 💾 Saving Changes

### Save to Current File

```csharp
// Check if modified
if (hexEditor.HasChanges)
{
    hexEditor.Save();
    MessageBox.Show("File saved successfully!");
}
else
{
    MessageBox.Show("No changes to save");
}
```

### Save As (New File)

```csharp
private void SaveAs_Click(object sender, RoutedEventArgs e)
{
    var dialog = new SaveFileDialog
    {
        Filter = "All Files (*.*)|*.*",
        Title = "Save file as"
    };

    if (dialog.ShowDialog() == true)
    {
        hexEditor.SaveAs(dialog.FileName);
        MessageBox.Show("File saved!");
    }
}
```

### Async Save with Progress

```csharp
private async void SaveAsync_Click(object sender, RoutedEventArgs e)
{
    progressBar.Visibility = Visibility.Visible;

    var progress = new Progress<double>(p => progressBar.Value = p);

    try
    {
        await hexEditor.SaveAsync(progress);
        MessageBox.Show("File saved successfully!");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}");
    }
    finally
    {
        progressBar.Visibility = Visibility.Collapsed;
    }
}
```

---

## ↩️ Undo & Redo

### Basic Undo/Redo

```csharp
// Undo last change
if (hexEditor.CanUndo)
{
    hexEditor.Undo();
}

// Redo
if (hexEditor.CanRedo)
{
    hexEditor.Redo();
}
```

### Check Undo/Redo Availability

```csharp
// Update UI buttons
undoButton.IsEnabled = hexEditor.CanUndo;
redoButton.IsEnabled = hexEditor.CanRedo;

// Get history depth
int undoDepth = hexEditor.UndoDepth;
int redoDepth = hexEditor.RedoDepth;

Console.WriteLine($"Undo: {undoDepth}, Redo: {redoDepth}");
```

### Clear Undo History

```csharp
// Clear all undo/redo history
hexEditor.ClearUndoHistory();

Console.WriteLine("Undo history cleared");
```

### Granular Clear Operations

```csharp
// Clear only modifications (keep insertions/deletions)
hexEditor.ClearModifications();

// Clear only insertions (keep modifications/deletions)
hexEditor.ClearInsertions();

// Clear only deletions (keep modifications/insertions)
hexEditor.ClearDeletions();

// Clear everything
hexEditor.ClearAllChanges();
```

---

## 📋 Selection

### Get Selection

```csharp
long selectionStart = hexEditor.SelectionStart;
long selectionLength = hexEditor.SelectionLength;
long selectionEnd = selectionStart + selectionLength;

Console.WriteLine($"Selection: 0x{selectionStart:X} - 0x{selectionEnd:X}");
```

### Set Selection

```csharp
// Select 256 bytes at position 0x1000
hexEditor.SelectionStart = 0x1000;
hexEditor.SelectionLength = 256;
```

### Select All

```csharp
hexEditor.SelectAll();
```

### Clear Selection

```csharp
hexEditor.SelectionLength = 0;
```

---

## 📌 Bookmarks

### Add Bookmark

```csharp
// Add bookmark at current position
hexEditor.AddBookmark(hexEditor.Position, "Important data");

// Add bookmark at specific position
hexEditor.AddBookmark(0x1000, "File header");
```

### Navigate to Bookmark

```csharp
// Get all bookmarks
var bookmarks = hexEditor.GetBookmarks();

// Navigate to first bookmark
if (bookmarks.Count > 0)
{
    hexEditor.SetPosition(bookmarks[0].Position);
}
```

### Remove Bookmark

```csharp
// Remove bookmark at position
hexEditor.RemoveBookmark(0x1000);

// Clear all bookmarks
hexEditor.ClearBookmarks();
```

---

## 🎨 Highlights

### Add Highlight

```csharp
// Highlight file header (256 bytes) in light blue
hexEditor.AddHighlight(0, 256, Colors.LightBlue, "File Header");

// Highlight data section in light green
hexEditor.AddHighlight(0x1000, 2048, Colors.LightGreen, "Data Section");
```

### Remove Highlight

```csharp
// Remove highlight at position
hexEditor.RemoveHighlight(0);

// Clear all highlights
hexEditor.ClearHighlights();
```

---

## 📊 Copy & Paste

### Copy to Clipboard

```csharp
// Copy selection as hex string
hexEditor.CopyToClipboard(
    hexEditor.SelectionStart,
    hexEditor.SelectionLength,
    ClipboardFormat.HexString
);

// Copy as C# array
hexEditor.CopyToClipboard(
    hexEditor.SelectionStart,
    hexEditor.SelectionLength,
    ClipboardFormat.CSharpArray
);
```

### Paste from Clipboard

```csharp
// Paste at current position
hexEditor.PasteFromClipboard(hexEditor.Position);
```

---

## 🔢 Display Modes

### Change Bytes Per Line

```csharp
// Default: 16 bytes per line
hexEditor.BytesPerLine = 16;

// Show more data
hexEditor.BytesPerLine = 32;

// Compact view
hexEditor.BytesPerLine = 8;
```

### Show/Hide Columns

```csharp
// Hide offset column
hexEditor.ShowOffset = false;

// Hide ASCII column
hexEditor.ShowASCII = false;

// Hide hex column (ASCII only)
hexEditor.ShowHex = false;
```

---

## 🎮 Keyboard Shortcuts

### Built-in Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+Z** | Undo |
| **Ctrl+Y** | Redo |
| **Ctrl+C** | Copy |
| **Ctrl+V** | Paste |
| **Ctrl+X** | Cut |
| **Ctrl+A** | Select All |
| **Ctrl+F** | Find (if implemented) |
| **Ctrl+G** | Go To (if implemented) |
| **Insert** | Toggle Insert/Overwrite |
| **Delete** | Delete selection |

### Custom Shortcuts

```csharp
// Add custom keyboard shortcuts
hexEditor.KeyDown += (s, e) =>
{
    if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
    {
        // Ctrl+F: Show find dialog
        ShowFindDialog();
        e.Handled = true;
    }
    else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
    {
        // Ctrl+G: Show go to dialog
        ShowGoToDialog();
        e.Handled = true;
    }
};
```

---

## 💡 Common Patterns

### Pattern 1: Safe Edit with Validation

```csharp
private bool SafeEdit(long position, byte value)
{
    // Validate
    if (position < 0 || position >= hexEditor.Length)
    {
        MessageBox.Show("Position out of range");
        return false;
    }

    if (hexEditor.ReadOnlyMode)
    {
        MessageBox.Show("File is read-only");
        return false;
    }

    // Perform edit
    try
    {
        hexEditor.ModifyByte(value, position);
        return true;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error: {ex.Message}");
        return false;
    }
}
```

### Pattern 2: Batch Operations

```csharp
// Efficient: Group multiple edits
hexEditor.BeginBatch();
try
{
    for (int i = 0; i < 1000; i++)
    {
        hexEditor.ModifyByte(0xFF, i);
    }
}
finally
{
    hexEditor.EndBatch();  // Update UI once
}
```

### Pattern 3: Progress Reporting

```csharp
private void ProcessFileWithProgress()
{
    progressBar.Visibility = Visibility.Visible;
    progressBar.Maximum = hexEditor.Length;

    for (long i = 0; i < hexEditor.Length; i++)
    {
        // Process byte
        byte value = hexEditor.GetByte(i);
        // ... do something

        // Update progress every 1000 bytes
        if (i % 1000 == 0)
        {
            progressBar.Value = i;
            statusLabel.Text = $"Processing: {i * 100 / hexEditor.Length}%";

            // Allow UI to update
            Application.Current.Dispatcher.Invoke(
                DispatcherPriority.Background,
                new Action(() => { }));
        }
    }

    progressBar.Visibility = Visibility.Collapsed;
}
```

---

## 🔗 Next Steps

### Learn More

- **[Search Operations](API-Search-Operations)** - Advanced search techniques
- **[API Reference](API-Reference)** - Complete API documentation
- **[Sample Applications](Sample-Applications)** - Working examples
- **[Best Practices](Best-Practices)** - Performance tips

### Build Real Applications

- **[Binary File Analyzer](Sample-Applications#binary-analyzer)** - Analyze file structures
- **[Hex File Diff Tool](Sample-Applications#binary-diff)** - Compare binary files
- **[ROM Patcher](Sample-Applications#rom-patcher)** - Game modding tool

---

<div align="center">
  <br/>
  <p>
    <b>🎓 You've mastered the basics!</b><br/>
    Ready for advanced features?
  </p>
  <br/>
  <p>
    👉 <a href="API-Reference"><b>API Reference</b></a> •
    <a href="Sample-Applications"><b>Sample Apps</b></a> •
    <a href="Best-Practices"><b>Best Practices</b></a>
  </p>
</div>
