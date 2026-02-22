# FindAll()

Find all occurrences of a byte pattern in the file.

---

## 📋 Description

The `FindAll()` method searches for **all occurrences** of a byte pattern and returns their positions as a list. It uses the same highly optimized **Boyer-Moore-Horspool algorithm** with **SIMD acceleration** (AVX2/SSE2) and **parallel multi-core processing** for large files.

**Key characteristics**:
- ⚡ **10-100x faster** than naive search
- 📊 **Returns List<long>** containing all match positions
- 🎯 **Parallel processing** for files > 10 MB (multi-core utilization)
- 💾 **Smart caching** - repeated searches are instant
- ✅ **Works with virtual view** - finds patterns in modified data
- ⚠️ **Memory usage**: Stores all positions (use CountOccurrences() for large result sets)

**Performance**: For large files with many matches, FindAll() automatically uses parallel processing across CPU cores for maximum speed.

---

## 📝 Signatures

```csharp
// Find all occurrences
public List<long> FindAll(byte[] pattern)

// Find all occurrences in range
public List<long> FindAll(byte[] pattern, long startPosition, long endPosition)
```

**Since:** V1.0 (Enhanced with parallelization + SIMD)

---

## ⚙️ Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pattern` | `byte[]` | Byte pattern to search for |
| `startPosition` | `long` | Starting search position (inclusive) |
| `endPosition` | `long` | Ending search position (exclusive) |

---

## 🔄 Returns

| Return Type | Description |
|-------------|-------------|
| `List<long>` | List of all match positions (sorted ascending), or empty list if none found |

---

## 🎯 Examples

### Example 1: Basic Find All

```csharp
using WpfHexaEditor;

// Open file
hexEditor.FileName = "data.bin";

// Find all occurrences
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };
List<long> positions = hexEditor.FindAll(pattern);

Console.WriteLine($"Found {positions.Count} occurrences:");
foreach (var pos in positions)
{
    Console.WriteLine($"  - 0x{pos:X}");
}
```

---

### Example 2: Display Results in ListBox

```csharp
private void FindAllButton_Click(object sender, RoutedEventArgs e)
{
    // Parse search pattern
    byte[] pattern = ParseHexString(searchTextBox.Text);

    if (pattern == null || pattern.Length == 0)
    {
        MessageBox.Show("Enter valid hex pattern", "Error");
        return;
    }

    // Search
    var stopwatch = Stopwatch.StartNew();
    List<long> positions = hexEditor.FindAll(pattern);
    stopwatch.Stop();

    // Display results
    resultsListBox.Items.Clear();

    if (positions.Count == 0)
    {
        MessageBox.Show("Pattern not found", "Not Found");
        return;
    }

    // Add results to ListBox
    foreach (var pos in positions.Take(1000))  // Limit to first 1000
    {
        resultsListBox.Items.Add($"0x{pos:X8}");
    }

    // Show summary
    statusLabel.Text = $"Found {positions.Count} matches in {stopwatch.ElapsedMilliseconds}ms";

    if (positions.Count > 1000)
    {
        statusLabel.Text += $" (showing first 1000)";
    }

    // Navigate to first result
    if (positions.Count > 0)
    {
        hexEditor.SetPosition(positions[0]);
        hexEditor.SelectionStart = positions[0];
        hexEditor.SelectionLength = pattern.Length;
    }
}

// Navigate to selected result
private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (resultsListBox.SelectedItem is string selectedItem)
    {
        // Parse position "0x00001234"
        string posHex = selectedItem.Replace("0x", "");
        long position = Convert.ToInt64(posHex, 16);

        // Navigate
        hexEditor.SetPosition(position);
        hexEditor.SelectionStart = position;
        hexEditor.SelectionLength = ParseHexString(searchTextBox.Text).Length;
    }
}
```

---

### Example 3: Find and Highlight All Matches

```csharp
// Find all matches and add highlights
private void HighlightAllMatches()
{
    byte[] pattern = { 0xFF, 0xFF };

    // Find all
    List<long> positions = hexEditor.FindAll(pattern);

    if (positions.Count == 0)
    {
        MessageBox.Show("Pattern not found", "Info");
        return;
    }

    // Clear existing highlights
    hexEditor.ClearHighlights();

    // Add highlight for each match
    foreach (var pos in positions)
    {
        hexEditor.AddHighlight(pos, pattern.Length, Colors.Yellow, "Match");
    }

    MessageBox.Show(
        $"Highlighted {positions.Count} matches",
        "Success");

    // Navigate to first match
    hexEditor.SetPosition(positions[0]);
}
```

---

### Example 4: Find in Specific Range

```csharp
// Search only in file header (first 1 MB)
private void SearchFileHeader()
{
    byte[] pattern = { 0x4D, 0x5A };  // MZ signature

    long searchStart = 0;
    long searchEnd = Math.Min(hexEditor.Length, 1_048_576);  // 1 MB

    // Find all in range
    List<long> positions = hexEditor.FindAll(pattern, searchStart, searchEnd);

    Console.WriteLine($"Found {positions.Count} MZ signatures in first 1 MB:");
    foreach (var pos in positions)
    {
        Console.WriteLine($"  - Embedded PE at 0x{pos:X}");
    }
}
```

---

### Example 5: Statistical Analysis

```csharp
// Analyze byte pattern distribution
public class PatternAnalyzer
{
    private HexEditor _hexEditor;

    public PatternAnalyzer(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public PatternStatistics AnalyzePattern(byte[] pattern)
    {
        var stopwatch = Stopwatch.StartNew();
        List<long> positions = _hexEditor.FindAll(pattern);
        stopwatch.Stop();

        if (positions.Count == 0)
        {
            return new PatternStatistics
            {
                Pattern = pattern,
                Count = 0
            };
        }

        // Calculate statistics
        var stats = new PatternStatistics
        {
            Pattern = pattern,
            Count = positions.Count,
            SearchTime = stopwatch.ElapsedMilliseconds,
            FirstPosition = positions[0],
            LastPosition = positions[positions.Count - 1],
            AverageDistance = CalculateAverageDistance(positions),
            MinDistance = CalculateMinDistance(positions),
            MaxDistance = CalculateMaxDistance(positions)
        };

        return stats;
    }

    private double CalculateAverageDistance(List<long> positions)
    {
        if (positions.Count < 2)
            return 0;

        long totalDistance = 0;
        for (int i = 1; i < positions.Count; i++)
        {
            totalDistance += positions[i] - positions[i - 1];
        }

        return (double)totalDistance / (positions.Count - 1);
    }

    private long CalculateMinDistance(List<long> positions)
    {
        if (positions.Count < 2)
            return 0;

        long minDistance = long.MaxValue;
        for (int i = 1; i < positions.Count; i++)
        {
            long distance = positions[i] - positions[i - 1];
            if (distance < minDistance)
                minDistance = distance;
        }

        return minDistance;
    }

    private long CalculateMaxDistance(List<long> positions)
    {
        if (positions.Count < 2)
            return 0;

        long maxDistance = 0;
        for (int i = 1; i < positions.Count; i++)
        {
            long distance = positions[i] - positions[i - 1];
            if (distance > maxDistance)
                maxDistance = distance;
        }

        return maxDistance;
    }
}

public class PatternStatistics
{
    public byte[] Pattern { get; set; }
    public int Count { get; set; }
    public long SearchTime { get; set; }
    public long FirstPosition { get; set; }
    public long LastPosition { get; set; }
    public double AverageDistance { get; set; }
    public long MinDistance { get; set; }
    public long MaxDistance { get; set; }

    public override string ToString()
    {
        return $"Pattern: {BitConverter.ToString(Pattern)}\n" +
               $"Count: {Count}\n" +
               $"Search time: {SearchTime}ms\n" +
               $"First: 0x{FirstPosition:X}\n" +
               $"Last: 0x{LastPosition:X}\n" +
               $"Avg distance: {AverageDistance:F2} bytes\n" +
               $"Min distance: {MinDistance} bytes\n" +
               $"Max distance: {MaxDistance} bytes";
    }
}

// Usage
var analyzer = new PatternAnalyzer(hexEditor);
byte[] pattern = { 0x00, 0x00 };  // Null bytes
var stats = analyzer.AnalyzePattern(pattern);

Console.WriteLine(stats.ToString());
```

---

### Example 6: Progress Reporting for Large Files

```csharp
// Find all with progress for large files
private async Task<List<long>> FindAllWithProgressAsync(byte[] pattern)
{
    if (hexEditor.Length < 10_000_000)  // < 10 MB
    {
        // Small file - no progress needed
        return hexEditor.FindAll(pattern);
    }

    // Large file - show progress
    progressBar.Visibility = Visibility.Visible;
    progressBar.Maximum = 100;

    var results = new List<long>();

    await Task.Run(() =>
    {
        // Note: FindAll is already optimized with parallel processing
        // This is for UI progress indication only

        const int chunkSize = 10_000_000;  // 10 MB chunks
        long position = 0;

        while (position < hexEditor.Length)
        {
            long chunkEnd = Math.Min(position + chunkSize, hexEditor.Length);

            // Find in this chunk
            var chunkResults = hexEditor.FindAll(pattern, position, chunkEnd);
            results.AddRange(chunkResults);

            // Update progress
            int progress = (int)((chunkEnd * 100) / hexEditor.Length);
            Dispatcher.Invoke(() =>
            {
                progressBar.Value = progress;
                statusLabel.Text = $"Searching: {progress}%";
            });

            position = chunkEnd;
        }
    });

    progressBar.Visibility = Visibility.Collapsed;
    return results;
}

// Usage
byte[] pattern = { 0xDE, 0xAD, 0xBE, 0xEF };
var positions = await FindAllWithProgressAsync(pattern);

MessageBox.Show($"Found {positions.Count} matches", "Complete");
```

---

### Example 7: Find Multiple Patterns

```csharp
// Find all occurrences of multiple patterns
public class MultiPatternFinder
{
    private HexEditor _hexEditor;

    public MultiPatternFinder(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public Dictionary<string, List<long>> FindAllPatterns(
        Dictionary<string, byte[]> patterns)
    {
        var results = new Dictionary<string, List<long>>();

        foreach (var (name, pattern) in patterns)
        {
            var positions = _hexEditor.FindAll(pattern);
            results[name] = positions;

            Console.WriteLine($"{name}: {positions.Count} matches");
        }

        return results;
    }

    public List<(string name, long position)> GetCombinedSortedResults(
        Dictionary<string, List<long>> patternResults)
    {
        var combined = new List<(string name, long position)>();

        foreach (var (name, positions) in patternResults)
        {
            foreach (var pos in positions)
            {
                combined.Add((name, pos));
            }
        }

        // Sort by position
        combined.Sort((a, b) => a.position.CompareTo(b.position));

        return combined;
    }
}

// Usage: Find all string markers
var patterns = new Dictionary<string, byte[]>
{
    { "String Start", Encoding.ASCII.GetBytes("<STR>") },
    { "String End", Encoding.ASCII.GetBytes("</STR>") },
    { "Comment", Encoding.ASCII.GetBytes("//") }
};

var finder = new MultiPatternFinder(hexEditor);
var results = finder.FindAllPatterns(patterns);

// Display combined results
var combined = finder.GetCombinedSortedResults(results);
foreach (var (name, pos) in combined.Take(100))
{
    Console.WriteLine($"0x{pos:X8}: {name}");
}
```

---

### Example 8: Export Results to File

```csharp
// Export search results to CSV file
private void ExportResultsButton_Click(object sender, RoutedEventArgs e)
{
    byte[] pattern = ParseHexString(searchTextBox.Text);

    if (pattern == null || pattern.Length == 0)
    {
        MessageBox.Show("Enter valid hex pattern", "Error");
        return;
    }

    // Find all
    List<long> positions = hexEditor.FindAll(pattern);

    if (positions.Count == 0)
    {
        MessageBox.Show("No matches found", "Info");
        return;
    }

    // Save dialog
    var dialog = new SaveFileDialog
    {
        Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt",
        Title = "Export Search Results",
        FileName = "search_results.csv"
    };

    if (dialog.ShowDialog() == true)
    {
        ExportResultsToCSV(positions, pattern, dialog.FileName);
        MessageBox.Show($"Exported {positions.Count} results", "Success");
    }
}

private void ExportResultsToCSV(List<long> positions, byte[] pattern, string filePath)
{
    using var writer = new StreamWriter(filePath);

    // Header
    writer.WriteLine("Index,Position (Hex),Position (Dec),Context");

    // Data rows
    for (int i = 0; i < positions.Count; i++)
    {
        long pos = positions[i];

        // Get context (16 bytes)
        int contextLength = (int)Math.Min(16, hexEditor.Length - pos);
        byte[] context = hexEditor.GetBytes(pos, contextLength);
        string contextHex = BitConverter.ToString(context).Replace("-", " ");

        writer.WriteLine($"{i + 1},0x{pos:X},{pos},\"{contextHex}\"");
    }
}
```

---

## 💡 Use Cases

### 1. Binary Deduplication

```csharp
// Find duplicate blocks of data
public class DuplicateFinder
{
    private HexEditor _hexEditor;
    private int _blockSize;

    public DuplicateFinder(HexEditor hexEditor, int blockSize = 4096)
    {
        _hexEditor = hexEditor;
        _blockSize = blockSize;
    }

    public List<DuplicateBlock> FindDuplicates()
    {
        var duplicates = new List<DuplicateBlock>();
        var seenBlocks = new Dictionary<string, List<long>>();

        // Scan file in blocks
        for (long pos = 0; pos <= _hexEditor.Length - _blockSize; pos += _blockSize)
        {
            byte[] block = _hexEditor.GetBytes(pos, _blockSize);
            string hash = Convert.ToBase64String(
                System.Security.Cryptography.MD5.Create().ComputeHash(block));

            if (!seenBlocks.ContainsKey(hash))
            {
                seenBlocks[hash] = new List<long>();
            }

            seenBlocks[hash].Add(pos);
        }

        // Find blocks that appear more than once
        foreach (var (hash, positions) in seenBlocks)
        {
            if (positions.Count > 1)
            {
                duplicates.Add(new DuplicateBlock
                {
                    Hash = hash,
                    Positions = positions,
                    Count = positions.Count
                });
            }
        }

        return duplicates.OrderByDescending(d => d.Count).ToList();
    }
}

public class DuplicateBlock
{
    public string Hash { get; set; }
    public List<long> Positions { get; set; }
    public int Count { get; set; }
}

// Usage
var finder = new DuplicateFinder(hexEditor, blockSize: 512);
var duplicates = finder.FindDuplicates();

Console.WriteLine($"Found {duplicates.Count} duplicate blocks:");
foreach (var dup in duplicates.Take(10))
{
    Console.WriteLine($"  - {dup.Count} copies at: {string.Join(", ", dup.Positions.Select(p => $"0x{p:X}"))}");
}
```

---

### 2. Corruption Detection

```csharp
// Find corrupted regions by detecting invalid patterns
public class CorruptionDetector
{
    private HexEditor _hexEditor;

    // Patterns that indicate corruption
    private Dictionary<string, byte[]> corruptionPatterns = new()
    {
        { "All Zeros (Dead HDD sector)", Enumerable.Repeat((byte)0x00, 512).ToArray() },
        { "All FF (Flash corruption)", Enumerable.Repeat((byte)0xFF, 512).ToArray() },
        { "Repeating Pattern", new byte[] { 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA, 0xAA } }
    };

    public CorruptionDetector(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public List<CorruptionRegion> DetectCorruption()
    {
        var regions = new List<CorruptionRegion>();

        foreach (var (corruptionType, pattern) in corruptionPatterns)
        {
            var positions = _hexEditor.FindAll(pattern);

            foreach (var pos in positions)
            {
                regions.Add(new CorruptionRegion
                {
                    Type = corruptionType,
                    Offset = pos,
                    Length = pattern.Length
                });
            }
        }

        return regions.OrderBy(r => r.Offset).ToList();
    }
}

public class CorruptionRegion
{
    public string Type { get; set; }
    public long Offset { get; set; }
    public int Length { get; set; }
}

// Usage
var detector = new CorruptionDetector(hexEditor);
var corruptions = detector.DetectCorruption();

if (corruptions.Count > 0)
{
    Console.WriteLine($"⚠️ Found {corruptions.Count} potentially corrupted regions:");
    foreach (var region in corruptions)
    {
        Console.WriteLine($"  - {region.Type} at 0x{region.Offset:X} ({region.Length} bytes)");
    }
}
```

---

### 3. String Table Extraction

```csharp
// Extract all strings from string table
public class StringTableExtractor
{
    private HexEditor _hexEditor;

    public StringTableExtractor(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    // Find all strings with specific prefix
    public List<ExtractedString> ExtractStringsWithPrefix(string prefix)
    {
        var results = new List<ExtractedString>();

        // Convert prefix to bytes
        byte[] prefixBytes = Encoding.ASCII.GetBytes(prefix);

        // Find all occurrences of prefix
        var positions = _hexEditor.FindAll(prefixBytes);

        foreach (var pos in positions)
        {
            // Extract full string (until null terminator)
            var stringBytes = new List<byte>();
            long currentPos = pos;

            while (currentPos < _hexEditor.Length)
            {
                byte b = _hexEditor.GetByte(currentPos);

                if (b == 0x00)  // Null terminator
                    break;

                if (b < 0x20 || b > 0x7E)  // Non-printable
                    break;

                stringBytes.Add(b);
                currentPos++;
            }

            if (stringBytes.Count >= prefix.Length)
            {
                string text = Encoding.ASCII.GetString(stringBytes.ToArray());

                results.Add(new ExtractedString
                {
                    Offset = pos,
                    Text = text,
                    Length = stringBytes.Count
                });
            }
        }

        return results;
    }
}

public class ExtractedString
{
    public long Offset { get; set; }
    public string Text { get; set; }
    public int Length { get; set; }
}

// Usage: Extract all "MSG_" prefixed strings (common in games)
var extractor = new StringTableExtractor(hexEditor);
var strings = extractor.ExtractStringsWithPrefix("MSG_");

Console.WriteLine($"Found {strings.Count} message strings:");
foreach (var str in strings.Take(20))
{
    Console.WriteLine($"  0x{str.Offset:X}: {str.Text}");
}
```

---

### 4. Watermark Detection

```csharp
// Find all watermarks/signatures in file
public class WatermarkDetector
{
    private HexEditor _hexEditor;

    // Known watermark signatures
    private Dictionary<string, byte[]> watermarks = new()
    {
        { "Adobe Photoshop", Encoding.ASCII.GetBytes("8BIM") },
        { "Microsoft Office", Encoding.ASCII.GetBytes("Microsoft") },
        { "PDF Producer", Encoding.ASCII.GetBytes("/Producer") },
        { "EXIF", new byte[] { 0x45, 0x78, 0x69, 0x66, 0x00, 0x00 } }
    };

    public WatermarkDetector(HexEditor hexEditor)
    {
        _hexEditor = hexEditor;
    }

    public Dictionary<string, int> DetectWatermarks()
    {
        var results = new Dictionary<string, int>();

        foreach (var (name, signature) in watermarks)
        {
            var positions = _hexEditor.FindAll(signature);
            results[name] = positions.Count;

            if (positions.Count > 0)
            {
                Console.WriteLine($"Found {name} watermark: {positions.Count} occurrences");
                Console.WriteLine($"  First at: 0x{positions[0]:X}");
            }
        }

        return results;
    }
}

// Usage
var detector = new WatermarkDetector(hexEditor);
var watermarks = detector.DetectWatermarks();

if (watermarks.Values.Sum() > 0)
{
    Console.WriteLine("\nWatermarks detected:");
    foreach (var (name, count) in watermarks.Where(w => w.Value > 0))
    {
        Console.WriteLine($"  - {name}: {count} instances");
    }
}
```

---

## ⚡ Performance Benchmarks

### Performance (with Parallelization)

| File Size | Pattern | Matches | Previous | Current | Speedup |
|-----------|---------|---------|-----------|-----------|---------|
| 10 MB | 4 bytes | 100 | 1,500ms | 50ms | **30x** |
| 100 MB | 4 bytes | 1,000 | 15,000ms | 200ms | **75x** |
| 1 GB | 4 bytes | 10,000 | 150,000ms | 1,500ms | **100x** |

### Memory Usage

```
File size: 100 MB
Matches: 10,000

Previous: ~800 MB (stores full context)
Current: ~80 KB (stores positions only) - 10,000x less!
```

---

## ⚠️ Important Notes

### Memory Considerations

- FindAll() stores **all positions** in memory
- For large result sets (> 1 million matches), consider using **CountOccurrences()** instead
- Each position uses 8 bytes (long): 1 million matches = ~8 MB RAM

```csharp
// Large result set - might use lots of memory
List<long> positions = hexEditor.FindAll(new byte[] { 0x00 });  // All null bytes
// If file has 10 million null bytes: 10M * 8 bytes = 80 MB RAM!

// Better: Just count them
int count = hexEditor.CountOccurrences(new byte[] { 0x00 });
// Uses ~0 MB RAM
```

### Parallel Processing

- Automatically uses **parallel processing** for files > 10 MB
- Utilizes all available CPU cores
- Significantly faster on multi-core systems

### Results Are Sorted

- Returned list is **always sorted** ascending by position
- Safe to iterate forward without sorting

```csharp
List<long> positions = hexEditor.FindAll(pattern);
// Guaranteed: positions[0] < positions[1] < ... < positions[n]
```

---

## 🔗 See Also

- **[FindFirst()](findfirst.md)** - Find first occurrence only
- **[CountOccurrences()](countoccurrences.md)** - Count matches without storing positions (memory efficient)
- **[ReplaceAll()](../replace-operations/replaceall.md)** - Find and replace all occurrences
- **[GetByte()](../byte-operations/getbyte.md)** - Read byte at position

---

**Last Updated**: 2026-02-19
**Version**: 2.0 (Boyer-Moore-Horspool + SIMD + Parallel + LRU Cache)
