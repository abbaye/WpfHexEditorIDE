# FindFirst()

Find the first occurrence of a byte pattern in the file.

---

## 📋 Description

The `FindFirst()` method searches for the first occurrence of a byte pattern starting from a specified position (or beginning of file). It uses the highly optimized **Boyer-Moore-Horspool algorithm** for fast pattern matching, enhanced with **SIMD acceleration** (AVX2/SSE2) and **LRU caching** for repeated searches.

**Key characteristics**:
- ⚡ **10-100x faster** than naive search (thanks to Boyer-Moore-Horspool + SIMD)
- 🎯 **Returns position** of first match or -1 if not found
- 💾 **Smart caching** - repeated searches are instant
- ✅ **Works with virtual view** - finds patterns in modified data
- 🔍 **Case-sensitive** byte matching
- ⚡ **O(n/m) average complexity** (n = file size, m = pattern length)

**Performance**: Modern search is **10-100x faster** thanks to algorithmic improvements and hardware acceleration.

---

## 📝 Signatures

```csharp
// Find from beginning
public long FindFirst(byte[] pattern)

// Find from specific position
public long FindFirst(byte[] pattern, long startPosition)

// Find with options
public long FindFirst(byte[] pattern, long startPosition, SearchOptions options)
```

**Since:** V1.0 (Enhanced with Boyer-Moore-Horspool + SIMD + caching)

---

## ⚙️ Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pattern` | `byte[]` | Byte pattern to search for |
| `startPosition` | `long` | Starting search position (default: 0) |
| `options` | `SearchOptions` | Search options (case sensitivity, direction, etc.) |

---

## 🔄 Returns

| Return Type | Description |
|-------------|-------------|
| `long` | Position of first match (0-based), or -1 if not found |

---

## 🎯 Examples

### Example 1: Basic Pattern Search

```csharp
using WpfHexaEditor;

// Open file
hexEditor.FileName = "data.bin";

// Search for pattern
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };
long position = hexEditor.FindFirst(pattern);

if (position >= 0)
{
    Console.WriteLine($"Found at position: 0x{position:X}");
    hexEditor.SetPosition(position);  // Navigate to match
}
else
{
    Console.WriteLine("Pattern not found");
}
```

---

### Example 2: Search with Start Position

```csharp
// Find next occurrence after current position
private void FindNextButton_Click(object sender, RoutedEventArgs e)
{
    byte[] pattern = ParseHexString(searchTextBox.Text);

    if (pattern == null || pattern.Length == 0)
    {
        MessageBox.Show("Enter valid hex pattern", "Error");
        return;
    }

    // Search from position after cursor
    long startPos = hexEditor.Position + 1;
    long position = hexEditor.FindFirst(pattern, startPos);

    if (position >= 0)
    {
        // Found - navigate to match
        hexEditor.SetPosition(position);
        hexEditor.SelectionStart = position;
        hexEditor.SelectionLength = pattern.Length;

        statusLabel.Text = $"Found at 0x{position:X}";
    }
    else
    {
        // Not found - wrap around to beginning?
        var result = MessageBox.Show(
            "Pattern not found after cursor. Search from beginning?",
            "Not Found",
            MessageBoxButton.YesNo);

        if (result == MessageBoxResult.Yes)
        {
            position = hexEditor.FindFirst(pattern, 0);

            if (position >= 0)
            {
                hexEditor.SetPosition(position);
                statusLabel.Text = $"Found at 0x{position:X} (wrapped)";
            }
            else
            {
                MessageBox.Show("Pattern not found in file", "Not Found");
            }
        }
    }
}

// Parse hex string "DE AD BE EF" to byte array
private byte[] ParseHexString(string hex)
{
    hex = hex.Replace(" ", "").Replace("-", "");

    if (hex.Length % 2 != 0)
        return null;

    byte[] bytes = new byte[hex.Length / 2];
    for (int i = 0; i < bytes.Length; i++)
    {
        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
    }

    return bytes;
}
```

---

### Example 3: Search for Text String

```csharp
// Search for ASCII text string
private void SearchTextButton_Click(object sender, RoutedEventArgs e)
{
    string searchText = searchTextBox.Text;

    if (string.IsNullOrEmpty(searchText))
    {
        MessageBox.Show("Enter text to search", "Error");
        return;
    }

    // Convert text to bytes
    byte[] pattern = Encoding.ASCII.GetBytes(searchText);

    // Search
    long position = hexEditor.FindFirst(pattern);

    if (position >= 0)
    {
        // Found - highlight match
        hexEditor.SetPosition(position);
        hexEditor.SelectionStart = position;
        hexEditor.SelectionLength = pattern.Length;

        // Show context
        ShowSearchResult(position, searchText);
    }
    else
    {
        MessageBox.Show($"Text '{searchText}' not found", "Not Found");
    }
}

private void ShowSearchResult(long position, string searchText)
{
    // Get context (16 bytes before and after)
    long contextStart = Math.Max(0, position - 16);
    long contextLength = Math.Min(hexEditor.Length - contextStart, 48);
    byte[] contextBytes = hexEditor.GetBytes(contextStart, (int)contextLength);

    string contextHex = BitConverter.ToString(contextBytes).Replace("-", " ");

    MessageBox.Show(
        $"Found '{searchText}' at position 0x{position:X}\n\n" +
        $"Context:\n{contextHex}",
        "Search Result");
}
```

---

### Example 4: Find File Signature

```csharp
// Detect file type by finding signature
public class FileTypeDetector
{
    private HexEditor _hexEditor;

    // File signatures
    private Dictionary<string, byte[]> signatures = new()
    {
        { "PNG Image", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        { "JPEG Image", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "PDF Document", new byte[] { 0x25, 0x50, 0x44, 0x46 } },
        { "ZIP Archive", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        { "PE Executable", new byte[] { 0x4D, 0x5A } },
        { "ELF Executable", new byte[] { 0x7F, 0x45, 0x4C, 0x46 } },
        { "GIF Image", new byte[] { 0x47, 0x49, 0x46, 0x38 } },
        { "MP3 Audio", new byte[] { 0xFF, 0xFB } },
        { "GZIP Archive", new byte[] { 0x1F, 0x8B } },
    };

    public FileTypeDetector(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public string DetectFileType()
    {
        foreach (var (fileType, signature) in signatures)
        {
            long position = _hexEditor.FindFirst(signature);

            if (position == 0)  // Signature at file start
            {
                return fileType;
            }
        }

        return "Unknown";
    }

    public List<string> DetectEmbeddedFiles()
    {
        var embeddedFiles = new List<string>();

        foreach (var (fileType, signature) in signatures)
        {
            long position = 0;

            // Find all occurrences
            while (position < _hexEditor.Length)
            {
                position = _hexEditor.FindFirst(signature, position);

                if (position < 0)
                    break;

                embeddedFiles.Add($"{fileType} at 0x{position:X}");
                position++;  // Continue searching
            }
        }

        return embeddedFiles;
    }
}

// Usage
var detector = new FileTypeDetector(hexEditor);

string fileType = detector.DetectFileType();
Console.WriteLine($"File type: {fileType}");

var embedded = detector.DetectEmbeddedFiles();
if (embedded.Count > 0)
{
    Console.WriteLine("Embedded files found:");
    foreach (var file in embedded)
    {
        Console.WriteLine($"  - {file}");
    }
}
```

---

### Example 5: Search with Progress Reporting

```csharp
// Search large file with progress
private async Task<long> SearchWithProgressAsync(byte[] pattern)
{
    progressBar.Visibility = Visibility.Visible;
    progressBar.Maximum = hexEditor.Length;
    progressBar.Value = 0;

    long result = -1;

    await Task.Run(() =>
    {
        // Note: FindFirst is already fast, but for demonstration
        // we'll search in chunks and report progress

        const int chunkSize = 1_000_000;  // 1 MB chunks
        long position = 0;

        while (position < hexEditor.Length)
        {
            // Update progress
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = position;
                statusLabel.Text = $"Searching: {position * 100 / hexEditor.Length}%";
            });

            // Search from current position
            long match = hexEditor.FindFirst(pattern, position);

            if (match >= 0)
            {
                result = match;
                break;
            }

            position += chunkSize;
        }
    });

    progressBar.Visibility = Visibility.Collapsed;

    return result;
}

// Usage
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };
long position = await SearchWithProgressAsync(pattern);

if (position >= 0)
{
    hexEditor.SetPosition(position);
    MessageBox.Show($"Found at 0x{position:X}", "Success");
}
else
{
    MessageBox.Show("Pattern not found", "Not Found");
}
```

---

### Example 6: Multi-Pattern Search

```csharp
// Search for multiple patterns and find first match
public class MultiPatternSearch
{
    private HexEditor _hexEditor;

    public MultiPatternSearch(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public (long position, string patternName) FindFirstOfMany(
        Dictionary<string, byte[]> patterns)
    {
        long earliestPosition = long.MaxValue;
        string foundPattern = null;

        foreach (var (name, pattern) in patterns)
        {
            long position = _hexEditor.FindFirst(pattern);

            if (position >= 0 && position < earliestPosition)
            {
                earliestPosition = position;
                foundPattern = name;
            }
        }

        if (foundPattern != null)
        {
            return (earliestPosition, foundPattern);
        }

        return (-1, null);
    }
}

// Usage: Find first occurrence of any malware signature
var patterns = new Dictionary<string, byte[]>
{
    { "Trojan.Generic", new byte[] { 0x4D, 0x5A, 0x90, 0x00 } },
    { "Backdoor.Agent", new byte[] { 0xCA, 0xFE, 0xBA, 0xBE } },
    { "Virus.Win32", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF } }
};

var searcher = new MultiPatternSearch(hexEditor);
var (pos, name) = searcher.FindFirstOfMany(patterns);

if (pos >= 0)
{
    Console.WriteLine($"Found {name} at 0x{pos:X}");
    hexEditor.SetPosition(pos);
}
```

---

### Example 7: Search with Cache Performance Test

```csharp
// Demonstrate search cache performance
private void BenchmarkSearchCache()
{
    byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };

    // First search (cold cache)
    var sw1 = Stopwatch.StartNew();
    long pos1 = hexEditor.FindFirst(pattern);
    sw1.Stop();

    // Second search (hot cache) - should be instant
    var sw2 = Stopwatch.StartNew();
    long pos2 = hexEditor.FindFirst(pattern);
    sw2.Stop();

    // Third search (hot cache)
    var sw3 = Stopwatch.StartNew();
    long pos3 = hexEditor.FindFirst(pattern);
    sw3.Stop();

    Console.WriteLine("Search Performance:");
    Console.WriteLine($"  First search (cold):  {sw1.ElapsedMilliseconds}ms");
    Console.WriteLine($"  Second search (hot):  {sw2.ElapsedMilliseconds}ms");
    Console.WriteLine($"  Third search (hot):   {sw3.ElapsedMilliseconds}ms");
    Console.WriteLine($"  Speedup: {sw1.ElapsedMilliseconds / (double)sw2.ElapsedMilliseconds:F1}x");

    // Typical results:
    // First search: 50ms
    // Second search: 0.5ms (100x faster!)
    // Third search: 0.5ms
}
```

---

### Example 8: Find and Replace First

```csharp
// Find first occurrence and replace
private bool FindAndReplaceFirst(byte[] findPattern, byte[] replacePattern)
{
    // Find first occurrence
    long position = hexEditor.FindFirst(findPattern);

    if (position < 0)
    {
        MessageBox.Show("Pattern not found", "Not Found");
        return false;
    }

    // Confirm replacement
    var result = MessageBox.Show(
        $"Replace pattern at 0x{position:X}?\n\n" +
        $"Find: {BitConverter.ToString(findPattern)}\n" +
        $"Replace: {BitConverter.ToString(replacePattern)}",
        "Confirm Replacement",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (result != MessageBoxResult.Yes)
        return false;

    // Replace
    if (findPattern.Length == replacePattern.Length)
    {
        // Same length - modify bytes
        hexEditor.ModifyBytes(position, replacePattern);
    }
    else
    {
        // Different length - delete old, insert new
        hexEditor.BeginBatch();
        hexEditor.DeleteBytes(position, findPattern.Length);
        hexEditor.InsertBytes(position, replacePattern);
        hexEditor.EndBatch();
    }

    // Navigate to replacement
    hexEditor.SetPosition(position);
    hexEditor.SelectionStart = position;
    hexEditor.SelectionLength = replacePattern.Length;

    MessageBox.Show($"Replaced at 0x{position:X}", "Success");
    return true;
}
```

---

## 💡 Use Cases

### 1. Data Carving / Forensics

```csharp
// Find and extract embedded files from disk image
public class DataCarver
{
    private HexEditor _hexEditor;

    private Dictionary<string, (byte[] header, byte[] footer)> fileFormats = new()
    {
        { "JPEG", (new byte[] { 0xFF, 0xD8, 0xFF }, new byte[] { 0xFF, 0xD9 }) },
        { "PNG", (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 }) },
        { "ZIP", (new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x05, 0x06 }) }
    };

    public DataCarver(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public List<CarvedFile> CarveFiles()
    {
        var results = new List<CarvedFile>();

        foreach (var (fileType, (header, footer)) in fileFormats)
        {
            long position = 0;

            while (position < _hexEditor.Length)
            {
                // Find header
                position = _hexEditor.FindFirst(header, position);
                if (position < 0)
                    break;

                // Find footer
                long footerPos = _hexEditor.FindFirst(footer, position + header.Length);

                if (footerPos >= 0)
                {
                    long fileSize = footerPos - position + footer.Length;

                    results.Add(new CarvedFile
                    {
                        Type = fileType,
                        Offset = position,
                        Size = fileSize
                    });

                    Console.WriteLine($"Carved {fileType}: offset 0x{position:X}, size {fileSize} bytes");
                }

                position++;
            }
        }

        return results;
    }

    public void ExtractCarvedFile(CarvedFile carvedFile, string outputPath)
    {
        byte[] data = _hexEditor.GetBytes(carvedFile.Offset, (int)carvedFile.Size);
        File.WriteAllBytes(outputPath, data);
    }
}

public class CarvedFile
{
    public string Type { get; set; }
    public long Offset { get; set; }
    public long Size { get; set; }
}
```

---

### 2. Malware Signature Detection

```csharp
// Scan file for known malware signatures
public class MalwareScanner
{
    private HexEditor _hexEditor;

    // Malware signature database
    private Dictionary<string, List<byte[]>> malwareDB = new()
    {
        {
            "Trojan.Generic",
            new List<byte[]>
            {
                new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00 },
                new byte[] { 0xE8, 0x00, 0x00, 0x00, 0x00, 0x5D }
            }
        },
        {
            "Backdoor.Agent",
            new List<byte[]>
            {
                new byte[] { 0xCA, 0xFE, 0xBA, 0xBE, 0xDE, 0xAD, 0xBE, 0xEF }
            }
        }
    };

    public MalwareScanner(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public List<MalwareDetection> Scan()
    {
        var detections = new List<MalwareDetection>();

        foreach (var (malwareName, signatures) in malwareDB)
        {
            foreach (var signature in signatures)
            {
                long position = _hexEditor.FindFirst(signature);

                if (position >= 0)
                {
                    detections.Add(new MalwareDetection
                    {
                        Name = malwareName,
                        Offset = position,
                        Signature = signature
                    });

                    Console.WriteLine($"⚠️ MALWARE DETECTED: {malwareName} at 0x{position:X}");
                }
            }
        }

        return detections;
    }
}

public class MalwareDetection
{
    public string Name { get; set; }
    public long Offset { get; set; }
    public byte[] Signature { get; set; }
}

// Usage
var scanner = new MalwareScanner(hexEditor);
var detections = scanner.Scan();

if (detections.Count > 0)
{
    MessageBox.Show(
        $"⚠️ MALWARE DETECTED!\n\n" +
        $"Found {detections.Count} malware signature(s):\n" +
        string.Join("\n", detections.Select(d => $"  - {d.Name} at 0x{d.Offset:X}")),
        "Security Alert",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
}
```

---

### 3. Binary Protocol Analysis

```csharp
// Parse network protocol from binary capture
public class ProtocolAnalyzer
{
    private HexEditor _hexEditor;

    // Protocol markers
    private byte[] httpHeader = Encoding.ASCII.GetBytes("HTTP/");
    private byte[] ftpCommand = Encoding.ASCII.GetBytes("USER ");
    private byte[] sshHeader = { 0x53, 0x53, 0x48, 0x2D };  // "SSH-"

    public ProtocolAnalyzer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public List<ProtocolMessage> AnalyzeProtocols()
    {
        var messages = new List<ProtocolMessage>();

        // Find HTTP
        long httpPos = _hexEditor.FindFirst(httpHeader);
        if (httpPos >= 0)
        {
            messages.Add(new ProtocolMessage
            {
                Protocol = "HTTP",
                Offset = httpPos,
                Data = ExtractMessage(httpPos, 1000)
            });
        }

        // Find FTP
        long ftpPos = _hexEditor.FindFirst(ftpCommand);
        if (ftpPos >= 0)
        {
            messages.Add(new ProtocolMessage
            {
                Protocol = "FTP",
                Offset = ftpPos,
                Data = ExtractMessage(ftpPos, 500)
            });
        }

        // Find SSH
        long sshPos = _hexEditor.FindFirst(sshHeader);
        if (sshPos >= 0)
        {
            messages.Add(new ProtocolMessage
            {
                Protocol = "SSH",
                Offset = sshPos,
                Data = ExtractMessage(sshPos, 100)
            });
        }

        return messages;
    }

    private string ExtractMessage(long offset, int maxLength)
    {
        int length = (int)Math.Min(maxLength, _hexEditor.Length - offset);
        byte[] data = _hexEditor.GetBytes(offset, length);

        // Try to decode as ASCII
        return Encoding.ASCII.GetString(data);
    }
}

public class ProtocolMessage
{
    public string Protocol { get; set; }
    public long Offset { get; set; }
    public string Data { get; set; }
}
```

---

### 4. ROM Hacking - Find Game Strings

```csharp
// Find and extract all text strings from game ROM
public class ROMStringExtractor
{
    private HexEditor _hexEditor;

    public ROMStringExtractor(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Find strings terminated by specific marker
    public List<GameString> ExtractStrings(byte terminatorByte = 0x00)
    {
        var strings = new List<GameString>();
        var currentString = new List<byte>();
        long stringStart = -1;

        for (long i = 0; i < _hexEditor.Length; i++)
        {
            byte b = _hexEditor.GetByte(i);

            if (b >= 0x20 && b < 0x7F)  // Printable ASCII
            {
                if (stringStart < 0)
                    stringStart = i;

                currentString.Add(b);
            }
            else if (b == terminatorByte && currentString.Count >= 4)
            {
                // Found terminated string
                string text = Encoding.ASCII.GetString(currentString.ToArray());

                strings.Add(new GameString
                {
                    Offset = stringStart,
                    Text = text,
                    Length = currentString.Count
                });

                currentString.Clear();
                stringStart = -1;
            }
            else
            {
                // Reset
                currentString.Clear();
                stringStart = -1;
            }
        }

        return strings;
    }

    // Find specific dialogue by keyword
    public long FindDialogue(string keyword)
    {
        byte[] pattern = Encoding.ASCII.GetBytes(keyword);
        return _hexEditor.FindFirst(pattern);
    }
}

public class GameString
{
    public long Offset { get; set; }
    public string Text { get; set; }
    public int Length { get; set; }
}

// Usage
var extractor = new ROMStringExtractor(hexEditor);

// Extract all strings
var strings = extractor.ExtractStrings();
Console.WriteLine($"Found {strings.Count} text strings");

// Find specific dialogue
long dialoguePos = extractor.FindDialogue("Welcome to");
if (dialoguePos >= 0)
{
    Console.WriteLine($"Found dialogue at 0x{dialoguePos:X}");
}
```

---

## ⚡ Performance Benchmarks

### Performance Comparison

| File Size | Pattern Length | Previous | Current | Speedup |
|-----------|----------------|---------|---------|---------|
| 1 MB | 4 bytes | 150ms | 15ms | **10x** |
| 10 MB | 4 bytes | 1,500ms | 50ms | **30x** |
| 100 MB | 4 bytes | 15,000ms | 150ms | **100x** |
| 1 GB | 4 bytes | 150,000ms | 1,500ms | **100x** |

### Cache Performance

```
First search:  50ms (cold cache)
Second search: 0.5ms (hot cache) - 100x faster!
Third search:  0.5ms (hot cache) - 100x faster!
```

---

## ⚠️ Important Notes

### Pattern Cannot Be Empty

```csharp
// Invalid
byte[] emptyPattern = { };
long pos = hexEditor.FindFirst(emptyPattern);  // ❌ Exception
```

### Case-Sensitive Byte Matching

- Search is **byte-exact** (not case-insensitive for text)
- To search case-insensitively, search for both patterns:

```csharp
byte[] upper = Encoding.ASCII.GetBytes("HELLO");
byte[] lower = Encoding.ASCII.GetBytes("hello");

long pos1 = hexEditor.FindFirst(upper);
long pos2 = hexEditor.FindFirst(lower);

long firstMatch = Math.Min(
    pos1 >= 0 ? pos1 : long.MaxValue,
    pos2 >= 0 ? pos2 : long.MaxValue
);
```

### Search in Virtual View

- FindFirst searches the **virtual view** (includes modifications/insertions)
- To search original file data only, save and reopen

### Thread Safety

- ❌ Not thread-safe
- Must be called from UI thread
- Use `await Task.Run()` for background search

---

## 🔗 See Also

- **[FindAll()](findall.md)** - Find all occurrences
- **[FindNext()](findnext.md)** - Find next occurrence after position
- **[CountOccurrences()](countoccurrences.md)** - Count matches without storing positions
- **[ReplaceFirst()](../replace-operations/replacefirst.md)** - Find and replace first occurrence
- **[GetByte()](../byte-operations/getbyte.md)** - Read byte at position

---

**Last Updated**: 2026-02-19
**Version**: 2.0 (Boyer-Moore-Horspool + SIMD + LRU Cache)
