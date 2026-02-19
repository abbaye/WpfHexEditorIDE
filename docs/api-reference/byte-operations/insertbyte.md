# InsertByte()

Insert a new byte at a specific position, increasing file length by 1.

---

## 📋 Description

The `InsertByte()` method inserts a new byte at the specified position, shifting all subsequent bytes forward. This is an **insertion** (not modification), so the file length increases by 1 byte. The operation follows **LIFO (Last-In-First-Out)** semantics when multiple insertions occur at the same position.

**Key characteristics**:
- ✅ File length increases by 1
- ✅ Fully undoable (Undo/Redo)
- ✅ Tracked as insertion (not modification)
- ✅ Visual feedback (byte appears in green by default)
- ✅ LIFO semantics (last insertion appears first)
- ⚡ Fast (O(1) for insertion tracking)

**LIFO Behavior**: When inserting multiple bytes at the same position, the **last inserted byte appears first** in the final sequence.

---

## 📝 Signatures

```csharp
// Insert single byte
public void InsertByte(byte value, long position)

// Insert multiple bytes (batch)
public void InsertBytes(long position, byte[] values)
```

**Since:** V1.0

---

## ⚙️ Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `value` | `byte` | Byte value to insert (0x00 to 0xFF) |
| `position` | `long` | Position in file (0-based) |
| `values` | `byte[]` | Array of values to insert (for InsertBytes) |

---

## 🔄 Returns

| Method | Return Type | Description |
|--------|-------------|-------------|
| `InsertByte()` | `void` | No return value |
| `InsertBytes()` | `void` | No return value |

---

## 🎯 Examples

### Example 1: Basic Insertion

```csharp
using WpfHexaEditor;

// Open file
hexEditor.FileName = "data.bin";
Console.WriteLine($"Original length: {hexEditor.Length}");

// Insert byte 0xFF at position 0x100
hexEditor.InsertByte(0xFF, 0x100);

Console.WriteLine($"New length: {hexEditor.Length}");  // +1 byte
```

**Before**:
```
00000100: 41 42 43 44  ABC
```

**After**:
```
00000100: FF 41 42 43 44  .ABCD
         ↑ Inserted
```

---

### Example 2: Enable Insert Mode

```csharp
private void ConfigureInsertMode()
{
    // Enable insert mode
    hexEditor.AllowInsertMode = true;

    // Check current mode
    if (hexEditor.IsInInsertMode)
    {
        Console.WriteLine("Insert mode active");
        statusLabel.Text = "Mode: INSERT";
    }
    else
    {
        Console.WriteLine("Overwrite mode active");
        statusLabel.Text = "Mode: OVERWRITE";
    }

    // Toggle with keyboard shortcut
    hexEditor.KeyDown += (s, e) =>
    {
        if (e.Key == Key.Insert)
        {
            hexEditor.ToggleInsertMode();
            e.Handled = true;
        }
    };
}
```

---

### Example 3: LIFO Insertion Semantics

```csharp
// Demonstrate LIFO (Last-In-First-Out) behavior
private void DemoLIFOInsertion()
{
    // Open file with initial content: [41 42 43] = "ABC"
    hexEditor.OpenMemory(new byte[] { 0x41, 0x42, 0x43 });

    Console.WriteLine("Original: " + GetBytesAsString(0, 3));  // "41 42 43"

    // Insert three bytes at position 1
    hexEditor.InsertByte(0x58, 1);  // Insert X
    hexEditor.InsertByte(0x59, 1);  // Insert Y
    hexEditor.InsertByte(0x5A, 1);  // Insert Z

    // Result: A Z Y X B C (LIFO order!)
    Console.WriteLine("After LIFO inserts: " + GetBytesAsString(0, 6));
    // Output: "41 5A 59 58 42 43" = "AZYXBC"
    //              ↑  ↑  ↑
    //              3rd 2nd 1st inserted
}

private string GetBytesAsString(long pos, int count)
{
    byte[] bytes = hexEditor.GetBytes(pos, count);
    return BitConverter.ToString(bytes).Replace("-", " ");
}
```

**Visual LIFO Explanation**:
```
Original:  [A] [B] [C]
           Position 1 ↑

Step 1: Insert X at pos 1
Result:    [A] [X] [B] [C]

Step 2: Insert Y at pos 1 (pushes X forward)
Result:    [A] [Y] [X] [B] [C]

Step 3: Insert Z at pos 1 (pushes Y and X forward)
Result:    [A] [Z] [Y] [X] [B] [C]

Final: A Z Y X B C (reverse of insertion order)
```

---

### Example 4: Insert with Undo Support

```csharp
private void InsertWithUndo()
{
    long position = 0x100;
    byte insertValue = 0xFF;

    // Insert byte (automatically recorded in undo stack)
    hexEditor.InsertByte(insertValue, position);

    Console.WriteLine($"Inserted 0x{insertValue:X2} at 0x{position:X}");
    Console.WriteLine($"New length: {hexEditor.Length}");
    Console.WriteLine($"Can undo: {hexEditor.CanUndo}");  // True

    // User can undo
    if (hexEditor.CanUndo)
    {
        hexEditor.Undo();
        Console.WriteLine($"After undo length: {hexEditor.Length}");  // Back to original
    }

    // User can redo
    if (hexEditor.CanRedo)
    {
        hexEditor.Redo();
        Console.WriteLine($"After redo length: {hexEditor.Length}");  // Insertion restored
    }
}
```

---

### Example 5: Insert Multiple Bytes Efficiently

```csharp
private void InsertMultipleBytes()
{
    // Method 1: InsertBytes (efficient - single operation)
    byte[] data = { 0xDE, 0xAD, 0xBE, 0xEF };
    hexEditor.InsertBytes(0x1000, data);
    Console.WriteLine("Inserted 4 bytes efficiently");

    // Method 2: Multiple InsertByte calls (less efficient, LIFO order)
    // Note: Results in REVERSE order due to LIFO!
    hexEditor.BeginBatch();
    foreach (byte b in data)
    {
        hexEditor.InsertByte(b, 0x2000);
    }
    hexEditor.EndBatch();

    // Result at 0x2000: EF BE AD DE (reversed!)
    // To insert in correct order with InsertByte, reverse the array first:
    Array.Reverse(data);
    hexEditor.BeginBatch();
    foreach (byte b in data)
    {
        hexEditor.InsertByte(b, 0x3000);
    }
    hexEditor.EndBatch();
    // Result at 0x3000: DE AD BE EF (correct order)
}
```

**Performance Comparison**:
```csharp
// Slow: Individual insertions (4 separate operations)
hexEditor.InsertByte(0xDE, 0x1000);
hexEditor.InsertByte(0xAD, 0x1001);
hexEditor.InsertByte(0xBE, 0x1002);
hexEditor.InsertByte(0xEF, 0x1003);
// Time: ~40ms, 4 UI updates

// Fast: Batch insertion (1 operation)
byte[] data = { 0xDE, 0xAD, 0xBE, 0xEF };
hexEditor.InsertBytes(0x1000, data);
// Time: ~10ms, 1 UI update (4x faster!)
```

---

### Example 6: Insert Structured Data

```csharp
// Insert a file header structure
public class FileHeader
{
    public byte[] Magic = { 0x4D, 0x5A };  // MZ
    public ushort Version = 0x0100;        // 1.0
    public uint DataOffset = 0x00001000;   // 4096 bytes
}

private void InsertFileHeader(long position)
{
    var header = new FileHeader();

    // Build header bytes
    var headerBytes = new List<byte>();

    // Magic (2 bytes)
    headerBytes.AddRange(header.Magic);

    // Version (2 bytes, little-endian)
    headerBytes.Add((byte)(header.Version & 0xFF));
    headerBytes.Add((byte)((header.Version >> 8) & 0xFF));

    // Data offset (4 bytes, little-endian)
    headerBytes.AddRange(BitConverter.GetBytes(header.DataOffset));

    // Insert entire header at once
    hexEditor.InsertBytes(position, headerBytes.ToArray());

    Console.WriteLine($"Inserted {headerBytes.Count}-byte header at 0x{position:X}");
}

// Usage
InsertFileHeader(0);  // Insert at file beginning
```

---

### Example 7: Insert with Validation

```csharp
private bool SafeInsertByte(long position, byte value)
{
    // Validate position (can insert at any valid position including Length)
    if (position < 0 || position > hexEditor.Length)
    {
        MessageBox.Show($"Position 0x{position:X} is out of range", "Error");
        return false;
    }

    // Check if insert mode is allowed
    if (!hexEditor.AllowInsertMode)
    {
        MessageBox.Show("Insert mode is not enabled", "Error");
        return false;
    }

    // Check if file is writable
    if (hexEditor.ReadOnlyMode || !hexEditor.CanWrite)
    {
        MessageBox.Show("File is read-only", "Error");
        return false;
    }

    // Perform insertion
    try
    {
        long oldLength = hexEditor.Length;
        hexEditor.InsertByte(value, position);
        long newLength = hexEditor.Length;

        // Log change
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] " +
                         $"Inserted 0x{value:X2} at 0x{position:X}");
        Console.WriteLine($"Length: {oldLength} → {newLength} (+1)");

        return true;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error inserting byte: {ex.Message}", "Error");
        return false;
    }
}
```

---

### Example 8: Interactive Insertion Dialog

```csharp
private void ShowInsertDialog()
{
    long currentPosition = hexEditor.Position;

    var dialog = new InsertDialog
    {
        Title = "Insert Byte",
        Position = currentPosition
    };

    if (dialog.ShowDialog() == true)
    {
        // Get insertion parameters
        long position = dialog.Position;
        byte value = dialog.Value;
        int count = dialog.Count;  // Repeat count

        // Insert byte(s)
        hexEditor.BeginBatch();

        try
        {
            if (count == 1)
            {
                // Single insertion
                hexEditor.InsertByte(value, position);
            }
            else
            {
                // Multiple insertions (fill with same value)
                byte[] values = Enumerable.Repeat(value, count).ToArray();
                hexEditor.InsertBytes(position, values);
            }

            MessageBox.Show(
                $"Inserted {count} byte(s) at 0x{position:X}\n" +
                $"New file length: {hexEditor.Length}",
                "Success");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error");
        }
        finally
        {
            hexEditor.EndBatch();
        }
    }
}
```

---

## 💡 Use Cases

### 1. Add File Header

```csharp
// Add missing header to corrupted file
private void AddFileHeader()
{
    // Define PNG header
    byte[] pngHeader = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47,  // PNG signature
        0x0D, 0x0A, 0x1A, 0x0A   // DOS line ending + EOF + DOS line ending
    };

    // Insert header at beginning
    hexEditor.InsertBytes(0, pngHeader);
    hexEditor.Save();

    Console.WriteLine("PNG header added successfully");
}
```

---

### 2. Inject Code (NOP Slide)

```csharp
// Insert NOP sled in executable for shellcode injection
private void InsertNOPSled(long position, int sledLength)
{
    // Create NOP sled (0x90 = NOP instruction)
    byte[] nops = Enumerable.Repeat((byte)0x90, sledLength).ToArray();

    // Insert NOP sled
    hexEditor.InsertBytes(position, nops);

    // Add highlight to visualize
    hexEditor.AddHighlight(position, sledLength, Colors.Yellow, "NOP Sled");

    Console.WriteLine($"Inserted {sledLength}-byte NOP sled at 0x{position:X}");
}

// Usage: Insert 16 NOPs at offset 0x1000
InsertNOPSled(0x1000, 16);
```

---

### 3. Add Padding Bytes

```csharp
// Add padding to align data to specific boundary
private void AlignToboundary(long position, int alignment)
{
    // Calculate padding needed
    int currentMod = (int)(position % alignment);
    if (currentMod == 0)
    {
        Console.WriteLine("Already aligned");
        return;
    }

    int paddingNeeded = alignment - currentMod;

    // Insert padding bytes (0x00)
    byte[] padding = new byte[paddingNeeded];
    hexEditor.InsertBytes(position, padding);

    Console.WriteLine($"Added {paddingNeeded} padding bytes to align to {alignment}-byte boundary");
}

// Usage: Align position 0x1005 to 16-byte boundary
// Position 0x1005 % 16 = 5, so need 11 bytes padding
AlignTooundary(0x1005, 16);  // Insert 11 null bytes
```

---

### 4. Insert Patch Signature

```csharp
// Insert patch signature to mark modified file
public class PatchSignature
{
    public byte[] Magic = { 0x50, 0x41, 0x54, 0x43, 0x48 };  // "PATCH"
    public byte Version = 0x01;
    public uint Timestamp;
    public byte[] PatcherName;

    public PatchSignature(string patcherName)
    {
        Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        PatcherName = Encoding.ASCII.GetBytes(patcherName.PadRight(16).Substring(0, 16));
    }

    public byte[] ToBytes()
    {
        var bytes = new List<byte>();
        bytes.AddRange(Magic);
        bytes.Add(Version);
        bytes.AddRange(BitConverter.GetBytes(Timestamp));
        bytes.AddRange(PatcherName);
        return bytes.ToArray();
    }
}

private void InsertPatchSignature(string patcherName)
{
    // Create signature
    var signature = new PatchSignature(patcherName);
    byte[] signatureBytes = signature.ToBytes();

    // Insert at end of file
    hexEditor.InsertBytes(hexEditor.Length, signatureBytes);

    Console.WriteLine($"Inserted {signatureBytes.Length}-byte patch signature");
    Console.WriteLine($"Patcher: {patcherName}");
    Console.WriteLine($"Timestamp: {signature.Timestamp}");
}
```

---

## ⚡ Performance Tips

### Use InsertBytes for Multiple Bytes

```csharp
// Slow: Individual insertions
for (int i = 0; i < 1000; i++)
{
    hexEditor.InsertByte(0xFF, position + i);  // Wrong! LIFO will reverse order
}
// Time: ~3000ms
// Result: Reversed order due to LIFO!

// Fast: Batch insertion
byte[] values = Enumerable.Repeat((byte)0xFF, 1000).ToArray();
hexEditor.InsertBytes(position, values);  // 1 call
// Time: ~100ms (30x faster!)
// Result: Correct order!
```

### Use BeginBatch/EndBatch for Complex Operations

```csharp
// Insert multiple non-contiguous insertions
hexEditor.BeginBatch();

hexEditor.InsertByte(0xFF, 0x1000);
hexEditor.InsertByte(0xAA, 0x2000);
hexEditor.InsertByte(0xBB, 0x3000);

hexEditor.EndBatch();  // Single UI update!
```

---

## ⚠️ Important Notes

### LIFO Semantics

**Critical**: Multiple insertions at the **same position** follow LIFO order:

```csharp
// Insert A, B, C at position 100
hexEditor.InsertByte(0x41, 100);  // A
hexEditor.InsertByte(0x42, 100);  // B (pushes A forward)
hexEditor.InsertByte(0x43, 100);  // C (pushes B and A forward)

// Result at position 100: C B A (reversed!)
// Position 100: 0x43 (C) - last inserted
// Position 101: 0x42 (B) - second inserted
// Position 102: 0x41 (A) - first inserted
```

**To insert in correct order**, use `InsertBytes` or insert at incrementing positions:

```csharp
// Method 1: InsertBytes (recommended)
byte[] data = { 0x41, 0x42, 0x43 };
hexEditor.InsertBytes(100, data);  // Result: A B C (correct!)

// Method 2: Insert at incrementing positions
hexEditor.InsertByte(0x41, 100);  // A at 100
hexEditor.InsertByte(0x42, 101);  // B at 101
hexEditor.InsertByte(0x43, 102);  // C at 102
// Result: A B C (correct!)
```

---

### File Length Increases

- InsertByte **always increases file length by 1**
- To replace bytes, use `ModifyByte()` instead
- Insertion affects all subsequent positions (they shift forward)

---

### Position Validation

- Position must be: `0 <= position <= Length` (note: can insert at Length!)
- Out-of-range position throws `ArgumentOutOfRangeException`
- Inserting at `Length` appends to file

```csharp
// Valid insertions
hexEditor.InsertByte(0xFF, 0);              // Insert at beginning
hexEditor.InsertByte(0xFF, hexEditor.Length);  // Append at end
hexEditor.InsertByte(0xFF, 100);            // Insert in middle

// Invalid
hexEditor.InsertByte(0xFF, -1);             // ❌ Exception
hexEditor.InsertByte(0xFF, hexEditor.Length + 1);  // ❌ Exception
```

---

### Insert Mode Must Be Enabled

```csharp
// Enable insert mode
hexEditor.AllowInsertMode = true;

// Check before inserting
if (!hexEditor.AllowInsertMode)
{
    MessageBox.Show("Insert mode not allowed");
    return;
}
```

---

### Undo/Redo

- Each `InsertByte()` creates undo entry
- Use `BeginBatch()` / `EndBatch()` to group insertions into single undo unit
- Undo history is unlimited (configurable)

---

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread
- Use `Dispatcher.Invoke()` if calling from background thread

---

## 🔗 See Also

- **[ModifyByte()](modifybyte.md)** - Change existing byte (length unchanged)
- **[DeleteBytes()](deletebytes.md)** - Remove bytes (decreases length)
- **[GetByte()](getbyte.md)** - Read byte value at position
- **[InsertBytes()](../editing/insertbytes.md)** - Insert multiple bytes efficiently
- **[BeginBatch() / EndBatch()](../core/beginbatch.md)** - Batch operations for performance

---

**Last Updated**: 2026-02-19
**Version**: V2.0
