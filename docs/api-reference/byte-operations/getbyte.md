# GetByte()

Read the value of a single byte at a specific position.

---

## 📋 Description

The `GetByte()` method reads and returns a single byte value from the specified position in the file. This method **reads from the virtual view**, meaning it returns:
- **Modified bytes** if the position was modified
- **Inserted bytes** if the position is an insertion
- **Original bytes** from the file otherwise

The method is **non-destructive** (read-only) and does not change the file or undo history.

---

## 📝 Signatures

```csharp
// Get single byte
public byte GetByte(long position)

// Get multiple bytes
public byte[] GetBytes(long position, int count)

// Get all bytes
public byte[] GetAllBytes()
```

**Since:** V1.0

---

## ⚙️ Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `position` | `long` | Position in file (0-based) |
| `count` | `int` | Number of bytes to read (for GetBytes) |

---

## 🔄 Returns

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetByte()` | `byte` | Byte value (0x00 to 0xFF) |
| `GetBytes()` | `byte[]` | Array of byte values |
| `GetAllBytes()` | `byte[]` | All bytes in file (virtual view) |

---

## 🎯 Examples

### Example 1: Read Single Byte (Basic)

```csharp
using WpfHexaEditor;

// Open file
hexEditor.FileName = "data.bin";

// Read byte at position 0x100
byte value = hexEditor.GetByte(0x100);

Console.WriteLine($"Byte at 0x100: 0x{value:X2} ({value})");
```

### Example 2: Read and Display Multiple Bytes

```csharp
private void DisplayBytesButton_Click(object sender, RoutedEventArgs e)
{
    long position = hexEditor.Position;  // Current cursor position
    int count = 16;  // Read 16 bytes

    // Read bytes
    byte[] bytes = hexEditor.GetBytes(position, count);

    // Format as hex string
    string hexString = BitConverter.ToString(bytes).Replace("-", " ");

    // Format as ASCII
    string asciiString = new string(bytes
        .Select(b => b >= 32 && b < 127 ? (char)b : '.')
        .ToArray());

    // Display
    var output = $"Position: 0x{position:X8}\n" +
                 $"Hex: {hexString}\n" +
                 $"ASCII: {asciiString}";

    MessageBox.Show(output, "Bytes at Cursor");
}
```

### Example 3: Read File Header

```csharp
// Read and analyze file header
private void AnalyzeFileHeader()
{
    hexEditor.FileName = "unknown.bin";

    // Read first 4 bytes (magic number)
    byte[] magic = hexEditor.GetBytes(0, 4);

    // Check file type
    string fileType = IdentifyFileType(magic);

    Console.WriteLine($"File type: {fileType}");
}

private string IdentifyFileType(byte[] magic)
{
    // Common file signatures
    if (magic[0] == 0x4D && magic[1] == 0x5A)
        return "PE Executable (MZ header)";

    if (magic[0] == 0xFF && magic[1] == 0xD8 && magic[2] == 0xFF)
        return "JPEG Image";

    if (magic[0] == 0x89 && magic[1] == 0x50 && magic[2] == 0x4E && magic[3] == 0x47)
        return "PNG Image";

    if (magic[0] == 0x50 && magic[1] == 0x4B)
        return "ZIP Archive";

    if (magic[0] == 0x1F && magic[1] == 0x8B)
        return "GZIP Archive";

    return "Unknown";
}
```

### Example 4: Byte Comparison

```csharp
// Compare bytes at two positions
private bool CompareBytesAt(long position1, long position2, int length)
{
    for (int i = 0; i < length; i++)
    {
        byte byte1 = hexEditor.GetByte(position1 + i);
        byte byte2 = hexEditor.GetByte(position2 + i);

        if (byte1 != byte2)
        {
            Console.WriteLine($"Difference at offset {i}: " +
                            $"0x{byte1:X2} != 0x{byte2:X2}");
            return false;
        }
    }

    return true;
}

// Usage
bool areEqual = CompareBytesAt(0x1000, 0x2000, 256);
Console.WriteLine($"Regions are equal: {areEqual}");
```

### Example 5: Byte Statistics

```csharp
// Calculate byte frequency
private Dictionary<byte, int> CalculateByteFrequency()
{
    var frequency = new Dictionary<byte, int>();

    // Initialize
    for (int i = 0; i <= 255; i++)
    {
        frequency[(byte)i] = 0;
    }

    // Count bytes
    for (long position = 0; position < hexEditor.Length; position++)
    {
        byte value = hexEditor.GetByte(position);
        frequency[value]++;
    }

    return frequency;
}

// Usage
var stats = CalculateByteFrequency();

Console.WriteLine("Most common bytes:");
foreach (var kvp in stats.OrderByDescending(x => x.Value).Take(10))
{
    Console.WriteLine($"  0x{kvp.Key:X2}: {kvp.Value} occurrences " +
                     $"({(kvp.Value * 100.0 / hexEditor.Length):F2}%)");
}
```

### Example 6: Read Structured Data

```csharp
// Read structured data from file
public class FileHeader
{
    public byte[] Magic { get; set; }       // 4 bytes
    public ushort Version { get; set; }     // 2 bytes
    public uint DataOffset { get; set; }    // 4 bytes
    public uint DataLength { get; set; }    // 4 bytes
}

private FileHeader ReadHeader(long position)
{
    var header = new FileHeader();

    // Read magic (4 bytes)
    header.Magic = hexEditor.GetBytes(position, 4);
    position += 4;

    // Read version (2 bytes, little-endian)
    byte versionLow = hexEditor.GetByte(position);
    byte versionHigh = hexEditor.GetByte(position + 1);
    header.Version = (ushort)(versionLow | (versionHigh << 8));
    position += 2;

    // Read data offset (4 bytes, little-endian)
    byte[] offsetBytes = hexEditor.GetBytes(position, 4);
    header.DataOffset = BitConverter.ToUInt32(offsetBytes, 0);
    position += 4;

    // Read data length (4 bytes, little-endian)
    byte[] lengthBytes = hexEditor.GetBytes(position, 4);
    header.DataLength = BitConverter.ToUInt32(lengthBytes, 0);

    return header;
}

// Usage
var header = ReadHeader(0);
Console.WriteLine($"Version: {header.Version}");
Console.WriteLine($"Data at: 0x{header.DataOffset:X}");
Console.WriteLine($"Data size: {header.DataLength} bytes");
```

### Example 7: Export Bytes to Different Formats

```csharp
public class ByteExporter
{
    private HexEditor _hexEditor;

    public ByteExporter(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Export as hex string
    public string ToHexString(long start, int length)
    {
        byte[] bytes = _hexEditor.GetBytes(start, length);
        return BitConverter.ToString(bytes).Replace("-", " ");
    }

    // Export as C# array
    public string ToCSharpArray(long start, int length)
    {
        byte[] bytes = _hexEditor.GetBytes(start, length);
        var values = string.Join(", ", bytes.Select(b => $"0x{b:X2}"));
        return $"new byte[] {{ {values} }}";
    }

    // Export as C array
    public string ToCArray(long start, int length)
    {
        byte[] bytes = _hexEditor.GetBytes(start, length);
        var values = string.Join(", ", bytes.Select(b => $"0x{b:X2}"));
        return $"{{ {values} }}";
    }

    // Export as binary string
    public string ToBinaryString(long start, int length)
    {
        byte[] bytes = _hexEditor.GetBytes(start, length);
        return string.Join(" ", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
    }

    // Export as Base64
    public string ToBase64(long start, int length)
    {
        byte[] bytes = _hexEditor.GetBytes(start, length);
        return Convert.ToBase64String(bytes);
    }
}

// Usage
var exporter = new ByteExporter(hexEditor);

string hex = exporter.ToHexString(0, 16);
Console.WriteLine($"Hex: {hex}");

string csharp = exporter.ToCSharpArray(0, 16);
Console.WriteLine($"C#: {csharp}");

string base64 = exporter.ToBase64(0, 16);
Console.WriteLine($"Base64: {base64}");
```

### Example 8: Pattern Matching

```csharp
// Check if pattern exists at position
private bool PatternMatchesAt(long position, byte[] pattern)
{
    if (position + pattern.Length > hexEditor.Length)
        return false;

    for (int i = 0; i < pattern.Length; i++)
    {
        byte value = hexEditor.GetByte(position + i);
        if (value != pattern[i])
            return false;
    }

    return true;
}

// Find all occurrences of pattern manually
private List<long> FindAllPatterns(byte[] pattern)
{
    var positions = new List<long>();

    for (long pos = 0; pos <= hexEditor.Length - pattern.Length; pos++)
    {
        if (PatternMatchesAt(pos, pattern))
        {
            positions.Add(pos);
            pos += pattern.Length - 1;  // Skip pattern length
        }
    }

    return positions;
}

// Usage
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };
var positions = FindAllPatterns(pattern);
Console.WriteLine($"Found {positions.Count} occurrences");
```

---

## 💡 Use Cases

### 1. Checksum Calculation

```csharp
// Calculate XOR checksum
private byte CalculateXorChecksum()
{
    byte checksum = 0;

    for (long i = 0; i < hexEditor.Length; i++)
    {
        checksum ^= hexEditor.GetByte(i);
    }

    return checksum;
}

// Calculate CRC-8 checksum
private byte CalculateCrc8()
{
    byte crc = 0;

    for (long i = 0; i < hexEditor.Length; i++)
    {
        byte data = hexEditor.GetByte(i);
        crc ^= data;

        for (int bit = 0; bit < 8; bit++)
        {
            if ((crc & 0x80) != 0)
            {
                crc = (byte)((crc << 1) ^ 0x07);
            }
            else
            {
                crc <<= 1;
            }
        }
    }

    return crc;
}
```

### 2. Text String Extraction

```csharp
// Extract null-terminated strings
private List<string> ExtractStrings(int minLength = 4)
{
    var strings = new List<string>();
    var currentString = new List<byte>();

    for (long i = 0; i < hexEditor.Length; i++)
    {
        byte value = hexEditor.GetByte(i);

        if (value >= 32 && value < 127)  // Printable ASCII
        {
            currentString.Add(value);
        }
        else if (value == 0)  // Null terminator
        {
            if (currentString.Count >= minLength)
            {
                string text = Encoding.ASCII.GetString(currentString.ToArray());
                strings.Add(text);
            }
            currentString.Clear();
        }
        else  // Non-printable
        {
            currentString.Clear();
        }
    }

    return strings;
}

// Usage
var strings = ExtractStrings(minLength: 5);
Console.WriteLine($"Found {strings.Count} strings:");
foreach (var str in strings.Take(10))
{
    Console.WriteLine($"  \"{str}\"");
}
```

### 3. Entropy Analysis (File Type Detection)

```csharp
// Calculate Shannon entropy (0-8 bits)
private double CalculateEntropy()
{
    var frequency = new int[256];

    // Count byte frequencies
    for (long i = 0; i < hexEditor.Length; i++)
    {
        byte value = hexEditor.GetByte(i);
        frequency[value]++;
    }

    // Calculate entropy
    double entropy = 0.0;
    long totalBytes = hexEditor.Length;

    for (int i = 0; i < 256; i++)
    {
        if (frequency[i] > 0)
        {
            double probability = (double)frequency[i] / totalBytes;
            entropy -= probability * Math.Log(probability, 2);
        }
    }

    return entropy;
}

// Analyze file type based on entropy
private string AnalyzeFileType()
{
    double entropy = CalculateEntropy();

    if (entropy < 1.0)
        return "Highly compressed or encrypted (very low entropy)";
    else if (entropy < 4.0)
        return "Text or structured data (low entropy)";
    else if (entropy < 7.0)
        return "Executable or mixed data (medium entropy)";
    else if (entropy < 7.9)
        return "Compressed or multimedia (high entropy)";
    else
        return "Encrypted or random data (very high entropy)";
}
```

### 4. Binary Diff

```csharp
// Compare two files and report differences
public class BinaryDiff
{
    public long Position { get; set; }
    public byte File1Byte { get; set; }
    public byte File2Byte { get; set; }
}

private List<BinaryDiff> CompareFiles(HexEditor hex1, HexEditor hex2)
{
    var diffs = new List<BinaryDiff>();
    long minLength = Math.Min(hex1.Length, hex2.Length);

    for (long i = 0; i < minLength; i++)
    {
        byte byte1 = hex1.GetByte(i);
        byte byte2 = hex2.GetByte(i);

        if (byte1 != byte2)
        {
            diffs.Add(new BinaryDiff
            {
                Position = i,
                File1Byte = byte1,
                File2Byte = byte2
            });
        }
    }

    return diffs;
}

// Usage
var diffs = CompareFiles(hexEditor1, hexEditor2);
Console.WriteLine($"Found {diffs.Count} differences:");
foreach (var diff in diffs.Take(10))
{
    Console.WriteLine($"  0x{diff.Position:X}: " +
                     $"0x{diff.File1Byte:X2} vs 0x{diff.File2Byte:X2}");
}
```

---

## ⚡ Performance Tips

### Batch Reading for Better Performance

```csharp
// Slow: Read bytes individually
byte[] result = new byte[1000];
for (int i = 0; i < 1000; i++)
{
    result[i] = hexEditor.GetByte(i);  // 1000 calls
}
// Time: ~100ms

// Fast: Read bytes in batch
byte[] result = hexEditor.GetBytes(0, 1000);  // 1 call
// Time: ~1ms (100x faster!)
```

### Cache Frequently Accessed Bytes

```csharp
// If reading same bytes multiple times, cache them
byte[] cache = hexEditor.GetBytes(0, 1000);

// Now access from cache instead of file
for (int i = 0; i < cache.Length; i++)
{
    byte value = cache[i];  // Fast: from memory
    // Process value...
}
```

---

## ⚠️ Important Notes

### Virtual View

`GetByte()` reads from the **virtual view**, which includes:
- ✅ Modified bytes (changed values)
- ✅ Inserted bytes (added data)
- ❌ Deleted bytes (not accessible)

To read original file bytes (ignoring edits):
```csharp
long physicalPos = hexEditor.VirtualToPhysical(virtualPos);
byte originalByte = hexEditor.GetOriginalByte(physicalPos);
```

### Position Validation

- Position must be: `0 <= position < Length`
- Out-of-range throws `ArgumentOutOfRangeException`
- Always validate before reading:

```csharp
if (position >= 0 && position < hexEditor.Length)
{
    byte value = hexEditor.GetByte(position);
}
```

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread
- Use `Dispatcher.Invoke()` from background threads

---

## 🔗 See Also

- **[ModifyByte()](modifybyte.md)** - Change byte value at position
- **[InsertByte()](insertbyte.md)** - Insert new byte (increases length)
- **[DeleteBytes()](deletebytes.md)** - Remove bytes (decreases length)
- **[GetOriginalByte()](getoriginalbyte.md)** - Read original file byte (ignoring edits)

---

**Last Updated**: 2026-02-19
**Version**: V2.0
