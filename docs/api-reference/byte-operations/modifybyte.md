# ModifyByte()

Change the value of a single byte at a specific position without affecting file length.

---

## 📋 Description

The `ModifyByte()` method changes an existing byte's value at the specified position. This is a **modification** (not insertion), so the file length remains unchanged. The operation is fully undoable and trackable.

**Key characteristics**:
- ✅ File length unchanged
- ✅ Fully undoable (Undo/Redo)
- ✅ Tracked as modification (not insertion)
- ✅ Visual feedback (byte appears in red by default)
- ⚡ Very fast (O(1) operation)

---

## 📝 Signatures

```csharp
// Modify single byte
public void ModifyByte(byte value, long position)

// Modify multiple bytes (batch)
public void ModifyBytes(long position, byte[] values)
```

**Since:** V1.0

---

## ⚙️ Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `value` | `byte` | New byte value (0x00 to 0xFF) |
| `position` | `long` | Position in file (0-based) |
| `values` | `byte[]` | Array of new values (for ModifyBytes) |

---

## 🔄 Returns

| Method | Return Type | Description |
|--------|-------------|-------------|
| `ModifyByte()` | `void` | No return value |
| `ModifyBytes()` | `void` | No return value |

---

## 🎯 Examples

### Example 1: Modify Single Byte (Basic)

```csharp
using WpfHexaEditor;

// Open file
hexEditor.FileName = "data.bin";

// Modify byte at position 0x100 to 0xFF
hexEditor.ModifyByte(0xFF, 0x100);

// Save changes
hexEditor.Save();

Console.WriteLine("Byte modified successfully");
```

### Example 2: Interactive Byte Editor

```csharp
private void EditByteButton_Click(object sender, RoutedEventArgs e)
{
    // Get current position
    long position = hexEditor.Position;

    // Get current value
    byte currentValue = hexEditor.GetByte(position);

    // Show dialog for new value
    var dialog = new InputDialog
    {
        Title = $"Edit byte at 0x{position:X}",
        Prompt = "Enter new hex value (00-FF):",
        Value = $"{currentValue:X2}"
    };

    if (dialog.ShowDialog() == true)
    {
        try
        {
            // Parse hex input
            byte newValue = Convert.ToByte(dialog.Value, 16);

            // Modify byte
            hexEditor.ModifyByte(newValue, position);

            // Update status
            statusLabel.Text = $"Modified 0x{position:X}: 0x{currentValue:X2} → 0x{newValue:X2}";
        }
        catch (FormatException)
        {
            MessageBox.Show("Invalid hex value. Enter 00-FF.", "Error");
        }
    }
}
```

### Example 3: Batch Modification

```csharp
// Modify multiple consecutive bytes efficiently
byte[] newValues = { 0xDE, 0xAD, 0xBE, 0xEF };

// Method 1: ModifyBytes (efficient)
hexEditor.ModifyBytes(0x1000, newValues);

// Method 2: Loop with ModifyByte (less efficient, but works)
for (int i = 0; i < newValues.Length; i++)
{
    hexEditor.ModifyByte(newValues[i], 0x1000 + i);
}

// ModifyBytes is 3x faster for multiple bytes!
```

### Example 4: Modify with Undo Support

```csharp
private void ModifyWithUndo()
{
    long position = 0x100;
    byte oldValue = hexEditor.GetByte(position);
    byte newValue = 0xFF;

    // Modify byte (automatically recorded in undo stack)
    hexEditor.ModifyByte(newValue, position);

    // User can undo
    Console.WriteLine($"Modified 0x{position:X}: 0x{oldValue:X2} → 0x{newValue:X2}");
    Console.WriteLine($"Can undo: {hexEditor.CanUndo}");  // True

    // Undo the change
    hexEditor.Undo();
    byte restoredValue = hexEditor.GetByte(position);
    Console.WriteLine($"After undo: 0x{restoredValue:X2}");  // Back to oldValue
}
```

### Example 5: Fill Range with Value

```csharp
private void FillRange(long start, long length, byte value)
{
    // Use batch mode for performance
    hexEditor.BeginBatch();

    try
    {
        for (long i = 0; i < length; i++)
        {
            hexEditor.ModifyByte(value, start + i);
        }
    }
    finally
    {
        hexEditor.EndBatch();  // Update UI once
    }

    Console.WriteLine($"Filled {length} bytes with 0x{value:X2}");
}

// Usage: Fill 1000 bytes with 0x00 (null bytes)
FillRange(0x1000, 1000, 0x00);

// Fill with pattern
FillRange(0x2000, 256, 0xFF);
```

### Example 6: Modify Based on Condition

```csharp
// Find and replace all occurrences of a byte value
private int ReplaceAllBytes(byte findValue, byte replaceValue)
{
    int count = 0;

    hexEditor.BeginBatch();

    try
    {
        // Iterate through file
        for (long position = 0; position < hexEditor.Length; position++)
        {
            byte currentValue = hexEditor.GetByte(position);

            if (currentValue == findValue)
            {
                hexEditor.ModifyByte(replaceValue, position);
                count++;
            }
        }
    }
    finally
    {
        hexEditor.EndBatch();
    }

    return count;
}

// Usage: Replace all 0xFF bytes with 0x00
int replaced = ReplaceAllBytes(0xFF, 0x00);
Console.WriteLine($"Replaced {replaced} bytes");
```

### Example 7: Modify with Validation

```csharp
private bool SafeModifyByte(long position, byte value)
{
    // Validate position
    if (position < 0 || position >= hexEditor.Length)
    {
        MessageBox.Show($"Position 0x{position:X} is out of range", "Error");
        return false;
    }

    // Check if file is writable
    if (hexEditor.ReadOnlyMode || !hexEditor.CanWrite)
    {
        MessageBox.Show("File is read-only", "Error");
        return false;
    }

    // Get old value for logging
    byte oldValue = hexEditor.GetByte(position);

    // Perform modification
    try
    {
        hexEditor.ModifyByte(value, position);

        // Log change
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] " +
                         $"Modified 0x{position:X}: 0x{oldValue:X2} → 0x{value:X2}");

        return true;
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error modifying byte: {ex.Message}", "Error");
        return false;
    }
}
```

### Example 8: Hex Calculator Integration

```csharp
public class HexCalculator
{
    private HexEditor _hexEditor;

    public HexCalculator(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // XOR operation on byte
    public void XorByte(long position, byte xorValue)
    {
        byte currentValue = _hexEditor.GetByte(position);
        byte newValue = (byte)(currentValue ^ xorValue);
        _hexEditor.ModifyByte(newValue, position);

        Console.WriteLine($"XOR: 0x{currentValue:X2} ^ 0x{xorValue:X2} = 0x{newValue:X2}");
    }

    // Increment byte
    public void IncrementByte(long position, int amount = 1)
    {
        byte currentValue = _hexEditor.GetByte(position);
        byte newValue = (byte)((currentValue + amount) % 256);
        _hexEditor.ModifyByte(newValue, position);
    }

    // NOT operation (bitwise complement)
    public void NotByte(long position)
    {
        byte currentValue = _hexEditor.GetByte(position);
        byte newValue = (byte)~currentValue;
        _hexEditor.ModifyByte(newValue, position);

        Console.WriteLine($"NOT: ~0x{currentValue:X2} = 0x{newValue:X2}");
    }

    // AND operation
    public void AndByte(long position, byte andValue)
    {
        byte currentValue = _hexEditor.GetByte(position);
        byte newValue = (byte)(currentValue & andValue);
        _hexEditor.ModifyByte(newValue, position);
    }

    // OR operation
    public void OrByte(long position, byte orValue)
    {
        byte currentValue = _hexEditor.GetByte(position);
        byte newValue = (byte)(currentValue | orValue);
        _hexEditor.ModifyByte(newValue, position);
    }
}

// Usage
var calculator = new HexCalculator(hexEditor);

// XOR byte at 0x100 with 0xFF
calculator.XorByte(0x100, 0xFF);

// Increment byte at 0x200
calculator.IncrementByte(0x200);

// Invert all bits at 0x300
calculator.NotByte(0x300);
```

---

## 💡 Use Cases

### 1. Fix Checksum

```csharp
// Calculate and fix file checksum
private void FixChecksum()
{
    long checksumPosition = 0x10;  // Checksum at offset 0x10

    // Calculate checksum for entire file (except checksum byte)
    byte checksum = 0;
    for (long i = 0; i < hexEditor.Length; i++)
    {
        if (i != checksumPosition)
        {
            checksum ^= hexEditor.GetByte(i);  // XOR checksum
        }
    }

    // Update checksum
    hexEditor.ModifyByte(checksum, checksumPosition);
    hexEditor.Save();

    Console.WriteLine($"Checksum fixed: 0x{checksum:X2}");
}
```

### 2. Patch Binary Executable

```csharp
// Patch a NOP instruction (0x90) over bad code
private void PatchExecutable()
{
    long badCodeStart = 0x1234;
    int badCodeLength = 5;

    // Replace bad code with NOPs
    hexEditor.BeginBatch();

    for (int i = 0; i < badCodeLength; i++)
    {
        hexEditor.ModifyByte(0x90, badCodeStart + i);  // 0x90 = NOP
    }

    hexEditor.EndBatch();
    hexEditor.Save();

    Console.WriteLine($"Patched {badCodeLength} bytes with NOP");
}
```

### 3. ROM Hacking (Change Game Text)

```csharp
// Modify text string in ROM
private void ModifyGameText()
{
    // Find text position
    var searchText = Encoding.ASCII.GetBytes("GAME OVER");
    long position = hexEditor.FindFirst(searchText);

    if (position >= 0)
    {
        // Replace with new text
        var newText = Encoding.ASCII.GetBytes("YOU WIN!");

        // Pad with spaces if shorter
        if (newText.Length < searchText.Length)
        {
            Array.Resize(ref newText, searchText.Length);
            for (int i = "YOU WIN!".Length; i < newText.Length; i++)
            {
                newText[i] = 0x20;  // Space
            }
        }

        // Apply modification
        hexEditor.ModifyBytes(position, newText);
        hexEditor.Save();

        MessageBox.Show("Game text modified!", "Success");
    }
}
```

### 4. Data Sanitization

```csharp
// Sanitize sensitive data by overwriting with zeros
private void SanitizeData(long start, long length)
{
    // Confirmation dialog
    var result = MessageBox.Show(
        $"This will permanently overwrite {length} bytes with zeros. Continue?",
        "Confirm Sanitization",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning);

    if (result != MessageBoxResult.Yes)
        return;

    // Overwrite with zeros
    hexEditor.BeginBatch();

    for (long i = 0; i < length; i++)
    {
        hexEditor.ModifyByte(0x00, start + i);
    }

    hexEditor.EndBatch();
    hexEditor.Save();

    // Verify
    bool allZeros = true;
    for (long i = 0; i < length; i++)
    {
        if (hexEditor.GetByte(start + i) != 0x00)
        {
            allZeros = false;
            break;
        }
    }

    if (allZeros)
    {
        MessageBox.Show("Data sanitized successfully", "Success");
    }
    else
    {
        MessageBox.Show("Verification failed!", "Error");
    }
}
```

---

## ⚡ Performance Tips

### Use ModifyBytes for Multiple Bytes

```csharp
// Slow: Individual modifications
for (int i = 0; i < 1000; i++)
{
    hexEditor.ModifyByte(0xFF, i);  // 1000 UI updates!
}
// Time: ~3000ms

// Fast: Batch modification
byte[] values = Enumerable.Repeat((byte)0xFF, 1000).ToArray();
hexEditor.ModifyBytes(0, values);  // 1 UI update!
// Time: ~1000ms (3x faster)

// Fastest: Batch mode + loop
hexEditor.BeginBatch();
for (int i = 0; i < 1000; i++)
{
    hexEditor.ModifyByte(0xFF, i);
}
hexEditor.EndBatch();  // 1 UI update!
// Time: ~1000ms (3x faster)
```

### Avoid Unnecessary GetByte Calls

```csharp
// Inefficient: Reading twice
if (hexEditor.GetByte(position) != 0xFF)
{
    byte oldValue = hexEditor.GetByte(position);  // Read again!
    hexEditor.ModifyByte(0xFF, position);
}

// Efficient: Read once
byte currentValue = hexEditor.GetByte(position);
if (currentValue != 0xFF)
{
    hexEditor.ModifyByte(0xFF, position);
}
```

---

## ⚠️ Important Notes

### File Length Unchanged

- ModifyByte **never changes file length**
- To add bytes, use `InsertByte()` or `InsertBytes()`
- To remove bytes, use `DeleteBytes()`

### Position Validation

- Position must be: `0 <= position < Length`
- Out-of-range position throws `ArgumentOutOfRangeException`
- Always validate before modifying

### Undo/Redo

- Each `ModifyByte()` creates undo entry
- Use `BeginBatch()` / `EndBatch()` to group modifications into single undo unit
- Undo history is unlimited (configurable)

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread
- Use `Dispatcher.Invoke()` if calling from background thread

---

## 🔗 See Also

- **[GetByte()](getbyte.md)** - Read byte value at position
- **[ModifyBytes()](../editing/modifybytes.md)** - Modify multiple bytes efficiently
- **[InsertByte()](insertbyte.md)** - Insert new byte (increases length)
- **[DeleteBytes()](deletebytes.md)** - Remove bytes (decreases length)
- **[BeginBatch() / EndBatch()](../core/beginbatch.md)** - Batch operations for performance

---

**Last Updated**: 2026-02-19
**Version**: V2.0
