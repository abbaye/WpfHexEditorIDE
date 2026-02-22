# HexEditor API Reference

Complete API documentation for WPF Hex Editor.

**Version**: 2.0
**Last Updated**: 2026-02-13
**Namespace**: `WpfHexaEditor`

---

## Table of Contents

- [Overview](#overview)
- [Properties](#properties)
  - [File and Stream Properties](#file-and-stream-properties)
  - [Selection Properties](#selection-properties)
  - [Visual Properties](#visual-properties)
  - [Edit Mode Properties](#edit-mode-properties)
  - [Undo/Redo Properties](#undoredo-properties)
  - [Search and Bookmark Properties](#search-and-bookmark-properties)
- [Methods](#methods)
  - [File Operations](#file-operations)
  - [Edit Operations](#edit-operations)
  - [Selection Operations](#selection-operations)
  - [Search Operations](#search-operations)
  - [Clipboard Operations](#clipboard-operations)
  - [Undo/Redo Operations](#undoredo-operations)
  - [Bookmark Operations](#bookmark-operations)
  - [Navigation Operations](#navigation-operations)
- [Events](#events)
- [Examples](#examples)

---

## Overview

`HexEditor` is a high-performance WPF hex editor control with:
- **Custom DrawingContext rendering** (99% faster than V1)
- **Native insert mode** support
- **MVVM architecture**
- **100% V1 compatibility** (drop-in replacement)

```csharp
using WpfHexaEditor;

// Basic usage
var hexEditor = new HexEditor();
hexEditor.OpenFile("myfile.bin");
```

---

## Properties

### File and Stream Properties

#### `FileName` (string)

Gets or sets the current file name.

```csharp
public string FileName { get; set; }
```

**Example:**
```csharp
hexEditor.FileName = @"C:\data\file.bin";
// Opens the file automatically when set
```

---

#### `IsFileOrStreamLoaded` (bool, readonly)

Gets whether a file or stream is currently loaded.

```csharp
public bool IsFileOrStreamLoaded { get; }
```

**Example:**
```csharp
if (hexEditor.IsFileOrStreamLoaded)
{
    Console.WriteLine($"File size: {hexEditor.Length} bytes");
}
```

---

#### `Length` (long, readonly)

Gets the total length of the current file/stream in bytes.

```csharp
public long Length { get; }
```

**Example:**
```csharp
long fileSize = hexEditor.Length;
Console.WriteLine($"File contains {fileSize:N0} bytes");
```

---

#### `VirtualLength` (long, readonly)

Gets the virtual length including pending insertions (when in Insert mode).

```csharp
public long VirtualLength { get; }
```

**Note**: In Overwrite mode, `VirtualLength == Length`. In Insert mode, `VirtualLength >= Length`.

---

### Selection Properties

#### `SelectionStart` (long)

Gets or sets the selection start position (virtual).

```csharp
public long SelectionStart { get; set; }
```

**Example:**
```csharp
// Select bytes 0x100 to 0x1FF
hexEditor.SelectionStart = 0x100;
hexEditor.SelectionStop = 0x1FF;
```

---

#### `SelectionStop` (long)

Gets or sets the selection end position (virtual).

```csharp
public long SelectionStop { get; set; }
```

---

#### `SelectionLength` (long, readonly)

Gets the length of the current selection.

```csharp
public long SelectionLength { get; }
```

**Example:**
```csharp
if (hexEditor.SelectionLength > 0)
{
    Console.WriteLine($"Selected {hexEditor.SelectionLength} bytes");
}
```

---

#### `HasSelection` (bool, readonly)

Gets whether there is an active selection.

```csharp
public bool HasSelection { get; }
```

---

### Visual Properties

#### `BytesPerLine` (int)

Gets or sets the number of bytes displayed per line (default: 16).

```csharp
public int BytesPerLine { get; set; }
```

**Valid values**: 8, 16, 24, 32
**Default**: 16

**Example:**
```csharp
// Display 32 bytes per line for wider monitors
hexEditor.BytesPerLine = 32;
```

---

#### `ZoomScale` (double)

Gets or sets the zoom scale factor (0.5 to 2.0).

```csharp
public double ZoomScale { get; set; }
```

**Valid range**: 0.5 (50%) to 2.0 (200%)
**Default**: 1.0 (100%)

**Example:**
```csharp
// Zoom in to 150%
hexEditor.ZoomScale = 1.5;

// Zoom out to 75%
hexEditor.ZoomScale = 0.75;
```

---

#### `ShowHeader` (bool)

Gets or sets whether the column headers are visible.

```csharp
public bool ShowHeader { get; set; }
```

**Default**: true

---

#### `ShowStatusBar` (bool)

Gets or sets whether the status bar is visible.

```csharp
public bool ShowStatusBar { get; set; }
```

**Default**: true

---

#### `ShowOffset` (bool)

Gets or sets whether the offset column is visible.

```csharp
public bool ShowOffset { get; set; }
```

**Default**: true

---

#### `ShowAscii` (bool)

Gets or sets whether the ASCII panel is visible.

```csharp
public bool ShowAscii { get; set; }
```

**Default**: true

---

#### `ShowByteToolTip` (bool)

Gets or sets whether tooltips are shown when hovering over bytes.

```csharp
public bool ShowByteToolTip { get; set; }
```

**Default**: false

**Example:**
```csharp
// Enable byte tooltips (shows position, value, ASCII)
hexEditor.ShowByteToolTip = true;
```

---

#### `SelectionFirstColor` (Color)

Gets or sets the primary selection highlight color.

```csharp
public Color SelectionFirstColor { get; set; }
```

**Default**: `#0078D4` (blue)

**Example:**
```csharp
// Set selection color to green
hexEditor.SelectionFirstColor = Colors.LimeGreen;
```

---

#### `ByteModifiedColor` (Color)

Gets or sets the color for modified bytes.

```csharp
public Color ByteModifiedColor { get; set; }
```

**Default**: `#FFA500` (orange)

---

#### `ByteAddedColor` (Color)

Gets or sets the color for added bytes (Insert mode).

```csharp
public Color ByteAddedColor { get; set; }
```

**Default**: `#4CAF50` (green)

---

#### `ByteDeletedColor` (Color)

Gets or sets the color for deleted bytes.

```csharp
public Color ByteDeletedColor { get; set; }
```

**Default**: `#F44336` (red)

---

### Edit Mode Properties

#### `EditMode` (EditMode enum)

Gets or sets the edit mode (Overwrite or Insert).

```csharp
public EditMode EditMode { get; set; }
```

**Values**:
- `EditMode.Overwrite` - Typing replaces existing bytes (default)
- `EditMode.Insert` - Typing inserts new bytes, shifting existing ones

**Example:**
```csharp
// Enable insert mode
hexEditor.EditMode = EditMode.Insert;

// Check current mode
if (hexEditor.EditMode == EditMode.Insert)
{
    Console.WriteLine("Insert mode active");
}
```

---

#### `ReadOnlyMode` (bool)

Gets or sets whether the editor is read-only.

```csharp
public bool ReadOnlyMode { get; set; }
```

**Default**: false

**Example:**
```csharp
// Make read-only
hexEditor.ReadOnlyMode = true;
```

---

### Undo/Redo Properties

#### `CanUndo` (bool, readonly)

Gets whether undo is possible.

```csharp
public bool CanUndo { get; }
```

**Example:**
```csharp
undoButton.IsEnabled = hexEditor.CanUndo;
```

---

#### `CanRedo` (bool, readonly)

Gets whether redo is possible.

```csharp
public bool CanRedo { get; }
```

---

#### `UndoCount` (long, readonly)

Gets the number of operations in the undo stack.

```csharp
public long UndoCount { get; }
```

---

#### `RedoCount` (long, readonly)

Gets the number of operations in the redo stack.

```csharp
public long RedoCount { get; }
```

---

### Search and Bookmark Properties

#### `AllowBookmark` (bool)

Gets or sets whether bookmarks are enabled.

```csharp
public bool AllowBookmark { get; set; }
```

**Default**: true

---

#### `BookmarkPositions` (IEnumerable<long>, readonly)

Gets the list of bookmarked positions.

```csharp
public IEnumerable<long> BookmarkPositions { get; }
```

**Example:**
```csharp
foreach (long position in hexEditor.BookmarkPositions)
{
    Console.WriteLine($"Bookmark at 0x{position:X}");
}
```

---

## Methods

### File Operations

#### `OpenFile(string fileName)`

Opens a file for editing.

```csharp
public void OpenFile(string fileName)
```

**Parameters:**
- `fileName` - Path to the file to open

**Example:**
```csharp
try
{
    hexEditor.OpenFile(@"C:\data\file.bin");
    Console.WriteLine($"Opened file: {hexEditor.Length} bytes");
}
catch (Exception ex)
{
    MessageBox.Show($"Failed to open file: {ex.Message}");
}
```

---

#### `OpenStream(System.IO.Stream stream)`

Opens a stream for editing.

```csharp
public void OpenStream(System.IO.Stream stream)
```

**Parameters:**
- `stream` - Stream to open (must be seekable)

**Example:**
```csharp
using var memoryStream = new MemoryStream(byteArray);
hexEditor.OpenStream(memoryStream);
```

---

#### `Save()`

Saves changes to the current file.

```csharp
public void Save()
```

**Example:**
```csharp
if (hexEditor.CanUndo)
{
    hexEditor.Save();
    Console.WriteLine("Changes saved");
}
```

---

#### `SaveAs(string fileName)`

Saves the file to a new location.

```csharp
public void SaveAs(string fileName)
```

**Parameters:**
- `fileName` - Path for the new file

**Example:**
```csharp
hexEditor.SaveAs(@"C:\data\file_backup.bin");
```

---

#### `Close()`

Closes the current file/stream.

```csharp
public void Close()
```

**Example:**
```csharp
hexEditor.Close();
Console.WriteLine("File closed");
```

---

### Edit Operations

#### `ModifyByte(byte value, long position)`

Modifies a byte at the specified position.

```csharp
public void ModifyByte(byte value, long position)
```

**Parameters:**
- `value` - New byte value
- `position` - Virtual position to modify

**Example:**
```csharp
// Change byte at position 0x100 to 0xFF
hexEditor.ModifyByte(0xFF, 0x100);
```

---

#### `InsertByte(byte value, long position)`

Inserts a byte at the specified position (Insert mode only).

```csharp
public void InsertByte(byte value, long position)
```

**Parameters:**
- `value` - Byte value to insert
- `position` - Virtual position to insert at

**Example:**
```csharp
// Requires Insert mode
hexEditor.EditMode = EditMode.Insert;
hexEditor.InsertByte(0x00, 0x100);
```

---

#### `DeleteByte(long position)`

Deletes a byte at the specified position.

```csharp
public void DeleteByte(long position)
```

**Parameters:**
- `position` - Virtual position to delete

---

#### `DeleteSelection()`

Deletes the current selection.

```csharp
public void DeleteSelection()
```

**Example:**
```csharp
if (hexEditor.HasSelection)
{
    hexEditor.DeleteSelection();
}
```

---

#### `FillSelection(byte value)`

Fills the current selection with a specific byte value.

```csharp
public void FillSelection(byte value)
```

**Parameters:**
- `value` - Byte value to fill with

**Example:**
```csharp
// Fill selection with 0x00
hexEditor.FillSelection(0x00);
```

---

### Selection Operations

#### `SelectAll()`

Selects all bytes in the file.

```csharp
public void SelectAll()
```

**Example:**
```csharp
hexEditor.SelectAll();
Console.WriteLine($"Selected {hexEditor.SelectionLength} bytes");
```

---

#### `ClearSelection()`

Clears the current selection.

```csharp
public void ClearSelection()
```

**Example:**
```csharp
hexEditor.ClearSelection();
```

---

#### `SetSelection(long start, long length)`

Sets a selection range.

```csharp
public void SetSelection(long start, long length)
```

**Parameters:**
- `start` - Starting position (virtual)
- `length` - Number of bytes to select

**Example:**
```csharp
// Select 256 bytes starting at 0x1000
hexEditor.SetSelection(0x1000, 256);
```

---

### Search Operations

#### `FindFirst(byte[] data)`

Finds the first occurrence of a byte pattern.

```csharp
public long FindFirst(byte[] data)
```

**Parameters:**
- `data` - Byte array to search for

**Returns:** Position of first match, or -1 if not found

**Example:**
```csharp
byte[] pattern = { 0x50, 0x4B, 0x03, 0x04 }; // ZIP signature
long position = hexEditor.FindFirst(pattern);

if (position >= 0)
{
    Console.WriteLine($"Found at position 0x{position:X}");
    hexEditor.SetSelection(position, pattern.Length);
}
```

---

#### `FindFirst(string text)`

Finds the first occurrence of a text string.

```csharp
public long FindFirst(string text)
```

**Parameters:**
- `text` - Text to search for (using current encoding)

**Returns:** Position of first match, or -1 if not found

**Example:**
```csharp
long position = hexEditor.FindFirst("ERROR");
if (position >= 0)
{
    hexEditor.SetPosition(position);
}
```

---

#### `FindNext(byte[] data)`

Finds the next occurrence of a byte pattern.

```csharp
public long FindNext(byte[] data)
```

**Parameters:**
- `data` - Byte array to search for

**Returns:** Position of next match, or -1 if not found

---

#### `FindAll(byte[] data)`

Finds all occurrences of a byte pattern.

```csharp
public List<long> FindAll(byte[] data)
```

**Parameters:**
- `data` - Byte array to search for

**Returns:** List of all positions where pattern was found

**Example:**
```csharp
byte[] pattern = { 0x00, 0x00 };
List<long> matches = hexEditor.FindAll(pattern);
Console.WriteLine($"Found {matches.Count} occurrences");
```

---

#### `ReplaceAll(byte[] findData, byte[] replaceData)`

Replaces all occurrences of a byte pattern.

```csharp
public int ReplaceAll(byte[] findData, byte[] replaceData)
```

**Parameters:**
- `findData` - Byte pattern to find
- `replaceData` - Byte pattern to replace with

**Returns:** Number of replacements made

**Example:**
```csharp
byte[] find = { 0x0D, 0x0A };    // CRLF
byte[] replace = { 0x0A };        // LF
int count = hexEditor.ReplaceAll(find, replace);
Console.WriteLine($"Replaced {count} occurrences");
```

---

### Clipboard Operations

#### `Copy()`

Copies the current selection to the clipboard.

```csharp
public bool Copy()
```

**Returns:** true if successful, false otherwise

**Example:**
```csharp
if (hexEditor.HasSelection)
{
    hexEditor.Copy();
}
```

---

#### `Paste()`

Pastes bytes from the clipboard at the current position.

```csharp
public bool Paste()
```

**Returns:** true if successful, false otherwise

**Example:**
```csharp
if (Clipboard.ContainsData("BinaryData"))
{
    hexEditor.Paste();
}
```

---

#### `Cut()`

Cuts the current selection to the clipboard.

```csharp
public bool Cut()
```

**Returns:** true if successful, false otherwise

---

### Undo/Redo Operations

#### `Undo()`

Undoes the last operation.

```csharp
public void Undo()
```

**Example:**
```csharp
if (hexEditor.CanUndo)
{
    hexEditor.Undo();
}
```

---

#### `Redo()`

Redoes the last undone operation.

```csharp
public void Redo()
```

**Example:**
```csharp
if (hexEditor.CanRedo)
{
    hexEditor.Redo();
}
```

---

#### `ClearUndoHistory()`

Clears the undo/redo history.

```csharp
public void ClearUndoHistory()
```

---

### Bookmark Operations

#### `SetBookmark(long position)`

Sets a bookmark at the specified position.

```csharp
public void SetBookmark(long position)
```

**Parameters:**
- `position` - Position to bookmark

**Example:**
```csharp
// Bookmark current position
hexEditor.SetBookmark(hexEditor.SelectionStart);
```

---

#### `ClearBookmark(long position)`

Removes a bookmark at the specified position.

```csharp
public void ClearBookmark(long position)
```

---

#### `ClearAllBookmarks()`

Removes all bookmarks.

```csharp
public void ClearAllBookmarks()
```

---

#### `NavigateToNextBookmark()`

Navigates to the next bookmark.

```csharp
public bool NavigateToNextBookmark()
```

**Returns:** true if navigated, false if no more bookmarks

---

#### `NavigateToPreviousBookmark()`

Navigates to the previous bookmark.

```csharp
public bool NavigateToPreviousBookmark()
```

---

### Navigation Operations

#### `SetPosition(long position)`

Sets the cursor position.

```csharp
public void SetPosition(long position)
```

**Parameters:**
- `position` - Position to navigate to (virtual)

**Example:**
```csharp
// Jump to beginning of file
hexEditor.SetPosition(0);

// Jump to end
hexEditor.SetPosition(hexEditor.VirtualLength - 1);
```

---

#### `ScrollToPosition(long position)`

Scrolls the viewport to make the specified position visible.

```csharp
public void ScrollToPosition(long position)
```

---

## Events

### `FileOpened`

Fired when a file is successfully opened.

```csharp
public event EventHandler FileOpened;
```

**Example:**
```csharp
hexEditor.FileOpened += (sender, e) =>
{
    var editor = sender as HexEditor;
    Console.WriteLine($"Opened: {editor.FileName} ({editor.Length} bytes)");
};
```

---

### `FileClosed`

Fired when a file is closed.

```csharp
public event EventHandler FileClosed;
```

---

### `SelectionChanged`

Fired when the selection changes.

```csharp
public event EventHandler SelectionChanged;
```

**Example:**
```csharp
hexEditor.SelectionChanged += (sender, e) =>
{
    var editor = sender as HexEditor;
    statusLabel.Text = $"Selected: {editor.SelectionLength} bytes";
};
```

---

### `ByteModified`

Fired when a byte is modified.

```csharp
public event EventHandler<ByteModifiedEventArgs> ByteModified;
```

**Example:**
```csharp
hexEditor.ByteModified += (sender, e) =>
{
    Console.WriteLine($"Byte at 0x{e.Position:X} changed to 0x{e.NewValue:X2}");
};
```

---

### `UndoCompleted`

Fired when an undo operation completes.

```csharp
public event EventHandler UndoCompleted;
```

---

### `RedoCompleted`

Fired when a redo operation completes.

```csharp
public event EventHandler RedoCompleted;
```

---

## Examples

### Example 1: Basic File Viewer

```csharp
// Create editor
var hexEditor = new HexEditor
{
    ReadOnlyMode = true,
    ShowByteToolTip = true
};

// Open file
hexEditor.OpenFile(@"C:\data\image.png");

// Find PNG signature
byte[] pngSignature = { 0x89, 0x50, 0x4E, 0x47 };
long position = hexEditor.FindFirst(pngSignature);

if (position >= 0)
{
    hexEditor.SetSelection(position, pngSignature.Length);
    MessageBox.Show($"PNG signature found at 0x{position:X}");
}
```

---

### Example 2: Binary Editor with Undo

```csharp
var hexEditor = new HexEditor
{
    EditMode = EditMode.Overwrite,
    ShowStatusBar = true
};

hexEditor.OpenFile(@"C:\data\config.bin");

// Modify bytes
hexEditor.ModifyByte(0xFF, 0x10);
hexEditor.ModifyByte(0x00, 0x11);

// Undo last change
if (hexEditor.CanUndo)
{
    hexEditor.Undo();
}

// Save
hexEditor.Save();
```

---

### Example 3: Search and Replace

```csharp
var hexEditor = new HexEditor();
hexEditor.OpenFile(@"C:\data\text.dat");

// Find all occurrences of "ERROR"
string searchText = "ERROR";
List<long> positions = hexEditor.FindAll(
    System.Text.Encoding.ASCII.GetBytes(searchText)
);

Console.WriteLine($"Found {positions.Count} occurrences of '{searchText}'");

// Replace all with "DEBUG"
byte[] findBytes = System.Text.Encoding.ASCII.GetBytes("ERROR");
byte[] replaceBytes = System.Text.Encoding.ASCII.GetBytes("DEBUG");
int replaced = hexEditor.ReplaceAll(findBytes, replaceBytes);

Console.WriteLine($"Replaced {replaced} occurrences");
hexEditor.Save();
```

---

### Example 4: Insert Mode Editing

```csharp
var hexEditor = new HexEditor
{
    EditMode = EditMode.Insert
};

hexEditor.OpenFile(@"C:\data\test.bin");

// Insert 4 bytes at position 0x100
hexEditor.SetPosition(0x100);
hexEditor.InsertByte(0x00, 0x100);
hexEditor.InsertByte(0x01, 0x101);
hexEditor.InsertByte(0x02, 0x102);
hexEditor.InsertByte(0x03, 0x103);

// Virtual length increased by 4
Console.WriteLine($"Original length: {hexEditor.Length}");
Console.WriteLine($"Virtual length: {hexEditor.VirtualLength}");

// Save (insertions become permanent)
hexEditor.Save();
```

---

### Example 5: Custom Visual Theme

```csharp
var hexEditor = new HexEditor
{
    // Custom colors
    SelectionFirstColor = Colors.DarkBlue,
    ByteModifiedColor = Colors.Gold,
    ByteAddedColor = Colors.LimeGreen,
    ByteDeletedColor = Colors.Crimson,

    // Layout
    BytesPerLine = 32,
    ZoomScale = 1.25,

    // Features
    ShowByteToolTip = true,
    ShowHeader = true,
    ShowStatusBar = true
};
```

---

### Example 6: Bookmarks

```csharp
var hexEditor = new HexEditor
{
    AllowBookmark = true
};

hexEditor.OpenFile(@"C:\data\log.bin");

// Find all "START" markers and bookmark them
string marker = "START";
byte[] markerBytes = System.Text.Encoding.ASCII.GetBytes(marker);
List<long> positions = hexEditor.FindAll(markerBytes);

foreach (long position in positions)
{
    hexEditor.SetBookmark(position);
}

Console.WriteLine($"Set {positions.Count} bookmarks");

// Navigate through bookmarks
while (hexEditor.NavigateToNextBookmark())
{
    Console.WriteLine($"Bookmark at 0x{hexEditor.SelectionStart:X}");
}
```

---

## See Also

- [Architecture.md](Architecture.md) - System architecture and design
- [MigrationGuide.md](MigrationGuide.md) - Migrating from V1 to V2
- [QuickStart.md](QuickStart.md) - Getting started guide
- [TestingStrategy.md](TestingStrategy.md) - Testing approach
- [V1CompatibilityStatus.md](V1CompatibilityStatus.md) - V1 compatibility status

---

**Document Version**: 1.0
**Author**: Claude Sonnet 4.5 with Derek Tremblay
**License**: Apache 2.0
