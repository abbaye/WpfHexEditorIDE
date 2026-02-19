# DeleteBytes()

Remove one or more bytes from a specific position, decreasing file length.

---

## 📋 Description

The `DeleteBytes()` method removes bytes starting at the specified position, shifting all subsequent bytes backward. This is a **deletion** operation, so the file length decreases by the number of bytes deleted. Deleted bytes are tracked separately and can be restored via Undo.

**Key characteristics**:
- ✅ File length decreases by deletion count
- ✅ Fully undoable (Undo/Redo)
- ✅ Tracked as deletion (not modification)
- ✅ Visual feedback (deleted bytes shown as strikethrough/grayed by default)
- ✅ Subsequent bytes shift backward
- ⚡ Fast (O(1) for deletion tracking)

**Important**: Deleted bytes are **tracked** but not immediately removed from the physical file until Save() is called.

---

## 📝 Signatures

```csharp
// Delete single byte
public void DeleteByte(long position)

// Delete multiple bytes
public void DeleteBytes(long position, int count)
```

**Since:** V1.0

---

## ⚙️ Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `position` | `long` | Starting position (0-based) |
| `count` | `int` | Number of bytes to delete |

---

## 🔄 Returns

| Method | Return Type | Description |
|--------|-------------|-------------|
| `DeleteByte()` | `void` | No return value |
| `DeleteBytes()` | `void` | No return value |

---

## 🎯 Examples

### Example 1: Delete Single Byte

```csharp
using WpfHexaEditor;

// Open file
hexEditor.FileName = "data.bin";
Console.WriteLine($"Original length: {hexEditor.Length}");

// Delete byte at position 0x100
hexEditor.DeleteByte(0x100);

Console.WriteLine($"New length: {hexEditor.Length}");  // -1 byte
```

**Before**:
```
00000100: 41 42 43 44 45  ABCDE
```

**After**:
```
00000100: 41 43 44 45  ACDE
         (B deleted, subsequent bytes shifted back)
```

---

### Example 2: Delete Multiple Bytes

```csharp
private void DeleteMultipleBytes()
{
    long position = 0x1000;
    int count = 16;  // Delete 16 bytes

    Console.WriteLine($"Before: Length = {hexEditor.Length}");

    // Delete bytes
    hexEditor.DeleteBytes(position, count);

    Console.WriteLine($"After: Length = {hexEditor.Length}");  // -16 bytes
    Console.WriteLine($"Deleted {count} bytes at 0x{position:X}");
}
```

**Before**:
```
00001000: DE AD BE EF CA FE BA BE 01 02 03 04 05 06 07 08
00001010: AA BB CC DD EE FF 11 22
```

**After**:
```
00001000: AA BB CC DD EE FF 11 22
         (First 16 bytes deleted, subsequent bytes moved back)
```

---

### Example 3: Delete with Undo Support

```csharp
private void DeleteWithUndo()
{
    long position = 0x100;
    int count = 4;

    // Save original data for verification
    byte[] originalBytes = hexEditor.GetBytes(position, count);

    // Delete bytes (automatically recorded in undo stack)
    hexEditor.DeleteBytes(position, count);

    Console.WriteLine($"Deleted {count} bytes at 0x{position:X}");
    Console.WriteLine($"New length: {hexEditor.Length}");
    Console.WriteLine($"Can undo: {hexEditor.CanUndo}");  // True

    // Undo deletion
    if (hexEditor.CanUndo)
    {
        hexEditor.Undo();
        Console.WriteLine($"After undo length: {hexEditor.Length}");  // Restored

        // Verify bytes restored
        byte[] restoredBytes = hexEditor.GetBytes(position, count);
        bool matches = originalBytes.SequenceEqual(restoredBytes);
        Console.WriteLine($"Bytes restored correctly: {matches}");
    }
}
```

---

### Example 4: Delete Selection

```csharp
private void DeleteSelectionButton_Click(object sender, RoutedEventArgs e)
{
    // Get current selection
    long selectionStart = hexEditor.SelectionStart;
    long selectionLength = hexEditor.SelectionLength;

    if (selectionLength == 0)
    {
        MessageBox.Show("No bytes selected", "Info");
        return;
    }

    // Confirm deletion
    var result = MessageBox.Show(
        $"Delete {selectionLength} selected bytes?",
        "Confirm Deletion",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (result == MessageBoxResult.Yes)
    {
        // Delete selected bytes
        hexEditor.DeleteBytes(selectionStart, (int)selectionLength);

        // Update status
        statusLabel.Text = $"Deleted {selectionLength} bytes at 0x{selectionStart:X}";

        // Clear selection
        hexEditor.SelectionLength = 0;
    }
}
```

---

### Example 5: Delete with Validation

```csharp
private bool SafeDeleteBytes(long position, int count)
{
    // Validate position
    if (position < 0 || position >= hexEditor.Length)
    {
        MessageBox.Show($"Position 0x{position:X} is out of range", "Error");
        return false;
    }

    // Validate count
    if (count <= 0)
    {
        MessageBox.Show("Count must be positive", "Error");
        return false;
    }

    // Check if enough bytes available
    if (position + count > hexEditor.Length)
    {
        MessageBox.Show(
            $"Cannot delete {count} bytes at position 0x{position:X}\n" +
            $"Only {hexEditor.Length - position} bytes available",
            "Error");
        return false;
    }

    // Check if file is writable
    if (hexEditor.ReadOnlyMode || !hexEditor.CanWrite)
    {
        MessageBox.Show("File is read-only", "Error");
        return false;
    }

    // Perform deletion
    try
    {
        long oldLength = hexEditor.Length;

        // Save deleted bytes for logging
        byte[] deletedBytes = hexEditor.GetBytes(position, count);

        hexEditor.DeleteBytes(position, count);

        long newLength = hexEditor.Length;

        // Log change
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Deleted {count} bytes at 0x{position:X}");
        Console.WriteLine($"Deleted data: {BitConverter.ToString(deletedBytes)}");
        Console.WriteLine($"Length: {oldLength} → {newLength} (-{count})");

        return true;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error deleting bytes: {ex.Message}", "Error");
        return false;
    }
}
```

---

### Example 6: Delete Range with Progress

```csharp
// Delete large range with progress reporting
private async Task DeleteRangeAsync(long start, long end)
{
    long count = end - start + 1;

    if (count > 1_000_000)  // > 1 MB
    {
        // Show progress dialog
        var progressDialog = new ProgressDialog
        {
            Title = "Deleting bytes...",
            Maximum = count
        };
        progressDialog.Show();

        // Delete in chunks
        const int chunkSize = 100_000;
        long remaining = count;
        long currentPos = start;

        hexEditor.BeginBatch();

        try
        {
            while (remaining > 0)
            {
                int deleteCount = (int)Math.Min(chunkSize, remaining);

                hexEditor.DeleteBytes(currentPos, deleteCount);

                remaining -= deleteCount;
                // Note: Position doesn't change because we deleted from currentPos

                // Update progress
                progressDialog.Value = count - remaining;
                await Task.Delay(1);  // Allow UI update
            }
        }
        finally
        {
            hexEditor.EndBatch();
            progressDialog.Close();
        }

        MessageBox.Show($"Deleted {count:N0} bytes", "Success");
    }
    else
    {
        // Small deletion, no progress needed
        hexEditor.DeleteBytes(start, (int)count);
    }
}
```

---

### Example 7: Delete Pattern Occurrences

```csharp
// Delete all occurrences of a specific pattern
private int DeleteAllPatterns(byte[] pattern)
{
    int deletionCount = 0;

    hexEditor.BeginBatch();

    try
    {
        // Find all occurrences
        var positions = hexEditor.FindAll(pattern);

        if (positions.Count == 0)
        {
            MessageBox.Show("Pattern not found", "Info");
            return 0;
        }

        // Confirm deletion
        var result = MessageBox.Show(
            $"Found {positions.Count} occurrences of pattern.\n" +
            $"Delete all {positions.Count * pattern.Length} bytes?",
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return 0;

        // Delete in reverse order (to preserve positions)
        for (int i = positions.Count - 1; i >= 0; i--)
        {
            hexEditor.DeleteBytes(positions[i], pattern.Length);
            deletionCount++;
        }

        Console.WriteLine($"Deleted {deletionCount} occurrences of pattern");
        return deletionCount;
    }
    finally
    {
        hexEditor.EndBatch();
    }
}

// Usage: Delete all null bytes
byte[] nullPattern = { 0x00 };
int deleted = DeleteAllPatterns(nullPattern);
Console.WriteLine($"Deleted {deleted} null bytes");
```

---

### Example 8: Delete Specific Structure

```csharp
// Delete a specific structure (e.g., remove file header)
public class StructureDeleter
{
    private HexEditor _hexEditor;

    public StructureDeleter(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Delete DOS stub from PE executable
    public void DeleteDOSStub()
    {
        // Check if PE file
        byte[] magic = _hexEditor.GetBytes(0, 2);
        if (magic[0] != 0x4D || magic[1] != 0x5A)  // MZ
        {
            throw new InvalidOperationException("Not a PE file");
        }

        // Get PE offset
        byte[] peOffsetBytes = _hexEditor.GetBytes(0x3C, 4);
        int peOffset = BitConverter.ToInt32(peOffsetBytes, 0);

        // DOS stub is between 0x40 and PE header
        int dosStubStart = 0x40;
        int dosStubLength = peOffset - dosStubStart;

        if (dosStubLength > 0)
        {
            // Delete DOS stub
            _hexEditor.DeleteBytes(dosStubStart, dosStubLength);

            Console.WriteLine($"Deleted {dosStubLength}-byte DOS stub");

            // Fix PE offset (now points to wrong location)
            int newPEOffset = dosStubStart;
            byte[] newOffsetBytes = BitConverter.GetBytes(newPEOffset);
            _hexEditor.ModifyBytes(0x3C, newOffsetBytes);

            Console.WriteLine($"Updated PE offset: 0x{newPEOffset:X}");
        }
    }

    // Delete specific section from file
    public void DeleteSection(string sectionName)
    {
        // Find section header
        long sectionHeaderPos = FindSectionHeader(sectionName);
        if (sectionHeaderPos < 0)
        {
            throw new InvalidOperationException($"Section '{sectionName}' not found");
        }

        // Read section info
        byte[] offsetBytes = _hexEditor.GetBytes(sectionHeaderPos + 20, 4);
        byte[] sizeBytes = _hexEditor.GetBytes(sectionHeaderPos + 16, 4);

        int sectionOffset = BitConverter.ToInt32(offsetBytes, 0);
        int sectionSize = BitConverter.ToInt32(sizeBytes, 0);

        // Delete section data
        _hexEditor.DeleteBytes(sectionOffset, sectionSize);

        // Delete section header (40 bytes)
        _hexEditor.DeleteBytes(sectionHeaderPos, 40);

        Console.WriteLine($"Deleted section '{sectionName}' ({sectionSize} bytes)");
    }

    private long FindSectionHeader(string name)
    {
        // Implementation to find section header by name
        // (simplified for example)
        return -1;
    }
}
```

---

## 💡 Use Cases

### 1. Remove Padding Bytes

```csharp
// Remove null byte padding from file
private void RemovePadding()
{
    // Find end of actual data (last non-zero byte)
    long lastDataPos = -1;

    for (long i = hexEditor.Length - 1; i >= 0; i--)
    {
        if (hexEditor.GetByte(i) != 0x00)
        {
            lastDataPos = i;
            break;
        }
    }

    if (lastDataPos < 0)
    {
        MessageBox.Show("File is entirely null bytes!", "Error");
        return;
    }

    // Calculate padding length
    long paddingLength = hexEditor.Length - lastDataPos - 1;

    if (paddingLength > 0)
    {
        // Delete padding
        hexEditor.DeleteBytes(lastDataPos + 1, (int)paddingLength);

        MessageBox.Show(
            $"Removed {paddingLength} padding bytes\n" +
            $"New file size: {hexEditor.Length} bytes",
            "Success");
    }
    else
    {
        MessageBox.Show("No padding found", "Info");
    }
}
```

---

### 2. Strip Metadata (EXIF from Image)

```csharp
// Remove EXIF metadata from JPEG image
private void StripJPEGMetadata()
{
    // Verify JPEG signature
    byte[] signature = hexEditor.GetBytes(0, 2);
    if (signature[0] != 0xFF || signature[1] != 0xD8)
    {
        MessageBox.Show("Not a JPEG file", "Error");
        return;
    }

    long position = 2;  // Skip SOI marker

    while (position < hexEditor.Length)
    {
        // Read marker
        byte markerFF = hexEditor.GetByte(position);
        byte markerType = hexEditor.GetByte(position + 1);

        if (markerFF != 0xFF)
            break;

        // Check if APP1 (EXIF) marker
        if (markerType == 0xE1)
        {
            // Read segment length
            byte[] lengthBytes = hexEditor.GetBytes(position + 2, 2);
            int segmentLength = (lengthBytes[0] << 8) | lengthBytes[1];

            // Delete entire APP1 segment (marker + length + data)
            hexEditor.DeleteBytes(position, segmentLength + 2);

            Console.WriteLine($"Deleted {segmentLength + 2}-byte EXIF segment");
            break;
        }

        // Move to next segment
        byte[] len = hexEditor.GetBytes(position + 2, 2);
        int length = (len[0] << 8) | len[1];
        position += length + 2;
    }

    hexEditor.Save();
    MessageBox.Show("EXIF metadata removed", "Success");
}
```

---

### 3. Remove Virus/Malware Signature

```csharp
// Remove known malware signature from infected file
public class MalwareRemover
{
    private HexEditor _hexEditor;

    // Known malware signatures
    private Dictionary<string, byte[]> malwareSignatures = new()
    {
        { "Trojan.Generic", new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 } },
        { "Backdoor.Agent", new byte[] { 0xCA, 0xFE, 0xBA, 0xBE, 0xDE, 0xAD, 0xBE, 0xEF } },
        // Add more signatures...
    };

    public MalwareRemover(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public int ScanAndRemove()
    {
        int removedCount = 0;

        _hexEditor.BeginBatch();

        try
        {
            foreach (var (malwareName, signature) in malwareSignatures)
            {
                // Find all occurrences
                var positions = _hexEditor.FindAll(signature);

                if (positions.Count > 0)
                {
                    Console.WriteLine($"Found {positions.Count} instances of {malwareName}");

                    // Delete in reverse order
                    for (int i = positions.Count - 1; i >= 0; i--)
                    {
                        _hexEditor.DeleteBytes(positions[i], signature.Length);
                        removedCount++;
                    }

                    Console.WriteLine($"Removed {positions.Count} instances of {malwareName}");
                }
            }
        }
        finally
        {
            _hexEditor.EndBatch();
        }

        if (removedCount > 0)
        {
            _hexEditor.Save();
        }

        return removedCount;
    }
}

// Usage
var remover = new MalwareRemover(hexEditor);
int removed = remover.ScanAndRemove();
if (removed > 0)
{
    MessageBox.Show($"Removed {removed} malware signatures", "Success");
}
else
{
    MessageBox.Show("No malware signatures found", "Clean");
}
```

---

### 4. Truncate File to Specific Size

```csharp
// Truncate file to exact size
private void TruncateFile(long targetSize)
{
    if (targetSize < 0)
    {
        MessageBox.Show("Target size must be positive", "Error");
        return;
    }

    long currentSize = hexEditor.Length;

    if (currentSize <= targetSize)
    {
        MessageBox.Show($"File is already {currentSize} bytes (≤ target)", "Info");
        return;
    }

    // Calculate bytes to remove
    long bytesToRemove = currentSize - targetSize;

    // Confirm truncation
    var result = MessageBox.Show(
        $"Truncate file from {currentSize:N0} to {targetSize:N0} bytes?\n" +
        $"This will delete the last {bytesToRemove:N0} bytes.\n\n" +
        $"This operation cannot be undone after saving!",
        "Confirm Truncation",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning);

    if (result == MessageBoxResult.Yes)
    {
        // Delete bytes from end
        hexEditor.DeleteBytes(targetSize, (int)bytesToRemove);

        Console.WriteLine($"Truncated file: {currentSize} → {targetSize} bytes");

        // Save immediately
        hexEditor.Save();

        MessageBox.Show(
            $"File truncated successfully\n" +
            $"New size: {hexEditor.Length:N0} bytes",
            "Success");
    }
}
```

---

## ⚡ Performance Tips

### Delete Multiple Bytes at Once

```csharp
// Slow: Delete bytes one by one
for (int i = 0; i < 1000; i++)
{
    hexEditor.DeleteByte(position);  // 1000 calls, 1000 UI updates!
}
// Time: ~5000ms

// Fast: Delete all at once
hexEditor.DeleteBytes(position, 1000);  // 1 call, 1 UI update!
// Time: ~50ms (100x faster!)
```

### Delete in Reverse Order for Multiple Ranges

```csharp
// When deleting multiple ranges, delete from END to START
// This preserves earlier positions

List<(long pos, int count)> rangesToDelete = new()
{
    (100, 10),
    (500, 20),
    (1000, 30)
};

// Sort by position descending
rangesToDelete = rangesToDelete.OrderByDescending(r => r.pos).ToList();

hexEditor.BeginBatch();

// Delete from end to start
foreach (var (pos, count) in rangesToDelete)
{
    hexEditor.DeleteBytes(pos, count);
}

hexEditor.EndBatch();
```

---

## ⚠️ Important Notes

### File Length Decreases

- DeleteBytes **always decreases file length**
- Deleted bytes are tracked until Save() is called
- Subsequent bytes shift backward to fill gap

### Position After Deletion

```csharp
// Before: Length = 1000
// Delete 10 bytes at position 100
hexEditor.DeleteBytes(100, 10);
// After: Length = 990
// Position 100 now contains what was at position 110
```

### Position Validation

- Position must be: `0 <= position < Length`
- Count must be: `position + count <= Length`
- Out-of-range throws `ArgumentOutOfRangeException`

```csharp
// Valid deletions
hexEditor.DeleteBytes(0, 10);              // Delete first 10 bytes
hexEditor.DeleteBytes(hexEditor.Length - 10, 10);  // Delete last 10 bytes

// Invalid
hexEditor.DeleteBytes(-1, 10);             // ❌ Exception (negative position)
hexEditor.DeleteBytes(0, hexEditor.Length + 1);    // ❌ Exception (count too large)
hexEditor.DeleteBytes(hexEditor.Length, 1);        // ❌ Exception (position out of range)
```

### Virtual View Until Save

- Deleted bytes are **marked for deletion** but still accessible until Save()
- Use `GetByte()` on deleted position returns the byte (until Save)
- Save() physically removes deleted bytes from file

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread
- Use `Dispatcher.Invoke()` if calling from background thread

---

## 🔗 See Also

- **[InsertByte()](insertbyte.md)** - Insert new byte (increases length)
- **[ModifyByte()](modifybyte.md)** - Change existing byte (length unchanged)
- **[GetByte()](getbyte.md)** - Read byte value at position
- **[ClearDeletions()](../editing/cleardeletions.md)** - Clear all pending deletions
- **[BeginBatch() / EndBatch()](../core/beginbatch.md)** - Batch operations for performance

---

**Last Updated**: 2026-02-19
**Version**: V2.0
